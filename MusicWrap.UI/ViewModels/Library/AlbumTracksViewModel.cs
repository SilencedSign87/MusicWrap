using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Controls.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicWrap.UI.ViewModels.Library
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

        [ObservableProperty] private List<DiskGroup> diskGroups = [];

        [ObservableProperty] private List<int> selectedTrackIds = [];

        private readonly MusicLibrary _library;
        private readonly string _searchQuery;

        public AlbumTracksViewModel(
            MusicLibrary library,
            int albumId,
            string dominantColor = "#1a1a1a",
            string foregroundColor = "#ffffff",
            string? searchQuery = null)
        {
            _library = library;
            _searchQuery = searchQuery?.Trim() ?? string.Empty;
            this.albumId = albumId;
            this.dominantColor = dominantColor;
            this.foregroundColor = foregroundColor;
            LoadAlbumAndTracks();
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
                .Select(t => new TrackRowItem
                {
                    Id = t.Id,
                    Title = t.Title,
                    DurationText = TimeSpan.FromSeconds(t.Duration).ToString(@"mm\:ss"),
                    TrackNumber = t.TrackNumber,
                    DiskNumber = t.Disk,
                    ArtistNames = GetArtistNames(t.ArtistIds),
                    CoverAssetPath = null
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                tracks = [.. tracks
                    .Where(t =>
                        t.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        t.ArtistNames.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        AlbumTitle.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                        AlbumArtists.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                    ];
            }

            // Group by disk
            DiskGroups = [.. tracks
                .GroupBy(t => t.DiskNumber)
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
            public List<TrackRowItem> Tracks { get; set; } = [];
        }
    }
}


