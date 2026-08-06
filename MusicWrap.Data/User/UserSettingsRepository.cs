using MessagePack;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User.Models;
using System;
using System.IO;

namespace MusicWrap.Data.User
{
    public interface IUserSettingsRepository
    {
        MusicWrapSettings Load();
        void Save(MusicWrapSettings settings);
        void Clear();
        void Backup();
    }

    public class UserSettingsRepository : IUserSettingsRepository, IRepository<MusicWrapSettings>
    {
        private static readonly object _lock = new();

        private static readonly string UserSettingsFilePath = Path.Combine(MusicWrapDirectories.SettingsDirectory, "user.settings.dat");
        private static readonly string UserSettingsBackupFilePath = Path.Combine(MusicWrapDirectories.SettingsDirectory, "user.settings.bak");

        public MusicWrapSettings Load()
        {
            lock (_lock)
            {
                if (!File.Exists(UserSettingsFilePath))
                {
                    return CreateDefault();
                }

                try
                {
                    var data = File.ReadAllBytes(UserSettingsFilePath);
                    return MessagePackSerializer.Deserialize<MusicWrapSettings>(data);
                }
                catch
                {
                    BackupCorrupted();
                    return CreateDefault();
                }
            }
        }

        public void Save(MusicWrapSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            lock (_lock)
            {
                settings.SavedAtUtc = DateTime.UtcNow;

                var data = MessagePackSerializer.Serialize(settings);
                AtomicFileStore.WriteAllBytes(UserSettingsFilePath, data, UserSettingsBackupFilePath);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                if (File.Exists(UserSettingsFilePath))
                {
                    File.Delete(UserSettingsFilePath);
                }
            }
        }

        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(UserSettingsFilePath))
                {
                    File.Copy(UserSettingsFilePath, UserSettingsBackupFilePath, true);
                }
            }
        }

        private static MusicWrapSettings CreateDefault()
        {
            return new MusicWrapSettings
            {
                StartupBehavior = StartupBehavior.RestoreQueueOnly,
                LastWindowMode = LastWindowMode.MainPlayer,
                SavedAtUtc = DateTime.UtcNow
            };
        }

        private static void BackupCorrupted()
        {
            var corrupted = UserSettingsFilePath + ".corrupted";
            File.Move(UserSettingsFilePath, corrupted, true);
        }
    }
}
