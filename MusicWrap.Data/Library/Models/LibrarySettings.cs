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
        [Key(2)] public bool TrackSortAscending { get; set; } = true;
        [Key(3)] public int TrackSortModeValue { get; set; } = 0;
        [Key(4)] public int? SelectedTabKeyValue { get; set; } = null;
        [Key(5)] public bool EntryListAscending { get; set; } = true;
    }
}
