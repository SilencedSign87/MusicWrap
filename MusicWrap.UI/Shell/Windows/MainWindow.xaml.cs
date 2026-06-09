using Jot;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
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
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(Tracker tracker, PlayerPage playerPage, MainWindowViewModel viewmodel)
        {
            InitializeComponent();
            _viewModel = viewmodel;
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
            if (App.IsShuttingDown || App.IsWindowTransitioning)
            {
                return;
            }

            ReleaseResources();

            if (App.ShouldKeepAppInTray())
            {
                return;
            }

            App.Services.GetService<IMusicPlayerService>()?.FlushPlaybackState();
            App.RequestShutdown();
        }
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StateChanged -= MainWindow_StateChanged;
            Closing -= MainWindow_Closing;
            Closed -= MainWindow_Closed;
        }

        private void ReleaseResources()
        {
            App.Services.GetService<IImageService>()?.ClearCache();

            _viewModel.Dispose();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}

