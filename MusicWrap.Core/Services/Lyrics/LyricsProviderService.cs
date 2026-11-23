using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;

using lyricsModel = MusicWrap.Data.Library.Models.Lyrics;

namespace MusicWrap.Core.Services.Lyrics
{
    public class LyricsProviderService : IDisposable
    {
        private readonly MusicPlayerService _player;
        private readonly ILibraryService _library;
        private readonly MetadataEditorService _metadataEditorService;
        private readonly ILogger<LyricsProviderService> _logger;
        private readonly LyricsData _lyricsLibrary;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly IMessenger _messenger;


        private readonly SemaphoreSlim _gate = new(1, 1);

        private ParsedLyrics _current = ParsedLyrics.Empty;
        private int _cachedTrackId = -1;
        private string _cachedPath = "";
        private long _cachedTicks = -1;
        private bool _disposed;

        public ParsedLyrics Current => _current;
        public event EventHandler<ParsedLyrics>? LyricsChanged;
        public LyricsProviderService(
            MusicPlayerService player, 
            ILibraryService library, 
            ILogger<LyricsProviderService> logger, 
            MetadataEditorService metadataEditorService, 
            LyricsData lyrics, 
            IMessenger messenger,
            ISaveCoordinator saveCoordinator)
        {
            _player = player;
            _library = library;
            _logger = logger;
            _metadataEditorService = metadataEditorService;
            _lyricsLibrary = lyrics;
            _saveCoordinator = saveCoordinator;
            _messenger = messenger;

            _player.TrackChanged += OnTrackChanged;
        }
        private void OnTrackChanged(object? sender, string e) => _ = GetCurrentAsync();

        public async Task<ParsedLyrics> GetCurrentAsync(CancellationToken ct = default)
        {
            int trackId = _player.CurrentTrackId;

            if (trackId <= 0) return UpdateCache(ParsedLyrics.Empty, -1, "", -1);

            var track = _library.GetTrackById(trackId);
            string path = track?.Path ?? _player.CurrentTrackPath;
            long ticks = track?.LastWriteTime ?? 0;

            // cache check
            if (trackId == _cachedTrackId && path == _cachedPath && ticks == _cachedTicks) return _current;

            await _gate.WaitAsync(ct);
            try
            {
                var parsed = await LoadIndexedLyricsAsync(trackId, path, ct);

                if (!parsed.HasContent)
                {
                    var externalPath = await SearchLyricFileForTrack(path, ct);

                    if (!string.IsNullOrEmpty(externalPath))
                    {
                        var raw = await ReadExternalFileAsync(externalPath, ct);

                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            parsed = LyricsParser.Parse(raw, LyricsSource.External);

                            RegisterLyricsForTrack(
                                trackId,
                                tag: "Original",
                                type: parsed.IsSynced ? LyricsType.synced : LyricsType.plain,
                                origin: LyricsOrigin.File,
                                provider: externalPath,
                                makeDefault: true
                                );

                            _saveCoordinator.Enqueue(SaveKind.Lyrics);
                        }
                    }
                }

                if (!parsed.HasContent)
                {
                    var raw = await ReadEmbeddedLyricsAsync(path, ct);

                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        parsed = LyricsParser.Parse(raw, LyricsSource.Embedded);

                        RegisterLyricsForTrack(
                            trackId,
                            tag: "Original",
                            type: parsed.IsSynced ? LyricsType.synced : LyricsType.plain,
                            origin: LyricsOrigin.Embedded,
                            provider: path,
                            makeDefault: true
                            );
                        _saveCoordinator.Enqueue(SaveKind.Lyrics);
                    }
                }

                return UpdateCache(parsed, trackId, path, ticks);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<string> SearchLyricFileForTrack(string trackPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(trackPath)) return string.Empty;

            var directory = Path.GetDirectoryName(trackPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return string.Empty;

            var trackName = Path.GetFileNameWithoutExtension(trackPath);

            string? best = null;
            int bestRank = int.MaxValue;

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    var ext = Path.GetExtension(file);
                    int rank = ext.ToLowerInvariant() switch
                    {
                        ".lrc" => 0,
                        ".txt" => 1,
                        "" => 2,
                        _ => int.MaxValue
                    };
                    if (rank >= bestRank) continue;

                    if (!string.Equals(Path.GetFileNameWithoutExtension(file), trackName,
                                       StringComparison.OrdinalIgnoreCase))
                        continue;

                    best = file;
                    bestRank = rank;
                    if (rank == 0) break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to enumerate lyric files in {Directory}", directory);
                return string.Empty;
            }

            if (best is null) return string.Empty;

            return best;

        }

        public ParsedLyrics GetLyricsForTrackId(int trackId)
        {
            var track = _library.GetTrackById(trackId);
            if (track is null || track.Id == 0) return ParsedLyrics.Empty;
            try
            {
                using var f = TagLib.File.Create(track.Path);
                return LyricsParser.Parse(f.Tag.Lyrics, LyricsSource.Embedded);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read lyrics from file {Path}", track.Path);
                return ParsedLyrics.Empty;
            }
        }

        public async Task<bool> EmbedLyricsAsync(int trackId, string rawLyrics, CancellationToken ct = default)
        {
            var success = await _metadataEditorService.EditTagAsync(trackId, tag =>
            {
                tag.Lyrics = rawLyrics;
            }, ct);

            return success;
        }
        public lyricsModel? GetSelectedLyrics(int trackId)
        {
            if (!_lyricsLibrary.TrackSelectedLyrics.TryGetValue(trackId, out var lyricsId))
                return null;

            return _lyricsLibrary.Lyrics.FirstOrDefault(l => l.Id == lyricsId);
        }

        public void SelectLyrics(int trackId, int lyricsId)
        {
            var selected = _lyricsLibrary.Lyrics.FirstOrDefault(l =>
                l.Id == lyricsId && l.TrackId == trackId);

            if (selected is null) return;

            foreach (var lyrics in _lyricsLibrary.Lyrics.Where(l => l.TrackId == trackId))
                lyrics.Default = false;

            selected.Default = true;
            _lyricsLibrary.TrackSelectedLyrics[trackId] = selected.Id;
            _saveCoordinator.Enqueue(SaveKind.Lyrics);
            Invalidate();

            _messenger.Send(new TrackLyricsChangedMessage(trackId, LyricsChangeType.SelectionChanged));
        }

        public List<lyricsModel> LoadLyricsByTrackId(int id)
        {
            var lyrics = _lyricsLibrary.Lyrics.FindAll(x => x.TrackId == id).OrderByDescending(x => x.Default).ThenByDescending(x => x.Type).ToList();
            return lyrics;
        }

        public void Invalidate() { _cachedTrackId = -1; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _player.TrackChanged -= OnTrackChanged;
            _gate.Dispose();
        }

        #region Internal

        private async Task<ParsedLyrics> LoadIndexedLyricsAsync(int trackId, string trackPath, CancellationToken ct = default)
        {
            if (!_lyricsLibrary.TrackSelectedLyrics.TryGetValue(trackId, out var lyricsId))
                return ParsedLyrics.Empty;

            var entry = _lyricsLibrary.Lyrics.FirstOrDefault(l => l.Id == lyricsId);
            if (entry is null)
            {
                _lyricsLibrary.TrackSelectedLyrics.Remove(trackId);
                _messenger.Send(new TrackLyricsChangedMessage(trackId, LyricsChangeType.SelectionChanged));
                return ParsedLyrics.Empty;
            }

            string? raw = entry.Origin switch
            {
                LyricsOrigin.Manual => entry.LyricsText,
                LyricsOrigin.File => await ReadExternalFileAsync(entry.Provider, ct),
                LyricsOrigin.Embedded => await ReadEmbeddedLyricsAsync(trackPath, ct),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(raw))
                return ParsedLyrics.Empty;

            var source = entry.Origin switch
            {
                LyricsOrigin.File => LyricsSource.External,
                LyricsOrigin.Embedded => LyricsSource.Embedded,
                _ => LyricsSource.None
            };

            return LyricsParser.Parse(raw, source);
        }

        private async Task<string?> ReadExternalFileAsync(string trackPath, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(trackPath)) return null;
            if (!File.Exists(trackPath)) return null;
            return await File.ReadAllTextAsync(trackPath, ct);
        }

        private async Task<string?> ReadEmbeddedLyricsAsync(string trackPath, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var f = TagLib.File.Create(trackPath);
                    return f.Tag.Lyrics;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read lyrics from file {Path}", trackPath);
                    return null;
                }
            }, ct);
        }

        private ParsedLyrics UpdateCache(ParsedLyrics p, int id, string path, long ticks)
        {
            _cachedTrackId = id;
            _cachedPath = path;
            _cachedTicks = ticks;
            _current = p;
            LyricsChanged?.Invoke(this, p);
            return p;
        }

        private bool RegisterLyricsForTrack(int trackId, string tag, LyricsType type, LyricsOrigin origin, string provider, bool makeDefault = false, string? userEditedLyrics = null)
        {
            var existing = _lyricsLibrary.Lyrics.FirstOrDefault(l => l.TrackId == trackId && l.Provider == provider && l.Origin == origin);

            if (existing is not null)
            {
                existing.Default = makeDefault;
                existing.Language = tag;
                existing.Type = type;
                existing.LyricsText = userEditedLyrics;

                return true;
            }

            var newLyrics = new lyricsModel
            {
                Id = _lyricsLibrary.GenerateLyricsId(),
                TrackId = trackId,
                Default = makeDefault,
                Language = tag,
                Origin = origin,
                Type = type,
                Provider = provider,
                LyricsText = origin == LyricsOrigin.Manual ? userEditedLyrics : null,
            };

            if (makeDefault)
            {
                var trackLyrics = _lyricsLibrary.Lyrics.Where(l => l.TrackId == trackId).ToList();
                foreach (var lyrics in _lyricsLibrary.Lyrics.Where(l => l.TrackId == trackId))
                    lyrics.Default = false;
            }

            _lyricsLibrary.Lyrics.Add(newLyrics);
            _lyricsLibrary.TrackSelectedLyrics[trackId] = newLyrics.Id;
            _messenger.Send(new TrackLyricsChangedMessage(trackId, LyricsChangeType.EntryAdded));
            return true;
        }

        #endregion
    }
}
