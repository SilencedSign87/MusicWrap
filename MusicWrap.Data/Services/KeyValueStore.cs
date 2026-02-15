using MessagePack;
using MusicWrap.Data.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.Data.Services
{
    public interface IKeyValueStore
    {
        void SetValue<T>(string key, T value);
        T? GetValue<T>(string key);
        bool ContainsKey(string key);
        void Backup();
        void Remove(string key);
        void SaveToDisk();
    }
    public class KeyValueStore : IKeyValueStore
    {
        private static readonly object _lock = new();
        private static KeyValueObj _data = null!;

        public KeyValueStore()
        {
            _data = Load();
        }

        public bool ContainsKey(string key)
        {
            lock (_lock)
            {
                return _data.KeyValues.Exists(kv => kv.Key == key);
            }
        }

        public T? GetValue<T>(string key)
        {
            lock (_lock)
            {
                var kv = _data.KeyValues.Find(kv => kv.Key == key);
                if (kv != null)
                {
                    return MessagePackSerializer.Deserialize<T>(kv.Value);
                }
                else
                {
                    return default;
                }
            }
        }

        public void Remove(string key)
        {
            lock (_lock)
            {
                var kv = _data.KeyValues.Find(kv => kv.Key == key);
                if (kv != null)
                {
                    _data.KeyValues.Remove(kv);
                }
            }
        }

        public void Backup()
        {
            lock (_lock)
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    File.Copy(AppPaths.SettingsFile, AppPaths.SettingsBackupFile, true);
                }
            }
        }

        public void SetValue<T>(string key, T value)
        {
            lock (_lock)
            {
                var kv = _data.KeyValues.Find(kv => kv.Key == key);
                var binValue = MessagePackSerializer.Serialize(value);
                if (kv != null)
                {
                    kv.Value = binValue;
                }
                else
                {
                    _data.KeyValues.Add(new KeyValue { Key = key, Value = binValue });
                }
            }
        }
        public void SaveToDisk()
        {
            lock (_lock)
            {
                var data = MessagePackSerializer.Serialize(_data);
                var tmp = AppPaths.SettingsFile + ".tmp";
                File.WriteAllBytes(tmp, data);

                if (File.Exists(AppPaths.SettingsFile))
                {
                    File.Replace(
                        tmp,
                        AppPaths.SettingsFile,
                        AppPaths.SettingsBackupFile
                        );
                }
                else
                {
                    File.Move(tmp, AppPaths.SettingsFile);
                }
            }
        }

        private KeyValueObj Load()
        {
            if (!File.Exists(AppPaths.SettingsFile)) return CreateEmpty();

            try
            {
                var data = File.ReadAllBytes(AppPaths.SettingsFile);
                return MessagePackSerializer.Deserialize<KeyValueObj>(data);
            }
            catch
            {
                BackupCorrupted();
                return CreateEmpty();
            }
        }

        private static KeyValueObj CreateEmpty()
        {
            return new KeyValueObj
            {
                KeyValues = []
            };
        }

        private static void BackupCorrupted()
        {
            var corrupted = AppPaths.SettingsFile + ".corrupted";

            File.Move(AppPaths.SettingsFile, corrupted, true);
        }
    }

    [MessagePackObject]
    public sealed class KeyValueObj
    {
        [Key(0)] public List<KeyValue> KeyValues;
    }
}
