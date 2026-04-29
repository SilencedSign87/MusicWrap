using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public enum TrackSortMode
    {
        Title,
        Year,
        ArtistName,
        Duration
    }

    public enum LibraryDetailTabKey
    {
        Albums,
        Tracks,
        About,
        Stats,
    }

    public enum SortDirectionOption
    {
        Ascending,
        Descending
    }

    public sealed class LibraryDetailTabItem
    {
        public required LibraryDetailTabKey Key { get; init; }
        public required string Title { get; init; }
    }

    public partial class LibraryEntryDetailPanelViewModel : ObservableObject
    {
        private readonly ILibraryCacheService _libraryCache;
        private readonly TracksContextMenuService _tracksContextMenuService;
        private readonly MusicLibrary _library;
        private readonly IMusicPlayerService _musicPlayerService;
        private LibraryViewModel? _hostLibraryViewModel;
        private int _headerStatsRequestId;
        private int _tracksViewRequestId;
        private int _entryPreloadRequestId;
        private List<int> _entryTrackIds = [];
        private LibraryEntryStatsModel? _cachedStats;

        [ObservableProperty] private LibraryEntry? currentEntry;

        [ObservableProperty] private string headerTitle = string.Empty;
        [ObservableProperty] private string? headerImagePath;
        [ObservableProperty] private string headerAlbumsCountText = "0";
        [ObservableProperty] private string headerTracksCountText = "0";
        [ObservableProperty] private string headerTotalDurationText = "00:00:00";

        [ObservableProperty] private ObservableCollection<LibraryDetailTabItem> tabs = [];
        [ObservableProperty] private LibraryDetailTabItem? selectedTab;

        [ObservableProperty] private ObservableCollection<AlbumSummary> albums = [];
        [ObservableProperty] private ObservableCollection<TrackRowItem> tracks = [];
        [ObservableProperty] private List<int> selectedTrackIds = [];
        [ObservableProperty] private List<int> allTrackIds = [];
        [ObservableProperty] private TrackSortMode selectedTrackSortMode = TrackSortMode.Year;
        [ObservableProperty] private bool sortAscending = false;
        [ObservableProperty] private SortDirectionOption selectedSortDirection = SortDirectionOption.Ascending;
        [ObservableProperty] private string trackSearchQuery = string.Empty;

        [ObservableProperty] private string statsLine1 = string.Empty;
        [ObservableProperty] private string statsLine2 = string.Empty;
        [ObservableProperty] private string statsLine3 = string.Empty;

        public LibraryEntryDetailPanelViewModel(ILibraryCacheService libraryCache, TracksContextMenuService tracksContextMenuService, MusicLibrary library, IMusicPlayerService musicPlayerService)
        {
            _libraryCache = libraryCache;
            _tracksContextMenuService = tracksContextMenuService;
            _library = library;
            _musicPlayerService = musicPlayerService;
        }

        public void AttachLibraryViewModel(LibraryViewModel? libraryViewModel)
        {
            _hostLibraryViewModel = libraryViewModel;
            _hostLibraryViewModel?.SetDetailSortOptions(SelectedTrackSortMode, SortAscending);
        }

        public void LoadEntry(LibraryEntry? entry)
        {
            CurrentEntry = entry;
            Albums.Clear();
            Tracks.Clear();
            SelectedTrackIds = [];
            AllTrackIds = [];
            TrackSearchQuery = string.Empty;
            _entryTrackIds = [];
            _cachedStats = null;
            StatsLine1 = string.Empty;
            StatsLine2 = string.Empty;
            StatsLine3 = string.Empty;

            if (entry is null)
            {
                Tabs = [];
                SelectedTab = null;
                HeaderTitle = string.Empty;
                HeaderImagePath = null;
                HeaderAlbumsCountText = "0";
                HeaderTracksCountText = "0";
                HeaderTotalDurationText = "00:00:00";
                return;
            }

            HeaderTitle = entry.Title;
            HeaderImagePath = entry.ImagePath;
            HeaderAlbumsCountText = "...";
            HeaderTracksCountText = "...";
            HeaderTotalDurationText = "...";

            Tabs = BuildTabs(entry.Type);
            SelectedTab = Tabs.FirstOrDefault();
            _ = LoadHeaderStatsDeferredAsync(entry);
            _ = PreloadEntryDataAsync(entry, SelectedTab?.Key);
        }

        private async Task PreloadEntryDataAsync(LibraryEntry entry, LibraryDetailTabKey? prioritizedTab)
        {
            var requestId = Interlocked.Increment(ref _entryPreloadRequestId);

            async Task LoadTracksAsync()
            {
                var trackIds = await Task.Run(() => _libraryCache.GetTrackIdsForEntry(entry));
                if (requestId != Volatile.Read(ref _entryPreloadRequestId))
                {
                    return;
                }

                _entryTrackIds = trackIds.ToList();
                await RefreshTracksViewAsync(false, true);
            }

            async Task LoadStatsAsync()
            {
                var stats = await Task.Run(() => _libraryCache.GetStatsForEntry(entry));
                if (requestId != Volatile.Read(ref _entryPreloadRequestId))
                {
                    return;
                }

                _cachedStats = stats;
                if (SelectedTab?.Key == LibraryDetailTabKey.Stats)
                {
                    ApplyStats(stats);
                }
            }

            if (prioritizedTab == LibraryDetailTabKey.Stats)
            {
                await LoadStatsAsync();
                await LoadTracksAsync();
                return;
            }

            await LoadTracksAsync();
            await LoadStatsAsync();
        }

        private async Task LoadHeaderStatsDeferredAsync(LibraryEntry entry)
        {
            var requestId = Interlocked.Increment(ref _headerStatsRequestId);

            var stats = await Task.Run(() =>
            {
                var albumCount = _libraryCache.GetAlbumsForEntry(entry).Count;
                var trackIds = _libraryCache.GetTrackIdsForEntry(entry);
                var totalSeconds = 0L;

                foreach (var trackId in trackIds)
                {
                    totalSeconds += _libraryCache.GetTrackById(trackId)?.Duration ?? 0;
                }

                return (albumCount, tracksCount: trackIds.Length, totalSeconds);
            });

            if (requestId != Volatile.Read(ref _headerStatsRequestId))
            {
                return;
            }

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(() => ApplyHeaderStats(stats.albumCount, stats.tracksCount, stats.totalSeconds));
                return;
            }

            ApplyHeaderStats(stats.albumCount, stats.tracksCount, stats.totalSeconds);
        }

        private void ApplyHeaderStats(int albumCount, int tracksCount, long totalSeconds)
        {
            HeaderAlbumsCountText = albumCount.ToString();
            HeaderTracksCountText = tracksCount.ToString();
            HeaderTotalDurationText = FormatDuration(totalSeconds);
        }

        private static string FormatDuration(long totalSeconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }

        private void ApplyStats(LibraryEntryStatsModel stats)
        {
            StatsLine1 = "Albums: " + stats.AlbumsCount;
            StatsLine2 = "Tracks: " + stats.TracksCount;
            StatsLine3 = "Artists: " + stats.ArtistsCount;
        }

        [RelayCommand]
        private void SelectTab(LibraryDetailTabItem? tab)
        {
            if (tab is null) return;
            SelectedTab = tab;
            LoadSelectedTab();
        }

        partial void OnSelectedTabChanged(LibraryDetailTabItem? value)
        {
            if (value is not null)
            {
                LoadSelectedTab();
            }
        }

        private void LoadSelectedTab()
        {
            if (CurrentEntry is null || SelectedTab is null)
                return;

            if (SelectedTab.Key == LibraryDetailTabKey.Albums)
            {
                return;
            }
            else if (SelectedTab.Key == LibraryDetailTabKey.Tracks)
            {
                _ = RefreshTracksViewAsync(false);
            }
            else if (SelectedTab.Key == LibraryDetailTabKey.Stats)
            {
                if (_cachedStats is not null)
                {
                    ApplyStats(_cachedStats);
                }
            }
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

        [RelayCommand]
        private void PlayAllTracks()
        {
            if (AllTrackIds.Count == 0)
            {
                return;
            }

            _musicPlayerService.SetQueue(AllTrackIds);
            _musicPlayerService.PlayTrack(AllTrackIds[0]);
        }
        [RelayCommand]
        private void ShuffleAllTracks()
        {
            if (AllTrackIds.Count == 0) return;
            _musicPlayerService.SetQueue(AllTrackIds.Shuffle());
            _musicPlayerService.PlayIndex(0);

        }

        [RelayCommand]
        private void SortTracksByTitle()
        {
            SelectedTrackSortMode = TrackSortMode.Title;
        }

        [RelayCommand]
        private void SortTracksByYear()
        {
            SelectedTrackSortMode = TrackSortMode.Year;
        }

        [RelayCommand]
        private void SortTracksByArtistName()
        {
            SelectedTrackSortMode = TrackSortMode.ArtistName;
        }

        [RelayCommand]
        private void SortTracksByDuration()
        {
            SelectedTrackSortMode = TrackSortMode.Duration;
        }

        [RelayCommand]
        private void SetSortAscending()
        {
            SortAscending = true;
        }

        [RelayCommand]
        private void SetSortDescending()
        {
            SortAscending = false;
        }

        partial void OnSelectedTrackSortModeChanged(TrackSortMode value)
        {
            OnPropertyChanged(nameof(IsSortByTitle));
            OnPropertyChanged(nameof(IsSortByYear));
            OnPropertyChanged(nameof(IsSortByArtistName));
            OnPropertyChanged(nameof(IsSortByDuration));
            _hostLibraryViewModel?.SetDetailSortOptions(value, SortAscending);
            _ = RefreshTracksViewAsync(false, true);
        }

        partial void OnSortAscendingChanged(bool value)
        {
            var expectedDirection = value ? SortDirectionOption.Ascending : SortDirectionOption.Descending;
            if (SelectedSortDirection != expectedDirection)
            {
                SelectedSortDirection = expectedDirection;
            }

            OnPropertyChanged(nameof(IsSortAscending));
            OnPropertyChanged(nameof(IsSortDescending));
            _hostLibraryViewModel?.SetDetailSortOptions(SelectedTrackSortMode, value);
            _ = RefreshTracksViewAsync(false, true);
        }

        partial void OnSelectedSortDirectionChanged(SortDirectionOption value)
        {
            var nextAscending = value == SortDirectionOption.Ascending;
            if (SortAscending != nextAscending)
            {
                SortAscending = nextAscending;
            }
        }

        partial void OnTrackSearchQueryChanged(string value)
        {
            _ = RefreshTracksViewAsync(true, true);
        }

        public bool IsSortByTitle => SelectedTrackSortMode == TrackSortMode.Title;
        public bool IsSortByYear => SelectedTrackSortMode == TrackSortMode.Year;
        public bool IsSortByArtistName => SelectedTrackSortMode == TrackSortMode.ArtistName;
        public bool IsSortByDuration => SelectedTrackSortMode == TrackSortMode.Duration;
        public bool IsSortAscending => SortAscending;
        public bool IsSortDescending => !SortAscending;

        private async Task RefreshTracksViewAsync(bool debounce, bool forceWhenTabHidden = false)
        {
            if (!forceWhenTabHidden && SelectedTab?.Key != LibraryDetailTabKey.Tracks)
            {
                return;
            }

            var requestId = Interlocked.Increment(ref _tracksViewRequestId);

            if (debounce)
            {
                await Task.Delay(280);
                if (requestId != Volatile.Read(ref _tracksViewRequestId))
                {
                    return;
                }
            }

            var sourceIds = _entryTrackIds.ToArray();
            var query = TrackSearchQuery?.Trim() ?? string.Empty;
            var sortMode = SelectedTrackSortMode;
            var ascending = SortAscending;

            var result = await Task.Run(() => BuildTracksResult(sourceIds, query, sortMode, ascending));

            if (requestId != Volatile.Read(ref _tracksViewRequestId))
            {
                return;
            }

            var previousSelection = SelectedTrackIds.ToHashSet();

            void Apply()
            {
                Tracks = new ObservableCollection<TrackRowItem>(result.Rows);
                AllTrackIds = result.TrackIds;
                SelectedTrackIds = result.TrackIds.Where(previousSelection.Contains).ToList();
                _hostLibraryViewModel?.ApplyGlobalTrackOrder(result.TrackIds);
            }

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(Apply);
                return;
            }

            Apply();
        }

        private (List<TrackRowItem> Rows, List<int> TrackIds) BuildTracksResult(int[] sourceIds, string query, TrackSortMode sortMode, bool ascending)
        {
            if (sourceIds.Length == 0)
            {
                return ([], []);
            }

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

            var albumById = _library.Albums.ToDictionary(a => a.Id, a => a);
            var albumArtistById = albumIds.ToDictionary(id => id, id => _libraryCache.GetArtistNamesForAlbum(id));
            var albumDurationById = albumIds.ToDictionary(
                id => id,
                id => _libraryCache.GetTracksForAlbum(id).Sum(trackId => _libraryCache.GetTrackById(trackId)?.Duration ?? 0));

            IEnumerable<int> orderedAlbumIds;

            if (sortMode == TrackSortMode.Title)
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id => albumById.TryGetValue(id, out var album) ? album.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumById.TryGetValue(id, out var album) ? album.Year : int.MaxValue);
            }
            else if (sortMode == TrackSortMode.ArtistName)
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumById.TryGetValue(id, out var album) ? album.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumById.TryGetValue(id, out var album) ? album.Year : int.MaxValue);
            }
            else if (sortMode == TrackSortMode.Duration)
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id => albumDurationById[id])
                    .ThenBy(id => albumById.TryGetValue(id, out var album) ? album.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                orderedAlbumIds = albumIds
                    .OrderBy(id =>
                    {
                        if (!albumById.TryGetValue(id, out var album) || album.Year <= 0)
                        {
                            return int.MaxValue;
                        }

                        return album.Year;
                    })
                    .ThenBy(id => albumById.TryGetValue(id, out var album) ? album.Title : string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(id => albumArtistById[id], StringComparer.OrdinalIgnoreCase);
            }

            if (!ascending)
            {
                orderedAlbumIds = orderedAlbumIds.Reverse();
            }

            var albumRankById = orderedAlbumIds
                .Select((albumId, index) => (albumId, index))
                .ToDictionary(x => x.albumId, x => x.index);

            var orderedRows = rows
                .OrderBy(r =>
                {
                    if (!trackById.TryGetValue(r.Id, out var track))
                    {
                        return int.MaxValue;
                    }

                    return albumRankById.TryGetValue(track.AlbumId, out var rank) ? rank : int.MaxValue;
                })
                .ThenBy(r => r.DiskNumber)
                .ThenBy(r => r.TrackNumber)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orderedIds = orderedRows.Select(r => r.Id).ToList();
            return (orderedRows, orderedIds);
        }

        private static ObservableCollection<LibraryDetailTabItem> BuildTabs(string type)
        {
            static LibraryDetailTabItem T(LibraryDetailTabKey key, string title) => new() { Key = key, Title = title };

            var entryType = type?.Trim() ?? string.Empty;

            if (entryType.Equals("Album", StringComparison.OrdinalIgnoreCase))
            {
                return [T(LibraryDetailTabKey.Tracks, "Tracks"), T(LibraryDetailTabKey.Stats, "Stats")];
            }

            if (entryType.Equals("Artist", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    T(LibraryDetailTabKey.Albums, "Albums"),
                    T(LibraryDetailTabKey.Tracks, "Tracks"),
                    T(LibraryDetailTabKey.About, "About"),
                    T(LibraryDetailTabKey.Stats, "Stats")
                ];
            }

            if (entryType.Equals("Genre", StringComparison.OrdinalIgnoreCase) || entryType.Equals("Decade", StringComparison.OrdinalIgnoreCase))
            {
                return [
                    T(LibraryDetailTabKey.Albums, "Albums"),
                    T(LibraryDetailTabKey.Tracks, "Tracks"),
                    T(LibraryDetailTabKey.Stats, "Stats")
                ];
            }

            return [T(LibraryDetailTabKey.Albums, "Albums"), T(LibraryDetailTabKey.Stats, "Stats")];
        }

    }
    public sealed class LibraryEntryStatsModel
    {
        public int AlbumsCount { get; init; }
        public int TracksCount { get; init; }
        public int ArtistsCount { get; init; }
    }
}
