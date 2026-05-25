using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Library.Models
{
    [MessagePackObject]
    public sealed class LibraryCache
    {
        [Key(0)] public LibraryEntry[]? _ByArtists { get; set; } = null;
        [Key(1)] public LibraryEntry[]? _ByAlbums { get; set; } = null;
        [Key(2)] public LibraryEntry[]? _ByGenres { get; set; } = null;
        [Key(3)] public LibraryEntry[]? _ByDecades { get; set; } = null;
    }
}
