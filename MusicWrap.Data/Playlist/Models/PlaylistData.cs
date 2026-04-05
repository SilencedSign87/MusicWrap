using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Playlist.Models
{
    [MessagePackObject]
    public sealed class PlaylistData
    {
        [Key(0)] public int Version { get; set; } = 1;
        [Key(1)] public Playlist[] Playlists { get; set; } = [];
    }
}
