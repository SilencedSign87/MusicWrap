using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.ViewModels.Settings
{
    public partial class SettingsGeneralViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _launchAtStartup;

        [RelayCommand]
        private void SaveSettings()
        {

        }
    }
}
