using MessagePack;
using MusicWrap.Data.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public class CoverAsset
    {
        [Key(0)] public int Id;
        /// <summary>
        /// Filename of the cover asset, relative to the cover assets directory and type. "/Roaming/MusicWrap/Data/Covers/*/*"
        /// </summary>
        [Key(1)] public required string FileName;

        [Key(2)] public string Fingerprint = string.Empty;
        [Key(3)] public string DominantColorHex = CommonColors.DominantColorFallback;
        [Key(4)] public string DominantForegroundHex = CommonColors.ForegroundOnFallback;
        [Key(5)] public string HighlightColorHex = CommonColors.HighlightColorFallback;
        [Key(6)] public string HighlightForegroundHex = CommonColors.ForegroundOnFallback;
    }
}
