using MessagePack;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Player.Models;
using System;
using System.IO;

namespace MusicWrap.Data.Player
{
    public interface IPlaybackRepository
    {
        PlaybackQueueSnapshot Load();
        void Save(PlaybackQueueSnapshot snapshot);
        void Clear();
        void Backup();
    }

    public class PlaybackRepository : IPlaybackRepository
    {
        private static readonly object _lock = new();

        private static readonly string QueueFilePath = Path.Combine(MusicWrapDirectories.LibraryDirectory, "queue.dat");
        private static readonly string QueueBackupFilePath = Path.Combine(MusicWrapDirectories.LibraryDirectory, "queue.bak");

        public PlaybackQueueSnapshot Load()
        {
            lock (_lock)
            {
                if (!File.Exists(QueueFilePath))
                {
                    return CreateEmpty();
                }

                try
                {
                    var data = File.ReadAllBytes(QueueFilePath);
                    return MessagePackSerializer.Deserialize<PlaybackQueueSnapshot>(data);
                }
                catch
                {
                    BackupCorrupted();
                    return CreateEmpty();
                }
            }
        }

        public void Save(PlaybackQueueSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (_lock)
            {
                snapshot.TrackIds ??= Array.Empty<int>();
                snapshot.SavedAtUtc = DateTime.UtcNow;

                var data = MessagePackSerializer.Serialize(snapshot);
                AtomicFileStore.WriteAllBytes(QueueFilePath, data, QueueBackupFilePath);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (File.Exists(QueueFilePath))
                {
                    File.Delete(QueueFilePath);
                }
            }
        }

        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(QueueFilePath))
                {
                    File.Copy(QueueFilePath, QueueBackupFilePath, true);
                }
            }
        }

        private static PlaybackQueueSnapshot CreateEmpty()
        {
            return new PlaybackQueueSnapshot
            {
                TrackIds = Array.Empty<int>(),
                CurrentIndex = -1,
                RepeatMode = 0,
                ContinueMode = 0,
                SavedAtUtc = DateTime.UtcNow
            };
        }

        private static void BackupCorrupted()
        {
            var corrupted = QueueFilePath + ".corrupted";
            File.Move(QueueFilePath, corrupted, true);
        }
    }
}
