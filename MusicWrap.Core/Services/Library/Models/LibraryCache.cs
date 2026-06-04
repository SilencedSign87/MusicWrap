using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Library.Models
{
    [MessagePackObject]
    public sealed class LibraryCache
    {
        [Key(0)] public LibraryEntry[]? ByTrackArtists { get; set; } = null;
        [Key(1)] public LibraryEntry[]? ByAlbumArtists { get; set; } = null;
        [Key(2)] public LibraryEntry[]? ByAlbums { get; set; } = null;
        [Key(3)] public LibraryEntry[]? ByGenres { get; set; } = null;
        [Key(4)] public LibraryEntry[]? ByDecades { get; set; } = null;
    }
}
