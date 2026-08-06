using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.User.Models
{

    [MessagePackObject]
    public sealed class FFMpegSettings
    {
        [Key(1)] public bool UseCustomFfmpegPath { get; set; } = true;
        [Key(2)] public string CustomFfmpegPath { get; set; } = string.Empty;
    }
}
