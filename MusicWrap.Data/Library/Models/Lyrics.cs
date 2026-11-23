using MessagePack;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public sealed class Lyrics
    {
        [Key(0)] public int Id {get; set;}
        [Key(1)] public required int TrackId {get; set;}
        [Key(2)] public required string Language {get; set;}
        [Key(3)] public required bool Default {get; set;}
        [Key(4)] public required LyricsType Type {get; set;}
        [Key(5)] public required LyricsOrigin Origin {get; set;}
        [Key(6)] public required string Provider {get; set;} // filepath or url

        [Key(10)] public required string? LyricsText {get; set;}
    }

    public enum LyricsOrigin
    {
        Manual,
        Embedded,
        File,
        Network
    }
    public enum LyricsType
    {
        plain,
        synced,
        enhanced
    }
}
