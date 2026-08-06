using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public sealed class YoutubeSettings
    {
        [Key(1)] public bool EnableYoutubeLibraryFolders { get; set; } = false;
        [Key(2)] public string YoutubeLibraryRootPath { get; set; } = string.Empty;
        [Key(3)] public string YoutubePathTemplate { get; set; } = "{artist}/{album}/{trackNumber} - {title}";
        [Key(4)] public SuportedFFMpegAudioFormat PreferredAudioFormatForYoutube { get; set; } = SuportedFFMpegAudioFormat.mp3;
    }
}
