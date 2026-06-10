using MessagePack;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.Data.Library
{
    public interface ILibraryRepository
    {
        MusicLibrary Load();
        void Save(MusicLibrary library);
        void Clear();
        void Backup();
    }
    public class LibraryRepository : ILibraryRepository, IRepository<MusicLibrary>
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
                    return MessagePackSerializer.Deserialize<MusicLibrary>(data);
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
        public void Clear() {
            lock (_lock) {
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
                Albums = [],
                Artists = [],
                CoverAssets = [],
                Directories = [],
                Genres = [],
                Version = 1
            };
        }
        private static void BackupCorrupted()
        {
            var corrupted = LibraryFile + ".corrupted";

            File.Move(LibraryFile, corrupted, true);
        }
    }
}
