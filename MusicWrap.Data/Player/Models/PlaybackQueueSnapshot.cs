using MessagePack;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Data.Player.Models
{
    [MessagePackObject]
    public sealed class PlaybackQueueSnapshot
    {
        [Key(0)] public int[] TrackIds { get; set; } = Array.Empty<int>();
        [Key(1)] public int CurrentIndex { get; set; } = -1;
        [Key(2)] public int CurrentPlaybackIndex { get; set; } = -1;
        [Key(3)] public int[] PlaybackOrderIndices { get; set; } = Array.Empty<int>();
        [Key(4)] public double PositionInSeconds { get; set; } = 0;
        [Key(5)] public PlaybackState PlaybackState { get; set; } = PlaybackState.Stopped;
        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
