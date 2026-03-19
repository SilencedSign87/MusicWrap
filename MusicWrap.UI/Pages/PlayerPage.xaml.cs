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

            //PlayerSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(PlayerSlider_DragStarted));
            //PlayerSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(PlayerSlider_DragCompleted));
        }

        private static string FormatTime(double seconds)
        {
            var time = System.TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss");
            return time.ToString(@"m\:ss");
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

        private void WaveformPlayerControl_SeekCanceled(object sender, EventArgs e)
        {
            if (_viewModel?.CancelSeekingCommand.CanExecute(null) == true)
            {
                _viewModel.CancelSeekingCommand.Execute(null);
            }
        }
    }
}
