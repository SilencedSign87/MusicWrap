using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Linq;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class AlbumTracksViewModel : ObservableObject
    {
        [ObservableProperty] private int albumId;

        [ObservableProperty] private string albumTitle = "";

        [ObservableProperty] private string albumArtists = "";

        [ObservableProperty] private int albumYear;

        [ObservableProperty] private string dominantColor = "#1a1a1a";

        [ObservableProperty] private string foregroundColor = "#ffffff";

        [ObservableProperty] private string albumPlayTooltip = "Play Album";

        [ObservableProperty] private string albumPlayGlyph = "\uE768";

        [ObservableProperty] private bool isAlbumPlaying;

        [ObservableProperty] private ObservableCollection<TrackRowItem> tracks = [];

        [ObservableProperty] private List<int> allTrackIds = [];

        [ObservableProperty] private List<int> selectedTrackIds = [];

        private readonly MusicLibrary _library;
        private readonly ILibraryCacheService _libraryCache;
        private readonly TracksContextMenuService _tracksContextMenuService;
        private readonly string _searchQuery;
        private HashSet<int> _albumTrackIds = [];

        public AlbumTracksViewModel(
            MusicLibrary library,
            ILibraryCacheService libraryCache,
            TracksContextMenuService tracksContextMenuService,
            int albumId,
            string dominantColor = "#1a1a1a",
            string foregroundColor = "#ffffff",
            string? searchQuery = null)
        {
            _library = library;
            _libraryCache = libraryCache;
            _tracksContextMenuService = tracksContextMenuService;
            _searchQuery = searchQuery?.Trim() ?? string.Empty;
            this.albumId = albumId;
            this.dominantColor = dominantColor;
            this.foregroundColor = foregroundColor;
            selectedTrackIds = [];
            LoadAlbumAndTracks();
        }

        private void LoadAlbumAndTracks()
        {
            var album = _library.Albums.FirstOrDefault(a => a.Id == AlbumId);
            AlbumTitle = album?.Title ?? "Unknown Album";
            AlbumYear = album?.Year ?? 0;
            AlbumArtists = _libraryCache.GetArtistNamesForAlbum(AlbumId);

            var tracksId = _libraryCache.GetTracksForAlbum(AlbumId, _searchQuery);
            var trackRows = _libraryCache.TrackIdsToTrackRowItems(tracksId)
                .OrderBy(t => t.DiskNumber)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .ToList();

            Tracks = new ObservableCollection<TrackRowItem>(trackRows);
            AllTrackIds = trackRows.Select(t => t.Id).ToList();
            _albumTrackIds = AllTrackIds.ToHashSet();
        }

        public bool ContainsTrack(int trackId)
        {
            return trackId > 0 && _albumTrackIds.Contains(trackId);
        }

        public void UpdatePlaybackState(int currentTrackId, bool isPlaying)
        {
            IsAlbumPlaying = isPlaying && ContainsTrack(currentTrackId);
            AlbumPlayTooltip = IsAlbumPlaying ? "Pause Album" : "Play Album";
            AlbumPlayGlyph = IsAlbumPlaying ? "\uE769" : "\uE768";
        }

        [RelayCommand]
        private void PlayNowSelectedTracks()
        {
            _tracksContextMenuService.PlayNow(SelectedTrackIds, AllTrackIds);
        }

        [RelayCommand]
        private void PlayNextSelectedTracks()
        {
            _tracksContextMenuService.PlayNext(SelectedTrackIds, AllTrackIds);
        }

        [RelayCommand]
        private void AddSelectedTracksToQueue()
        {
            _tracksContextMenuService.AddToQueue(SelectedTrackIds);
        }

    }
}





