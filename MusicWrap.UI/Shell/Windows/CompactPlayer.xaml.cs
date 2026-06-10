using Jot;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;

namespace MusicWrap.UI.Shell.Windows
{
    public partial class CompactPlayer : Window
    {
        private PlayerViewModel? _viewModel;
        private readonly QueueListPage _queuePage;
        private readonly WindowManager _windowManager;

        private bool _isQueueOpen = false;
        private const int _playerWidth = 250;
        private const int _compactHeight = 320;
        private const int _expandedHeight = 700;

        public CompactPlayer(Tracker tracker, PlayerViewModel playervm, QueueListPage queuepage, WindowManager wm)
        {
            _viewModel = playervm;
            _windowManager = wm;
            _queuePage = queuepage;

            InitializeComponent();
            InitializeWindow();

            DataContext = _viewModel;

            Closed += CompactPlayer_Closed;
            Closing += CompactPlayer_Closing;

            QueueTab.Content = queuepage;
        }

        private void CompactPlayer_Closed(object? sender, EventArgs e)
        {
            Closed -= CompactPlayer_Closed;
        }

        private void InitializeWindow()
        {
            Width = _playerWidth;
            Height = _compactHeight;
        }

        private void HandleOpenMainPlayer(object sender, RoutedEventArgs e)
        {
            _windowManager.SwitchToMainPlayer();
        }

        private void HandleOpenQueue(object sender, RoutedEventArgs e)
        {
            _isQueueOpen = !_isQueueOpen;
            if (_isQueueOpen)
            {
                Height = _expandedHeight;
                QueuePanel.Visibility = Visibility.Visible;
                QueuePanel.Height = _expandedHeight - _compactHeight;
                PanelIcon.Text = "\xE70E";
            }
            else
            {
                QueuePanel.Visibility = Visibility.Collapsed;
                Height = _compactHeight;
                PanelIcon.Text = "\xE70D";
            }
        }

        private void HandleToggleAllwayOnTop(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            if (Topmost)
            {
                PinIconFont.Text = "\ue77a";
            }
            else
            {
                PinIconFont.Text = "\ue718";
            }
        }

        private void HandleCloseApp(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CompactPlayer_Closing(object? sender, CancelEventArgs e)
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

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }
 
    }
}


