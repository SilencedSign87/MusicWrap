using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core;
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
        Name,
        AlbumName,
        AlbumYear,
        Duration
    }

    public enum LibraryDetailTabKey
    {
        Albums,
        Tracks,
        Profile,
        Stats,
        More
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
        private int _headerStatsRequestId;
        private int _tracksViewRequestId;
        private List<int> _entryTrackIds = [];

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
        [ObservableProperty] private TrackSortMode selectedTrackSortMode = TrackSortMode.AlbumName;
        [ObservableProperty] private string trackSearchQuery = string.Empty;

        [ObservableProperty] private string statsLine1 = string.Empty;
        [ObservableProperty] private string statsLine2 = string.Empty;
        [ObservableProperty] private string statsLine3 = string.Empty;

        public bool IsAlbumsTabActive => SelectedTab?.Key == LibraryDetailTabKey.Albums;
        public bool IsTracksTabActive => SelectedTab?.Key == LibraryDetailTabKey.Tracks;
        public bool IsProfileTabActive => SelectedTab?.Key == LibraryDetailTabKey.Profile;
        public bool IsStatsTabActive => SelectedTab?.Key == LibraryDetailTabKey.Stats;
        public bool IsMoreTabActive => SelectedTab?.Key == LibraryDetailTabKey.More;
        public bool IsSortByName => SelectedTrackSortMode == TrackSortMode.Name;
        public bool IsSortByAlbumName => SelectedTrackSortMode == TrackSortMode.AlbumName;
        public bool IsSortByAlbumYear => SelectedTrackSortMode == TrackSortMode.AlbumYear;
        public bool IsSortByDuration => SelectedTrackSortMode == TrackSortMode.Duration;

        public LibraryEntryDetailPanelViewModel(ILibraryCacheService libraryCache, TracksContextMenuService tracksContextMenuService, MusicLibrary library, IMusicPlayerService musicPlayerService)
        {
            _libraryCache = libraryCache;
            _tracksContextMenuService = tracksContextMenuService;
            _library = library;
            _musicPlayerService = musicPlayerService;
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
                RaiseTabFlags();
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

            RaiseTabFlags();
        }

        private void LoadSelectedTab()
        {
            if (CurrentEntry is null || SelectedTab is null)
                return;

            if (SelectedTab.Key == LibraryDetailTabKey.Albums)
            {
                Albums = new ObservableCollection<AlbumSummary>(_libraryCache.GetAlbumsForEntry(CurrentEntry));
            }
            else if (SelectedTab.Key == LibraryDetailTabKey.Tracks)
            {
                var ids = _libraryCache.GetTrackIdsForEntry(CurrentEntry);
                _entryTrackIds = ids.ToList();
                SelectedTrackIds = [];
                _ = RefreshTracksViewAsync(false);
            }
            else if (SelectedTab.Key == LibraryDetailTabKey.Stats)
            {
                var stats = _libraryCache.GetStatsForEntry(CurrentEntry);
                StatsLine1 = "Albums: " + stats.AlbumsCount;
                StatsLine2 = "Tracks: " + stats.TracksCount;
                StatsLine3 = "Artists: " + stats.ArtistsCount;
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
        private void SortTracksByName()
        {
            SelectedTrackSortMode = TrackSortMode.Name;
        }

        [RelayCommand]
        private void SortTracksByAlbumName()
        {
            SelectedTrackSortMode = TrackSortMode.AlbumName;
        }

        [RelayCommand]
        private void SortTracksByAlbumYear()
        {
            SelectedTrackSortMode = TrackSortMode.AlbumYear;
        }

        [RelayCommand]
        private void SortTracksByDuration()
        {
            SelectedTrackSortMode = TrackSortMode.Duration;
        }

        partial void OnSelectedTrackSortModeChanged(TrackSortMode value)
        {
            OnPropertyChanged(nameof(IsSortByName));
            OnPropertyChanged(nameof(IsSortByAlbumName));
            OnPropertyChanged(nameof(IsSortByAlbumYear));
            OnPropertyChanged(nameof(IsSortByDuration));

            _ = RefreshTracksViewAsync(false);
        }

        partial void OnTrackSearchQueryChanged(string value)
        {
            _ = RefreshTracksViewAsync(true);
        }

        private async Task RefreshTracksViewAsync(bool debounce)
        {
            if (SelectedTab?.Key != LibraryDetailTabKey.Tracks)
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

            var result = await Task.Run(() => BuildTracksResult(sourceIds, query, sortMode));

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
            }

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                await Application.Current.Dispatcher.InvokeAsync(Apply);
                return;
            }

            Apply();
        }

        private (List<TrackRowItem> Rows, List<int> TrackIds) BuildTracksResult(int[] sourceIds, string query, TrackSortMode sortMode)
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

            IEnumerable<TrackRowItem> ordered;

            if (sortMode == TrackSortMode.Name)
            {
                ordered = rows
                    .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.ArtistNames, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.AlbumName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.DiskNumber)
                    .ThenBy(t => t.TrackNumber);
            }
            else if (sortMode == TrackSortMode.AlbumName)
            {
                ordered = rows
                    .OrderBy(t => t.AlbumName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.DiskNumber)
                    .ThenBy(t => t.TrackNumber)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase);
            }
                    else if (sortMode == TrackSortMode.AlbumYear)
            {
                var albumYearById = _library.Albums.ToDictionary(a => a.Id, a => a.Year);

                ordered = rows
                    .OrderBy(t =>
                    {
                        var track = _libraryCache.GetTrackById(t.Id);
                        if (track is null)
                        {
                            return int.MaxValue;
                        }

                        return albumYearById.TryGetValue(track.AlbumId, out var year) ? year : int.MaxValue;
                    })
                    .ThenBy(t => t.AlbumName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.DiskNumber)
                    .ThenBy(t => t.TrackNumber)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                ordered = rows
                    .OrderBy(t => _libraryCache.GetTrackById(t.Id)?.Duration ?? int.MaxValue)
                    .ThenBy(t => t.Title, StringComparer.OrdinalIgnoreCase);
            }

                    var orderedRows = ordered.ToList();
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
                    T(LibraryDetailTabKey.Profile, "Profile"),
                    T(LibraryDetailTabKey.More, "More"),
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

        private void RaiseTabFlags()
        {
            OnPropertyChanged(nameof(IsAlbumsTabActive));
            OnPropertyChanged(nameof(IsTracksTabActive));
            OnPropertyChanged(nameof(IsProfileTabActive));
            OnPropertyChanged(nameof(IsStatsTabActive));
            OnPropertyChanged(nameof(IsMoreTabActive));
        }

    }
    public sealed class LibraryEntryStatsModel
    {
        public int AlbumsCount { get; init; }
        public int TracksCount { get; init; }
        public int ArtistsCount { get; init; }
    }
}
