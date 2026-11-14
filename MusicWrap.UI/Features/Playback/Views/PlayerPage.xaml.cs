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
        public PlayerPage(PlayerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

        }
    }
}

