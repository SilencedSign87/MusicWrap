using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.State.Services;
using MusicWrap.UI.Services;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        [ObservableProperty] private string listBy = "AlbumArtist"; // Album, Artist, Genre, Decade

        [ObservableProperty] private bool ascending = false;

        [ObservableProperty] private IReadOnlyList<LibraryEntry> entries = [];

        [ObservableProperty] private CollectionViewSource entriesViewSource = new();

        [ObservableProperty] private LibraryEntry? selectedEntry;

        private string _activeSearchQuery = string.Empty;
        private bool _isInitializing;
        private int _loadEntriesRequestId;
        private readonly IProgress<ScanProgress> _scanProgress;

        // Services
        private readonly ILibraryScanner _scanner;
        private readonly ILibraryService _libraryService;
        private readonly IMusicPlayerService _player;
        private readonly IStatusService _statusService;
        private readonly IImageService _imageService;
        private readonly ILogger<LibraryViewModel> _logger;

        public LibraryViewModel(
            ILibraryScanner scanner,
            ILibraryService libraryService,
            UserSettings settings,
            IMusicPlayerService player,
            IImageService imageService,
            IStatusService statusService,
            ILogger<LibraryViewModel> logger)
        {
            _scanner = scanner;
            _imageService = imageService;
            _libraryService = libraryService;
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
        public bool IsAlbumArtistView => ListBy == "AlbumArtist";
        public bool IsTrackArtistView => ListBy == "TrackArtist";
        public bool IsGenreView => ListBy == "Genre";
        public bool IsDecadeView => ListBy == "Decade";

        partial void OnListByChanged(string value)
        {
            OnPropertyChanged(nameof(IsAlbumView));
            OnPropertyChanged(nameof(IsAlbumArtistView));
            OnPropertyChanged(nameof(IsTrackArtistView));
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

        [RelayCommand]
        private async Task RescanAllDirectories()
        {
            await RunWithStatusAsync("Rescanning Library...", async () =>
            {
                await _scanner.ScanAllDirectories(_scanProgress, null);
                _libraryService.InvalidateCache();
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
                _libraryService.InvalidateCache();
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
                _libraryService.InvalidateCache();
                await LoadEntriesAsync();
            }, "Files added");

        }

        [RelayCommand]
        private async Task Refresh()
        {
            await RunWithStatusAsync("Reloading library", async () =>
            {
                _libraryService.InvalidateCache();
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
            _ = LoadEntriesAsync();
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
                var loadedEntries = await _libraryService.GetEntriesAsync(listBySnapshot, ascendingSnapshot);

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

            return _libraryService.GetTrackIdsForEntry(entry, query).Length > 0;
        }

        public void PlayAlbum(int albumId)
        {
            var allTracks = _libraryService.GetTrackQueueForAlbum(albumId);

            _player.SetQueue(allTracks);
            _player.PlayIndex(0);
        }

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



