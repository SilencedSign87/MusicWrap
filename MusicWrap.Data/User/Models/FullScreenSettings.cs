using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public class FullScreenSettings
    {
        [Key(0)] public PreferredVisualizer PreferredVisualizer { get; set; } = PreferredVisualizer.LineSpectrum;
        [Key(1)] public SpectrumType SpectrumType { get; set; } = SpectrumType.Normal;
        [Key(2)] public bool BlurEffect { get; set; } = true;
        [Key(3)] public FullscreenPlayerPanel ActivePanel { get; set; } = FullscreenPlayerPanel.None;
    }

    public enum FullscreenPlayerPanel
    {
        None,
        Lyrics,
        Queue
    }
}
