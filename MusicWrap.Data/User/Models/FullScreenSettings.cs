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
        [Key(4)] public int WaveformRenderMode { get; set; } = 1;
        [Key(5)] public bool WaveformMirrored { get; set; } = false;
        [Key(6)] public bool TintForeground { get; set; } = false;
    }

    public enum FullscreenPlayerPanel
    {
        None,
        Lyrics,
        Queue
    }
}
