using Hardcodet.Wpf.TaskbarNotification;
using Jot;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Favorites.Views;
using MusicWrap.UI.Features.Library.Components;
using MusicWrap.UI.Features.Library.Views;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Playlist.Views;
using MusicWrap.UI.Features.Providers.Views;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.ViewModel;
using MusicWrap.UI.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUIDispatcher _uiDispatcher;

        public MainWindow(Tracker tracker, IServiceProvider serviceProvider, PlayerPage playerPage, IUIDispatcher uiDispatcher, MainWindowViewModel viewmodel)
        {
            InitializeComponent();
            _viewModel = viewmodel;
            DataContext = _viewModel;

            _serviceProvider = serviceProvider;
            _uiDispatcher = uiDispatcher;
            PlayerContainer.Children.Add(playerPage);

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
            if (WindowState == WindowState.Maximized)
            {
                BorderWindow.Padding = new Thickness(8);
            }
            else
            {
                BorderWindow.Padding = new Thickness(0);
            }
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




