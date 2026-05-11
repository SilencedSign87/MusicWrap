using MessagePack;

namespace MusicWrap.Data.Library.Models
{
    [MessagePackObject]
    public sealed class MusicLibrary
    {
        [Key(0)] public List<Track> Tracks = [];
        [Key(1)] public List<ScanDirectory> Directories = [];
        [Key(2)] public List<CoverAsset> CoverAssets = [];


        [Key(999)] public int Version;
        [Key(1000)] public int NextTrackId { get; set; } = 1;
        [Key(1001)] public int NextCoverId { get; set; } = 1;

        public int GenerateTrackId() => NextTrackId++;
        public int GenerateCoverId() => NextCoverId++;
    }
}
