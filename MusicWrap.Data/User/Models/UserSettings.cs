using MessagePack;
using MusicWrap.Data.Library.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public sealed class UserSettings : INotifyPropertyChanged
    {
        private bool _keepAppInTray = false;
        private float _preferredVolume = 1.0f;
        private RepeatMode _repeatMode = RepeatMode.None;
        private bool _isShuffleEnabled = false;
        private ContinueMode _continueMode = ContinueMode.None;
        [Key(0)] public int PreferredDeviceIndex { get; set; } = -1; // default audio output
        [Key(1)] public SampleRatePreference PreferredSampleRate { get; set; } = SampleRatePreference.Auto;
        [Key(2)] public OutputMode PreferredOutputMode { get; set; } = OutputMode.WasapiShared;
        [Key(3)] public float PreferredVolume
        {
            get => _preferredVolume;
            set
            {
                value = Math.Clamp(value, 0.0f, 1.0f);
                if (_preferredVolume != value)
                {
                    _preferredVolume = value;
                    OnPropertyChanged();
                }
            }
        }
        [Key(4)] public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.RestorePosition;
        [Key(5)] public LastWindowMode LastWindowMode { get; set; } = LastWindowMode.MainPlayer;

        [Key(8)] public bool KeepAppInTray
        {
            get => _keepAppInTray;
            set
            {
                if (_keepAppInTray != value)
                {
                    _keepAppInTray = value;
                    OnPropertyChanged();
                }
            }
        }

        [Key(11)] public RepeatMode RepeatMode
        {
            get => _repeatMode;
            set
            {
                if (_repeatMode != value)
                {
                    _repeatMode = value;
                    OnPropertyChanged();
                }
            }
        }
        [Key(12)] public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                if (_isShuffleEnabled != value)
                {
                    _isShuffleEnabled = value;
                    OnPropertyChanged();
                }
            }
        }
        [Key(13)] public ContinueMode ContinueMode
        {
            get => _continueMode;
            set
            {
                if (_continueMode != value)
                {
                    _continueMode = value;
                    OnPropertyChanged();
                }
            }
        }
        [Key(14)] public LibrarySettings LibrarySettings { get; set; } = new LibrarySettings();
        [Key(15)] public bool IsSidebarOpen { get; set; } = true;
        [Key(16)] public int MainWindowTab{ get; set; } = 0;

        // FFMpeg settings
        [Key(9)] public FFMpegSettings FFMpegSettings { get; set; } = new FFMpegSettings();
        // YouTube library settings
        [Key(10)] public YoutubeSettings YoutubeSettings { get; set; } = new YoutubeSettings();

        // Misc settings
        [Key(17)] public TrayPopupPosition TrayPopupPosition { get; set; } = TrayPopupPosition.BottomRight;

        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    [MessagePackObject]
    public sealed class FFMpegSettings
    {
        [Key(1)] public bool UseCustomFfmpegPath { get; set; } = true;
        [Key(2)] public string CustomFfmpegPath { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public sealed class YoutubeSettings
    {
        [Key(1)] public bool EnableYoutubeLibraryFolders { get; set; } = false;
        [Key(2)] public string YoutubeLibraryRootPath { get; set; } = string.Empty;
        [Key(3)] public string YoutubePathTemplate { get; set; } = "{artist}/{album}/{trackNumber} - {title}";
        [Key(4)] public SuportedFFMpegAudioFormat PreferredAudioFormatForYoutube { get; set; } = SuportedFFMpegAudioFormat.mp3;
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
        WasapiShared = 0,
        WasapiExclusive = 1,
    }
    public enum StartupBehavior
    {
        StartClean = 0,
        RestoreQueueOnly = 1,
        RestoreQueueAndIndexOnly = 2,
        RestorePosition = 3,
        RestorePlayback = 4
    }
    public enum SuportedFFMpegAudioFormat
    {
        webm,
        mp3,
        aac,
        flac,
        wav,
        opus,
        vorbis,
        alac,
        ac3,
        eac3
    }
    public enum LibraryEntryType
    {
        Album,
        TrackArtist,
        AlbumArtist,
        Genre,
        Decade
    }
    public enum TrayPopupPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
