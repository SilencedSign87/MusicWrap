using MessagePack;
using MusicWrap.Data.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Playlist
{
    public interface IPlaylistRepository
    {
        PlaylistData Load();
        void Save(PlaylistData playlist);
        void Clear();
        void Backup();
    }
    public class PlaylistRepository : IPlaylistRepository
    {
        public static readonly string PlaylistFile = System.IO.Path.Combine(MusicWrapDirectories.LibraryDirectory, "playlist.dat");
        public static readonly string BackupDirectory = System.IO.Path.Combine(MusicWrapDirectories.LibraryDirectory, "playlist.dak");
        public static readonly object _lock = new();

        public PlaylistData Load()
        {
            throw new NotImplementedException();
        }

        public void Save(PlaylistData playlist)
        {
            throw new NotImplementedException();
        }
        public void Backup()
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }
    }

    [MessagePackObject]
    public class PlaylistData
    {
        [Key(0)] public Models.Playlist[] Playlists { get; set; } = [];
    }
}
