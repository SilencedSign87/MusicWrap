using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.Data.Services
{
    public interface ILibraryStore
    {
        MusicLibrary Load();
        void Save(MusicLibrary library);
        void Backup();
    }
    public class LibraryStore : ILibraryStore
    {
        private static readonly object _lock = new();

        public MusicLibrary Load()
        {
            lock (_lock)
            {

                if (!File.Exists(AppPaths.LibraryFile)) return CreateEmpty();

                try
                {
                    var data = File.ReadAllBytes(AppPaths.LibraryFile);
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
                var tmp = AppPaths.LibraryFile + ".tmp";
                File.WriteAllBytes(tmp, data);

                if (File.Exists(AppPaths.LibraryFile))
                {
                    File.Replace(
                        tmp,
                        AppPaths.LibraryFile,
                        AppPaths.BackupFile
                        );
                }
                else
                {
                    File.Move(tmp, AppPaths.LibraryFile);
                }
            }
        }
        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(AppPaths.LibraryFile))
                {
                    File.Copy(AppPaths.LibraryFile, AppPaths.BackupFile, true);
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
            var corrupted = AppPaths.LibraryFile + ".corrupted";

            File.Move(AppPaths.LibraryFile, corrupted, true);
        }

    }

    public static class AppPaths
    {
        public static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicWrap"
            );
        public static readonly string LibraryFile = Path.Combine(Root, "library.dat");
        public static readonly string BackupFile = Path.Combine(Root, "library.bak");

        public static readonly string SettingsFile = Path.Combine(Root, "settings.dat");
        public static readonly string SettingsBackupFile = Path.Combine(Root, "settings.bak");

        public static readonly string CoversDir = Path.Combine(Root, "covers");
        public static readonly string CacheDir = Path.Combine(Root, "cache");
        public static readonly string LogsDir = Path.Combine(Root, "logs");
        public static readonly string TempDir = Path.Combine(Root, "temp");

        static AppPaths()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(CoversDir);
        }

    }
}
