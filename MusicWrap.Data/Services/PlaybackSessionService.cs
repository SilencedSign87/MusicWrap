using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Services
{
    public interface IPlaybackSessionService
    {
        void Save(PlaybackSessionSnapshot snapshot);
        PlaybackSessionSnapshot? Load();
        void Clear();
    }
    public class PlaybackSessionService : IPlaybackSessionService
    {
        private const string PlaybackSessionKey = "Playback_session";
        private readonly IKeyValueStore _keyValueStore;
        public PlaybackSessionService(IKeyValueStore store)
        {
            _keyValueStore = store;
        }
        public void Save(PlaybackSessionSnapshot snapshot)
        {
            if (snapshot == null) return;

            snapshot.QueueTrackIds ??= Array.Empty<int>();
            snapshot.SavedAtUtc = DateTime.UtcNow;

            _keyValueStore.SetValue(PlaybackSessionKey, snapshot);
            _keyValueStore.SaveToDisk();
        }
        public PlaybackSessionSnapshot? Load()
        {
            try
            {
                var snapshot = _keyValueStore.GetValue<PlaybackSessionSnapshot>(PlaybackSessionKey);
                if (snapshot == null) return null;

                snapshot.QueueTrackIds ??= Array.Empty<int>();
                return snapshot;
            }
            catch
            {
                return null;
            }
        }
        public void Clear()
        {
            _keyValueStore.Remove(PlaybackSessionKey);
            _keyValueStore.SaveToDisk();
        }


    }

    [MessagePackObject]
    public sealed class PlaybackSessionSnapshot
    {
        [Key(0)] public int[] QueueTrackIds { get; set; } = Array.Empty<int>();
        [Key(1)] public int CurrentIndex { get; set; } = -1;
        [Key(2)] public double PositionInSeconds { get; set; } = 0;
        [Key(3)] public float Volume { get; set; } = 1.0f;

        [Key(4)] public int RepeatMode { get; set; } = 0;
        [Key(5)] public int ContinueMode { get; set; } = 0;
        [Key(6)] public int PlaybackState { get; set; } = 0; // Stopped/Playing/Paused
        [Key(7)] public int PlayerMode { get; set; } = 0; // 0:Normal/1:Compact

        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    }
}
