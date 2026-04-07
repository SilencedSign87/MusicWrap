using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

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

        [Key(100)] public int CoverId;
    }
}
