using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.Services;
using System.Collections.ObjectModel;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Shared.Services;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class AlbumTracksViewModel : ObservableObject, IDisposable
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

        private readonly ILibraryService _libraryCache;
        private readonly SearchService _searchService;
        private readonly TrackActionService _tracksContextMenuService;
        private readonly string _searchQuery;
        private readonly TrackSortMode _sortMode;
        private HashSet<int> _albumTrackIds = [];

        private readonly int[]? _filteredTrackIds;
        private int[] _orderedTrackIds = [];
        private bool _disposed = false;

        public AlbumTracksViewModel(
            ILibraryService libraryCache,
            SearchService searchService,
            TrackActionService tracksContextMenuService,
            int albumId,
            string dominantColor = "#1a1a1a",
            string foregroundColor = "#ffffff",
            string? searchQuery = null,
            TrackSortMode sortMode = TrackSortMode.Year,
            int[]? filteredTrackIds = null
            )
        {
            _libraryCache = libraryCache;
            _searchService = searchService;
            _tracksContextMenuService = tracksContextMenuService;
            _searchQuery = searchQuery?.Trim() ?? string.Empty;
            _sortMode = sortMode;
            _filteredTrackIds = filteredTrackIds;
            this.albumId = albumId;
            this.dominantColor = dominantColor;
            this.foregroundColor = foregroundColor;
            selectedTrackIds = [];
            LoadAlbumAndTracks();
        }

        private void LoadAlbumAndTracks()
        {
            var album = _libraryCache.GetAlbumById(AlbumId);
            AlbumTitle = album?.Title ?? "Unknown Album";
            AlbumYear = album?.Year ?? 0;
            AlbumArtists = _libraryCache.GetArtistNamesForAlbum(AlbumId);

            var allTrackIds = _filteredTrackIds ?? _libraryCache.GetTracksForAlbum(AlbumId, true);
            _orderedTrackIds = allTrackIds;

            var trackRows = SortTracks(_libraryCache.TrackIdsToTrackRowItems(allTrackIds)).ToList();

            Tracks = new ObservableCollection<TrackRowItem>(trackRows);
            AllTrackIds = trackRows.Select(t => t.Id).ToList();
            _albumTrackIds = allTrackIds.ToHashSet();
        }

        private IEnumerable<TrackRowItem> SortTracks(List<TrackRowItem> rows)
        {
            if (_sortMode == TrackSortMode.Title)
            {
                return rows
                    .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.ArtistNames, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.DiskNumber)
                    .ThenBy(t => t.TrackNumber);
            }

            if (_sortMode == TrackSortMode.ArtistName)
            {
                return rows
                    .OrderBy(t => t.ArtistNames, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.DiskNumber)
                    .ThenBy(t => t.TrackNumber);
            }

            if (_sortMode == TrackSortMode.Duration)
            {
                return rows
                    .OrderBy(t => _libraryCache.GetTrackById(t.Id)?.Duration ?? int.MaxValue)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase);
            }

            return rows
                .OrderBy(t => AlbumYear)
                .ThenBy(t => t.DiskNumber)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase);
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
        public int[] GetPlayableTrackIds() => _orderedTrackIds;

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

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Tracks.Clear();
            AllTrackIds.Clear();
            SelectedTrackIds.Clear();
            _albumTrackIds.Clear();
            _orderedTrackIds = [];
        }
    }
}





