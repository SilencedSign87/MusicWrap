using MessagePack;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Playlist.Models;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static readonly string PlaylistBackupFile = System.IO.Path.Combine(MusicWrapDirectories.LibraryDirectory, "playlist.bak");
        public static readonly object _lock = new();

        public PlaylistData Load()
        {
            lock (_lock)
            {
                if (!File.Exists(PlaylistFile)) return CreateEmpty();

                try
                {
                    var data = File.ReadAllBytes(PlaylistFile);
                    return MessagePackSerializer.Deserialize<PlaylistData>(data);
                }
                catch
                {
                    BackupCorrupted();
                    return CreateEmpty();
                }
            }
        }

        public void Save(PlaylistData playlist)
        {
            lock (_lock)
            {
                var data = MessagePackSerializer.Serialize(playlist);
                AtomicFileStore.WriteAllBytes(PlaylistFile, data, PlaylistBackupFile);
            }
        }
        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(PlaylistFile))
                {
                    File.Copy(PlaylistFile, PlaylistBackupFile, true);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (!File.Exists(PlaylistFile)) return;
                File.Delete(PlaylistFile);
            }
        }
        private PlaylistData CreateEmpty()
        {
            return new PlaylistData
            {
                Version = 1,
                Playlists = []
            };
        }
        private void BackupCorrupted()
        {
            var corrupted = PlaylistFile + ".corrupted";
            File.Move(PlaylistFile, corrupted, true);
        }
    }
}
