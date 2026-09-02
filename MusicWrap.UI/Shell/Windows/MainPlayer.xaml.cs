using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Shell.ViewModel;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainPlayer : UserControl
    {
        public MainPlayer(PlayerPage playerPage, MainPlayerViewModel viewmodel)
        {
            InitializeComponent();
            DataContext = viewmodel;

            PlayerContainer.Children.Add(playerPage);
        }

    }
}

