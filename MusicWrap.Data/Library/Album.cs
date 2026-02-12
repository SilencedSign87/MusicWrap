using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library
{
    [MessagePackObject]
    public sealed class Album
    {
        [Key(0)] public int Id;

        [Key(1)] public required string Title;

        [Key(2)] public required int[] ArtistIds;

        [Key(3)] public int CoverId;

        [Key(4)] public int Year;
    }
}
