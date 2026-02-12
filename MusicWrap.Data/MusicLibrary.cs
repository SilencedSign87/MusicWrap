using MessagePack;
using MusicWrap.Data.Library;

namespace MusicWrap.Data
{
    [MessagePackObject]
    public sealed class MusicLibrary
    {
        [Key(0)] public List<Track> Tracks;
        [Key(1)] public List<Album> Albums;
        [Key(2)] public List<Artist> Artists;
        [Key(3)] public List<ScanDirectory> Directories;
        [Key(4)] public List<CoverAsset> CoverAssets;
        [Key(5)] public List<Genre> Genres;


        [Key(999)] public int Version;

        [Key(1000)] public int NextArtistId {get; set;} = 1;
        [Key(1001)] public int NextAlbumId {get; set;} = 1;
        [Key(1002)] public int NextTrackId {get; set;} = 1;
        [Key(1003)] public int NextCoverId { get; set; } = 1;
        [Key(1004)] public int NextGenreId { get; set; } = 1;


        public int GenerateArtistId() => NextArtistId++;
        public int GenerateAlbumId() => NextAlbumId++;
        public int GenerateTrackId() => NextTrackId++;
        public int GenerateCoverId() => NextCoverId++;
        public int GenerateGenreId() => NextGenreId++;
    }
}
