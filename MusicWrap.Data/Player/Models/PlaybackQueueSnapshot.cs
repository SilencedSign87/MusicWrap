using MessagePack;
using System;

namespace MusicWrap.Data.Player.Models
{
    [MessagePackObject]
    public sealed class PlaybackQueueSnapshot
    {
        [Key(0)] public int[] TrackIds { get; set; } = Array.Empty<int>();
        [Key(1)] public int CurrentIndex { get; set; } = -1;
        [Key(3)] public int[] PlaybackOrderIndices { get; set; } = Array.Empty<int>();
        [Key(4)] public double PositionInSeconds { get; set; } = 0;
        [Key(5)] public int PlaybackStateValue { get; set; } = 0;
        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
