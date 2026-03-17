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
        public static readonly string Root = MusicWrapDirectories.ApplicationDirectory;

        public static readonly string LibraryFile = Path.Combine(MusicWrapDirectories.LibraryDirectory, "library.dat");
        public static readonly string BackupFile = Path.Combine(MusicWrapDirectories.LibraryDirectory, "library.bak");

        public static readonly string SettingsFile = Path.Combine(MusicWrapDirectories.SettingsDirectory, "settings.dat");
        public static readonly string SettingsBackupFile = Path.Combine(MusicWrapDirectories.SettingsDirectory, "settings.bak");

        public static readonly string CoversDir = MusicWrapDirectories.CoverDirectory;
        public static readonly string CacheDir = MusicWrapDirectories.CacheDirectory;
        public static readonly string LogsDir = MusicWrapDirectories.LogsDirectory;
        public static readonly string TempDir = MusicWrapDirectories.TemporaryDirectory;

        static AppPaths()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(CoversDir);
            Directory.CreateDirectory(CacheDir);
            Directory.CreateDirectory(LogsDir);
            Directory.CreateDirectory(TempDir);
            Directory.CreateDirectory(MusicWrapDirectories.LibraryDirectory);
            Directory.CreateDirectory(MusicWrapDirectories.SettingsDirectory);
        }
    }
}
