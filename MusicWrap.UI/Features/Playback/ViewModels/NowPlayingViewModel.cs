using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace MusicWrap.UI.Features.Playback.ViewModels
{
    public partial class NowPlayingViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        public partial string TrackTitle { get; set; } = "No track playing";
        [ObservableProperty]
        public partial string TrackAlbum { get; set; } = "";
        [ObservableProperty]
        public partial string? DominantColorHex { get; set; } = "#808080";

        [ObservableProperty]
        public partial string? ForegroundColorHex { get; set; } = "#FFFFFF";

        [ObservableProperty]
        public partial string? HighlightColorHex { get; set; } = "#808080";

        [ObservableProperty]
        public partial string? HighlightForegroundHex { get; set; } = "#FFFFFF";

        [ObservableProperty]
        public partial string? TrackImagePath { get; set; }

        [ObservableProperty]
        public partial bool ShowLyrics { get; set; }
        [ObservableProperty]
        public partial bool BlurEffect { get; set; } = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsVisualizerVisible))]
        public partial PreferredVisualizer PreferredVisualizer { get; set; } = PreferredVisualizer.LineSpectrum;

        public bool IsVisualizerVisible => PreferredVisualizer != PreferredVisualizer.None;

        private int[] _currentArtistIds = [];

        private bool _disposed;

        public ObservableCollection<LinkItem> TrackArtists { get; } = [];
        public ObservableCollection<LinkItem> TrackAlbumArtists { get; } = [];

        private readonly ILibraryService _libraryService;
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly MusicWrapSettings _settings;
        private readonly ISaveCoordinator _saveCoordinator;
        public NowPlayingViewModel(ILibraryService libraryService, IMusicPlayerService musicPlayerService, MusicWrapSettings settings, ISaveCoordinator saveCoordinator)
        {
            _libraryService = libraryService;
            _musicPlayerService = musicPlayerService;
            _settings = settings;
            _saveCoordinator = saveCoordinator;

            ShowLyrics = settings.NowPlaying.ShowLyrics;
            BlurEffect = settings.NowPlaying.BlurEffect;
            PreferredVisualizer = settings.NowPlaying.PreferredVisualizer;

            _musicPlayerService.TrackChanged += OnTrackChanged;

            _ = LoadTrackData();
        }

        #region Partial
        partial void OnShowLyricsChanged(bool value) => SyncNowPlayingSettings();
        partial void OnBlurEffectChanged(bool value) => SyncNowPlayingSettings();
        partial void OnPreferredVisualizerChanged(PreferredVisualizer value)
        {
            SyncNowPlayingSettings();
        }
        private void SyncNowPlayingSettings()
        {
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

            TrackTitle = "No track playing";
            TrackAlbum = "";
            DominantColorHex = "#808080";
            ForegroundColorHex = "#FFFFFF";
            TrackImagePath = null;
            TrackArtists.Clear();
            TrackAlbumArtists.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _musicPlayerService.TrackChanged -= OnTrackChanged;
        }
    }
}
