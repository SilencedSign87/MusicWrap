using MusicWrap.UI.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para TrackInformationPage.xaml
    /// </summary>
    public partial class TrackInformationPage : UserControl
    {
        private TrackInformationViewModel _vm;
        public TrackInformationPage(TrackInformationViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri is not null)
                Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}

