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
            TrySaveLibrary();
            base.OnExit(e);
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

            //Player
            services.AddSingleton<IMusicPlayerService, MusicPlayerService>();

            // UI
            services.AddTransient<MainWindow>();
            services.AddTransient<CompactPlayer>();
            services.AddTransient<SettingsWindow>();

            // Services
            services.AddSingleton<ILibraryCacheService, LibraryCacheService>();


            // View Models
            services.AddTransient<DirectoriesManagerViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<AlbumTracksViewModel>();
            services.AddSingleton<QueueViewModel>();
            services.AddSingleton<DeviceViewModel>();
            //services.AddTransient<AlbumViewModel>();

            // Player
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
                if (index < 0 || index >= queue.Length)
                    index = 0;

                player.SetVolume(0);
                player.PlayIndex(index);

                var position = snapshot.PositionInSeconds;
                position = Math.Clamp(position, 0, player.Duration);

                player.Seek(position);

                switch (snapshot.PlaybackState)
                {
                    case 0:// stopped
                        player.Stop();
                        break;
                    case 1: // playing
                        player.Play();
                        break;
                    case 2: // paused
                        player.Pause();
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
