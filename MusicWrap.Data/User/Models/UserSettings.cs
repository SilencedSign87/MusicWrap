using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public sealed class UserSettings
    {
        [Key(0)] public int PreferredDeviceIndex { get; set; } = -1; // default audio output
        [Key(1)] public SampleRatePreference PreferredSampleRate { get; set; } = SampleRatePreference.Auto;
        [Key(2)] public OutputMode PreferredOutputMode { get; set; } = OutputMode.BassDefault;
        [Key(3)] public float PreferredVolume { get; set; } = 1.0f;
        [Key(4)] public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.RestoreQueueOnly;
        [Key(5)] public LastWindowMode LastWindowMode { get; set; } = LastWindowMode.MainPlayer;
        [Key(6)] public string LibraryListBy { get; set; } = "Artist";
        [Key(7)] public bool LibraryAscending { get; set; } = false;
        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

    }
    public enum LastWindowMode
    {
        MainPlayer = 0,
        CompactPlayer = 1,
    }

    public enum SampleRatePreference
    {
        Auto = -1,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000
    }

    public enum OutputMode
    {
        BassDefault = 0,
        WasapiShared = 1,
        WasapiExclusive = 2,
    }
    public enum StartupBehavior
    {
        StartClean = 0,
        RestoreQueueOnly = 1,
        RestoreQueueAndIndexOnly = 2,
        ResumePlayback = 3
    }
}
