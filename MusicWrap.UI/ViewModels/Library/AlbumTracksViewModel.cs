using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using System;
using System.Collections.Generic;
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
        private List<DiskGroup> diskGroups = [];

        private MusicLibrary _library;
        private IMusicPlayerService? _playerService;

        public AlbumTracksViewModel(MusicLibrary library, int albumId, string dominantColor = "#1a1a1a", string foregroundColor = "#ffffff", IMusicPlayerService? playerService = null)
        {
            _library = library;
            _playerService = playerService;
            this.albumId = albumId;
            this.dominantColor = dominantColor;
            this.foregroundColor = foregroundColor;
            LoadAlbumAndTracks();
        }

        [RelayCommand]
        private void PlayTrack(int trackId)
        {
            if (_playerService == null) return;

            // Get all tracks from the album in order
            var allTracks = _library.Tracks
                .Where(t => t.AlbumId == albumId)
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

        private void LoadAlbumAndTracks()
        {
            // Get album info
            var album = _library.Albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null) return;

            albumTitle = album.Title;
            albumYear = album.Year;
            
            // Get album artists
            var artists = _library.Artists.Where(a => album.ArtistIds.Contains(a.Id)).Select(a => a.Name);
            albumArtists = string.Join(", ", artists);

            // Get and group tracks by disk
            var tracks = _library.Tracks
                .Where(t => t.AlbumId == albumId)
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

            // Group by disk
            diskGroups = tracks
                .GroupBy(t => t.Disk)
                .Select(g => new DiskGroup
                {
                    DiskNumber = g.Key,
                    DiskTitle = g.Key > 0 ? $"Disc {g.Key}" : "Tracks",
                    Tracks = g.ToList()
                })
                .ToList();
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


