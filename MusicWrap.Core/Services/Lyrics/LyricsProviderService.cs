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
        private readonly IMusicPlayerService _player;
        private readonly ILibraryService _library;
        private readonly ILogger<LyricsProviderService> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private ParsedLyrics _current = ParsedLyrics.Empty;
        private int _cachedTrackId = -1;
        private string _cachedPath = "";
        private long _cachedTicks = -1;
        private bool _disposed;

        public ParsedLyrics Current => _current;
        public event EventHandler<ParsedLyrics>? LyricsChanged;
        public LyricsProviderService(IMusicPlayerService player, ILibraryService library, ILogger<LyricsProviderService> logger)
        {
            _player = player;
            _library = library;
            _logger = logger;
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
                }
                var parsed = LyricsParser.Parse(raw, LyricsSource.Embedded);
                return UpdateCache(parsed, trackId, path, ticks);
            }
            finally
            {
                _gate.Release();
            }
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
