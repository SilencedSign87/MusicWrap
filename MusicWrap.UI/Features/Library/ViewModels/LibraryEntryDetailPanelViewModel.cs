using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

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

    public partial class LibraryEntryDetailPanelViewModel : ObservableObject, IDisposable
    {
        private readonly ILibraryService _libraryCache;
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IwindowsImageService _imageService;
        private int _headerStatsRequestId;


        [ObservableProperty] private LibraryEntry? currentEntry;

        [ObservableProperty] private string headerTitle = string.Empty;
        [ObservableProperty] private string? headerImagePath;
        [ObservableProperty] private string headerAlbumsCountText = "0";
        [ObservableProperty] private string headerTracksCountText = "0";
        [ObservableProperty] private string headerTotalDurationText = "00:00:00";

        [ObservableProperty] private ObservableCollection<LibraryDetailTabItem> tabs = [];
        [ObservableProperty] private LibraryDetailTabItem? selectedTab;

        [ObservableProperty] private TrackSortMode selectedTrackSortMode = TrackSortMode.Year;
        [ObservableProperty] private bool sortAscending = false;
        [ObservableProperty] private SortDirectionOption selectedSortDirection = SortDirectionOption.Ascending;

        private bool _isDisposed = false;

        [ObservableProperty] private LibraryEntryTracksViewModel? tracksViewModel;
        [ObservableProperty] private LibraryEntryAlbumViewModel? albumEntriesViewModel;

        public LibraryEntryDetailPanelViewModel(
            ILibraryService libraryCache,
            IMusicPlayerService musicPlayerService,
            IServiceProvider serviceProvider,
            IwindowsImageService imageService
            )
        {
            _libraryCache = libraryCache;
            _musicPlayerService = musicPlayerService;
            _serviceProvider = serviceProvider;
            _imageService = imageService;
        }

        public void LoadEntry(LibraryEntry? entry)
        {
            var previousEntryType = CurrentEntry?.Type;
            CurrentEntry = entry;
            _imageService.ClearCache();

            if (entry is null)
            {
                Tabs = [];
                SelectedTab = null;
                HeaderTitle = string.Empty;
                HeaderImagePath = null;
                HeaderAlbumsCountText = "0";
                HeaderTracksCountText = "0";
                HeaderTotalDurationText = "00:00:00";
                AlbumEntriesViewModel?.SelectedEntry = null;
                TracksViewModel?.SelectedEntry = null;
                return;
            }

            HeaderTitle = entry.Title;
            HeaderImagePath = entry.ImagePath;
            HeaderAlbumsCountText = "...";
            HeaderTracksCountText = "...";
            HeaderTotalDurationText = "...";

            if (previousEntryType != entry.Type)
                Tabs = BuildTabs(entry.Type);

            var preferred = SelectedTab is { } current
                ? Tabs.FirstOrDefault(t => t.Key == current.Key)
                : null;

            SelectedTab = preferred ?? Tabs.FirstOrDefault();

            LoadSelectedTab();

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

        #region Internal
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

        private void LoadSelectedTab()
        {
            if (CurrentEntry is null || SelectedTab is null)
                return;


            var key = SelectedTab.Key;

            switch (key)
            {
                case LibraryDetailTabKey.Albums:
                    TracksViewModel?.SelectedEntry = null;

                    EnsureAlbumViewModel();
                    if (AlbumEntriesViewModel is not null)
                    {
                        AlbumEntriesViewModel.SortMode = SelectedTrackSortMode;
                        AlbumEntriesViewModel.SortAscending = SortAscending;
                        AlbumEntriesViewModel.SelectedEntry = CurrentEntry;

                    }
                    break;
                case LibraryDetailTabKey.Tracks:
                    AlbumEntriesViewModel?.SelectedEntry = null;

                    EnsureTrackViewModel();
                    if (TracksViewModel is not null)
                    {
                        TracksViewModel.SortMode = SelectedTrackSortMode;
                        TracksViewModel.SortAscending = SortAscending;
                        TracksViewModel.SelectedEntry = CurrentEntry;
                    }
                    break;
                default:
                    break;
            }
        }
        private void EnsureAlbumViewModel() => AlbumEntriesViewModel ??= _serviceProvider.GetRequiredService<LibraryEntryAlbumViewModel>();
        private void EnsureTrackViewModel() => TracksViewModel ??= _serviceProvider.GetRequiredService<LibraryEntryTracksViewModel>();
        #endregion
        #region Relay Commands

        [RelayCommand]
        private void SelectTab(LibraryDetailTabItem? tab)
        {
            if (tab is null) return;
            SelectedTab = tab;
        }

        [RelayCommand]
        private void PlayAllTracks()
        {
            var trackIds = TracksViewModel?.AllTrackIds;
            if (trackIds is null || trackIds.Count == 0)
                return;
            if (_musicPlayerService.IsShuffleEnabled)
                _musicPlayerService.ToggleShuffle();
            _musicPlayerService.SetQueue(trackIds);
            _musicPlayerService.PlayIndex(0);
        }
        [RelayCommand]
        private void ShuffleAllTracks()
        {
            var trackIds = TracksViewModel?.AllTrackIds;
            if (trackIds is null || trackIds.Count == 0)
                return;
            _musicPlayerService.SetQueue(trackIds);
            if (!_musicPlayerService.IsShuffleEnabled)
                _musicPlayerService.ToggleShuffle();
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

        #endregion

        #region Partial Properties
        partial void OnSelectedTabChanged(LibraryDetailTabItem? value)
        {
            LoadSelectedTab();
        }
        partial void OnSelectedTrackSortModeChanged(TrackSortMode value)
        {
            OnPropertyChanged(nameof(IsSortByTitle));
            OnPropertyChanged(nameof(IsSortByYear));
            OnPropertyChanged(nameof(IsSortByArtistName));
            OnPropertyChanged(nameof(IsSortByDuration));

            switch (SelectedTab?.Key)
            {
                case LibraryDetailTabKey.Albums:
                    AlbumEntriesViewModel?.SortMode = value;
                    break;
                case LibraryDetailTabKey.Tracks:
                    TracksViewModel?.SortMode = value;
                    break;
            }
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

            switch (SelectedTab?.Key)
            {
                case LibraryDetailTabKey.Albums:
                    AlbumEntriesViewModel?.SortAscending = value;
                    break;
                case LibraryDetailTabKey.Tracks:
                    TracksViewModel?.SortAscending = value;
                    break;
            }
        }

        partial void OnSelectedSortDirectionChanged(SortDirectionOption value)
        {
            var nextAscending = value == SortDirectionOption.Ascending;
            if (SortAscending != nextAscending)
            {
                SortAscending = nextAscending;
            }
        }

        #endregion

        public bool IsSortByTitle => SelectedTrackSortMode == TrackSortMode.Title;
        public bool IsSortByYear => SelectedTrackSortMode == TrackSortMode.Year;
        public bool IsSortByArtistName => SelectedTrackSortMode == TrackSortMode.ArtistName;
        public bool IsSortByDuration => SelectedTrackSortMode == TrackSortMode.Duration;
        public bool IsSortAscending => SortAscending;
        public bool IsSortDescending => !SortAscending;

        private static ObservableCollection<LibraryDetailTabItem> BuildTabs(LibraryEntryType type)
        {
            static LibraryDetailTabItem T(LibraryDetailTabKey key, string title) => new() { Key = key, Title = title };

            switch (type)
            {
                case LibraryEntryType.Album:
                    return [T(LibraryDetailTabKey.Tracks, "Tracks"), T(LibraryDetailTabKey.Stats, "Stats")];
                case LibraryEntryType.TrackArtist:
                case LibraryEntryType.AlbumArtist:
                    return [
                        T(LibraryDetailTabKey.Albums, "Albums"),
                T(LibraryDetailTabKey.Tracks, "Tracks"),
                T(LibraryDetailTabKey.About, "About"),
                T(LibraryDetailTabKey.Stats, "Stats")
                    ];
                case LibraryEntryType.Genre:
                case LibraryEntryType.Decade:
                    return [
                        T(LibraryDetailTabKey.Albums, "Albums"),
                T(LibraryDetailTabKey.Tracks, "Tracks"),
                T(LibraryDetailTabKey.Stats, "Stats")
                    ];
                default:
                    return [T(LibraryDetailTabKey.Albums, "Albums"), T(LibraryDetailTabKey.Stats, "Stats")];
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            (TracksViewModel as IDisposable)?.Dispose();
            TracksViewModel = null;
            (AlbumEntriesViewModel as IDisposable)?.Dispose();
            AlbumEntriesViewModel = null;
            CurrentEntry = null;
            //_entryTrackIds = [];
        }
    }
}
