using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public partial class SettingsIndexViewModel : ObservableObject
    {
        [ObservableProperty]
        private string selectedTab = "general";

        [RelayCommand]
        private void ChangeTab(string tab)
        {
            SelectedTab = tab;
        }
    }
}

