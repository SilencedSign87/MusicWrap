using MessagePack;

namespace MusicWrap.Data.Playlist.Models
{
    [MessagePackObject]
    public sealed class PlaylistData
    {
        [Key(0)] public int Version { get; set; } = 1;
        [Key(1)] public List<Playlist> Playlists { get; set; } = [];

        [Key(1000)] public int NextPlaylistId { get; set; } = 1;

        public int GenerateNextPlaylistId() => NextPlaylistId++;


    }
}
