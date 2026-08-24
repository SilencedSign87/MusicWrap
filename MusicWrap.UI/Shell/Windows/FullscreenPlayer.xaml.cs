using MusicWrap.UI.Shell.ViewModel;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para FullScreenWindow.xaml
    /// </summary>
    public partial class FullScreenWindow : UserControl
    {
        public FullScreenWindow(FullscreenWindowViewModel viewmodel)
        {
            InitializeComponent();

            DataContext = viewmodel;
        }
    }
}
