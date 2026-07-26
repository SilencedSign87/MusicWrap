using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Activity;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Search;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Library.Services;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryViewModel : ObservableObject, IDisposable
    {

        [ObservableProperty] private IReadOnlyList<LibraryEntry> entries = [];

        [ObservableProperty] private CollectionViewSource entriesViewSource = new();

        private bool _isDisposing;
        private int _loadEntriesRequestId;
        private readonly IProgress<ScanProgress> _scanProgress;

        // Services
        private readonly ILibraryScanner _scanner;
        private readonly ILibraryService _LibraryCache;
        private readonly IMusicPlayerService _player;
        private readonly ILogger _logger;
        private readonly SearchService _searchService;
        private readonly ActivityService _activityService;
        private readonly UserSettings _userSettings;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly IMessenger _messenger;
        private readonly IUIDispatcher _uiDispatcher;
        private readonly LibraryWorkspace _workspace;

        public LibraryWorkspace Workspace => _workspace;

        public LibraryViewModel(
            ILibraryScanner scanner,
            ILibraryService libraryCache,
            UserSettings settings,
            IMusicPlayerService player,
            IMessenger messenger,
            SearchService searchService,
            ActivityService activityService,
            ISaveCoordinator saveCoordinator,
            IUIDispatcher uiDispatcher,
            LibraryWorkspace workspace,
            ILogger<LibraryViewModel> logger)
        {
            _scanner = scanner;
            _messenger = messenger;
            _LibraryCache = libraryCache;
            _activityService = activityService;
            _player = player;
            _logger = logger;
            _searchService = searchService;
            _userSettings = settings;
            _saveCoordinator = saveCoordinator;
            _workspace = workspace;
            _uiDispatcher = uiDispatcher;

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

            //_workspace.PropertyChanged += OnWorkspacePropertyChanged;

            _searchService.SearchSubmitted += _searchService_SearchSubmitted;

            _ = LoadEntriesAsync();

            _messenger.Register<EntriesReadyMessage>(this, (r, m) =>
            {
                _uiDispatcher.Invoke(() => _ = LoadEntriesAsync());
            });

        }
        private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryWorkspace.ListBy) ||
                e.PropertyName == nameof(LibraryWorkspace.EntryListAscending))
            {
                _ = LoadEntriesAsync();
            }
        }
        private void _searchService_SearchSubmitted(object? sender, string e)
        {
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
            await LoadEntriesAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSetSelection))]
        private void SetSelection(LibraryEntry entry)
        {
            _workspace.SelectedEntry = entry;
        }

        private bool CanSetSelection(LibraryEntry entry)
        {
            return entry != null && !ReferenceEquals(_workspace.SelectedEntry, entry);
        }

        private async Task LoadEntriesAsync()
        {

            var requestId = Interlocked.Increment(ref _loadEntriesRequestId);
            var listBySnapshot = _workspace.ListBy;
            var ascendingSnapshot = _workspace.EntryListAscending;

            try
            {
                var loadedEntries = await _LibraryCache.GetEntriesAsync(listBySnapshot, ascendingSnapshot, true);
                
                if (requestId != Volatile.Read(ref _loadEntriesRequestId)) return;

                
                ApplyGrouping(loadedEntries, ascendingSnapshot);
                
                _logger.LogInformation(
                    "Loaded {Count} entries for ListBy={ListBy}, Ascending={Ascending}",
                    loadedEntries.Count, listBySnapshot, ascendingSnapshot);

                if (!_workspace.IsInitialized)
                {
                    _workspace.Initialize(Entries);
                    _workspace.PropertyChanged += OnWorkspacePropertyChanged;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading library entries for ListBy={ListBy}, Ascending={Ascending}", listBySnapshot, ascendingSnapshot);
            }
        }

        private void ApplyGrouping(IReadOnlyList<LibraryEntry> entries, bool ascendingSnapshot)
        {
            var currentId = _workspace.SelectedEntry?.Id;
            var currentType = _workspace.SelectedEntry?.Type;

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

            if (_workspace.IsInitialized)
            {
                LibraryEntry? newSelection = null;
                if (currentId.HasValue && currentType.HasValue)
                    newSelection = Entries.FirstOrDefault(e => e.Id == currentId.Value && e.Type == currentType);

                newSelection ??= Entries.FirstOrDefault();

                if (!ReferenceEquals(_workspace.SelectedEntry, newSelection))
                    _workspace.SelectedEntry = newSelection;
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
            _messenger.UnregisterAll(this);

            EntriesViewSource = new();
            Entries = [];
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



