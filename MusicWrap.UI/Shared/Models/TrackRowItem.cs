using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Controls.Models
{
    public enum TrackVisualMode
    {
        CompactNoCover = 0,
        WithCoverArt = 1,
        ListImage = 2
    }
    public enum TrackIndexDisplayMode
    {
        None = 0,
        ListIndex = 1,
        TrackNumber = 2
    }
    public sealed record TrackReorderRequest(
        int SourceTrackId,
        int TargetTrackId,
        bool PlaceAfterTarget
    );
}


