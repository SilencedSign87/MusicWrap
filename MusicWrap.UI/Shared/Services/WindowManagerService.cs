using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Threading;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Windows;
using System.Windows;

namespace MusicWrap.UI.Shared.Services
{
    public class WindowManagerService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MusicWrapSettings _userSettings;
        public bool IsShuttingDown { get; set; }

        private readonly List<IDisposable> _trackedDisposables = [];

        // windows
        public ShellWindow? ShellWindow { get; private set; }
        public Window? CurrentWindow => ShellWindow;
        private NewPlaylistWindow? newPlaylistWindow = null;
        private InformationWindow? metadataEditorWindow = null;
        private SettingsWindow? settingsWindow = null;

        public event Action<Window?>? CurrentWindowChanged;

        // scope
        private readonly IServiceScopeFactory _scopeFactory;
        private IServiceScope? _metadataEditorScope;
        private readonly TaskbarController _taskbarController;
        private readonly IUIDispatcher _dispatcher;

        public WindowManagerService(IServiceProvider serviceProvider, IServiceScopeFactory scopeFactory,MusicWrapSettings userSettings, TaskbarController taskbarController, IUIDispatcher dispatcher)
        {
            _serviceProvider = serviceProvider;
            _scopeFactory = scopeFactory;
            _userSettings = userSettings;
            _taskbarController = taskbarController;
            _dispatcher = dispatcher;
            _userSettings.PropertyChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // restore window if tray is disabled to prevent the app from being stuck in tray
            if (e.PropertyName == nameof(MusicWrapSettings.KeepAppInTray))
            {   
                if(!_userSettings.KeepAppInTray && ShellWindow is not null && !ShellWindow.IsVisible)
                {
                    _dispatcher.Invoke(() =>
                    {
                        ShowOrRestoreCurrentWindow();
                    });
                }
            }
        }

        #region Dialog launchers
        public void LaunchSettingsWindow()
        {
            if (IsShuttingDown)
            {
                return;
            }
            if (settingsWindow is null)
            {
                settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                var currentWindow = CurrentWindow;
                if (currentWindow is null) return;
                settingsWindow.Closed += (_, _) => settingsWindow = null;
                WindowHelper.LauchFromParent(currentWindow, settingsWindow, false);
            }
            else
            {
                settingsWindow.Activate();
                return;
            }
        }
        public void LaunchInformationWindow(List<int> trackIds)
        {
            if(trackIds is null || trackIds.Count == 0 || IsShuttingDown || CurrentWindow is null) return;

            if(metadataEditorWindow is { IsLoaded: true} w)
            {
                w.Initialize(trackIds);
                w.Activate();
                return;
            }

            var scope = _scopeFactory.CreateScope();
            var window = scope.ServiceProvider.GetRequiredService<InformationWindow>();

            window.Closed += (_, _) =>
            {
                metadataEditorWindow = null;
                _metadataEditorScope?.Dispose();
                _metadataEditorScope = null;
            };

            metadataEditorWindow = window;
            _metadataEditorScope = scope;

            window.Initialize(trackIds);
            WindowHelper.LauchFromParent(CurrentWindow, window, false);

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

                newPlaylistWindow.Closed += (_, _) => newPlaylistWindow = null;
            }
            else
            {
                newPlaylistWindow.AddTracks(tracksId ?? []);
            }

            newPlaylistWindow.Activate();
        }

        #endregion
        #region Cleanup
        public void TrackForCleanup(IDisposable disposable)
        {
            _trackedDisposables.Add(disposable);
        }
        #endregion

        #region Window Switching

        public void SwitchToCompactPlayer() => ShowMode(PlayerMode.CompactPlayer);
        public void SwitchToFullScreenPlayer() => ShowMode(PlayerMode.FullScreenPlayer);
        public void SwitchToMainPlayer() => ShowMode(PlayerMode.MainPlayer);

        #endregion
        #region Window Management
        public void ShowOrRestoreCurrentWindow()
        {
            if (ShellWindow is not null && IsWindowUsable(ShellWindow))
            {
                ShellWindow.ApplyMode(_userSettings.LastWindowMode);
                TryShowWindow(ShellWindow);
                return;
            }

            ShowMode(_userSettings.LastWindowMode);
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
        private UIElement GetContent(PlayerMode mode) => mode switch
        {
            PlayerMode.CompactPlayer => _serviceProvider.GetRequiredService<CompactPlayer>(),
            PlayerMode.FullScreenPlayer => _serviceProvider.GetRequiredService<FullScreenWindow>(),
            _ => _serviceProvider.GetRequiredService<MainPlayer>(),
        };
        private void ShowMode(PlayerMode mode)
        {
            if (IsShuttingDown) return;
           
            EnsureShellWindow();

            var content = GetContent(mode);

            ShellWindow!.ApplyMode(mode);
            ShellWindow.SetContent(content);

            if (!ShellWindow.IsVisible)
                ShellWindow.Show();

            ShellWindow.Activate();
            ShellWindow.Focus();

            _userSettings.LastWindowMode = mode;

        }
        private void EnsureShellWindow()
        {
            if (ShellWindow is not null)
                return;

            ShellWindow = _serviceProvider.GetRequiredService<ShellWindow>();

            TrackCurrentWindow(ShellWindow);

            CurrentWindowChanged?.Invoke(ShellWindow);
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
            _taskbarController.Attach(window);

            window.Closed += (_, _) =>
            {
                _taskbarController.Detach();

                if (ReferenceEquals(ShellWindow, window))
                    ShellWindow = null;

                if (ShouldKeepAppInTray())
                {
                    CleanupForTray();
                    return;
                }

                RequestShutdown();
            };
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
        }
        #endregion
    }
}
