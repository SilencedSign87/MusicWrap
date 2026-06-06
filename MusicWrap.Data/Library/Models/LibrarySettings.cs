using MessagePack;
using MusicWrap.Data.User.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public sealed class LibrarySettings
    {
        [Key(0)] public int? SelectedEntryId { get; set; } = null;
        [Key(1)] public LibraryEntryType EntryType { get; set; } = LibraryEntryType.AlbumArtist;
        [Key(2)] public bool LibraryAscending { get; set; } = true;
    }
}
