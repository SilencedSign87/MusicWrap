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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
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

        [ObservableProperty] private List<object> albumsForSelectedEntry = [];

        [ObservableProperty] private int? expandedAlbumId = null;

        [ObservableProperty] private bool isLoading;

        [ObservableProperty] private bool isLoadingIndeterminate = true;

        [ObservableProperty] private double loadingProgressValue = 0;

        private CancellationTokenSource? _imageCts;

        private bool _isInitializing;
        private int _loadEntriesRequestId;
        private readonly IProgress<ScanProgress> _scanProgress;

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
                // clear previous images
                foreach (var item in AlbumsForSelectedEntry.OfType<AlbumData>())
                {
                    item.CoverImage = null;
                }

                AlbumsForSelectedEntry = [];
                return;
            }

            LoadAlbumsForEntry(value);
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

                ApplyGrouping(loadedEntries, ascendingSnapshot);

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

        private void LoadAlbumsForEntry(LibraryEntry entry)
        {
            var summaries = _LibraryCache.GetAlbumsForEntry(entry);

            var albums = summaries.Select(s => new AlbumData
            {
                Id = s.Id,
                Title = s.Title,
                Year = s.Year,
                ArtistNames = s.ArtistNames,
                ImagePath = s.ImagePath,
                BlurredImagePath = s.BluredImagePath,
                CoverImage = null, // will be loaded asynchronously
                DominantColor = s.DominantColorHex,
                ForegroundColor = s.ForegroundColorHex,
            }).ToList();

            AlbumsForSelectedEntry = [.. albums.Cast<object>()];

            ExpandedAlbumId = null;
            _imageCts = new CancellationTokenSource();

            _ = LoadCoverImagesAsync(albums, _imageCts.Token);
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
                                bmp.DecodePixelWidth = 200;
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
            if (ExpandedAlbumId == albumId)
            {
                CollapseAlbum();
                return;
            }

            CollapseAlbum();

            // Find the album index and calculate row end
            var albums = AlbumsForSelectedEntry.OfType<AlbumData>().ToList();
            var albumIndex = albums.FindIndex(a => a.Id == albumId);

            if (albumIndex == -1) return;

            // Get the album's colors
            var selectedAlbum = albums[albumIndex];


            const int albumWidth = 200;
            int albumsPerRow = Math.Max(1, (int)(currentAvailableWidth / albumWidth));


            int rowNumber = albumIndex / albumsPerRow;
            int insertIndex = Math.Min((rowNumber + 1) * albumsPerRow, albums.Count);

            var newList = new List<object>(albums.Take(insertIndex));
            newList.Add(new TrackListPlaceholder
            {
                AlbumId = albumId,
                DominantColor = selectedAlbum.DominantColor,
                ImagePath = selectedAlbum.BlurredImagePath,
                ForegroundColor = selectedAlbum.ForegroundColor
            });
            newList.AddRange(albums.Skip(insertIndex));

            AlbumsForSelectedEntry = newList;
            ExpandedAlbumId = albumId;
        }

        public void CollapseAlbum()
        {
            if (ExpandedAlbumId == null) return;

            // Remove placeholder
            AlbumsForSelectedEntry = [.. AlbumsForSelectedEntry.Where(item => item is not TrackListPlaceholder)];

            ExpandedAlbumId = null;
        }

        public MusicLibrary GetLibrary() => _library;

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
