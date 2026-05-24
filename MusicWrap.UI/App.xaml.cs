using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Bootstrap;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shell.Windows;
using Serilog;
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

            base.OnStartup(e);

            try
            {
                SplashScreen splash = new("Resources/SplashScreen.png");
                splash.Show(autoClose: false, topMost: true);

                Services = StartupOrquestrator.BuildServiceProvider();

                _ = StartupOrquestrator.InitializeAsync(Services, splash);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Fatal error during application startup");
            }

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

                if (!IsWindowTransitioning && !ShouldKeepAppInTray())
                {
                    RequestShutdown();
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




