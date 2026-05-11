using MessagePack;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.User.Models;
using System.IO;

namespace MusicWrap.Data.User
{
    public interface IUserSettingsRepository
    {
        UserSettings Load();
        void Save(UserSettings settings);
        void Clear();
        void Backup();
    }

    public class UserSettingsRepository : IUserSettingsRepository
    {
        private static readonly object _lock = new();

        private static readonly string UserSettingsFilePath = Path.Combine(MusicWrapDirectories.SettingsDirectory, "user.settings.dat");
        private static readonly string UserSettingsBackupFilePath = Path.Combine(MusicWrapDirectories.SettingsDirectory, "user.settings.bak");

        public UserSettings Load()
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
                    return MessagePackSerializer.Deserialize<UserSettings>(data);
                }
                catch
                {
                    BackupCorrupted();
                    return CreateDefault();
                }
            }
        }

        public void Save(UserSettings settings)
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

        private static UserSettings CreateDefault()
        {
            return new UserSettings
            {
                PreferredDeviceIndex = -1,
                PreferredSampleRate = SampleRatePreference.Auto,
                PreferredOutputMode = OutputMode.WasapiShared,
                PreferredVolume = 1.0f,
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
