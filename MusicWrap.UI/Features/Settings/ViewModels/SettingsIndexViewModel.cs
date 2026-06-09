using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Features.Settings.Views;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public partial class SettingsIndexViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentControl))]
        private int selectedIndex;

        public UserControl? CurrentControl => SelectedIndex switch
        {
            0 => _serviceProvider.GetRequiredService<SettingsGeneralPage>(),
            1 => _serviceProvider.GetRequiredService<DevicePage>(),
            2 => _serviceProvider.GetRequiredService<SettingsDirectoriesManagerPage>(),
            3 => _serviceProvider.GetRequiredService<SettingsYoutubeProviderPage>(),
            4 => _serviceProvider.GetRequiredService<AboutPage>(),
            _ => null
        };

        public SettingsIndexViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
    }
}

