using MessagePack;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public sealed class Track
    {
        // Identity
        [Key(0)] public int Id;
        [Key(1)] public long FileSize;
        [Key(2)] public long LastWriteTime; // fingerprint
        [Key(3)] public required string FilePath;

        // Generic tags
        [Key(4)] public string? Title;
        [Key(5)] public string[] Artists = [];
        [Key(6)] public string[] AlbumArtists = [];
        [Key(7)] public string? AlbumName;
        [Key(8)] public string[] Genres = [];
        [Key(9)] public int TrackNumber = 1;
        [Key(10)] public int DiskNumber = 1;
        [Key(11)] public int? ReleaseYear;
        [Key(12)] public DateTime? ReleaseDate;

        // Format tags
        [Key(13)] public required int DurationSeconds;
        [Key(14)] public int BitRate;
        [Key(15)] public int SampleRate;
        [Key(16)] public int BitDepth;
        [Key(17)] public bool Lossless;
        [Key(18)] public int Channels;

        [Key(19)] public string? MusicBrainzId;
        [Key(20)] public TrackOrigin Origin = TrackOrigin.Local;

        [Key(21)] public string? ExternalId;

        // References
        [Key(100)] public int[] CoverIds = [];
    }

    public enum TrackOrigin
    {
        Local = 0,
        Youtube = 1,
    }
}
