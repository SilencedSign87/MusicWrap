using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Collections.ObjectModel;

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

        private readonly ILibraryService _libraryService;
        private readonly ITrackRowItemFactory _trackRowItemFactory;
        private readonly TracksContextMenuService _tracksContextMenuService;
        private readonly string _searchQuery;
        private readonly TrackSortMode _sortMode;
        private HashSet<int> _albumTrackIds = [];
        private readonly LibraryEntry? _entryContext;

        public AlbumTracksViewModel(
            ILibraryService libraryService,
            ITrackRowItemFactory trackRowItemFactory,
            TracksContextMenuService tracksContextMenuService,
            int albumId,
            string dominantColor = "#1a1a1a",
            string foregroundColor = "#ffffff",
            string? searchQuery = null,
            TrackSortMode sortMode = TrackSortMode.Year,
            LibraryEntry? entryContext = null)
        {
            _libraryService = libraryService;
            _trackRowItemFactory = trackRowItemFactory;
            _tracksContextMenuService = tracksContextMenuService;
            _searchQuery = searchQuery?.Trim() ?? string.Empty;
            _entryContext = entryContext;

            _sortMode = sortMode;
            this.albumId = albumId;
            this.dominantColor = dominantColor;
            this.foregroundColor = foregroundColor;
            selectedTrackIds = [];
            LoadAlbumAndTracks();
        }

        private void LoadAlbumAndTracks()
        {
            var allTrackIds = _libraryService.GetTracksForAlbum(AlbumId);
            var firstTrack = allTrackIds.Length > 0 ? _libraryService.GetTrackById(allTrackIds[0]) : null;

            AlbumTitle = firstTrack?.AlbumName ?? "Unknown Album";
            AlbumYear = firstTrack?.ReleaseYear ?? 0;
            AlbumArtists = _libraryService.GetArtistNamesForAlbum(AlbumId);

            IEnumerable<int> filteredTracks = allTrackIds;

            // filter by entry context
            if (_entryContext is not null)
            {
                var entryTracks = _libraryService.GetTrackIdsForEntry(_entryContext);
                filteredTracks = filteredTracks.Where(t => entryTracks.Contains(t));
            }

            var displayTrackIds = filteredTracks.ToArray();
            var trackRows = SortTracks(_trackRowItemFactory.Build(displayTrackIds));

            Tracks = new ObservableCollection<TrackRowItem>(trackRows);
            AllTrackIds = [.. displayTrackIds];
            _albumTrackIds = displayTrackIds.ToHashSet();
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
                    .OrderBy(t => _libraryService.GetTrackById(t.Id)?.DurationSeconds ?? int.MaxValue)
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





