using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Player.Models
{
    [MessagePackObject]
    public sealed class PlaybackQueueSnapshot
    {
        [Key(0)] public int[] TrackIds { get; set; } = Array.Empty<int>();
        [Key(1)] public int CurrentIndex { get; set; } = -1;
        [Key(6)] public int CurrentPlaybackIndex { get; set; } = -1;
        [Key(7)] public bool IsShuffleEnabled { get; set; } = false;
        [Key(8)] public int[] PlaybackOrderIndices { get; set; } = Array.Empty<int>();
        [Key(2)] public double PositionInSeconds { get; set; } = 0;
        [Key(3)] public int RepeatMode { get; set; } = 0;
        [Key(4)] public int ContinueMode { get; set; } = 0;
        [Key(5)] public int PlaybackState { get; set; } = 0; // 0: paused, 1: stopped, 2: paused 
        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
