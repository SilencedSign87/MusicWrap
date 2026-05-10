using Jot;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playback;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para CompactPlayer.xaml
    /// </summary>
    public partial class CompactPlayer : Window
    {
        private PlayerViewModel? _viewModel;
        private bool _isQueueOpen = false;
        private const int _playerWidth = 275;
        private const int _compactHeight = 370;
        private const int _expandedHeight = 700;
        private Window? _searcherWindow;

        public CompactPlayer(Tracker tracker)
        {
            InitializeComponent();
            InitializeWindowSize();
            _viewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewModel;

            Closed += CompactPlayer_Closed;
            Closing += CompactPlayer_Closing;
        }

        private void CompactPlayer_Closed(object? sender, EventArgs e)
        {
            Closed -= CompactPlayer_Closed;
            if (_searcherWindow != null)
            {
                _searcherWindow.Close();
            }
        }

        private void InitializeWindowSize()
        {
            Width = _playerWidth;
            Height = _compactHeight;
        }

        private void HandleOpenMainPlayer(object sender, RoutedEventArgs e)
        {
            App.ShowMain();
        }

        private void HandleOpenQueue(object sender, RoutedEventArgs e)
        {
            _isQueueOpen = !_isQueueOpen;
            if (_isQueueOpen)
            {
                Height = _expandedHeight;
                QueuePanel.Visibility = Visibility.Visible;
                QueuePanel.Height = _expandedHeight - _compactHeight;
                QueueFontIcon.Text = "\ue70e";
            }
            else
            {
                QueuePanel.Visibility = Visibility.Collapsed;
                Height = _compactHeight;
                QueueFontIcon.Text = "\ue70d";
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
            if (App.IsShuttingDown || App.IsWindowTransitioning)
            {
                return;
            }

            if (App.ShouldKeepAppInTray())
            {
                App.Services.GetService<ITrayService>()?.HideFlyout();
                return;
            }

            App.Services.GetService<IMusicPlayerService>()?.FlushPlaybackState();
            App.RequestShutdown();
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }

        private void WaveformPlayerControl_SeekStarted(object sender, EventArgs e)
        {
            if (_viewModel?.StartSeekingCommand.CanExecute(null) == true)
            {
                _viewModel.StartSeekingCommand.Execute(null);
            }
        }

        private void WaveformPlayerControl_SeekEnded(object sender, double e)
        {
            if (_viewModel?.EndSeekingCommand.CanExecute(e) == true)
            {
                _viewModel.EndSeekingCommand.Execute(e);
            }
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (_searcherWindow != null)
            {
                _searcherWindow.Activate();
                return;
            }
            _searcherWindow = new LibrarySearcherWindow
            {
                Owner = this
            };
            _searcherWindow.Show();
            _searcherWindow.Closed += (s, args) => _searcherWindow = null;
        }

        private void WaveformPlayerControl_SeekCanceled(object sender, EventArgs e)
        {
            if (_viewModel?.CancelSeekingCommand.CanExecute(null) == true)
            {
                _viewModel.CancelSeekingCommand.Execute(null);
            }
        }
    }
}


