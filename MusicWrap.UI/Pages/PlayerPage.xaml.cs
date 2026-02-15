using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Pages
{
    /// <summary>
    /// Lógica de interacción para PlayerPage.xaml
    /// </summary>
    public partial class PlayerPage : UserControl
    {
        private PlayerViewModel? _viewModel;
        private bool _isUserSeeking = false;

        public PlayerPage()
        {
            InitializeComponent();

            _viewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewModel;
        }

        private void DeviceBtn_Click(object sender, RoutedEventArgs e)
        {
            DevicePopup.IsOpen = true;
        }

        private void QueueBtn_Click(object sender, RoutedEventArgs e)
        {
            QueuePopup.IsOpen = true;
        }

        private void PlusButton_Click(object sender, RoutedEventArgs e)
        {
            PlusPopup.IsOpen = true;
        }

        private void Seek(double position)
        {
            _viewModel?.SeekCommand.Execute(position);
        }
        private void PlayerSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isUserSeeking = true;
        }

        private void PlayerSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var slider = sender as Slider;
            _isUserSeeking = false;
            if (slider != null)
            {
                Seek(slider.Value);
            }
        }

    }
}
