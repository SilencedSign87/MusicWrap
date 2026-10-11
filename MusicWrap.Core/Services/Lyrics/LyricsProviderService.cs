using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Lyrics
{
    public class LyricsProviderService : IDisposable
    {
        private readonly MusicPlayerService _player;
        private readonly ILibraryService _library;
        private readonly MetadataEditorService _metadataEditorService;
        private readonly ILogger<LyricsProviderService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private ParsedLyrics _current = ParsedLyrics.Empty;
        private int _cachedTrackId = -1;
        private string _cachedPath = "";
        private long _cachedTicks = -1;
        private bool _disposed;

        public ParsedLyrics Current => _current;
        public event EventHandler<ParsedLyrics>? LyricsChanged;
        public LyricsProviderService(MusicPlayerService player, ILibraryService library, ILogger<LyricsProviderService> logger, MetadataEditorService metadataEditorService)
        {
            _player = player;
            _library = library;
            _logger = logger;
            _metadataEditorService = metadataEditorService;
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
                if (trackId == _cachedTrackId && path == _cachedPath && ticks == _cachedTicks) return _current;

                string? raw = null;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    raw = await Task.Run(() =>
                    {
                        try
                        {
                            using var f = TagLib.File.Create(path);
                            return f.Tag.Lyrics;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to read lyrics from file {Path}", path);
                            return null;
                        }
                    }, ct);

                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        raw = await SearchLyricFileForTrack(path, ct);
                    }

                }
                var parsed = LyricsParser.Parse(raw, LyricsSource.Embedded);
                return UpdateCache(parsed, trackId, path, ticks);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<string> SearchLyricFileForTrack(string trackPath, CancellationToken ct = default)
        {
            var directory = Path.GetDirectoryName(trackPath);
            if (directory is null) return string.Empty;

            var trackFileName = Path.GetFileNameWithoutExtension(trackPath);

            var supportedExtensions = new[] { ".lrc", ".txt" };

            var lyricFiles = Directory.GetFiles(directory)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (lyricFiles.Count == 0) return string.Empty;

            var bestMatch = lyricFiles
                .Select(file => new
                {
                    FilePath = file,
                    FileNameWithoutExtension = Path.GetFileNameWithoutExtension(file),
                    LevenshteinDistance = LyricsProviderService.LevenshteinDistance(trackFileName, Path.GetFileNameWithoutExtension(file))
                })
                .OrderBy(x => x.LevenshteinDistance)
                .FirstOrDefault();

            var contents = await File.ReadAllTextAsync(lyricFiles[0], ct);

            return contents;
        }

        private static int LevenshteinDistance(string trackFileName, string v)
        {
            var distance = new int[trackFileName.Length + 1, v.Length + 1];

            for (int i = 0; i <= trackFileName.Length; i++)
                distance[i, 0] = i;

            for (int j = 0; j <= v.Length; j++)
                distance[0, j] = j;

            for (int i = 1; i <= trackFileName.Length; i++)
            {
                for (int j = 1; j <= v.Length; j++)
                {
                    int subtitutionCost = 0;
                    if (trackFileName[i] != v[j])
                        subtitutionCost = 1;

                    distance[i, j] = Math.Min(
                        distance[i - 1, j] + 1,
                        Math.Min(
                        distance[i, j - 1] + 1,
                        distance[i - 1, j - 1] + subtitutionCost
                        )
                    );
                }
            }

            return distance[trackFileName.Length, v.Length];
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

        private ParsedLyrics UpdateCache(ParsedLyrics p, int id, string path, long ticks)
        {
            _cachedTrackId = id;
            _cachedPath = path;
            _cachedTicks = ticks;
            _current = p;
            LyricsChanged?.Invoke(this, p);
            return p;
        }
        public void Invalidate() { _cachedTrackId = -1; }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _player.TrackChanged -= OnTrackChanged;
            _gate.Dispose();
        }
    }
}
