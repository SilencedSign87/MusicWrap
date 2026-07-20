using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.Features.Playback.ViewModels
{
    public partial class NowPlayingViewModel : ObservableObject
    {
        [ObservableProperty]
        private string trackTitle = "No track playing";
        [ObservableProperty]
        private string trackAlbum = "";
        [ObservableProperty]
        private string? dominantColorHex = "#808080";
        [ObservableProperty]
        private string? foregroundColorHex = "#FFFFFF";
        [ObservableProperty]
        private string? trackImagePath;

        private int _currentTrackId;
        private int _currentAlbumId;
        private int[] _currentArtistIds = [];

        public ObservableCollection<LinkItem> TrackArtists { get; } = [];
        public ObservableCollection<LinkItem> TrackAlbumArtists { get; } = [];

        private readonly ILibraryService _libraryService;
        private readonly IMusicPlayerService _musicPlayerService;

        public NowPlayingViewModel(ILibraryService libraryService, IMusicPlayerService musicPlayerService)
        {
            _libraryService = libraryService;
            _musicPlayerService = musicPlayerService;

            _musicPlayerService.TrackChanged += OnTrackChanged;

            _ = LoadTrackData();
        }

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

            _currentTrackId = track.Id;
            TrackTitle = track.Title;

            Album? album = null;

            if (track.AlbumId != 0)
            {
                _currentAlbumId = track.AlbumId;
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
                    ForegroundColorHex = coverAsset.ForegroundColorHex;
                }
            }

        }

        private void SetEmptyState()
        {
            _currentAlbumId = 0;
            _currentArtistIds = [];
            _currentTrackId = 0;

            TrackTitle = "No track playing";
            TrackAlbum = "";
            DominantColorHex = "#808080";
            ForegroundColorHex = "#FFFFFF";
            TrackImagePath = null;
            TrackArtists.Clear();
            TrackAlbumArtists.Clear();
        }
    }
}
