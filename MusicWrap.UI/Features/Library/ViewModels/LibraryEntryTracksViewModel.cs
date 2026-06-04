using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryEntryTracksViewModel : ObservableObject, IDisposable
    {
        private readonly ILibraryService _libraryCache;
        private readonly TracksContextMenuService _tracksContextMenuService;
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly SearchService _searchService;
        private readonly IUIDispatcher _uiDispatcher;

        private int[] _sourceTrackIds = [];
        private int _refreshRequestId;
        private bool _disposed = false;

        [ObservableProperty] private TrackSortMode sortMode;
        [ObservableProperty] private bool sortAscending;

        [ObservableProperty] private ObservableCollection<TrackRowItem> tracks = [];
        [ObservableProperty] private List<int> allTrackIds = [];
        [ObservableProperty] private List<int> selectedTrackIds = [];

        public LibraryEntryTracksViewModel(
            ILibraryService libraryCache,
            TracksContextMenuService tracksContextMenuService,
            IMusicPlayerService musicPlayerService,
            SearchService searchService,
            IUIDispatcher uiDispatcher
           )
        {
            _libraryCache = libraryCache;
            _tracksContextMenuService = tracksContextMenuService;
            _musicPlayerService = musicPlayerService;
            _searchService = searchService;
            _uiDispatcher = uiDispatcher;

            _searchService.QueryChanged += _searchService_QueryChanged;
        }

        #region Relay Commands
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
        #endregion
        #region Public methods

        public void SetSourceTrackIds(int[] trackIds)
        {
            _sourceTrackIds = trackIds;
            _ = RefreshAsync(true);
        }
        #endregion
        #region Partial methods
        partial void OnSortModeChanged(TrackSortMode value)
        {
            _ = RefreshAsync(false);
        }
        partial void OnSortAscendingChanged(bool value)
        {
            _ = RefreshAsync(false);
        }

        #endregion
        private void _searchService_QueryChanged(object? sender, string e)
        {
            _ = RefreshAsync(true);
        }
        private async Task RefreshAsync(bool debounce)
        {
            var requestid = Interlocked.Increment(ref _refreshRequestId);
            if (debounce)
            {
                await Task.Delay(200);
                if (requestid != _refreshRequestId)
                    return;
            }

            var sourceIds = _sourceTrackIds;
            var query = _searchService.ActiveQuery.Trim();
            var sortmode = SortMode;
            var ascending = SortAscending;

            var result = await Task.Run(() => BuildTracksResult(sourceIds, query, sortmode, ascending));

            if (requestid != Volatile.Read(ref _refreshRequestId)) return;

            var previousSelection = SelectedTrackIds.ToHashSet();
            if (!_uiDispatcher.CanAccess())
            {
                _uiDispatcher.Invoke(() =>
                {
                    ApplyResult(result, previousSelection);
                });
                return;
            }
            ApplyResult(result, previousSelection);
        }

        private void ApplyResult(
            (List<TrackRowItem> Rows, List<int> TrackIds) result,
            HashSet<int> previousSelection)
        {
            Tracks = new ObservableCollection<TrackRowItem>(result.Rows);
            AllTrackIds = result.TrackIds;
            SelectedTrackIds = result.TrackIds.Where(previousSelection.Contains).ToList();
        }

        private (List<TrackRowItem> Rows, List<int> TrackIds) BuildTracksResult(
            int[] sourceIds, string query, TrackSortMode sortMode, bool ascending)
        {
            if (sourceIds.Length == 0)
                return ([], []);
            var rows = _libraryCache.TrackIdsToTrackRowItems(sourceIds);
            if (!string.IsNullOrWhiteSpace(query))
            {
                rows = rows
                    .Where(r =>
                        r.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.ArtistNames.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        r.AlbumName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            var trackById = sourceIds
                .Select(id => _libraryCache.GetTrackById(id))
                .Where(t => t is not null)
                .Cast<Track>()
                .ToDictionary(t => t.Id, t => t);
            var albumIds = rows
                .Select(r => trackById.TryGetValue(r.Id, out var t) ? t.AlbumId : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var albumById = albumIds.ToDictionary(id => id, id => _libraryCache.GetAlbumById(id));
            var albumArtistById = albumIds.ToDictionary(id => id, id => _libraryCache.GetArtistNamesForAlbum(id));
            var albumDurationById = albumIds.ToDictionary(
                id => id,
                id => _libraryCache.GetTracksForAlbum(id).Sum(trackId => _libraryCache.GetTrackById(trackId)?.Duration ?? 0));
            IEnumerable<int> orderedAlbumIds;
            if (sortMode == TrackSortMode.Title)
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id => albumById.TryGetValue(id, out var a) ? a?.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumById.TryGetValue(id, out var a) ? a?.Year : int.MaxValue);
            }
            else if (sortMode == TrackSortMode.ArtistName)
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumById.TryGetValue(id, out var a) ? a?.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumById.TryGetValue(id, out var a) ? a?.Year : int.MaxValue);
            }
            else if (sortMode == TrackSortMode.Duration)
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id => albumDurationById[id])
                    .ThenBy(id => albumById.TryGetValue(id, out var a) ? a?.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id =>
                    {
                        if (!albumById.TryGetValue(id, out var a) || a?.Year <= 0)
                            return int.MaxValue;
                        return a?.Year;
                    })
                    .ThenBy(id => albumById.TryGetValue(id, out var a) ? a?.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase);
            }
            if (!ascending)
                orderedAlbumIds = orderedAlbumIds.Reverse();
            var albumRankById = orderedAlbumIds
                .Select((albumId, index) => (albumId, index))
                .ToDictionary(x => x.albumId, x => x.index);
            var orderedRows = rows
                .OrderBy(r =>
                {
                    if (!trackById.TryGetValue(r.Id, out var track))
                        return int.MaxValue;
                    return albumRankById.TryGetValue(track.AlbumId, out var rank) ? rank : int.MaxValue;
                })
                .ThenBy(r => r.DiskNumber)
                .ThenBy(r => r.TrackNumber)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var orderedIds = orderedRows.Select(r => r.Id).ToList();
            return (orderedRows, orderedIds);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _searchService.QueryChanged -= _searchService_QueryChanged;

            Tracks.Clear();
            AllTrackIds.Clear();
            SelectedTrackIds.Clear();
            _sourceTrackIds = [];
        }
    }
}
