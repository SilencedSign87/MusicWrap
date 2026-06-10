using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.Playlist.Models;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Saving
{
    public class SaveScheduler : ISaveCoordinator, IDisposable
    {
        private static readonly TimeSpan SettingsDebounce = TimeSpan.FromMilliseconds(800);
        private static readonly TimeSpan PlaybackDebounce = TimeSpan.FromMilliseconds(1200);
        private static readonly TimeSpan LibraryDebounce = TimeSpan.FromMilliseconds(3500);
        private static readonly TimeSpan CacheDebounce = TimeSpan.FromMilliseconds(2500);
        private static readonly TimeSpan PlaylistDebounce = TimeSpan.FromMilliseconds(2500);
        private static readonly TimeSpan WorkerTick = TimeSpan.FromMilliseconds(250);

        private readonly object _lock = new();
        private readonly SemaphoreSlim _saveGate = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;

        // Repository
        private readonly ILibraryRepository _libraryRepo;
        private readonly IPlaybackRepository _playbackRepo;
        private readonly IUserSettingsRepository _settingsRepo;
        private readonly IPlaylistRepository _playlistRepo;
        private readonly ILibraryService _libraryCache;

        // Entities
        private readonly MusicLibrary _library;
        private readonly UserSettings _userSettings;
        private readonly PlaylistData _playlistData;

        private readonly IMusicPlayerService _player;

        // debounce
        private SaveKind _pending;
        private DateTime _nextSettingsUtc = DateTime.MinValue;
        private DateTime _nextPlaybackUtc = DateTime.MinValue;
        private DateTime _nextLibraryUtc = DateTime.MinValue;
        private DateTime _nextCacheUtc = DateTime.MinValue;
        private DateTime _nextPlaylistUtc = DateTime.MinValue;

        public SaveScheduler(
            ILibraryRepository libraryRepo,
        IPlaybackRepository playbackRepo,
        IUserSettingsRepository settingsRepo,
        IPlaylistRepository playlistRepo,
        ILibraryService libraryCache,
        MusicLibrary library,
        UserSettings userSettings,
        PlaylistData playlistData,
        IMusicPlayerService player)
        {
            _libraryRepo = libraryRepo;
            _playbackRepo = playbackRepo;
            _settingsRepo = settingsRepo;
            _playlistRepo = playlistRepo;
            _libraryCache = libraryCache;
            _library = library;
            _userSettings = userSettings;
            _playlistData = playlistData;
            _player = player;

            _worker = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        public void Enqueue(SaveKind kind)
        {
            if (kind == SaveKind.None) return;

            var now = DateTime.UtcNow;
            lock (_lock)
            {
                _pending |= kind;

                if ((kind & SaveKind.Settings) != 0) _nextSettingsUtc = now + SettingsDebounce;
                if ((kind & SaveKind.Playback) != 0) _nextPlaybackUtc = now + PlaybackDebounce;
                if ((kind & SaveKind.Library) != 0) _nextLibraryUtc = now + LibraryDebounce;
                if ((kind & SaveKind.Cache) != 0) _nextCacheUtc = now + CacheDebounce;
                if ((kind & SaveKind.Playlist) != 0) _nextPlaylistUtc = now + PlaylistDebounce;
            }
        }

        public async Task FlushAsync(CancellationToken ct = default)
        {
            SaveKind toSave;
            lock (_lock)
            {
                toSave = _pending | SaveKind.Settings | SaveKind.Playback
                   | SaveKind.Library | SaveKind.Cache | SaveKind.Playlist;

                _pending = SaveKind.None;
            }

            await PersistAsync(toSave, ct);
        }
        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _worker.GetAwaiter().GetResult();
            }
            catch
            {

            }
            _saveGate.Dispose();
            _cts.Dispose();
        }

        #region Internal
        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(WorkerTick, ct);
                    var due = DequeueDueKinds(DateTime.UtcNow);
                    if (due != SaveKind.None)
                        await PersistAsync(due, ct);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch
            {

            }
        }
        private SaveKind DequeueDueKinds(DateTime nowUtc)
        {
            lock (_lock)
            {
                SaveKind due = SaveKind.None;
                if ((_pending & SaveKind.Settings) != 0 && nowUtc >= _nextSettingsUtc) { due |= SaveKind.Settings; _pending &= ~SaveKind.Settings; }
                if ((_pending & SaveKind.Playback) != 0 && nowUtc >= _nextPlaybackUtc) { due |= SaveKind.Playback; _pending &= ~SaveKind.Playback; }
                if ((_pending & SaveKind.Library) != 0 && nowUtc >= _nextLibraryUtc) { due |= SaveKind.Library; _pending &= ~SaveKind.Library; }
                if ((_pending & SaveKind.Cache) != 0 && nowUtc >= _nextCacheUtc) { due |= SaveKind.Cache; _pending &= ~SaveKind.Cache; }
                if ((_pending & SaveKind.Playlist) != 0 && nowUtc >= _nextPlaylistUtc) { due |= SaveKind.Playlist; _pending &= ~SaveKind.Playlist; }
                return due;
            }
        }
        private async Task PersistAsync(SaveKind kinds, CancellationToken ct)
        {
            if (kinds == SaveKind.None) return;
            await _saveGate.WaitAsync(ct);
            try
            {
                if ((kinds & SaveKind.Cache) != 0)
                    try { _libraryCache.SaveToDisk(); } catch { }
                if ((kinds & SaveKind.Library) != 0)
                    try { _libraryRepo.Save(_library); } catch { }
                if ((kinds & SaveKind.Playback) != 0)
                    try { _playbackRepo.Save(_player.BuildPlaybackSnapshot()); } catch { }
                if ((kinds & SaveKind.Settings) != 0)
                    try { _settingsRepo.Save(_userSettings); } catch { }
                if ((kinds & SaveKind.Playlist) != 0)
                    try { _playlistRepo.Save(_playlistData); } catch { }
            }
            finally
            {
                _saveGate.Release();
            }
        }
        #endregion
    }
}
