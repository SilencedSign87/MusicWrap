using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Playlist.Models
{
    [MessagePackObject]
    public class Playlist
    {
        [Key(0)] public int Id { get; set; }
        [Key(1)] public string Name { get; set; } = string.Empty;
        [Key(2)] public int[] TracksId { get; set; } = [];
        [Key(100)] public int CoverId;
    }
}
