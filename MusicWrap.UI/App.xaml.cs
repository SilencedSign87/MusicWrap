using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Metadata;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Core.Services.Providers.Youtube;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Sources.Providers.Local;
using MusicWrap.Core.Sources.Providers.Runtime;
using MusicWrap.Core.Sources.Providers.Youtube;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.Player.Models;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Controls;
using MusicWrap.UI.Features.Favorites.Views;
using MusicWrap.UI.Features.Library.Components;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Features.Library.Views;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.ViewModels;
using MusicWrap.UI.Features.Playlist.Views;
using MusicWrap.UI.Features.Providers.ViewModels;
using MusicWrap.UI.Features.Settings.ViewModels;
using MusicWrap.UI.Features.State.Services;
using MusicWrap.UI.Features.State.ViewModels;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.ViewModels;
using Serilog;
using Serilog.Enrichers;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Windows;

namespace MusicWrap.UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private const string SingleInstanceMutexName = "MusicWrap.SingleInstance";
        public static Window? CurrentWindow { get; private set; }
        public static bool IsShuttingDown { get; private set; }
        public static bool IsWindowTransitioning => _windowTransitionDepth > 0;
        public static IServiceProvider Services { get; private set; } = default!;
        private static int _windowTransitionDepth;
        private ISaveCoordinator? _saveCoordinator;
        private ISaveOrchestration? _saveOrchestration;
        private ITrayService? _trayService;
        private UserSettings? _userSettings;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetDropDownMenuToBeRightAligned();
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            IsShuttingDown = false;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentUserName()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}")
                .WriteTo.File(
                    path: System.IO.Path.Combine(MusicWrapDirectories.LogsDirectory, "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] " +
                    "[Machine:{MachineName}] [User:{EnvironmentUserName}] [Thread:{ThreadId}] " +
                    "{Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("Application starting up");

            base.OnStartup(e);

            try
            {

                // Recreate app data folders if they were deleted between runs.
                MusicWrapDirectories.EnsureCreated();


                SplashScreen splash = new("Resources/SplashScreen.png");
                splash.Show(autoClose: false, topMost: true);

                Services = ConfigureServices();

                InitializeServicesAsync(splash);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during application startup");
            }

        }

        private void _trayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowOrRestoreCurrentWindow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            IsShuttingDown = true;

            try
            {
                var save = Services.GetService<ISaveCoordinator>();
                if (save is not null)
                {
                    save.Enqueue(SaveKind.Cache | SaveKind.Library | SaveKind.Playback | SaveKind.Settings | SaveKind.Playlist);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    save.FlushAsync(cts.Token).GetAwaiter().GetResult();
                }
                else
                {
                    TrySaveLibrary();
                }
                var trayService = Services.GetService<ITrayService>();
                if (trayService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while saving data on exit");
            }
            finally
            {
                if (_saveCoordinator is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                if (_saveOrchestration is IDisposable orchestrationDisposable)
                {
                    orchestrationDisposable.Dispose();
                }

                Log.Information("Application exiting");
                Log.CloseAndFlush();
                base.OnExit(e);
            }
        }

        public static void ShowMain()
        {
            if (CurrentWindow is MainWindow existingMain && TryShowWindow(existingMain))
            {
                return;
            }

            if (IsWindowUsable(CurrentWindow))
            {
                CloseForWindowTransition(CurrentWindow!);
            }

            var main = Services.GetRequiredService<MainWindow>();
            TrackCurrentWindow(main);
            main.Show();

            CurrentWindow = main;
        }

        public static void ShowCompactPlayer()
        {
            if (CurrentWindow is CompactPlayer existingCompact && TryShowWindow(existingCompact))
            {
                return;
            }

            if (IsWindowUsable(CurrentWindow))
            {
                CloseForWindowTransition(CurrentWindow!);
            }

            var player = Services.GetRequiredService<CompactPlayer>();
            TrackCurrentWindow(player);
            player.Show();

            CurrentWindow = player;
        }

        public static void ShowOrRestoreCurrentWindow()
        {
            if (!TryShowWindow(CurrentWindow))
            {
                ShowMain();
            }
        }

        public static void RequestShutdown()
        {
            IsShuttingDown = true;
            Current?.Shutdown();
        }

        public static bool ShouldKeepAppInTray()
        {
            if (Services is null)
            {
                return false;
            }

            var settings = Services.GetService<UserSettings>();
            return settings?.KeepAppInTray == true;
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
                    playbackRepository.Save(player.BuildPlaybackSnapshot());

                    if (userSettings is not null && userSettingsRepository is not null)
                    {
                        var isCompactLastSession = CurrentWindow is CompactPlayer;
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

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(dispose: true);
            });

            services.AddSingleton<ITrayService, TrayService>();

            //Data layer
            services.AddSingleton<ILibraryRepository, LibraryRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<ILibraryRepository>().Load());// Provide Music library
            services.AddSingleton<IPlaybackRepository, PlaybackRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IPlaybackRepository>().Load()); // Provide Queue settings
            services.AddSingleton<IUserSettingsRepository, UserSettingsRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IUserSettingsRepository>().Load()); // Provide user settings
            services.AddSingleton<IPlaylistRepository, PlaylistRepository>();
            services.AddSingleton(sp => sp.GetRequiredService<IPlaylistRepository>().Load()); // Provide playlist data

            // Services
            services.AddSingleton<IUIDispatcher, UIDispatcher>();
            services.AddSingleton<IImageService, ImageService>();
            services.AddSingleton<TracksContextMenuService>();
            services.AddSingleton<ILibraryScanner, LibraryScanner>();
            services.AddSingleton<ILibraryIndexer, LibraryIndexer>();
            services.AddSingleton<ILibraryCacheService, LibraryCacheService>();
            services.AddSingleton<ILibraryCacheStore, LibraryCacheStoreAdapter>();
            services.AddSingleton<ISaveOrchestration, SaveOrchestration>();
            services.AddSingleton<ISaveStateProvider>(sp => (ISaveStateProvider)sp.GetRequiredService<ISaveOrchestration>());
            services.AddSingleton<ISaveCoordinator, SaveScheduler>();
            services.AddSingleton<IMetadataAutocompleteService, MetadataAutocompleteService>();
            services.AddSingleton<IEditMetadataService, EditMetadataService>();

            // Providers
            services.AddSingleton<ITrackSourceProvider, LocalTrackSourceProvider>();
            services.AddSingleton<IYoutubeResolutionService, YoutubeResolutionService>();
            services.AddSingleton<IYoutubeStagingService, YoutubeStagingService>();
            services.AddSingleton<IYoutubeSearchService, YoutubeSearchService>();
            services.AddSingleton<IYoutubeLibraryIndexingService, YoutubeLibraryIndexingService>();
            services.AddSingleton<IYoutubeIndexingWorkflowService, YoutubeIndexingWorkflowService>();
            services.AddSingleton<ITrackSourceProvider, YoutubeSourceProvider>();
            services.AddSingleton<ITrackPlaybackResolver, TrackPlaybackResolver>();
            services.AddSingleton<IPlaylistService, PlaylistService>();
            services.AddSingleton<IStatusService, StatusService>();

            //Player
            services.AddSingleton<IMusicPlayerService, MusicPlayerService>();

            // View Models
            services.AddTransient<DirectoriesManagerViewModel>();
            services.AddTransient<SettingsGeneralViewModel>();
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<LibraryEntryDetailPanelViewModel>();
            services.AddTransient<AlbumTracksViewModel>();
            services.AddTransient<PlaylistViewModel>();
            services.AddTransient<PlaylistManagerViewModel>();
            services.AddTransient<SettingsYoutubeViewModel>();
            services.AddTransient<YoutubeProviderViewModel>();
            services.AddSingleton<IndexingViewModel>();
            services.AddSingleton<QueueViewModel>();
            services.AddSingleton<DeviceViewModel>();
            services.AddSingleton<PlayerViewModel>();
            services.AddSingleton<CommandPaletteViewModel>();
            services.AddSingleton<TaskbarIconViewModel>();
            services.AddTransient<MetadataEditorViewModel>();
            services.AddSingleton<DJControlViewModel>();
            services.AddSingleton<StatusbarViewModel>();

            // UI
            services.AddTransient<MainWindow>();
            services.AddTransient<CompactPlayer>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<IndexingWindow>();
            services.AddTransient<MetadataEditorWindow>();

            return services.BuildServiceProvider();
        }

        private async void InitializeServicesAsync(SplashScreen splash)
        {
            int windowToShow = 0;
            try
            {
                Services.GetRequiredService<MusicLibrary>();
                _userSettings = Services.GetRequiredService<UserSettings>();
                var player = Services.GetRequiredService<IMusicPlayerService>();
                _trayService = Services.GetRequiredService<ITrayService>();

                _userSettings.PropertyChanged += _userSettings_PropertyChanged;
                ApplyTrayBehavior(_userSettings.KeepAppInTray);

                var listBy = string.IsNullOrWhiteSpace(_userSettings.LibraryListBy)
                    ? "Artist"
                    : _userSettings.LibraryListBy;
                var ascending = _userSettings.LibraryAscending;

                var libraryCache = Services.GetRequiredService<ILibraryCacheService>();
                await libraryCache.InitializeAsync(listBy, ascending);

                Services.GetRequiredService<PlayerViewModel>();
                player.LoadInitialState(_userSettings);
                windowToShow = (int)_userSettings.LastWindowMode;
                _saveCoordinator = Services.GetRequiredService<ISaveCoordinator>();
                _saveOrchestration = Services.GetRequiredService<ISaveOrchestration>();

                if (_userSettings.KeepAppInTray)
                {
                    _trayService.Initialize();
                }
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

        private void _userSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserSettings.KeepAppInTray))
            {
                ApplyTrayBehavior(_userSettings?.KeepAppInTray ?? false);
            }
        }
        private void ApplyTrayBehavior(bool keepInTray)
        {
            ShutdownMode = keepInTray
                ? ShutdownMode.OnExplicitShutdown
                : ShutdownMode.OnLastWindowClose;

            if (keepInTray)
            {
                _trayService?.Initialize();
                return;
            }

            if (_trayService is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (CurrentWindow is not null && !CurrentWindow.IsVisible && IsWindowUsable(CurrentWindow))
            {
                CurrentWindow.Show();
                CurrentWindow.Activate();
            }
        }
        private static bool TryShowWindow(Window? window)
        {
            if (!IsWindowUsable(window))
            {
                return false;
            }

            if (window!.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            if (!window.IsVisible)
            {
                window.Show();
            }

            window.Activate();
            window.Focus();
            return true;
        }

        private static bool IsWindowUsable(Window? window)
        {
            return window is not null
                && window.IsLoaded
                && !window.Dispatcher.HasShutdownStarted
                && !window.Dispatcher.HasShutdownFinished;
        }

        private static void TrackCurrentWindow(Window window)
        {
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(CurrentWindow, window))
                {
                    CurrentWindow = null;
                }
            };
        }

        private static void SetDropDownMenuToBeRightAligned()
        {
            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            Action setAlignmentValue = () =>
            {
                if (SystemParameters.MenuDropAlignment && menuDropAlignmentField != null) menuDropAlignmentField.SetValue(null, false);
            };

            setAlignmentValue();

            SystemParameters.StaticPropertyChanged += (sender, e) =>
            {
                setAlignmentValue();
            };
        }

        private static void CloseForWindowTransition(Window window)
        {
            _windowTransitionDepth++;
            try
            {
                window.Close();
            }
            finally
            {
                _windowTransitionDepth--;
            }
        }
        #endregion
    }

}




