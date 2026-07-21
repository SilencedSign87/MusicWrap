using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Windows;
using System.Windows;

namespace MusicWrap.UI.Shared.Services
{
    public class WindowManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly UserSettings _userSettings;


        private int _windowTransitionDepth = 0;
        public bool IsShuttingDown { get; set; }
        public bool IsWindowTransitioning => _windowTransitionDepth > 0;

        private readonly List<IDisposable> _trackedDisposables = [];

        // windows
        public Window? CurrentWindow { get; private set; }
        private NewPlaylistWindow? newPlaylistWindow = null;
        private MetadataEditorWindow? metadataEditorWindow = null;
        public event Action<Window?>? CurrentWindowChanged;

        public WindowManager(IServiceProvider serviceProvider, UserSettings userSettings)
        {
            _serviceProvider = serviceProvider;
            _userSettings = userSettings;
        }

        #region Dialog launchers
        public void LaunchSettingsWindow()
        {
            var currentWindow = CurrentWindow;
            if (currentWindow is null) return;

            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();

            WindowHelper.LauchFromParent(currentWindow, settingsWindow, false);
        }

        public void LaunchIndexingWindow()
        {
            var currentWindow = CurrentWindow;
            if (currentWindow is null) return;

            var IndexingWindow = _serviceProvider.GetRequiredService<IndexingWindow>();

            WindowHelper.LauchFromParent(currentWindow, IndexingWindow, false);

        }

        public void LaunchNewPlaylistWindow(IEnumerable<int>? tracksId = null)
        {
            var currentWindow = CurrentWindow;
            if (currentWindow is null) return;

            if (newPlaylistWindow is null)
            {
                newPlaylistWindow = _serviceProvider.GetRequiredService<NewPlaylistWindow>();

                newPlaylistWindow.Initialize(tracksId);

                WindowHelper.LauchFromParent(currentWindow, newPlaylistWindow, false);

                newPlaylistWindow.Closed += NewPlaylistWindow_Closed;
            }
            else
            {
                newPlaylistWindow.AddTracks(tracksId ?? []);
            }

            newPlaylistWindow.Activate();
        }

        private void NewPlaylistWindow_Closed(object? sender, EventArgs e)
        {
            newPlaylistWindow?.Closed -= NewPlaylistWindow_Closed;
            newPlaylistWindow = null;
        }

        #endregion
        #region Cleanup
        public void TrackForCleanup(IDisposable disposable)
        {
            _trackedDisposables.Add(disposable);
        }
        #endregion

        #region Window Switching

        public void SwitchToCompactPlayer() => ShowCompactPlayer();
        public void SwitchToFullScreenPlayer() => ShowFullScreenPlayer();
        public void SwitchToMainPlayer() => ShowMainPlayer();

        #endregion
        #region Window Management
        public void ShowOrRestoreCurrentWindow()
        {
            if (!TryShowWindow(CurrentWindow))
            {
                switch (_userSettings.LastWindowMode)
                {
                    case LastWindowMode.FullScreen:
                        ShowFullScreenPlayer();
                        break;
                    case LastWindowMode.CompactPlayer:
                        ShowCompactPlayer();
                        break;
                    default:
                        ShowMainPlayer();
                        break;
                }
            }
        }
        public void RequestShutdown()
        {
            IsShuttingDown = true;
            Application.Current.Shutdown();
        }
        public bool ShouldKeepAppInTray() =>
            _userSettings?.KeepAppInTray == true;
        #endregion
        #region Internal
        private void ShowMainPlayer()
        {
            if (CurrentWindow is MainWindow existingMain && TryShowWindow(existingMain))
                return;

            if (IsWindowUsable(CurrentWindow))
                CloseForWindowTransition(CurrentWindow!);

            var main = _serviceProvider.GetRequiredService<MainWindow>();
            TrackCurrentWindow(main);

            if (main.DataContext is IDisposable disposable)
            {
                TrackForCleanup(disposable);
            }

            main.Show();
            CurrentWindow = main;
            CurrentWindowChanged?.Invoke(main);
            _userSettings.LastWindowMode = LastWindowMode.MainPlayer;
        }
        private void ShowCompactPlayer()
        {
            if (CurrentWindow is CompactPlayer existingCompact && TryShowWindow(existingCompact))
                return;

            if (IsWindowUsable(CurrentWindow))
                CloseForWindowTransition(CurrentWindow!);

            var player = _serviceProvider.GetRequiredService<CompactPlayer>();
            TrackCurrentWindow(player);

            player.Show();
            CurrentWindow = player;
            CurrentWindowChanged?.Invoke(player);
            _userSettings.LastWindowMode = LastWindowMode.CompactPlayer;
        }
        private void ShowFullScreenPlayer()
        {
            if (CurrentWindow is FullScreenWindow existing && TryShowWindow(existing))
                return;

            if (IsWindowUsable(CurrentWindow))
                CloseForWindowTransition(CurrentWindow!);

            var fullscreen = _serviceProvider.GetRequiredService<FullScreenWindow>();

            TrackCurrentWindow(fullscreen);

            fullscreen.Show();
            CurrentWindow = fullscreen;
            CurrentWindowChanged?.Invoke(fullscreen);
            _userSettings.LastWindowMode = LastWindowMode.FullScreen;
        }
        private static bool TryShowWindow(Window? window)
        {
            if (!IsWindowUsable(window))
                return false;

            if (window!.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            if (!window.IsVisible)
                window.Show();

            window.Activate();
            window.Focus();
            return true;
        }
        private static bool IsWindowUsable(Window? window) =>
            window is not null
            && window.IsLoaded
            && !window.Dispatcher.HasShutdownStarted
            && !window.Dispatcher.HasShutdownFinished;

        private void TrackCurrentWindow(Window window)
        {
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(CurrentWindow, window))
                    CurrentWindow = null;
                // no windows and tray
                if (!IsWindowTransitioning && CurrentWindow is null && ShouldKeepAppInTray())
                {
                    CleanupForTray();
                    return;
                }

                // no windows and no tray
                if (!IsWindowTransitioning && !ShouldKeepAppInTray())
                    RequestShutdown();

            };
        }
        private void CloseForWindowTransition(Window window)
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

        private void CleanupForTray()
        {
            var imageService = _serviceProvider.GetService<IwindowsImageService>();
            imageService?.ClearCache();
            var libraryService = _serviceProvider.GetService<ILibraryService>();
            libraryService?.ClearLibraryCache();

            foreach (var d in _trackedDisposables)
            {
                try { d.Dispose(); } catch { }
            }
            _trackedDisposables.Clear();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        #endregion
    }
}
