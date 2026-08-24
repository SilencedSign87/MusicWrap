using MusicWrap.Core.Saving;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Shared.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para ShellWindow.xaml
    /// </summary>
    public partial class ShellWindow : Window
    {
        private bool _isInitializing = true;
        private readonly WindowManagerService _windowManager;
        private readonly MusicWrapSettings _settings;
        public PlayerMode CurrentMode { get; private set; }
        public ShellWindow(WindowManagerService windowManager, MusicWrapSettings settings)
        {
            InitializeComponent();
            _windowManager = windowManager;
            _settings = settings;
            CurrentMode = PlayerMode.MainPlayer;
        }
        public void ApplyMode(PlayerMode mode)
        {
            CaptureCurrentModeBounds();
            CurrentMode = mode;

            switch (CurrentMode)
            {
                case PlayerMode.MainPlayer:
                    ConfigureMainPlayer();
                    RestoreStateBounds(_settings.MainPlayerBounds, true);
                    break;
                case PlayerMode.CompactPlayer:
                    ConfigureCompactPlayer();
                    RestoreStateBounds(_settings.CompactPlayerBounds, false);
                    break;
                case PlayerMode.FullScreenPlayer:
                    ConfigureFullScreenPlayer();
                    break;
            }
            if (_isInitializing) _isInitializing = false;

        }
        public void SetContent(UIElement content)
        {
            ContentHost.Content = content;
        }

        #region PlayerModes
        private void ConfigureMainPlayer()
        {
            if (WindowState != WindowState.Normal)
                WindowState = WindowState.Normal;

            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Topmost = false;

            MinWidth = 900;
            MinHeight = 670;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;

            Width = 1200;
            Height = 800;

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 32,
                CornerRadius = new CornerRadius(12),
                GlassFrameThickness = new Thickness(-1),
                ResizeBorderThickness = new Thickness(4),
                UseAeroCaptionButtons = true,
                NonClientFrameEdges = NonClientFrameEdges.None
            });

            UpdateContentLayout();
            UpdateLayout();
        }

        private void ConfigureCompactPlayer()
        {
            if (WindowState != WindowState.Normal)
                WindowState = WindowState.Normal;

            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanMinimize;
            WindowState = WindowState.Normal;
            Topmost = false;

            MinWidth = 0;
            MinHeight = 0;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;

            Width = 250;
            Height = 320;

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 250,
                CornerRadius = new CornerRadius(12),
                GlassFrameThickness = new Thickness(1),
                ResizeBorderThickness = new Thickness(4),
                UseAeroCaptionButtons = true,
                NonClientFrameEdges = NonClientFrameEdges.None
            });

            ContentHost.Margin = new Thickness(0);

            UpdateLayout();
        }

        private void ConfigureFullScreenPlayer()
        {
            WindowChrome.SetWindowChrome(this, null);

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Manual;
            Topmost = false;

            MinWidth = 0;
            MinHeight = 0;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;

            ContentHost.Margin = new Thickness(0);
            WindowState = WindowState.Maximized;

            UpdateLayout();
        }
        private void CaptureCurrentModeBounds()
        {
            if (_isInitializing || CurrentMode == PlayerMode.FullScreenPlayer) return;

            var target = CurrentMode == PlayerMode.MainPlayer
                ? _settings.MainPlayerBounds
                : _settings.CompactPlayerBounds;

            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            target.Left = bounds.Left;
            target.Top = bounds.Top;
            target.Width = bounds.Width;
            target.Height = bounds.Height;
            target.IsMaximized = CurrentMode == PlayerMode.MainPlayer && WindowState == WindowState.Maximized;
        }
        private void RestoreStateBounds(WindowBoundsState state, bool allowMaximize)
        {
            if (double.IsNaN(state.Left) || double.IsNaN(state.Top) ||
                double.IsNaN(state.Width) || double.IsNaN(state.Height))
                return;

            Left = state.Left;
            Top = state.Top;
            if (CurrentMode == PlayerMode.MainPlayer)
            {
                Width = state.Width;
                Height = state.Height;
            }

            if (allowMaximize && state.IsMaximized)
                WindowState = WindowState.Maximized;
        }
        #endregion

        #region Listeners
        private void ShellWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                if (CurrentMode == PlayerMode.FullScreenPlayer)
                    _windowManager.SwitchToMainPlayer();
                else
                    _windowManager.SwitchToFullScreenPlayer();

                e.Handled = true;
            }

            if (e.Key == Key.Escape &&
                CurrentMode == PlayerMode.FullScreenPlayer)
            {
                _windowManager.SwitchToMainPlayer();
                e.Handled = true;
            }
        }

        private void ShellWindow_Closing(object? sender, CancelEventArgs e)
        {
            CaptureCurrentModeBounds();

            if (_windowManager.IsShuttingDown)
                return;

            if (_windowManager.ShouldKeepAppInTray())
                return;

            _windowManager.RequestShutdown();
        }

        private void RootShellWindow_StateChanged(object sender, EventArgs e)
        {
            UpdateContentLayout();
        }
        private void UpdateContentLayout()
        {
            ContentHost.Margin = CurrentMode == PlayerMode.MainPlayer &&
                                 WindowState == WindowState.Maximized
                ? new Thickness(8)
                : new Thickness(0);
        }
        private void ShellWindow_Closed(object? sender, EventArgs e)
        {
            Closing -= ShellWindow_Closing;
            Closed -= ShellWindow_Closed;
            KeyDown -= ShellWindow_KeyDown;
        }
        #endregion

    }
}
