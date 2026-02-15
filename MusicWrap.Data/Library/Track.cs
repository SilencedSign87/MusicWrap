using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace MusicWrap.Data.Library
{
    [MessagePackObject]
    public sealed class Track
    {
        [Key(0)] public int Id;
        [Key(1)] public required string Path;
        [Key(2)] public required string Title;

        // Relaciones
        [Key(3)] public required int[] ArtistIds;
        [Key(4)] public int AlbumId;
        [Key(5)] public required int[] GenreIds;

        [Key(6)] public int Duration;

        [Key(7)] public long FileSize;
        [Key(8)] public long LastWriteTime;

        [Key(9)] public int Disk;
        [Key(10)] public int TrackNumber;

        [Key(100)] public int CoverId;

    }
}
