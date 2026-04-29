using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public  partial class SettingsIndexViewModel : ObservableObject
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

