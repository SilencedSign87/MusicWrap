using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.Player.Models;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.Playlist.Models;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;

namespace MusicWrap.Data.Infrastructure.Saving
{
    [Flags]
    public enum SaveKind
    {
        None = 0,
        Settings = 1,
        Playback = 2,
        Library = 4,
        Cache = 8,
        Playlist = 16,
    }

    public interface ISaveCoordinator
    {
        void Enqueue(SaveKind kind);
        Task FlushAsync(CancellationToken ct = default);
    }

    public interface ISaveStateProvider
    {
        PlaybackQueueSnapshot BuildPlaybackSnapshot();
        float GetCurrentVolume();
        LastWindowMode GetCurrentWindowMode();
    }

    public interface ILibraryCacheStore
    {
        void Save();
    }

    public sealed class SaveScheduler : ISaveCoordinator, IDisposable
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

        private readonly ILibraryRepository _libraryRepository;
        private readonly MusicLibrary _library;
        private readonly IPlaybackRepository _playbackRepository;
        private readonly IUserSettingsRepository _userSettingsRepository;
        private readonly UserSettings _userSettings;
        private readonly IPlaylistRepository _playlistRepository;
        private readonly PlaylistData _playlistData;
        private readonly ISaveStateProvider _saveStateProvider;
        private readonly ILibraryCacheStore _libraryCacheStore;

        private SaveKind _pending;
        private DateTime _nextSettingsUtc = DateTime.MinValue;
        private DateTime _nextPlaybackUtc = DateTime.MinValue;
        private DateTime _nextLibraryUtc = DateTime.MinValue;
        private DateTime _nextCacheUtc = DateTime.MinValue;
        private DateTime _nextPlaylistUtc = DateTime.MinValue;

        public SaveScheduler(
            ILibraryRepository libraryRepository,
            MusicLibrary library,
            IPlaybackRepository playbackRepository,
            IUserSettingsRepository userSettingsRepository,
            UserSettings userSettings,
            IPlaylistRepository playlistRepository,
            PlaylistData playlistData,
            ISaveStateProvider saveStateProvider,
            ILibraryCacheStore libraryCacheStore)
        {
            _libraryRepository = libraryRepository;
            _library = library;
            _playbackRepository = playbackRepository;
            _userSettingsRepository = userSettingsRepository;
            _userSettings = userSettings;
            _playlistRepository = playlistRepository;
            _playlistData = playlistData;
            _saveStateProvider = saveStateProvider;
            _libraryCacheStore = libraryCacheStore;

            _worker = Task.Run(() => WorkerLoopAsync(_cts.Token));
        }

        public void Enqueue(SaveKind kind)
        {
            if (kind == SaveKind.None)
            {
                return;
            }

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
                toSave = _pending | SaveKind.Settings | SaveKind.Playback | SaveKind.Library | SaveKind.Cache | SaveKind.Playlist;
                _pending = SaveKind.None;
            }

            await PersistAsync(toSave, ct);
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(WorkerTick, ct);
                    var due = DequeueDueKinds(DateTime.UtcNow);
                    if (due != SaveKind.None)
                    {
                        await PersistAsync(due, ct);
                    }
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

                if ((_pending & SaveKind.Settings) != 0 && nowUtc >= _nextSettingsUtc)
                {
                    due |= SaveKind.Settings;
                    _pending &= ~SaveKind.Settings;
                }

                if ((_pending & SaveKind.Playback) != 0 && nowUtc >= _nextPlaybackUtc)
                {
                    due |= SaveKind.Playback;
                    _pending &= ~SaveKind.Playback;
                }

                if ((_pending & SaveKind.Library) != 0 && nowUtc >= _nextLibraryUtc)
                {
                    due |= SaveKind.Library;
                    _pending &= ~SaveKind.Library;
                }

                if ((_pending & SaveKind.Cache) != 0 && nowUtc >= _nextCacheUtc)
                {
                    due |= SaveKind.Cache;
                    _pending &= ~SaveKind.Cache;
                }

                if ((_pending & SaveKind.Playlist) != 0 && nowUtc >= _nextPlaylistUtc)
                {
                    due |= SaveKind.Playlist;
                    _pending &= ~SaveKind.Playlist;
                }

                return due;
            }
        }

        private async Task PersistAsync(SaveKind kinds, CancellationToken ct)
        {
            if (kinds == SaveKind.None)
            {
                return;
            }

            await _saveGate.WaitAsync(ct);
            try
            {
                if ((kinds & SaveKind.Cache) != 0)
                {
                    try
                    {
                        _libraryCacheStore.Save();
                    }
                    catch
                    {
                    }
                }

                if ((kinds & SaveKind.Library) != 0)
                {
                    try
                    {
                        _libraryRepository.Save(_library);
                    }
                    catch
                    {
                    }
                }

                if ((kinds & SaveKind.Playback) != 0)
                {
                    try
                    {
                        _playbackRepository.Save(_saveStateProvider.BuildPlaybackSnapshot());
                    }
                    catch
                    {
                    }
                }

                if ((kinds & SaveKind.Settings) != 0)
                {
                    try
                    {
                        _userSettings.PreferredVolume = Math.Clamp(_saveStateProvider.GetCurrentVolume(), 0f, 1f);
                        _userSettings.LastWindowMode = _saveStateProvider.GetCurrentWindowMode();
                        _userSettingsRepository.Save(_userSettings);
                    }
                    catch
                    {
                    }
                }

                if ((kinds & SaveKind.Playlist) != 0)
                {
                    try
                    {
                        _playlistRepository.Save(_playlistData);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                _saveGate.Release();
            }
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
    }
}