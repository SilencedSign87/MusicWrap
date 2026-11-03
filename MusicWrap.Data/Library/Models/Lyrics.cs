using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public sealed class Lyrics
    {
        [Key(0)] public int Id;
        [Key(1)] public required int TrackId;
        [Key(2)] public required string Language;
        [Key(3)] public required bool Original;
        [Key(4)] public required LyricsType Type;
        [Key(5)] public required LyricsOrigin Origin;
        [Key(6)] public required string Provider;

        [Key(10)] public required string? LyricsText;
    }

    public enum LyricsOrigin
    {
        user,
        embedded,
        file,
        network
    }
    public enum LyricsType
    {
        plain,
        synced,
        enhanced
    }
}
