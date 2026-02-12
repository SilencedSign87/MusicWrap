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
        private bool _isQueueOpen = false;
        public CompactPlayer()
        {
            InitializeComponent();
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
                Height = 600;
                QueuePanel.Visibility = Visibility.Visible;
                QueuePanel.Height = 600 - 390;
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
    }
}
