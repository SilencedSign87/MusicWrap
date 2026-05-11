using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para PlayerPage.xaml
    /// </summary>
    public partial class PlayerPage : UserControl
    {
        private readonly PlayerViewModel _viewModel;
        public PlayerPage()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewModel;

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

