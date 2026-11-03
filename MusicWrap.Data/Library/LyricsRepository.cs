using MessagePack;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library
{
    [MessagePackObject]
    public class LyricsData
    {
        [Key(0)] public List<Lyrics> Lyrics = [];

        [Key(999)] public int Version;

        [Key(1000)] public int NextLyricsId { get; set; } = 1;

        public int GenerateLyricsId() => NextLyricsId++;
    }
    public class LyricsRepository : IRepository<LyricsData>
    {
        public static readonly string LyricsLibraryFile = Path.Combine(MusicWrapDirectories.LibraryDirectory, "lyricslibrary.dat");
        public static readonly string LyricsBackupFile = Path.Combine(MusicWrapDirectories.LibraryDirectory, "lyricslibrary.bak");

        private static readonly object _lock = new();

        public LyricsData Load()
        {
            lock (_lock)
            {
                if (!File.Exists(LyricsLibraryFile)) return CreateEmpty();
                try
                {
                    var data = File.ReadAllBytes(LyricsLibraryFile);
                    return MessagePackSerializer.Deserialize<LyricsData>(data);
                }
                catch
                {
                    BackupCorrupted();
                    return CreateEmpty();
                }
            }
        }
        public void Save(LyricsData entity)
        {
            lock (_lock)
            {
                var data = MessagePackSerializer.Serialize(entity);
                AtomicFileStore.WriteAllBytes(LyricsLibraryFile, data, LyricsBackupFile);
            }
        }
        public void Clear()
        {
            lock (_lock)
            {
                if (!File.Exists(LyricsLibraryFile)) return;
                File.Delete(LyricsLibraryFile);
            }
        }
        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(LyricsLibraryFile))
                {
                    File.Copy(LyricsLibraryFile, LyricsBackupFile, true);
                }
            }
        }

        private static LyricsData CreateEmpty()
        {
            return new LyricsData
            {
                Lyrics = [],
                Version = 1,
            };
        }

        private static void BackupCorrupted()
        {
            var corrupted = LyricsLibraryFile + ".corrupted";
            File.Move(LyricsLibraryFile, corrupted, true);
        }


    }

}
