using MessagePack;

namespace MusicWrap.Data.Playlist.Models
{
    [MessagePackObject]
    public sealed class PlaylistItem
    {
        [Key(0)] public int TrackId { get; set; }
        [Key(1)] public long AddedAtUtcTicks { get; set; }
    }
}
