using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para TrackInformationPage.xaml
    /// </summary>
    public partial class TrackInformationPage : UserControl
    {
        private PlayerViewModel _vm;
        public TrackInformationPage()
        {
            InitializeComponent();
            _vm = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _vm;
        }

        private void OpenArtwork(object sender, RoutedEventArgs e)
        {
            _vm.OpenArtworkOnDefaultApp();
        }
    }
}

