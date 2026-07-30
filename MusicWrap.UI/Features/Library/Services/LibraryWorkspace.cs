using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.User.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace MusicWrap.UI.Features.Library.Services
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

    public sealed class LibraryDetailTabItem
    {
        public required LibraryDetailTabKey Key { get; init; }
        public required string Title { get; init; }
    }

    public sealed partial class LibraryWorkspace : ObservableObject
    {
        private readonly UserSettings _userSettings;
        private bool _isRestoring = true;
        private bool _isInitialized;

        public LibraryWorkspace(UserSettings settings)
        {
            _userSettings = settings;
        }

        #region Properties

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetViewModeCommand))]
        private LibraryEntryType _listBy = LibraryEntryType.AlbumArtist;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEntryListAscending))]
        [NotifyPropertyChangedFor(nameof(IsEntryListDescending))]
        private bool _entryListAscending = true;

        [ObservableProperty]
        private LibraryEntry? _selectedEntry;
        [ObservableProperty]
        private ObservableCollection<LibraryDetailTabItem> _tabs = [];
        [ObservableProperty]
        private LibraryDetailTabItem? _selectedTab;
        [ObservableProperty]
        private TrackSortMode _trackSortMode = TrackSortMode.Year;
        [ObservableProperty]
        private bool _sortAscending = false;

        public bool IsInitialized => _isInitialized;
        #endregion

        #region Computed Properties
        // entry
        public bool IsAlbumView => ListBy == LibraryEntryType.Album;
        public bool IsTrackArtistView => ListBy == LibraryEntryType.TrackArtist;
        public bool IsAlbumArtistView => ListBy == LibraryEntryType.AlbumArtist;
        public bool IsGenreView => ListBy == LibraryEntryType.Genre;
        public bool IsDecadeView => ListBy == LibraryEntryType.Decade;

        public bool IsEntryListAscending => EntryListAscending;
        public bool IsEntryListDescending => !EntryListAscending;

        // tab visibility
        public bool IsAlbumsTabSelected => SelectedTab?.Key == LibraryDetailTabKey.Albums;
        public bool IsTracksTabSelected => SelectedTab?.Key == LibraryDetailTabKey.Tracks;
        public bool IsAboutTabSelected => SelectedTab?.Key == LibraryDetailTabKey.About;
        public bool IsStatsTabSelected => SelectedTab?.Key == LibraryDetailTabKey.Stats;
        // sort helpers
        public bool IsSortByTitle => TrackSortMode == TrackSortMode.Title;
        public bool IsSortByYear => TrackSortMode == TrackSortMode.Year;
        public bool IsSortByArtistName => TrackSortMode == TrackSortMode.ArtistName;
        public bool IsSortByDuration => TrackSortMode == TrackSortMode.Duration;
        public bool IsSortAscending => SortAscending;
        public bool IsSortDescending => !SortAscending;
        #endregion

        #region Relay Commands
        [RelayCommand(CanExecute = nameof(CanSetViewMode))]
        private void SetViewMode(string mode)
        {
            if (Enum.TryParse<LibraryEntryType>(mode, ignoreCase: true, out var result))
                ListBy = result;
        }
        private bool CanSetViewMode(string mode)
        {
            if (!Enum.TryParse<LibraryEntryType>(mode, ignoreCase: true, out var result))
                return false;
            return ListBy != result;
        }
        [RelayCommand] private void SetEntryListAscending() => EntryListAscending = true;
        [RelayCommand] private void SetEntryListDescending() => EntryListAscending = false;
        [RelayCommand] private void SortTracksByTitle() => TrackSortMode = TrackSortMode.Title;
        [RelayCommand] private void SortTracksByYear() => TrackSortMode = TrackSortMode.Year;
        [RelayCommand] private void SortTracksByArtistName() => TrackSortMode = TrackSortMode.ArtistName;
        [RelayCommand] private void SortTracksByDuration() => TrackSortMode = TrackSortMode.Duration;
        [RelayCommand] private void SetSortAscending() => SortAscending = true;
        [RelayCommand] private void SetSortDescending() => SortAscending = false;
        #endregion

        #region Lifecycle
        partial void OnListByChanged(LibraryEntryType value) => FlushSettings();
        partial void OnEntryListAscendingChanged(bool value) => FlushSettings();
        partial void OnSelectedEntryChanged(LibraryEntry? value)
        {
            RebuildTabs(value);
            FlushSettings();
        }
        partial void OnSelectedTabChanged(LibraryDetailTabItem? value)
        {
            OnPropertyChanged(nameof(IsAlbumsTabSelected));
            OnPropertyChanged(nameof(IsTracksTabSelected));
            OnPropertyChanged(nameof(IsAboutTabSelected));
            OnPropertyChanged(nameof(IsStatsTabSelected));
            FlushSettings();
        }
        partial void OnTrackSortModeChanged(TrackSortMode value)
        {
            OnPropertyChanged(nameof(IsSortByTitle));
            OnPropertyChanged(nameof(IsSortByYear));
            OnPropertyChanged(nameof(IsSortByArtistName));
            OnPropertyChanged(nameof(IsSortByDuration));
            FlushSettings();
        }
        partial void OnSortAscendingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsSortAscending));
            OnPropertyChanged(nameof(IsSortDescending));
            FlushSettings();
        }
        partial void OnSelectedEntryChanged(LibraryEntry? oldValue, LibraryEntry? newValue)
        {
            Debug.WriteLine($"SelectedEntry changed from {oldValue?.Title ?? "null"} to {newValue?.Title ?? "null"}");
        }
        #endregion

        #region Public
        public void SelectTab(LibraryDetailTabItem? tab)
        {
            if (tab is not null) SelectedTab = tab;
        }
        public void Initialize(IReadOnlyList<LibraryEntry> entries)
        {
            try
            {
                var saved = _userSettings.LibrarySettings;

                ListBy = saved.EntryType;
                EntryListAscending = saved.EntryListAscending;
                TrackSortMode = (TrackSortMode)saved.TrackSortModeValue;
                SortAscending = saved.TrackSortAscending;
                
                SelectedEntry = saved.SelectedEntryId is int id
                    ? entries.FirstOrDefault(e => e.Id == id)
                    : null;

                SelectedEntry ??= entries.FirstOrDefault();
                
                if (SelectedEntry is not null && saved.SelectedTabKeyValue is int tabKey)
                {
                    var targetTab = Tabs.FirstOrDefault(t => (int)t.Key == tabKey);
                    if (targetTab is not null)
                        SelectedTab = targetTab;
                }

                _isInitialized = true;
            }
            finally
            {
                _isRestoring = false;
            }
        }
        #endregion

        #region Internal
        private void FlushSettings()
        {
            if (_isRestoring) return;

            var ls = _userSettings.LibrarySettings;
            ls.EntryType = ListBy;
            ls.EntryListAscending = EntryListAscending;
            ls.SelectedEntryId = SelectedEntry?.Id;
            ls.TrackSortAscending = SortAscending;
            ls.TrackSortModeValue = (int)TrackSortMode;
            ls.SelectedTabKeyValue = SelectedTab is not null ? (int)SelectedTab.Key : null;
        }
        private void RebuildTabs(LibraryEntry? entry)
        {
            if (entry is null)
            {
                Tabs = [];
                SelectedTab = null;
                return;
            }
            var newTabs = BuildTabs(entry.Type);
            Tabs = newTabs;

            var preferred = SelectedTab is { } current
                ? newTabs.FirstOrDefault(t => t.Key == current.Key)
                : null;
            SelectedTab = preferred ?? newTabs.FirstOrDefault();
        }
        private static ObservableCollection<LibraryDetailTabItem> BuildTabs(LibraryEntryType type)
        {
            static LibraryDetailTabItem T(LibraryDetailTabKey key, string title) =>
                new() { Key = key, Title = title };
            return type switch
            {
                LibraryEntryType.Album => [T(LibraryDetailTabKey.Tracks, "Tracks"), T(LibraryDetailTabKey.Stats, "Stats")],
                LibraryEntryType.TrackArtist or LibraryEntryType.AlbumArtist =>
                    [T(LibraryDetailTabKey.Albums, "Albums"), T(LibraryDetailTabKey.Tracks, "Tracks"),
                 T(LibraryDetailTabKey.About, "About"), T(LibraryDetailTabKey.Stats, "Stats")],
                LibraryEntryType.Genre or LibraryEntryType.Decade =>
                    [T(LibraryDetailTabKey.Albums, "Albums"), T(LibraryDetailTabKey.Tracks, "Tracks"),
                 T(LibraryDetailTabKey.Stats, "Stats")],
                _ => [T(LibraryDetailTabKey.Albums, "Albums"), T(LibraryDetailTabKey.Stats, "Stats")],
            };
        }
        #endregion

    }
}
