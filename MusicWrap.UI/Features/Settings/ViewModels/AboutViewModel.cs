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
                    IconGlyph = "\xE8F1",
                    Name = "Managed Bass",
                    Description = "Free Open-Source Cross-Platform .Net Wrapper for Un4seen Bass audio library and its AddOns.",
                    Url ="https://github.com/ManagedBass/Home"
                },
                new Credits{
                    IconGlyph = "\xE8F1",
                    Name = "TagLibSharp",
                    Description = ".NET platform-independent library for reading and writing metadata in media files.",
                    Url ="https://github.com/mono/taglib-sharp"
                },
                new Credits
                {
                    IconGlyph = "\xE8F1",
                    Name = "NetVips",
                    Description = ".NET binding for the libvips image processing library. ",
                    Url ="https://kleisauke.github.io/net-vips/"
                },
                 new Credits
                {
                    IconGlyph = "\xE8F1",
                    Name = "MessagePack for C#",
                    Description = "The extremely fast MessagePack serializer for C#.",
                    Url ="https://github.com/MessagePack-CSharp/MessagePack-CSharp"
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

