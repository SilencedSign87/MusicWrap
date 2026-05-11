using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Settings.ViewModels;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Settings.Views
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
            if (sender is ListView listview)
            {
                vm.SetSelectedDirectories([.. listview.SelectedItems.Cast<ScanDirectory>()]);
            }
        }
    }
}


