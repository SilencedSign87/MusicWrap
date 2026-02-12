using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library
{
    [MessagePackObject]
    public class Genre
    {
        [Key(0)]
        public int Id { get; set; }
        [Key(1)]
        public string Name { get; set; } = string.Empty;
    }
}
