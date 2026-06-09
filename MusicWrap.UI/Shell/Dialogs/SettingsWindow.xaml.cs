using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Settings.Views;
using MusicWrap.UI.Features.Settings.ViewModels;
using System.Windows;
using System.Windows.Controls;

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



