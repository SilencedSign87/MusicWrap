using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Library;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.Features.Activity.Services;
using System.IO;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAlbumView))]
        [NotifyPropertyChangedFor(nameof(IsTrackArtistView))]
        [NotifyPropertyChangedFor(nameof(IsAlbumArtistView))]
        [NotifyPropertyChangedFor(nameof(IsGenreView))]
        [NotifyPropertyChangedFor(nameof(IsDecadeView))]
        private string listBy = "Artist"; // Album, Artist, Genre, Decade

        [ObservableProperty] private bool ascending = true;

        [ObservableProperty] private IReadOnlyList<LibraryEntry> entries = [];

        [ObservableProperty] private CollectionViewSource entriesViewSource = new();

        [ObservableProperty] private LibraryEntry? selectedEntry;

        private bool _isInitializing;
        private bool _isDisposing;
        private int _loadEntriesRequestId;
        private readonly IProgress<ScanProgress> _scanProgress;

        // Services
        private readonly ILibraryScanner _scanner;
        private readonly ILibraryService _LibraryCache;
        private readonly IMusicPlayerService _player;
        private readonly IImageService _imageService;
        private readonly ILogger<LibraryViewModel> _logger;
        private readonly SearchService _searchService;
        private readonly ActivityService _activityService;

        public LibraryViewModel(
            ILibraryScanner scanner,
            ILibraryService libraryCache,
            UserSettings settings,
            IMusicPlayerService player,
            IImageService imageService,
            SearchService searchService,
            ActivityService activityService,
            ILogger<LibraryViewModel> logger)
        {
            _scanner = scanner;
            _imageService = imageService;
            _LibraryCache = libraryCache;
            _activityService = activityService;
            _player = player;
            _logger = logger;
            _searchService = searchService;

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
            });

            _isInitializing = true;

            ListBy = string.IsNullOrWhiteSpace(settings.LibraryListBy) ? "Artist" : settings.LibraryListBy;
            Ascending = settings.LibraryAscending;

            _isInitializing = false;

            _searchService.SearchSubmitted += _searchService_SearchSubmitted;

            _ = LoadEntriesAsync();

        }

        private void _searchService_SearchSubmitted(object? sender, string e)
        {
            _ = LoadEntriesAsync();
        }

        public bool IsAlbumView => ListBy == "Album";
        public bool IsTrackArtistView => ListBy == "Track_Artist";
        public bool IsAlbumArtistView => ListBy == "Album_Artist";
        public bool IsGenreView => ListBy == "Genre";
        public bool IsDecadeView => ListBy == "Decade";

        partial void OnListByChanged(string value)
        {
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
            using var scope = _activityService.Start(
                "Rescanning library",
                "Preparing scan...",
                cancellable: true
                );

            var activity = scope.Activity;

            try
            {
                var progress = new Progress<ScanProgress>(p =>
                {
                    var phase = p.State switch
                    {
                        ScanState.Fingerprinting => "Fingerprinting",
                        ScanState.Scanning => "Scanning",
                        ScanState.Saving => "Saving",
                        _ => "Processing"
                    };

                    var total = Math.Max(1, p.TotalFiles);
                    var detail = string.IsNullOrWhiteSpace(p.CurrentFile)
                        ? phase
                        : $"{phase} — {p.CurrentFile}";
                    activity.ReportProgress((double)p.FilesProcessed / total, detail);
                });

                await _scanner.ScanAllDirectories(progress, scope.CancellationToken);

                _LibraryCache.InvalidateCache();
                _imageService.ClearCache();
                await LoadEntriesAsync();

                activity.Complete();
            }
            catch (OperationCanceledException)
            {
                activity.MarkCancelled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rescanning library");
                activity.Fail(ex.Message);
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
            if (dialog.ShowDialog() != true) return;

            var selectedPath = dialog.FolderName;
            if (selectedPath is null) return;

            _scanner.AddDirectory(selectedPath, true);

            using var scope = _activityService.Start("Adding folder", Path.GetFileName(selectedPath), cancellable: true);
            var activity = scope.Activity;
            try
            {
                var progress = new Progress<ScanProgress>(p =>
                {
                    var total = Math.Max(1, p.TotalFiles);
                    var detail = string.IsNullOrWhiteSpace(p.CurrentFile)
                        ? p.State.ToString()
                        : $"{p.State} — {Path.GetFileName(p.CurrentFile)}";
                    activity.ReportProgress((double)p.FilesProcessed / total, detail);
                });
                await _scanner.ScanDirectory(selectedPath, progress, scope.CancellationToken);
                _LibraryCache.InvalidateCache();
                await LoadEntriesAsync();
                activity.Complete();
            }
            catch (OperationCanceledException)
            {
                activity.MarkCancelled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding folder {Path}", selectedPath);
                activity.Fail(ex.Message);
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
            if (dialog.ShowDialog() != true) return;

            var selectedFiles = dialog.FileNames;
            if (selectedFiles is null || selectedFiles.Length == 0) return;

            using var scope = _activityService.Start(
                "Adding files",
                $"{selectedFiles.Length} file(s) selected",
                cancellable: true);

            var activity = scope.Activity;
            try
            {
                var progress = new Progress<ScanProgress>(p =>
                {
                    var total = Math.Max(1, p.TotalFiles);
                    var detail = string.IsNullOrWhiteSpace(p.CurrentFile)
                        ? p.State.ToString()
                        : $"{p.State} — {Path.GetFileName(p.CurrentFile)}";
                    activity.ReportProgress((double)p.FilesProcessed / total, detail);
                });
                await _scanner.ScanFiles(selectedFiles, progress, scope.CancellationToken);
                _LibraryCache.InvalidateCache();
                await LoadEntriesAsync();
                activity.Complete();
            }
            catch (OperationCanceledException)
            {
                activity.MarkCancelled();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding files");
                activity.Fail(ex.Message);
            }

        }

        [RelayCommand]
        private async Task Refresh()
        {
            _LibraryCache.InvalidateCache();
            await LoadEntriesAsync();
        }

        public void NotifySearchSubmitted()
        {
            _ = LoadEntriesAsync();
        }

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
                var loadedEntries = await _LibraryCache.GetEntriesAsync(listBySnapshot, ascendingSnapshot, true);

                if (requestId != Volatile.Read(ref _loadEntriesRequestId)) return;

                ApplyGrouping(loadedEntries, ascendingSnapshot);

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

            if (EntriesViewSource?.View is not null)
            {
                EntriesViewSource.View.Filter = null;
            }

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

        public void PlayAlbum(int albumId)
        {
            var allTracks = _LibraryCache.GetTrackQueueForAlbum(albumId);

            _player.SetQueue(allTracks);
            _player.PlayIndex(0);
        }

        public void Dispose()
        {
            if (_isDisposing) return;
            _isDisposing = true;

            _searchService.SearchSubmitted -= _searchService_SearchSubmitted;

            EntriesViewSource = new();
            Entries = [];
            SelectedEntry = null;
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



