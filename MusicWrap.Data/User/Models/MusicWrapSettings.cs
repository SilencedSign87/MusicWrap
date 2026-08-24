using MessagePack;
using MusicWrap.Data.Library.Models;
namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public sealed class MusicWrapSettings : ObservableClass
    {
        private bool _keepAppInTray = false;
        [Key(0)] public PlaybackSettings Playback { get; set; } = new PlaybackSettings();
        [Key(1)] public LibrarySettings Library { get; set; } = new LibrarySettings();
        [Key(2)] public FFMpegSettings FFMpeg { get; set; } = new FFMpegSettings();
        [Key(3)] public YoutubeSettings Youtube { get; set; } = new YoutubeSettings();
        [Key(4)] public NowPlayingSettings NowPlaying { get; set; } = new NowPlayingSettings();
        [Key(5)] public StartupBehavior StartupBehavior { get; set; } = StartupBehavior.RestorePosition;
        [Key(6)] public PlayerMode LastWindowMode { get; set; } = PlayerMode.MainPlayer;

        [Key(7)] public bool KeepAppInTray
        {
            get => _keepAppInTray;
            set => SetProperty(ref _keepAppInTray, value);
        }
        [Key(8)] public bool IsSidebarOpen { get; set; } = true;
        [Key(9)] public int MainWindowTab{ get; set; } = 0;
        [Key(10)] public TrayPopupPosition TrayPopupPosition { get; set; } = TrayPopupPosition.BottomRight;
        [Key(11)] public ThemePreference AppThemePreference { get; set; } = ThemePreference.System;
        [Key(12)] public WindowBoundsState MainPlayerBounds { get; set; } = new WindowBoundsState();
        [Key(13)] public WindowBoundsState CompactPlayerBounds { get; set; } = new WindowBoundsState();

        [Key(100)] public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum PlayerMode
    {
        MainPlayer = 0,
        CompactPlayer = 1,
        FullScreenPlayer = 2
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
    public enum ThemePreference
    {
        System = 0,
        Light = 1,
        Dark = 2,
    }

    [MessagePackObject]
    public sealed class WindowBoundsState
    {
        [Key(0)] public double Left { get; set; } = double.NaN;
        [Key(1)] public double Top { get; set; } = double.NaN;
        [Key(2)] public double Width { get; set; } = double.NaN;
        [Key(3)] public double Height { get; set; } = double.NaN;
        [Key(4)] public bool IsMaximized { get; set; } = false;

    }
}
