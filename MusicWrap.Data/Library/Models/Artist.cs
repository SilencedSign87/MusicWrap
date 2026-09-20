using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public sealed class Artist
    {
        [Key(0)] public int Id;
        [Key(1)] public required string Name;
        [Key(2)] public string? SortName;

        [IgnoreMember]
        public string ResolvedName => string.IsNullOrWhiteSpace(SortName) ? Name : SortName;
    }
}
