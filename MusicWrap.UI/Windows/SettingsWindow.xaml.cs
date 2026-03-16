using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Pages.MainWindow;
using MusicWrap.UI.Pages.Settings;
using MusicWrap.UI.ViewModels.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MusicWrap.UI.Windows
{
    /// <summary>
    /// Lógica de interacción para SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private string currentTab;
        private SettingsIndexViewModel viewModel => (SettingsIndexViewModel)DataContext;
        public SettingsWindow()
        {
            InitializeComponent();

            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            NavigateToPage(viewModel.SelectedTab);
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(viewModel.SelectedTab))
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
