using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.Services;
using System.Collections.ObjectModel;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Search;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.Data.Helpers;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class AlbumTracksViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] private int albumId;

        [ObservableProperty] private string albumTitle = "";

        [ObservableProperty] private string albumArtists = "";

        [ObservableProperty] private int albumYear;

        [ObservableProperty] private string dominantColor = CommonColors.DominantColorFallback;

        [ObservableProperty] private string foregroundColor = CommonColors.ForegroundOnFallback;

        [ObservableProperty] private string highlightColor = CommonColors.HighlightColorFallback;

        [ObservableProperty] private string highlightForeground = CommonColors.ForegroundOnFallback;

        [ObservableProperty] private string albumPlayTooltip = "Play Album";

        [ObservableProperty] private string albumPlayGlyph = "\uE768";

        [ObservableProperty] private bool isAlbumPlaying;

        [ObservableProperty] private ObservableCollection<TrackRowItem> tracks = [];

        [ObservableProperty] private List<int> allTrackIds = [];

        [ObservableProperty] private List<int> selectedTrackIds = [];

        private readonly ILibraryService _libraryService;
        private readonly TrackActionService _tracksContextMenuService;
        private HashSet<int> _albumTrackIds = [];

        private readonly int[]? _filteredTrackIds;
        private int[] _orderedTrackIds = [];
        private bool _disposed = false;

        public AlbumTracksViewModel(
            ILibraryService libraryCache,
            TrackActionService tracksContextMenuService,
            int albumId,
            int[]? filteredTrackIds = null
            )
        {
            _libraryService = libraryCache;
            _tracksContextMenuService = tracksContextMenuService;
            _filteredTrackIds = filteredTrackIds;
            this.albumId = albumId;
            selectedTrackIds = [];
            LoadAlbumAndTracks();
        }

        private void LoadAlbumAndTracks()
        {
            var album = _libraryService.GetAlbumById(AlbumId);
            AlbumTitle = album?.Title ?? CommonStrings.UnknownAlbum;
            AlbumYear = album?.Year ?? 0;
            AlbumArtists = _libraryService.GetArtistNamesForAlbum(AlbumId);

            if (album?.CoverId > 0)
            {
                var cover = _libraryService.GetCoverAsset(album.CoverId);
                if (cover is not null)
                {
                    DominantColor = cover.DominantColorHex;
                    ForegroundColor = cover.DominantForegroundHex;
                    HighlightColor = cover.HighlightColorHex;
                    HighlightForeground = cover.HighlightForegroundHex;
                }
            }


            var allTrackIds = _filteredTrackIds ?? _libraryService.GetTracksForAlbum(AlbumId, true);
            _orderedTrackIds = allTrackIds;

            var trackRows = SortTracks(_libraryService.TrackIdsToTrackRowItems(allTrackIds)).ToList();

            Tracks = new ObservableCollection<TrackRowItem>(trackRows);
            AllTrackIds = trackRows.Select(t => t.Id).ToList();
            _albumTrackIds = allTrackIds.ToHashSet();
        }

        private IEnumerable<TrackRowItem> SortTracks(List<TrackRowItem> rows)
        {
            return rows
                    .OrderBy(t => t.DiskNumber)
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





