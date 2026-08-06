using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public class NowPlayingSettings
    {
        [Key(0)] public bool ShowLyrics { get; set; } = false;
        [Key(1)] public PreferredVisualizer PreferredVisualizer { get; set; } = PreferredVisualizer.LineSpectrum;
        [Key(2)] public bool BlurEffect { get; set; } = true;
    }

    public enum PreferredVisualizer
    {
        None,
        LineSpectrum,
        GradientSpectrum,
    }
}
