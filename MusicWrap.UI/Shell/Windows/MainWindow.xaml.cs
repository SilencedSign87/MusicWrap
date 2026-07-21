using Jot;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Shell.ViewModel;
using System.ComponentModel;
using System.Windows;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly WindowManager _windowManager;
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(Tracker tracker, PlayerPage playerPage, MainWindowViewModel viewmodel, WindowManager windowManager)
        {
            InitializeComponent();
            _viewModel = viewmodel;
            _windowManager = windowManager;
            DataContext = _viewModel;

            PlayerContainer.Children.Add(playerPage);

            Loaded += (s, e) =>
            {
             BorderWindow.Padding = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
            };

            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
            Closed += MainWindow_Closed;

            UpdateBackdrop();


            tracker.Track(this);
        }

        private void UpdateBackdrop()
        {
            if (!BackdropHelper.IsBackdropSupported() && !BackdropHelper.IsBackdropSupported())
            {
                this.SetResourceReference(BackgroundProperty, "WindowBackground");
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            BorderWindow.Padding = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_windowManager.IsShuttingDown || _windowManager.IsWindowTransitioning)
            {
                return;
            }

            if (_windowManager.ShouldKeepAppInTray())
            {
                return;
            }

            _windowManager.RequestShutdown();
        }
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StateChanged -= MainWindow_StateChanged;
            Closing -= MainWindow_Closing;
            Closed -= MainWindow_Closed;
        }
        private void MainWindowRoot_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F11)
            {
                _windowManager.SwitchToFullScreenPlayer();
                e.Handled = true;
            }

        }

        private void ReleaseResources()
        {
            App.Services.GetService<IwindowsImageService>()?.ClearCache();

            _viewModel.Dispose();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

    }
}

