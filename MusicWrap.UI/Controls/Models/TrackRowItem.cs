using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Controls.Models
{
    public enum TrackVisualMode
    {
        CompactNoCover = 0,
        WithCoverArt = 1
    }
    public enum TrackIndexDisplayMode
    {
        None = 0,
        ListIndex = 1,
        TrackNumber = 2
    }
    public sealed class TrackRowItem
    {
        public int Id { get; init; }
        public int ListIndex { get; init; }
        public int DiskNumber { get; init; }
        public int TrackNumber { get; init; }
        public string Title { get; init; } = "";
        public string ArtistNames { get; init; } = "";
        public string DurationText { get; init; } = "";
        public string? CoverAssetPath { get; init; }
        public string TrackNumberDisplay => TrackNumber > 0 ? TrackNumber.ToString() : "";
    }
}
