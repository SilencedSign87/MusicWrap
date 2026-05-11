using MessagePack;

namespace MusicWrap.Data.Playlist.Models
{
    [MessagePackObject]
    public sealed class Playlist
    {
        [Key(0)] public int Id { get; set; }
        [Key(1)] public string Name { get; set; } = string.Empty;
        [Key(2)] public List<PlaylistItem> Items { get; set; } = [];
        [Key(3)] public long CreatedAtUtcTicks { get; set; }
        [Key(4)] public long UpdatedAtUtcTicks { get; set; }
    }
}
