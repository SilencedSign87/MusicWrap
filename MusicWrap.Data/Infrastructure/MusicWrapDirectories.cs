using System.IO;

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
        public static readonly string BlurImageDirectory = Path.Combine(CoverDirectory, "Blur"); // 800px
        public static readonly string SmallImageDirectory = Path.Combine(CoverDirectory, "Small"); // 50px
        public static readonly string MediumImageDirectory = Path.Combine(CoverDirectory, "Medium"); // 150px
        public static readonly string LargeImageDirectory = Path.Combine(CoverDirectory, "Large"); // 300px

        public static readonly string SettingsDirectory = Path.Combine(ApplicationDirectory, "Settings");

        public static void EnsureCreated()
        {
            Directory.CreateDirectory(ApplicationDirectory);
            Directory.CreateDirectory(LibraryDirectory);
            Directory.CreateDirectory(TemporaryDirectory);
            Directory.CreateDirectory(CacheDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(CoverDirectory);
            Directory.CreateDirectory(BlurImageDirectory);
            Directory.CreateDirectory(SmallImageDirectory);
            Directory.CreateDirectory(MediumImageDirectory);
            Directory.CreateDirectory(LargeImageDirectory);
            Directory.CreateDirectory(SettingsDirectory);
        }
    }
}
