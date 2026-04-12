using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public partial class AboutViewModel : ObservableObject
    {
        [ObservableProperty] private string appName = "MusicWrap";
        [ObservableProperty] private string appVersion = "1.0.0";
        [ObservableProperty] private string developerUrl = "https://github.com/SilencedSign87";
        [ObservableProperty] private ObservableCollection<Credits> credits;
        public AboutViewModel()
        {
            credits = [
                new Credits
                {
                    IconGlyph = "\xE8F1",
                    Name = "BASS",
                    Description = "Audio Library",
                    Url = "https://www.un4seen.com/"
                },
                new Credits
                {
                    IconGlyph = "\xEA86",
                    Name = "BASSmix",
                    Description = "Add-on",
                    Url = "https://www.un4seen.com/"
                },
                new Credits
                {
                    IconGlyph = "\xE8F1",
                    Name = "BASS.NET",
                    Description = "Wrapper for the BASS Audio Library",
                    Url ="https://www.radio42.com/bass/index.html"
                }
            ];
        }
    }

    public sealed class Credits
    {
        public string IconGlyph { get; set; } = "\uE8D7";
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}

