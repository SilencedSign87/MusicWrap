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

        [ObservableProperty] private string albumPlayGlyph = "\uE768";

        [ObservableProperty] private bool isAlbumPlaying;

        [ObservableProperty] private List<DiskGroup> diskGroups = [];

        [ObservableProperty] private List<int> allTrackIds = [];

        private readonly MusicLibrary _library;
        private readonly string _searchQuery;
        private HashSet<int> _albumTrackIds = [];

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

            // Get ALL track IDs ordered by disk/track number (unfiltered)
            var allTrackIds = _library.Tracks
                .Where(t => t.AlbumId == AlbumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)
                .ToList();

            // Store all track IDs for context menu use
            AllTrackIds = allTrackIds;
            _albumTrackIds = allTrackIds.ToHashSet();

            // Filter by search query (if any)
            var displayTrackIds = allTrackIds;
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                displayTrackIds = [.. allTrackIds
                    .Where(trackId => 
                    {
                        var track = _library.Tracks.FirstOrDefault(t => t.Id == trackId);
                        if (track == null) return false;
                        
                        return track.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                               GetArtistNames(track.ArtistIds).Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                               AlbumTitle.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                               AlbumArtists.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase);
                    })
                    ];
            }

            // Group filtered track IDs by disk
            DiskGroups = [.. displayTrackIds
                .GroupBy(trackId => _library.Tracks.FirstOrDefault(t => t.Id == trackId)?.Disk ?? 0)
                .Select(g => new DiskGroup
                {
                    DiskNumber = g.Key,
                    DiskTitle = g.Key > 0 ? $"Disc {g.Key}" : "Tracks",
                    Tracks = [.. g
                        .Select(trackId => _library.Tracks.FirstOrDefault(t => t.Id == trackId))
                        .Where(track => track != null)
                        .Select(track => new TrackRowItem
                        {
                            Id = track!.Id,
                            Title = track.Title,
                            ArtistNames = GetArtistNames(track.ArtistIds),
                            DurationText = TimeSpan.FromSeconds(track.Duration).ToString(@"m\:ss"),
                            TrackNumber = track.TrackNumber,
                            DiskNumber = track.Disk
                        })]
                })];
        }

        private string GetArtistNames(int[] artistIds)
        {
            var artists = _library.Artists.Where(a => artistIds.Contains(a.Id)).Select(a => a.Name);
            return string.Join(", ", artists);
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

        public class DiskGroup
        {
            public int DiskNumber { get; set; }
            public string DiskTitle { get; set; } = "";
            public List<TrackRowItem> Tracks { get; set; } = [];
        }
    }
}


