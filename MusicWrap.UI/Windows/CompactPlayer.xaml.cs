using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MusicWrap.UI.Windows
{
    /// <summary>
    /// Lógica de interacción para CompactPlayer.xaml
    /// </summary>
    public partial class CompactPlayer : Window
    {
        private PlayerViewModel? _viewModel;
        private bool _isUserSeeking = false;
        private bool _isQueueOpen = false;
        
        public CompactPlayer()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewModel;
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
                Height = 700;
                QueuePanel.Visibility = Visibility.Visible;
                QueuePanel.Height = 700 - 390;
                QueueFontIcon.Text = "\ue70e";
            }
            else
            {
                QueuePanel.Visibility = Visibility.Collapsed;
                Height = 390;
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
            // TODO: launch window to browse the library
        }
    }
}
