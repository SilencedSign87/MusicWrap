using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Library;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Pages.Settings
{
    /// <summary>
    /// Lógica de interacción para SettingsDirectoriesManagerPage.xaml
    /// </summary>
    public partial class SettingsDirectoriesManagerPage : UserControl
    {
        public DirectoriesManagerViewModel vm;
        public SettingsDirectoriesManagerPage()
        {
            InitializeComponent();

            vm = App.Services.GetRequiredService<DirectoriesManagerViewModel>();
            DataContext = vm;
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(sender is ListView listview)
            {
                vm.SetSelectedDirectories([.. listview.SelectedItems.Cast<ScanDirectory>()]);
            }
        }
    }
}
