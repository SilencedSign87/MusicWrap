using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Helpers;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Lyrics.Viewmodel;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using System.Collections.ObjectModel;

namespace MusicWrap.UI.Features.Playback.ViewModels
{
    public partial class NowPlayingViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        public partial string TrackTitle { get; set; } = CommonStrings.NoTrackPlaying;
        [ObservableProperty]
        public partial string TrackAlbum { get; set; } = "";
        [ObservableProperty]
        public partial string? DominantColorHex { get; set; } = CommonColors.DominantColorFallback;

        [ObservableProperty]
        public partial string? ForegroundColorHex { get; set; } = CommonColors.ForegroundOnFallback;

        [ObservableProperty]
        public partial string? HighlightColorHex { get; set; } = CommonColors.HighlightColorFallback;

        [ObservableProperty]
        public partial string? HighlightForegroundHex { get; set; } = CommonColors.ForegroundOnFallback;

        [ObservableProperty]
        public partial string? TrackImagePath { get; set; }

        [ObservableProperty]
        public partial bool ShowLyrics { get; set; }
        [ObservableProperty]
        public partial bool BlurEffect { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsVisualizerVisible))]
        [NotifyCanExecuteChangedFor(nameof(SetVisualizerCommand))]
        public partial PreferredVisualizer PreferredVisualizer { get; set; } = PreferredVisualizer.LineSpectrum;

        public bool IsVisualizerVisible => PreferredVisualizer != PreferredVisualizer.None;

        private int[] _currentArtistIds = [];

        private bool _disposed;
        private bool _isInitializing = true;


        public ObservableCollection<LinkItem> TrackArtists { get; } = [];
        public ObservableCollection<LinkItem> TrackAlbumArtists { get; } = [];

        private readonly ILibraryService _libraryService;
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly MusicWrapSettings _settings;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly WindowManagerService _windowManagerService;
        public LyricsViewModel LyricsViewModel { get; }
        public NowPlayingViewModel(ILibraryService libraryService, IMusicPlayerService musicPlayerService, MusicWrapSettings settings, ISaveCoordinator saveCoordinator, WindowManagerService windowManagerService, LyricsViewModel lyricsViewModel)
        {
            _libraryService = libraryService;
            _musicPlayerService = musicPlayerService;
            _settings = settings;
            _saveCoordinator = saveCoordinator;
            _windowManagerService = windowManagerService;
            LyricsViewModel = lyricsViewModel;
            _isInitializing = true;

            ShowLyrics = settings.NowPlaying.ShowLyrics;
            BlurEffect = settings.NowPlaying.BlurEffect;
            PreferredVisualizer = settings.NowPlaying.PreferredVisualizer;
            _isInitializing = false;

            _musicPlayerService.TrackChanged += OnTrackChanged;

            _ = LoadTrackData();
        }
        #region Relay Commands
        [RelayCommand(CanExecute = nameof(CanSetVisualizer))]
        private void SetVisualizer(string visualizer)
        {
            if (Enum.TryParse<PreferredVisualizer>(visualizer, out var parsed))
            {
                PreferredVisualizer = parsed;
            }
        }
        private bool CanSetVisualizer(string visualizer)
        {
            if(!Enum.TryParse<PreferredVisualizer>(visualizer, out var parsed))
            {
                return false;
            }
            return PreferredVisualizer != parsed;
        }
        [RelayCommand]
        private void OpenProperties()
        {
            _windowManagerService.LaunchInformationWindow([_musicPlayerService.CurrentTrackId]);
        }
        #endregion
        #region Partial
        partial void OnShowLyricsChanged(bool value) => SyncNowPlayingSettings();
        partial void OnBlurEffectChanged(bool value) => SyncNowPlayingSettings();
        partial void OnPreferredVisualizerChanged(PreferredVisualizer value) => SyncNowPlayingSettings();
        private void SyncNowPlayingSettings()
        {
            if (_isInitializing) return;
            _settings.NowPlaying.ShowLyrics = ShowLyrics;
            _settings.NowPlaying.BlurEffect = BlurEffect;
            _settings.NowPlaying.PreferredVisualizer = PreferredVisualizer;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        #endregion

        private void OnTrackChanged(object? sender, string e)
        {
            _ = LoadTrackData();
        }

        private async Task LoadTrackData()
        {
            var trackid = _musicPlayerService.CurrentTrackId;

            var track = _libraryService.GetTrackById(trackid);
            if (track == null)
            {
                SetEmptyState();
                return;
            }

            TrackTitle = track.Title;

            Album? album = null;

            if (track.AlbumId != 0)
            {
                album = _libraryService.GetAlbumById(track.AlbumId);
                TrackAlbum = album?.Title ?? "";
            }
            else
            {
                TrackAlbum = "";
            }

            // track artists
            _currentArtistIds = track.ArtistIds ?? [];
            TrackArtists.Clear();
            var artistsNames = _libraryService.GetArtistNamesByIds(_currentArtistIds);
            for (int i = 0; i < artistsNames.Length; i++)
            {
                TrackArtists.Add(new LinkItem(
                    artistsNames[i], _currentArtistIds[i], LinkType.Artist
                    ));
            }

            int coverId = track.CoverId;
            if (coverId == 0 && album != null)
                coverId = album.CoverId;
            if (coverId > 0)
            {
                var coverAsset = _libraryService.GetCoverAsset(coverId);
                if (coverAsset != null)
                {
                    TrackImagePath = coverAsset.FileName;
                    DominantColorHex = coverAsset.DominantColorHex;
                    ForegroundColorHex = coverAsset.DominantForegroundHex;
                    HighlightColorHex = coverAsset.HighlightColorHex;
                    HighlightForegroundHex = coverAsset.HighlightForegroundHex;
                }
            }

        }

        private void SetEmptyState()
        {
            _currentArtistIds = [];

            TrackTitle = CommonStrings.NoTrackPlaying;
            TrackAlbum = "";
            DominantColorHex = CommonColors.DominantColorFallback;
            ForegroundColorHex =CommonColors.ForegroundOnFallback;
            HighlightColorHex = CommonColors.HighlightColorFallback;
            HighlightForegroundHex = CommonColors.ForegroundOnFallback;
            TrackImagePath = null;
            TrackArtists.Clear();
            TrackAlbumArtists.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            (LyricsViewModel as IDisposable)?.Dispose();
            _musicPlayerService.TrackChanged -= OnTrackChanged;
        }
    }
}
