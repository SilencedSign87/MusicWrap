using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class AlbumTracksViewModel : ObservableObject
    {
        [ObservableProperty]
        private int albumId;

        [ObservableProperty]
        private string albumTitle = "";

        [ObservableProperty]
        private string albumArtists = "";

        [ObservableProperty]
        private int albumYear;

        [ObservableProperty]
        private string dominantColor = "#1a1a1a";

        [ObservableProperty]
        private string foregroundColor = "#ffffff";

        [ObservableProperty]
        private string albumPlayTooltip = "Play Album";

        [ObservableProperty]
        private List<DiskGroup> diskGroups = [];

        private readonly MusicLibrary _library;
        private readonly IMusicPlayerService _playerService;
        private readonly IEditMetadataService _editMetadataService;
        private readonly IPlaylistManagerCoordinator _playlistManagerCoordinator;
        private readonly string _searchQuery;

        public AlbumTracksViewModel(
            MusicLibrary library,
            IMusicPlayerService playerService,
            IEditMetadataService editMetadataService,
            IPlaylistManagerCoordinator playlistManagerCoordinator,
            int albumId,
            string dominantColor = "#1a1a1a",
            string foregroundColor = "#ffffff",
            string? searchQuery = null)
        {
            _editMetadataService = editMetadataService;
            _library = library;
            _playerService = playerService;
            _searchQuery = searchQuery?.Trim() ?? string.Empty;
            _playlistManagerCoordinator = playlistManagerCoordinator;
            this.albumId = albumId;
            this.dominantColor = dominantColor;
            this.foregroundColor = foregroundColor;
            LoadAlbumAndTracks();
        }
        [RelayCommand]
        private void PlayPauseAlbum()
        {
            if (_playerService == null) return;
            var firstTrack = _library.Tracks
                .Where(t => t.AlbumId == AlbumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .FirstOrDefault();
            if (firstTrack != null)
            {
                PlayTrackCommand.Execute(firstTrack.Id);
            }
        }

        [RelayCommand]
        private void PlayTrack(int trackId)
        {
            if (_playerService == null) return;

            // Get all tracks from the album in order
            var allTracks = _library.Tracks
                .Where(t => t.AlbumId == AlbumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)
                .ToList();

            // Set the queue with all album tracks
            _playerService.SetQueue(allTracks);

            // Play the specific track
            _playerService.PlayTrack(trackId);
        }


        [RelayCommand]
        private void PlayNext(int track)
        {
            var currentQueue = _playerService.GetQueue() ?? [];
            // insert track after the currently playing track
            var newQueue = new List<int>();
            foreach (var trackItem in currentQueue)
            {
                newQueue.Add(trackItem);
                if (trackItem == _playerService.CurrentTrackId)
                {
                    newQueue.Add(track);
                }
            }
            _playerService?.SetQueue(newQueue, true);
        }

        [RelayCommand]
        private void AddToQueue(int track)
        {
            _playerService.AddToQueue(track);
        }

        [RelayCommand]
        private void EditTrack(int track)
        {
            _editMetadataService.OpenMetadataWindow(track, MetadataEntityType.Track);
        }

        [RelayCommand]
        private void ShowInExplorer(int track)
        {
            var path = _library.Tracks.FirstOrDefault(t => t.Id == track)?.Path;
            if (path != null)
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    }
                );
            }
        }
        [RelayCommand]
        private void AddToPlaylist(int track)
        {
            _playlistManagerCoordinator.AddToManager(new[] { track });
        }

        [RelayCommand]
        private void ShowProperties(int track)
        {

        }
        private void LoadAlbumAndTracks()
        {
            // Get album info
            var album = _library.Albums.FirstOrDefault(a => a.Id == AlbumId);
            if (album == null) return;

            AlbumTitle = album.Title;
            AlbumYear = album.Year;

            // Get album artists
            var artists = _library.Artists.Where(a => album.ArtistIds.Contains(a.Id)).Select(a => a.Name);
            AlbumArtists = string.Join(", ", artists);

            // Get and group tracks by disk
            var tracks = _library.Tracks
                .Where(t => t.AlbumId == AlbumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => new TrackItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    Duration = t.Duration,
                    Disk = t.Disk,
                    TrackNumber = t.TrackNumber,
                    ArtistNames = GetArtistNames(t.ArtistIds)
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                tracks = tracks
                    .Where(t =>
                        t.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        t.ArtistNames.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        AlbumTitle.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        AlbumArtists.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Group by disk
            DiskGroups = [.. tracks
                .GroupBy(t => t.Disk)
                .Select(g => new DiskGroup
                {
                    DiskNumber = g.Key,
                    DiskTitle = g.Key > 0 ? $"Disc {g.Key}" : "Tracks",
                    Tracks = [.. g]
                })];
        }

        private string GetArtistNames(int[] artistIds)
        {
            var artists = _library.Artists.Where(a => artistIds.Contains(a.Id)).Select(a => a.Name);
            return string.Join(", ", artists);
        }

        public class DiskGroup
        {
            public int DiskNumber { get; set; }
            public string DiskTitle { get; set; } = "";
            public List<TrackItem> Tracks { get; set; } = [];
        }

        public class TrackItem
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public int Duration { get; set; }
            public int Disk { get; set; }
            public int TrackNumber { get; set; }
            public string ArtistNames { get; set; } = "";
            public string FormattedDuration => TimeSpan.FromSeconds(Duration).ToString(@"mm\:ss");
            public string TrackNumberDisplay => TrackNumber > 0 ? TrackNumber.ToString() : "";
        }
    }
}


