using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library
{
    [MessagePackObject]
    public sealed class ScanDirectory
    {
        [Key(0)] public required string Path { get; set; }
        [Key(1)] public bool Recursive { get; set; }
        [Key(2)] public DateTime LastScan { get; set; }
    }
}
