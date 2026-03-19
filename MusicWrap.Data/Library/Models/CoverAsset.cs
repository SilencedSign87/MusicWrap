using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public class CoverAsset
    {
        [Key(0)] public int Id;
        [Key(1)] public required string FileName; // Root is /Roaming/MusicWrap/Data/Covers/**.*
        [Key(2)] public string Fingerprint;
        [Key(3)] public string DominantColorHex;
        [Key(4)] public string ForegroundColorHex;
    }
}
