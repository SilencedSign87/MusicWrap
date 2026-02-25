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

        public PlayerPage()
        {
            InitializeComponent();

            _viewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewModel;

            PlayerSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(PlayerSlider_DragStarted));
            PlayerSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(PlayerSlider_DragCompleted));
        }
        private void PlayerSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _viewModel?.StartSeekingCommand.Execute(null);
        }

        private void PlayerSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _viewModel?.EndSeekingCommand.Execute(PlayerSlider.Value);
        }

        private void PlayerSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Thumb) // ignore if is dragging the thumb
            {
                _viewModel?.StartSeekingCommand.Execute(null);
            }
        }

        private void PlayerSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not Thumb) // ignore if is dragging the thumb
            {
                _viewModel?.EndSeekingCommand.Execute(PlayerSlider.Value);
            }
        }

        private void PlayerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_viewModel != null)
            {
                if (Math.Abs(_viewModel.CurrentPosition - e.NewValue) > 0.1)
                {
                    _viewModel.FormattedPosition = FormatTime(e.NewValue);
                }
            }
        }

        private static string FormatTime(double seconds)
        {
            var time = System.TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss");
            return time.ToString(@"m\:ss");
        }
    }
}
