using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Core;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Application;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class LibraryViewModel : ObservableObject
    {
        [ObservableProperty] private string listBy = "Artist"; // Album, Artist, Genre, Decade

        [ObservableProperty] private bool ascending = true;

        [ObservableProperty] private IReadOnlyList<LibraryEntry> entries = [];

        [ObservableProperty] private CollectionViewSource entriesViewSource = new();

        [ObservableProperty] private LibraryEntry? selectedEntry;

        //[ObservableProperty] private List<object> albumsForSelectedEntry = [];
        [ObservableProperty] private ObservableCollection<AlbumGridRowModel> gridRows = [];

        [ObservableProperty] private int? expandedAlbumId = null;

        [ObservableProperty] private bool isLoading;

        [ObservableProperty] private bool isLoadingIndeterminate = true;

        [ObservableProperty] private double loadingProgressValue = 0;
        [ObservableProperty] private int layoutColumns = 1;

        private List<AlbumData> _visibleAlbums = [];

        private CancellationTokenSource? _imageCts;
        private string _activeSearchQuery = string.Empty;

        private bool _isInitializing;
        private int _loadEntriesRequestId;
        private readonly IProgress<ScanProgress> _scanProgress;
        const int albumWidth = 150;

        // Services
        private readonly MusicLibrary _library;
        private readonly ILibraryScanner _scanner;
        private readonly ILibraryCacheService _LibraryCache;
        private readonly IMusicPlayerService _player;

        public LibraryViewModel(MusicLibrary library, ILibraryScanner scanner, ILibraryCacheService libraryCache, UserSettings settings, IMusicPlayerService player)
        {
            _library = library;
            _scanner = scanner;
            _LibraryCache = libraryCache;
            _player = player;
            IsLoading = false;

            _scanProgress = new Progress<ScanProgress>(progress =>
            {
                LoadingProgressValue = progress.TotalFiles > 0
                    ? (double)progress.FilesProcessed / progress.TotalFiles * 100d
                    : 0d;
            });

            _isInitializing = true;

            ListBy = string.IsNullOrWhiteSpace(settings.LibraryListBy) ? "Artist" : settings.LibraryListBy;
            Ascending = settings.LibraryAscending;

            _isInitializing = false;

            _ = LoadEntriesAsync();

        }

        public bool IsAlbumView => ListBy == "Album";
        public bool IsArtistView => ListBy == "Artist";
        public bool IsGenreView => ListBy == "Genre";
        public bool IsDecadeView => ListBy == "Decade";

        partial void OnListByChanged(string value)
        {
            OnPropertyChanged(nameof(IsAlbumView));
            OnPropertyChanged(nameof(IsArtistView));
            OnPropertyChanged(nameof(IsGenreView));
            OnPropertyChanged(nameof(IsDecadeView));

            if (_isInitializing) return;

            _ = LoadEntriesAsync();
        }

        partial void OnAscendingChanged(bool value)
        {
            if (_isInitializing) return;
            _ = LoadEntriesAsync();
        }

        partial void OnSelectedEntryChanged(LibraryEntry? value)
        {
            _imageCts?.Cancel(); // cancel previous image loading if any
            _imageCts = null;

            if (value == null)
            {
                _visibleAlbums.Clear();
                GridRows.Clear();
                ExpandedAlbumId = null;
                return;
            }

            RefreshVisibleAlbumsData();
            //LoadAlbumsForEntry(value);
        }

        [RelayCommand]
        private async Task RescanAllDirectories()
        {
            IsLoading = true;
            IsLoadingIndeterminate = false;
            LoadingProgressValue = 0;

            try
            {
                await _scanner.ScanAllDirectories(_scanProgress, null);
                _LibraryCache.InvalidateCache();
                await LoadEntriesAsync();
            }
            finally
            {
                IsLoading = false;
                IsLoadingIndeterminate = true;
                LoadingProgressValue = 0;
            }
        }

        [RelayCommand]
        private async Task AddFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Music Folder",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                var selectedPath = dialog.FolderName;
                if (selectedPath is not null)
                {
                    _scanner.AddDirectory(selectedPath, true);
                }

                IsLoading = true;
                IsLoadingIndeterminate = false;
                LoadingProgressValue = 0;

                try
                {
                    await _scanner.ScanAllDirectories(_scanProgress, null);
                    _LibraryCache.InvalidateCache();
                    await LoadEntriesAsync();
                }
                finally
                {
                    IsLoading = false;
                    IsLoadingIndeterminate = true;
                    LoadingProgressValue = 0;
                }
            }
        }

        [RelayCommand]
        private async Task AddFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Music Files",
                Multiselect = true,
            };
            if (dialog.ShowDialog() == true)
            {
                var selectedFiles = dialog.FileNames;
                if (selectedFiles is null || selectedFiles.Length == 0) return;

                IsLoading = true;
                IsLoadingIndeterminate = false;
                LoadingProgressValue = 0;

                try
                {
                    await _scanner.ScanFiles(selectedFiles, _scanProgress, null);
                    _LibraryCache.InvalidateCache();
                    await LoadEntriesAsync();
                }
                finally
                {
                    IsLoading = false;
                    IsLoadingIndeterminate = true;
                    LoadingProgressValue = 0;
                }
            }

        }

        [RelayCommand]
        private void Refresh()
        {
            _LibraryCache.InvalidateCache();
            _ = LoadEntriesAsync();
        }

        public void ApplySearchFilter(string? query)
        {
            _activeSearchQuery = query?.Trim() ?? string.Empty;
            RefreshVisibleAlbumsData();
            _ = LoadEntriesAsync();
        }

        public void SetLayoutColumns(int columns)
        {
            columns = Math.Max(1, columns);
            if (LayoutColumns != columns)
            {
                LayoutColumns = columns;
                ReflowRowsFromVisibleAlbums();
            }
        }

        private void RefreshVisibleAlbumsData()
        {
            if (SelectedEntry == null)
            {
                _visibleAlbums.Clear();
                GridRows.Clear();
                ExpandedAlbumId = null;
                return;
            }
            var albums = _LibraryCache.GetAlbumsForEntry(SelectedEntry)
                        .Select(s => new AlbumData
                        {
                            Id = s.Id,
                            Title = s.Title,
                            Year = s.Year,
                            ArtistNames = s.ArtistNames,
                            ImagePath = s.ImagePath,
                            BlurredImagePath = s.BluredImagePath,
                            CoverImage = null,
                            DominantColor = s.DominantColorHex,
                            ForegroundColor = s.ForegroundColorHex,
                        }).ToList();
            if (!string.IsNullOrWhiteSpace(_activeSearchQuery))
            {
                albums = FilterAlbums(albums, _activeSearchQuery);
            }
            _visibleAlbums = albums;
            ReflowRowsFromVisibleAlbums();
            _imageCts?.Cancel();
            _imageCts = new CancellationTokenSource();
            var pending = _visibleAlbums.Where(a => a.ImagePath is not null && a.CoverImage is null).ToList();
            if (pending.Count > 0)
            {
                _ = LoadCoverImagesAsync(pending, _imageCts.Token);
            }
        }
        public string ActiveSearchQuery => _activeSearchQuery;

        [RelayCommand]
        private void SetViewMode(string mode)
        {
            if (!string.IsNullOrEmpty(mode))
            {
                ListBy = mode;
            }
        }

        [RelayCommand]
        private void SetAscending()
        {
            Ascending = true;
        }

        [RelayCommand]
        private void SetDescending()
        {
            Ascending = false;
        }

        private async Task LoadEntriesAsync()
        {
            var requestId = Interlocked.Increment(ref _loadEntriesRequestId);
            var listBySnapshot = ListBy;
            var ascendingSnapshot = Ascending;

            IsLoading = true;
            IsLoadingIndeterminate = true;

            try
            {
                DateTime timeStart = DateTime.Now;
                var loadedEntries = await _LibraryCache.GetEntriesAsync(listBySnapshot, ascendingSnapshot);

                if (requestId != Volatile.Read(ref _loadEntriesRequestId)) return;

                var filteredEntries = FilterEntries(loadedEntries, _activeSearchQuery);
                ApplyGrouping(filteredEntries, ascendingSnapshot);

                DateTime timeEnd = DateTime.Now;
                Debug.WriteLine($"Loaded {loadedEntries.Count} entries in {(timeEnd - timeStart).TotalSeconds:F2} seconds");
            }
            finally
            {
                IsLoading = false;
                IsLoadingIndeterminate = true;
                LoadingProgressValue = 0;
            }
        }

        private void ApplyGrouping(IReadOnlyList<LibraryEntry> entries, bool ascendingSnapshot)
        {
            var selectedId = SelectedEntry?.Id;
            var selectedType = SelectedEntry?.Type;

            var normalizedEntries = entries.Select(e =>
            new LibraryEntry
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                GroupKey = NormalizeGroupKey(e.GroupKey),
                Type = e.Type,
                ImagePath = e.ImagePath,
            }).ToArray();

            Entries = normalizedEntries;

            var viewSource = new CollectionViewSource { Source = Entries };
            viewSource.SortDescriptions.Clear();
            viewSource.GroupDescriptions.Clear();

            viewSource.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(LibraryEntry.GroupKey))
                );

            viewSource.SortDescriptions.Add(
                new SortDescription(nameof(LibraryEntry.GroupKey), ascendingSnapshot ? ListSortDirection.Ascending : ListSortDirection.Descending)
                );

            viewSource.SortDescriptions.Add(
                new SortDescription(nameof(LibraryEntry.Title), ascendingSnapshot ? ListSortDirection.Ascending : ListSortDirection.Descending)
                );

            EntriesViewSource = viewSource;

            if (selectedId.HasValue && !string.IsNullOrWhiteSpace(selectedType))
            {
                SelectedEntry = Entries.FirstOrDefault(e => e.Id == selectedId.Value && e.Type == selectedType);
            }

            if (SelectedEntry == null && Entries.Count > 0)
            {
                SelectedEntry = Entries[0];
            }
        }

        private static string NormalizeGroupKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return "#";
            }

            var trimmed = key.Trim();
            return trimmed.Length == 0 ? "#" : trimmed.ToUpperInvariant();
        }

        private IReadOnlyList<LibraryEntry> FilterEntries(IReadOnlyList<LibraryEntry> entries, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return entries;

            var q = query.Trim();
            var albumById = _library.Albums.ToDictionary(a => a.Id);
            var artistById = _library.Artists.ToDictionary(a => a.Id, a => a.Name);
            var tracksByAlbumId = _library.Tracks
                .GroupBy(t => t.AlbumId)
                .ToDictionary(g => g.Key, g => g.ToArray());

            return entries
                .Where(entry => EntryMatchesQuery(entry, q, albumById, artistById, tracksByAlbumId))
                .ToArray();
        }

        private bool EntryMatchesQuery(
            LibraryEntry entry,
            string query,
            IReadOnlyDictionary<int, Album> albumById,
            IReadOnlyDictionary<int, string> artistById,
            IReadOnlyDictionary<int, Track[]> tracksByAlbumId)
        {
            if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var albumId in GetRelatedAlbumIds(entry))
            {
                if (!albumById.TryGetValue(albumId, out var album))
                    continue;

                if (album.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return true;

                var artistNames = album.ArtistIds
                    .Where(id => artistById.ContainsKey(id))
                    .Select(id => artistById[id]);

                if (artistNames.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (!tracksByAlbumId.TryGetValue(albumId, out var tracks))
                    continue;

                if (tracks.Any(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    return true;

                if (tracks
                    .SelectMany(t => t.ArtistIds)
                    .Distinct()
                    .Where(id => artistById.ContainsKey(id))
                    .Select(id => artistById[id])
                    .Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            return false;
        }

        private IEnumerable<int> GetRelatedAlbumIds(LibraryEntry entry)
        {
            return entry.Type switch
            {
                "Album" => [entry.Id],
                "Artist" => _library.Albums.Where(a => a.ArtistIds.Contains(entry.Id)).Select(a => a.Id),
                "Genre" => _library.Tracks
                    .Where(t => t.GenreIds.Contains(entry.Id))
                    .Select(t => t.AlbumId)
                    .Distinct(),
                "Decade" => GetAlbumIdsForDecade(entry.Title),
                _ => []
            };
        }

        private IEnumerable<int> GetAlbumIdsForDecade(string decadeTitle)
        {
            if (string.IsNullOrWhiteSpace(decadeTitle))
                return [];

            var clean = decadeTitle.Trim().TrimEnd('s', 'S');
            if (!int.TryParse(clean, out var decadeStart))
                return [];

            var decadeEnd = decadeStart + 10;
            return _library.Albums
                .Where(a => a.Year >= decadeStart && a.Year < decadeEnd)
                .Select(a => a.Id);
        }

        public void PlayAlbum(int albumId)
        {
            var allTracks = _LibraryCache.GetTrackQueueForAlbum(albumId);

            _player.SetQueue(allTracks);
            _player.PlayIndex(0);
        }

        private void LoadAlbumsForEntry(LibraryEntry entry)
        {
            // Wait for the view to provide the real viewport width before building rows.
            GridRows.Clear();
            ExpandedAlbumId = null;
        }
        private static async Task LoadCoverImagesAsync(List<AlbumData> albums, CancellationToken ct)
        {
            using var sem = new SemaphoreSlim(3); // limit concurrent image loading

            var tasks = albums
                .Where(a => a.ImagePath is not null)
                .Select(async (album) =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (ct.IsCancellationRequested) return;
                        var image = await Task.Run(() =>
                        {
                            try
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.DecodePixelWidth = 150;
                                bmp.UriSource = new Uri(album.ImagePath!, UriKind.Absolute);
                                bmp.EndInit();
                                bmp.Freeze();
                                return (BitmapSource)bmp;
                            }
                            catch { return null; }
                        }, ct).ConfigureAwait(false);

                        if (image is not null && !ct.IsCancellationRequested)
                        {
                            album.CoverImage = image;
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        sem.Release();
                    }
                });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
        }
        public void ExpandAlbum(int albumId, int currentAvailableWidth = 1000)
        {
            var row = GridRows.FirstOrDefault(r => r.Albums.Any(a => a.Id == albumId));
            if (row == null) return;

            if (row.ExpandedAlbumId == albumId)
            {
                row.ExpandedAlbumId = null;
                row.ExpandedImagePath = null;
                ExpandedAlbumId = null;
                return;
            }

            foreach (var r in GridRows)
            {
                r.ExpandedAlbumId = null;
                r.ExpandedImagePath = null;
            }

            var album = row.Albums.First(a => a.Id == albumId);
            row.ExpandedAlbumId = albumId;
            row.ExpandedImagePath = album.BlurredImagePath;
            row.ExpandedDominantColor = album.DominantColor;
            row.ExpandedForegroundColor = album.ForegroundColor;

            ExpandedAlbumId = albumId;
        }

        public void CollapseAlbum()
        {
            foreach (var row in GridRows)
            {
                row.ExpandedAlbumId = null;
                row.ExpandedImagePath = null;
            }
            ExpandedAlbumId = null;
        }

        //public void RebuildRows(int containerWidth)
        //{
        //    const int minTileWidth = 150;
        //    const int gutter = 16;
        //    const int minColumns = 1;

        //    if (SelectedEntry == null)
        //    {
        //        GridRows.Clear();
        //        return;
        //    }

        //    int tileFootprint = minTileWidth + gutter;
        //    int columns = Math.Max(minColumns, Math.Max(1, containerWidth) / tileFootprint);
        //    int? expandedAlbumIdSnapshot = ExpandedAlbumId;

        //    var albums = _LibraryCache.GetAlbumsForEntry(SelectedEntry)
        //                .Select(s => new AlbumData
        //                {
        //                    Id = s.Id,
        //                    Title = s.Title,
        //                    Year = s.Year,
        //                    ArtistNames = s.ArtistNames,
        //                    ImagePath = s.ImagePath,
        //                    BlurredImagePath = s.BluredImagePath,
        //                    CoverImage = null,
        //                    DominantColor = s.DominantColorHex,
        //                    ForegroundColor = s.ForegroundColorHex,
        //                }).ToList();

        //    if (!string.IsNullOrWhiteSpace(_activeSearchQuery))
        //    {
        //        albums = FilterAlbums(albums, _activeSearchQuery);
        //    }

        //    var rows = new ObservableCollection<AlbumGridRowModel>();
        //    for (int i = 0; i < albums.Count; i += columns)
        //    {
        //        var rowAlbums = albums.Skip(i).Take(columns).ToList();
        //        rows.Add(new AlbumGridRowModel
        //        {
        //            Albums = rowAlbums,
        //            ColumnCount = columns
        //        });
        //    }
        //    GridRows = rows;
        //    ExpandedAlbumId = null;

        //    if (expandedAlbumIdSnapshot.HasValue)
        //    {
        //        var expandedRow = GridRows.FirstOrDefault(r => r.Albums.Any(a => a.Id == expandedAlbumIdSnapshot.Value));
        //        if (expandedRow != null)
        //        {
        //            var expandedAlbum = expandedRow.Albums.First(a => a.Id == expandedAlbumIdSnapshot.Value);
        //            expandedRow.ExpandedAlbumId = expandedAlbum.Id;
        //            expandedRow.ExpandedImagePath = expandedAlbum.BlurredImagePath;
        //            expandedRow.ExpandedDominantColor = expandedAlbum.DominantColor;
        //            expandedRow.ExpandedForegroundColor = expandedAlbum.ForegroundColor;
        //            ExpandedAlbumId = expandedAlbum.Id;
        //        }
        //    }

        //    _imageCts = new CancellationTokenSource();
        //    _ = LoadCoverImagesAsync(albums, _imageCts.Token);
        //}

        private void ReflowRowsFromVisibleAlbums()
        {
            var columns = Math.Max(1, LayoutColumns);
            int? expandedAlbumIdSnapshot = ExpandedAlbumId;

            var rows = new ObservableCollection<AlbumGridRowModel>();
            for (int i = 0; i < _visibleAlbums.Count; i += columns)
            {
                rows.Add(new AlbumGridRowModel
                {
                    Albums = _visibleAlbums.Skip(i).Take(columns).ToList(),
                });
            }

            GridRows = rows;
            ExpandedAlbumId = null;

            if (expandedAlbumIdSnapshot.HasValue) {
                var expandedRow = GridRows.FirstOrDefault(r => r.Albums.Any(a => a.Id == expandedAlbumIdSnapshot.Value));
                if (expandedRow is not null)
                {
                    var expandedAlbum = expandedRow.Albums.First(a => a.Id == expandedAlbumIdSnapshot.Value);
                    expandedRow.ExpandedAlbumId = expandedAlbum.Id;
                    expandedRow.ExpandedImagePath = expandedAlbum.BlurredImagePath;
                    expandedRow.ExpandedDominantColor = expandedAlbum.DominantColor;
                    expandedRow.ExpandedForegroundColor = expandedAlbum.ForegroundColor;
                    ExpandedAlbumId = expandedAlbum.Id;
                }
            }
        }

        private List<AlbumData> FilterAlbums(List<AlbumData> albums, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return albums;

            var q = query.Trim();
            var artistById = _library.Artists.ToDictionary(a => a.Id, a => a.Name);

            bool MatchesTrackOrTrackArtist(int albumId)
            {
                return _library.Tracks
                    .Where(t => t.AlbumId == albumId)
                    .Any(t =>
                        t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        t.ArtistIds
                            .Where(id => artistById.ContainsKey(id))
                            .Select(id => artistById[id])
                            .Any(name => name.Contains(q, StringComparison.OrdinalIgnoreCase))
                    );
            }

            return albums
                .Where(a =>
                    a.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.ArtistNames.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    MatchesTrackOrTrackArtist(a.Id))
                .ToList();
        }

        public MusicLibrary GetLibrary() => _library;

        public class AlbumGridRowModel : INotifyPropertyChanged
        {
            public List<AlbumData> Albums { get; set; } = [];

            private int? _expandedAlbumId;
            public int? ExpandedAlbumId
            {
                get => _expandedAlbumId;
                set
                {
                    if (_expandedAlbumId != value)
                    {
                        _expandedAlbumId = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedAlbumId)));
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedAlbum)));
                    }
                }
            }

            private string? _expandedImagePath;
            public string? ExpandedImagePath
            {
                get => _expandedImagePath;
                set
                {
                    if (_expandedImagePath != value)
                    {
                        _expandedImagePath = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedImagePath)));
                    }
                }
            }

            private string _expandedDominantColor = "#808080";
            public string ExpandedDominantColor
            {
                get => _expandedDominantColor;
                set
                {
                    if (_expandedDominantColor != value)
                    {
                        _expandedDominantColor = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedDominantColor)));
                    }
                }
            }

            private string _expandedForegroundColor = "#FFFFFF";
            public string ExpandedForegroundColor
            {
                get => _expandedForegroundColor;
                set
                {
                    if (_expandedForegroundColor != value)
                    {
                        _expandedForegroundColor = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExpandedForegroundColor)));
                    }
                }
            }

            public AlbumData? ExpandedAlbum => Albums.FirstOrDefault(a => a.Id == ExpandedAlbumId);

            //public int ColumnCount { get; set; } = 1;

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class AlbumData : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public int Year { get; set; }
            public string ArtistNames { get; set; } = string.Empty;
            public string? ImagePath { get; set; }
            public string? BlurredImagePath { get; set; }
            public string DominantColor { get; set; } = "#808080";
            public string ForegroundColor { get; set; } = "#FFFFFF";

            private BitmapSource? _coverImage;
            public BitmapSource? CoverImage
            {
                get => _coverImage;
                set
                {
                    if (_coverImage != value)
                    {
                        _coverImage = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CoverImage)));
                    }
                }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class TrackListPlaceholder
        {
            public int AlbumId { get; set; }
            public string? ImagePath { get; set; }
            public string DominantColor { get; set; } = "#1a1a1a";
            public string ForegroundColor { get; set; } = "#ffffff";
        }
    }
}
