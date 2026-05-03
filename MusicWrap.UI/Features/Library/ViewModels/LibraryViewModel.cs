using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
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
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Features.State.Services;
using Microsoft.Extensions.Logging;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        [ObservableProperty] private string listBy = "Artist"; // Album, Artist, Genre, Decade

        [ObservableProperty] private bool ascending = true;

        [ObservableProperty] private IReadOnlyList<LibraryEntry> entries = [];

        [ObservableProperty] private CollectionViewSource entriesViewSource = new();

        [ObservableProperty] private LibraryEntry? selectedEntry;

        [ObservableProperty] private ObservableCollection<AlbumGridRowModel> gridRows = [];

        [ObservableProperty] private int? expandedAlbumId = null;

        [ObservableProperty] private int layoutColumns = 1;
        [ObservableProperty] private TrackSortMode detailSortMode = TrackSortMode.Year;
        [ObservableProperty] private bool detailSortAscending = true;

        private List<AlbumData> _visibleAlbums = [];
        private Dictionary<int, int> _albumDurationById = [];

        private CancellationTokenSource? _imageCts;
        private string _activeSearchQuery = string.Empty;

        private bool _isInitializing;
        private int _loadEntriesRequestId;
        private readonly IProgress<ScanProgress> _scanProgress;

        // Services
        private readonly MusicLibrary _library;
        private readonly ILibraryScanner _scanner;
        private readonly ILibraryCacheService _LibraryCache;
        private readonly IMusicPlayerService _player;
        private readonly IStatusService _statusService;
        private readonly IImageService _imageService;
        private readonly ILogger<LibraryViewModel> _logger;

        public LibraryViewModel(MusicLibrary library,
            ILibraryScanner scanner,
            ILibraryCacheService libraryCache,
            UserSettings settings,
            IMusicPlayerService player,
            IImageService imageService,
            IStatusService statusService,
            ILogger<LibraryViewModel> logger)
        {
            _library = library;
            _scanner = scanner;
            _imageService = imageService;
            _LibraryCache = libraryCache;
            _player = player;
            _statusService = statusService;
            _logger = logger;

            _scanProgress = new Progress<ScanProgress>(progress =>
            {
                var maximun = Math.Max(1, progress.TotalFiles);
                var phase = progress.State switch
                {
                    ScanState.Fingerprinting => "Fingerprinting",
                    ScanState.Scanning => "Scanning",
                    ScanState.Saving => "Saving",
                    _ => "Processing"
                };

                var detail = string.IsNullOrWhiteSpace(progress.CurrentFile)
                ? phase
                : $"{phase} ({progress.FilesProcessed}/{progress.TotalFiles})";

                _statusService.ReportProgress(
                    progress.FilesProcessed,
                    maximun,
                    false,
                    detail,
                    StatusbarSlotKind.Left
                    );
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
            _albumDurationById = [];

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

        partial void OnDetailSortModeChanged(TrackSortMode value)
        {
            if (SelectedEntry is null)
            {
                return;
            }

            RefreshVisibleAlbumsData();
        }

        partial void OnDetailSortAscendingChanged(bool value)
        {
            if (SelectedEntry is null)
            {
                return;
            }

            RefreshVisibleAlbumsData();
        }

        [RelayCommand]
        private async Task RescanAllDirectories()
        {
            await RunWithStatusAsync("Rescanning Library...", async () =>
            {
                await _scanner.ScanAllDirectories(_scanProgress, null);
                _LibraryCache.InvalidateCache();
                _imageService.ClearCache();
                await LoadEntriesAsync();
            }, "Library Rescan Complete");
        }

        [RelayCommand]
        private async Task AddFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Music Folder",
                Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;

            var selectedPath = dialog.FolderName;
            if (selectedPath is null) return;

            _scanner.AddDirectory(selectedPath, true);

            await RunWithStatusAsync("Scanning added folder...", async () =>
            {
                await _scanner.ScanDirectory(selectedPath, _scanProgress, null);
                _LibraryCache.InvalidateCache();
                await LoadEntriesAsync();
            }, "Folder folder added");
        }

        [RelayCommand]
        private async Task AddFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Music Files",
                Multiselect = true,
            };
            if (dialog.ShowDialog() != true) return;

            var selectedFiles = dialog.FileNames;
            if (selectedFiles is null || selectedFiles.Length == 0) return;

            await RunWithStatusAsync("Scanning files...", async () =>
            {
                await _scanner.ScanFiles(selectedFiles, _scanProgress, null);
                _LibraryCache.InvalidateCache();
                await LoadEntriesAsync();
            }, "Files added");

        }

        [RelayCommand]
        private async Task Refresh()
        {
            await RunWithStatusAsync("Reloading library", async () =>
            {
                _LibraryCache.InvalidateCache();
                await LoadEntriesAsync();
            });
        }

        private async Task RunWithStatusAsync(string title, Func<Task> work, string? successMessage = null)
        {
            _statusService.PublishState(StatusbarSlotKind.Left, title);

            try
            {
                await work();

                if (!string.IsNullOrWhiteSpace(successMessage))
                {
                    _statusService.PublishState(StatusbarSlotKind.Left, successMessage);
                    await Task.Delay(2000);

                    _statusService.ClearSlot(StatusbarSlotKind.Left);
                }
            }
            finally
            {
                _statusService.ClearSlot(StatusbarSlotKind.Left);
                _statusService.ClearProgress();
            }
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

        public void SetDetailSortOptions(TrackSortMode sortMode, bool ascending)
        {
            DetailSortMode = sortMode;
            DetailSortAscending = ascending;
        }

        public void ApplyGlobalTrackOrder(IReadOnlyList<int> orderedTrackIds)
        {
            if (SelectedEntry is null || orderedTrackIds.Count == 0 || _visibleAlbums.Count == 0)
            {
                return;
            }

            var albumOrder = new List<int>(_visibleAlbums.Count);
            var seenAlbumIds = new HashSet<int>();

            foreach (var trackId in orderedTrackIds)
            {
                var albumId = _LibraryCache.GetTrackById(trackId)?.AlbumId;
                if (!albumId.HasValue)
                {
                    continue;
                }

                if (seenAlbumIds.Add(albumId.Value))
                {
                    albumOrder.Add(albumId.Value);
                }
            }

            if (albumOrder.Count == 0)
            {
                return;
            }

            var rankByAlbumId = albumOrder
                .Select((albumId, index) => (albumId, index))
                .ToDictionary(x => x.albumId, x => x.index);

            _visibleAlbums = _visibleAlbums
                .OrderBy(a => rankByAlbumId.TryGetValue(a.Id, out var rank) ? rank : int.MaxValue)
                .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ReflowRowsFromVisibleAlbums();
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

            albums = SortAlbumsByCurrentMode(albums);

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

            try
            {
                DateTime timeStart = DateTime.Now;
                var loadedEntries = await _LibraryCache.GetEntriesAsync(listBySnapshot, ascendingSnapshot);

                if (requestId != Volatile.Read(ref _loadEntriesRequestId)) return;

                var filteredEntries = FilterEntries(loadedEntries, _activeSearchQuery);
                ApplyGrouping(filteredEntries, ascendingSnapshot);

                DateTime timeEnd = DateTime.Now;
                _logger.LogInformation("Loaded {Count} entries in {Seconds:F2} seconds for ListBy={ListBy}, Ascending={Ascending}", loadedEntries.Count, (timeEnd - timeStart).TotalSeconds, listBySnapshot, ascendingSnapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading library entries for ListBy={ListBy}, Ascending={Ascending}", listBySnapshot, ascendingSnapshot);
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

            return entries
                .Where(e => EntryMatchesQuery(e, q))
                .ToArray();
        }

        private bool EntryMatchesQuery(LibraryEntry entry, string query)
        {
            if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return _LibraryCache.GetTrackIdsForEntry(entry, query).Length > 0;
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
        private async Task LoadCoverImagesAsync(List<AlbumData> albums, CancellationToken ct)
        {
            const int batchSize = 24;

            foreach (var batch in albums.Chunk(batchSize))
            {
                ct.ThrowIfCancellationRequested();

                using var sem = new SemaphoreSlim(3);

                var tasks = batch.Select(async album =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        var bmp = await _imageService.LoadAsync(
                            album.ImagePath,
                            ImageVariant.Medium,
                            180,
                            ct).ConfigureAwait(false);

                        if (bmp is not null && !ct.IsCancellationRequested)
                        {
                            album.CoverImage = bmp;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    finally
                    {
                        sem.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
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

            if (expandedAlbumIdSnapshot.HasValue)
            {
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

            return albums
                .Where(a =>
                    a.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.ArtistNames.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    _LibraryCache.GetTracksForAlbum(a.Id, q).Length > 0)
                .ToList();
        }

        private List<AlbumData> SortAlbumsByCurrentMode(List<AlbumData> albums)
        {
            IEnumerable<AlbumData> ordered;

            if (DetailSortMode == TrackSortMode.Title)
            {
                ordered = DetailSortAscending
                    ? albums
                        .OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.Year)
                    : albums
                        .OrderByDescending(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(a => a.Year);
            }
            else if (DetailSortMode == TrackSortMode.ArtistName)
            {
                ordered = DetailSortAscending
                    ? albums
                        .OrderBy(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.Year)
                    : albums
                        .OrderByDescending(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(a => a.Year);
            }
            else if (DetailSortMode == TrackSortMode.Duration)
            {
                ordered = DetailSortAscending
                    ? albums
                        .OrderBy(a => GetAlbumDurationSeconds(a.Id))
                        .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase)
                    : albums
                        .OrderByDescending(a => GetAlbumDurationSeconds(a.Id))
                        .ThenByDescending(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                ordered = DetailSortAscending
                    ? albums
                        .OrderBy(a => a.Year == 0 ? int.MaxValue : a.Year)
                        .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase)
                    : albums
                        .OrderByDescending(a => a.Year == 0 ? int.MinValue : a.Year)
                        .ThenByDescending(a => a.Title, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(a => a.ArtistNames, StringComparer.OrdinalIgnoreCase);
            }

            return ordered.ToList();
        }

        private int GetAlbumDurationSeconds(int albumId)
        {
            if (_albumDurationById.TryGetValue(albumId, out var duration))
            {
                return duration;
            }

            var total = _LibraryCache.GetTracksForAlbum(albumId)
                .Select(trackId => _LibraryCache.GetTrackById(trackId)?.Duration ?? 0)
                .Sum();

            _albumDurationById[albumId] = total;
            return total;
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



