using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library
{
    [MessagePackObject]
    public class KeyValue
    {
        [Key(0)] public required string Key { get; set; }
        [Key(1)] public required byte[] Value { get; set; }
    }
}
