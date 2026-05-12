using MessagePack;
using MusicWrap.Data.Helpers;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library.Models;
using System.IO;

namespace MusicWrap.Data.Library
{
    public interface ILibraryRepository
    {
        MusicLibrary Load();
        void Save(MusicLibrary library);
        void Clear();
        void Backup();
    }
    public class LibraryRepository : ILibraryRepository
    {
        public static readonly string LibraryFile = Path.Combine(MusicWrapDirectories.LibraryDirectory, "library.dat");
        public static readonly string BackupFile = Path.Combine(MusicWrapDirectories.LibraryDirectory, "library.bak");

        private static readonly object _lock = new();

        public MusicLibrary Load()
        {
            lock (_lock)
            {

                if (!File.Exists(LibraryFile)) return CreateEmpty();

                try
                {
                    var data = File.ReadAllBytes(LibraryFile);
                    var library = MessagePackSerializer.Deserialize<MusicLibrary>(data);
                    Rewrite(library);
                    return library;
                }
                catch
                {
                    BackupCorrupted();
                    return CreateEmpty();
                }
            }
        }

        public void Save(MusicLibrary library)
        {
            lock (_lock)
            {
                var data = MessagePackSerializer.Serialize(library);
                AtomicFileStore.WriteAllBytes(LibraryFile, data, BackupFile);
            }
        }
        public void Clear()
        {
            lock (_lock)
            {
                if (!File.Exists(LibraryFile)) return;
                File.Delete(LibraryFile);
            }
        }
        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(LibraryFile))
                {
                    File.Copy(LibraryFile, BackupFile, true);
                }
            }
        }

        private static MusicLibrary CreateEmpty()
        {
            return new MusicLibrary
            {
                Tracks = [],
                CoverAssets = [],
                Directories = [],
                Version = 1
            };
        }
        private static void BackupCorrupted()
        {
            var corrupted = LibraryFile + ".corrupted";

            File.Move(LibraryFile, corrupted, true);
        }

        private static void Rewrite(MusicLibrary musicLibrary)
        {
            foreach(var t in musicLibrary.Tracks)
            {
                t.Title = TrackStringPool.Intern(t.Title);
                t.Artists = TrackStringPool.Intern(t.Artists);
                t.AlbumArtists = TrackStringPool.Intern(t.AlbumArtists);
                t.AlbumName = TrackStringPool.Intern(t.AlbumName);
                t.Genres = TrackStringPool.Intern(t.Genres);
            }

        }
    }
}
