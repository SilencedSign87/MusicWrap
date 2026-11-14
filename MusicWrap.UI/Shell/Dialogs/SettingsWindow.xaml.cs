using MusicWrap.UI.Features.Settings.ViewModels;
using System.Windows;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsIndexViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }

    }
}



