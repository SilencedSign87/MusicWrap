using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.Data.Infrastructure
{
    public static class MusicWrapDirectories
    {
        public static readonly string ApplicationDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MusicWrap");
        public static readonly string LibraryDirectory = Path.Combine(ApplicationDirectory, "Library");
        public static readonly string TemporaryDirectory = Path.Combine(ApplicationDirectory, "Temp");
        public static readonly string CacheDirectory = Path.Combine(ApplicationDirectory, "Cache");
        public static readonly string LogsDirectory = Path.Combine(ApplicationDirectory, "Logs");
        public static readonly string CoverDirectory = Path.Combine(ApplicationDirectory, "Covers");
        public static readonly string SettingsDirectory = Path.Combine(ApplicationDirectory, "Settings");

        public static void EnsureCreated()
        {
            Directory.CreateDirectory(ApplicationDirectory);
            Directory.CreateDirectory(LibraryDirectory);
            Directory.CreateDirectory(TemporaryDirectory);
            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(CoverDirectory);
            Directory.CreateDirectory(SettingsDirectory);
        }
    }
}
