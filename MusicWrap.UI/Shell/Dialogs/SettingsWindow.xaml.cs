using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Settings.ViewModels;
using MusicWrap.UI.Features.Settings.Views;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private string currentTab = string.Empty;
        private SettingsIndexViewModel viewModel => (SettingsIndexViewModel)DataContext;
        public SettingsWindow()
        {
            InitializeComponent();

            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            NavigateToPage(viewModel.SelectedTab);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(viewModel.SelectedTab))
            {
                NavigateToPage(viewModel.SelectedTab);
            }
        }

        private void NavigateToPage(string tab)
        {
            if (tab == currentTab)
                return;

            currentTab = tab;

            UserControl newControl = tab switch
            {
                "general" => new SettingsGeneralPage(),
                "library" => new SettingsDirectoriesManagerPage(),
                "player" => new DevicePage(),
                "about" => new AboutPage(),
                "youtube" => new SettingsYoutubeProviderPage(),
                _ => new SettingsGeneralPage()
            };

            SettingsContentControl.Content = newControl;
        }

        protected override void OnClosed(EventArgs e)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            base.OnClosed(e);
        }
    }
}



