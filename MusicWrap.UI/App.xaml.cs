using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Application;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.Player.Models;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Pages.MainWindow;
using MusicWrap.UI.Services;
using MusicWrap.UI.ViewModels;
using MusicWrap.UI.ViewModels.Library;
using MusicWrap.UI.ViewModels.Settings;
using MusicWrap.UI.Windows;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MusicWrap.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Window? CurrentWindow { get; private set; }
        public static IServiceProvider Services { get; private set; } = default!;
        private TaskbarIcon? _trayIcon;
        private IMusicPlayerService? _player;
        private ISaveCoordinator? _saveCoordinator;
        private bool _saveHooksAttached;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Recreate app data folders if they were deleted between runs.
            MusicWrapDirectories.EnsureCreated();

            SplashScreen splash = new("Resources/SplashScreen.png");
            splash.Show(autoClose: false, topMost: true);

            Services = ConfigureServices();

            InitializeServicesAsync(splash);

        }

        private void _trayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (CurrentWindow is not null)
            {
                if (CurrentWindow.WindowState == WindowState.Minimized)
                    CurrentWindow.WindowState = WindowState.Normal;
                CurrentWindow.Activate();
                CurrentWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TrayIconManager.DisposeTrayIcon();
            try
            {
                var save = Services.GetService<ISaveCoordinator>();
                if (save is not null)
                {
                    save.Enqueue(SaveKind.Cache | SaveKind.Library | SaveKind.Playback | SaveKind.Settings);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    save.FlushAsync(cts.Token).GetAwaiter().GetResult();
                }
                else
                {
                    TrySaveLibrary();
                }
            }
            catch
            {
            }
            finally
            {
                DetachSaveHooks();
                if (_saveCoordinator is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                base.OnExit(e);
            }
        }

        public static void ShowMain()
        {
            var main = Services.GetRequiredService<MainWindow>();
            main.Show();

            CurrentWindow?.Close();

            CheckMemory();

            CurrentWindow = main;
        }

        public static void ShowCompactPlayer()
        {
            var player = Services.GetRequiredService<CompactPlayer>();
            player.Show();

            CurrentWindow?.Close();

            CheckMemory();

            CurrentWindow = player;
        }

        #region Internal

        private static void TrySaveLibrary()
        {
            try
            {
                if (Services is null) return;
                var store = Services.GetService<ILibraryRepository>();
                var library = Services.GetService<MusicLibrary>();
                var libraryCache = Services.GetService<ILibraryCacheService>();
                libraryCache?.SaveToDisk();

                if (store != null && library != null)
                {
                    store.Save(library);
                }

                var playbackRepository = Services.GetService<IPlaybackRepository>();
                var userSettingsRepository = Services.GetService<IUserSettingsRepository>();
                var userSettings = Services.GetService<UserSettings>();
                var player = Services.GetService<IMusicPlayerService>();
                if (playbackRepository is not null && player is not null)
                {
                    var isCompactLastSession = CurrentWindow is CompactPlayer;

                    var snapshot = new PlaybackQueueSnapshot
                    {
                        TrackIds = player.GetQueue(),
                        CurrentIndex = player.CurrentQueueIndex,
                        PositionInSeconds = player.CurrentPosition,
                        RepeatMode = (int)player.RepeatMode,
                        ContinueMode = (int)player.ContinueMode,
                        PlaybackState = player.IsPlaying ? 1 : (player.IsPaused ? 2 : 0)
                    };

                    playbackRepository.Save(snapshot);

                    if (userSettings is not null && userSettingsRepository is not null)
                    {
                        userSettings.LastWindowMode = isCompactLastSession ? LastWindowMode.CompactPlayer : LastWindowMode.MainPlayer;
                        userSettings.PreferredVolume = player.Volume;
                        userSettingsRepository.Save(userSettings);
                    }
                }
            }
            catch
            {

            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            //Data layer
            services.AddSingleton<ILibraryRepository, LibraryRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<ILibraryRepository>().Load());// Provide Music library
            services.AddSingleton<IPlaybackRepository, PlaybackRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IPlaybackRepository>().Load()); // Provide Queue settings
            services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IUserSettingsRepository>().Load()); // Provide user settings

            // Services
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<ILibraryIndexer, LibraryIndexer>();
            services.AddSingleton<ILibraryCacheService, LibraryCacheService>();
            services.AddSingleton<ISaveCoordinator, SaveCoordinator>();

            //Player
            services.AddSingleton<IMusicPlayerService, MusicPlayerService>();

            // UI
            services.AddTransient<MainWindow>();
            services.AddTransient<CompactPlayer>();
            services.AddTransient<SettingsWindow>();

            // View Models
            services.AddTransient<DirectoriesManagerViewModel>();
            services.AddTransient<SettingsGeneralViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<AlbumTracksViewModel>();
            services.AddSingleton<QueueViewModel>();
            services.AddSingleton<DeviceViewModel>();
            services.AddSingleton<PlayerViewModel>();

            return services.BuildServiceProvider();
        }

        private async void InitializeServicesAsync(SplashScreen splash)
        {
            int windowToShow = 0;
            try
            {
                // Force load

                Services.GetRequiredService<MusicLibrary>();
                var userSettings = Services.GetRequiredService<UserSettings>();
                var player = Services.GetRequiredService<IMusicPlayerService>();

                // Apply persisted audio preferences before restoring playback/caching state.
                if (userSettings.PreferredDeviceIndex >= 0 && userSettings.PreferredDeviceIndex != player.CurrentDeviceIndex)
                {
                    player.ChangeOutputDevice(userSettings.PreferredDeviceIndex);
                }

                player.ChangeOutputMode(userSettings.PreferredOutputMode);

                int preferredSampleRate = (int)userSettings.PreferredSampleRate;
                if (preferredSampleRate != player.CurrentSampleRate)
                {
                    player.ChangeSampleRate(preferredSampleRate);
                }

                player.SetVolume(Math.Clamp(userSettings.PreferredVolume, 0f, 1f));

                var listBy = string.IsNullOrWhiteSpace(userSettings.LibraryListBy)
                    ? "Artist"
                    : userSettings.LibraryListBy;
                var ascending = userSettings.LibraryAscending;

                var LibraryCache = Services.GetRequiredService<ILibraryCacheService>();
                await LibraryCache.InitializeAsync(listBy, ascending);

                _trayIcon = TrayIconManager.GetTrayIcon();
                _trayIcon.TrayMouseDoubleClick += _trayIcon_TrayMouseDoubleClick;

                windowToShow = TryRestorePlaybackSession();

                AttachSaveHooks();
            }
            catch
            {

            }
            finally
            {

                splash.Close(TimeSpan.FromSeconds(0.5));
                if (windowToShow == 1)
                {
                    ShowCompactPlayer();
                }
                else
                {
                    ShowMain();

                }
            }
        }

        private static int TryRestorePlaybackSession()
        {
            try
            {
                int playerMode = 0;
                var userSettings = Services.GetService<UserSettings>();
                if (userSettings is not null)
                {
                    playerMode = (int)userSettings.LastWindowMode;
                }

                var playbackRepository = Services.GetService<IPlaybackRepository>();
                var player = Services.GetService<IMusicPlayerService>();
                if (playbackRepository is null || player is null) return playerMode;

                var startupBehavior = userSettings?.StartupBehavior ?? StartupBehavior.RestoreQueueOnly;
                if (startupBehavior == StartupBehavior.StartClean) return playerMode;

                var snapshot = playbackRepository.Load();
                if (snapshot.TrackIds.Length == 0) return playerMode;

                var library = Services.GetService<MusicLibrary>();
                if (library is null) return playerMode;

                var validIds = new HashSet<int>(library.Tracks.Select(t => t.Id));
                var queue = snapshot.TrackIds.Where(id => validIds.Contains(id)).ToArray();
                if (queue.Length == 0) return playerMode;

                player.SetQueue(queue, false);
                player.RepeatMode = (RepeatMode)snapshot.RepeatMode;
                player.ContinueMode = (ContinueMode)snapshot.ContinueMode;

                int index = snapshot.CurrentIndex;
                if (index < 0 || index >= queue.Length) index = 0;

                switch (startupBehavior)
                {
                    case StartupBehavior.RestoreQueueOnly:
                        player.Stop();
                        break;

                    case StartupBehavior.RestoreQueueAndIndexOnly:
                        player.SetSilentIndex(index);
                        player.Stop();
                        break;

                    case StartupBehavior.ResumePlayback:
                        player.SetVolume(0f);
                        player.PlayIndex(index);

                        var position = Math.Clamp(snapshot.PositionInSeconds, 0, player.Duration);
                        player.Seek(position);

                        switch (snapshot.PlaybackState)
                        {
                            case 0: player.Stop(); break;
                            case 1: player.Play(); break;
                            case 2: player.Pause(); break;
                            default: player.Pause(); break;
                        }
                        break;
                }

                if (userSettings is not null)
                {
                    player.SetVolume(Math.Clamp(userSettings.PreferredVolume, 0f, 1f));
                }

                return playerMode;
            }
            catch
            {
                return 0;
            }
        }
        
        private void AttachSaveHooks()
        {
            if (_saveHooksAttached) return;
            _player = Services.GetService<IMusicPlayerService>();
            _saveCoordinator = Services.GetService<ISaveCoordinator>();

            if (_player is null || _saveCoordinator is null) return;

            _player.QueueChanged += OnQueueChanged;
            _player.PlaybackStateChanged += OnPlaybackStateChanged;
            _player.TrackChanged += OnTrackChanged;
            _player.DeviceIndexChanged += OnDeviceIndexChanged;
            _player.SampleRateChanged += OnSampleRateChanged;
            _player.OutputModeChanged += OnOutputModeChanged;

            _saveHooksAttached = true;
        }
        private void DetachSaveHooks()
        {
            if (!_saveHooksAttached) return;
            if (_player is not null)
            {
                _player.QueueChanged -= OnQueueChanged;
                _player.PlaybackStateChanged -= OnPlaybackStateChanged;
                _player.TrackChanged -= OnTrackChanged;
                _player.DeviceIndexChanged -= OnDeviceIndexChanged;
                _player.SampleRateChanged -= OnSampleRateChanged;
                _player.OutputModeChanged -= OnOutputModeChanged;
            }
            _saveHooksAttached = false;
        }

        private void OnOutputModeChanged(object? sender, OutputMode e)
        {
            _saveCoordinator?.Enqueue(SaveKind.Settings);
        }

        private void OnSampleRateChanged(object? sender, SampleRateChangedEventArgs e)
        {
            _saveCoordinator?.Enqueue(SaveKind.Settings);
        }

        private void OnDeviceIndexChanged(object? sender, int e)
        {
            _saveCoordinator?.Enqueue(SaveKind.Settings);
        }

        private void OnTrackChanged(object? sender, string e)
        {
            _saveCoordinator?.Enqueue(SaveKind.Playback);
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState e)
        {
            _saveCoordinator?.Enqueue(SaveKind.Playback);
        }

        private void OnQueueChanged(object? sender, int[] e)
        {
            _saveCoordinator?.Enqueue(SaveKind.Playback);
        }

        private static void CheckMemory()
        {
#if DEBUG
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
#endif
        }
        #endregion
    }

}
