using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using MusicWrap.Data.Services;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class LibraryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string listBy = "Artist"; // Album, Artist, Genre, Decade

        [ObservableProperty]
        private bool ascending = true;

        [ObservableProperty]
        private List<LibraryEntry> entries = [];

        [ObservableProperty]
        private CollectionViewSource entriesViewSource = new();

        [ObservableProperty]
        private LibraryEntry? selectedEntry;

        [ObservableProperty]
        private List<object> albumsForSelectedEntry = [];

        [ObservableProperty]
        private int? expandedAlbumId = null;

        private CancellationTokenSource? _imageCts;

        private readonly MusicLibrary _library;
        private readonly ILibraryScanner _scanner;
        private readonly ILibraryCacheService _LibraryCache;
        private readonly IMusicPlayerService _player;

        private static readonly string CoversBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicWrap",
            "covers"
            );

        public LibraryViewModel(MusicLibrary library, ILibraryScanner scanner, ILibraryCacheService libraryCache, IKeyValueStore settings, IMusicPlayerService player)
        {
            _library = library;
            _scanner = scanner;
            _LibraryCache = libraryCache;
            _player = player;
            // Load Initial Settings
            ListBy = settings.GetValue<string>("library_list_by") ?? "Artist";
            Ascending = settings.GetValue<bool>("library_list_ascending");

            LoadEntries();
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

            LoadEntries();
        }

        partial void OnAscendingChanged(bool value)
        {
            LoadEntries();
        }

        partial void OnSelectedEntryChanged(LibraryEntry? value)
        {
            _imageCts?.Cancel(); // cancel previous image loading if any

            if (value == null)
            {
                AlbumsForSelectedEntry = [];
                return;
            }

            LoadAlbumsForEntry(value);
        }

        [RelayCommand]
        private void AddFolder()
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

                _scanner.ScanAllDirectories(null, null);
                LoadEntries();
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Music Files",
                Multiselect = true,
            };
            if (dialog.ShowDialog() == true)
            {
                var selectedFiles = dialog.FileNames;
                _scanner.ScanFiles(selectedFiles, null, null);
                LoadEntries();
            }

        }

        [RelayCommand]
        private void Refresh()
        {
            _LibraryCache.InvalidateCache();
            LoadEntries();
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

        private async void LoadEntries()
        {
            DateTime timeStart = DateTime.Now;
            var entries = await _LibraryCache.GetEntriesAsync(ListBy, Ascending);

            ApplyGrouping(entries);

            DateTime timeEnd = DateTime.Now;
            Debug.WriteLine($"Loaded {entries.Count} entries in {(timeEnd - timeStart).TotalSeconds:F2} seconds");
        }

        private void ApplyGrouping(List<LibraryEntry> entries)
        {
            Entries = entries;

            EntriesViewSource = new CollectionViewSource { Source = Entries };

            Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                EntriesViewSource.GroupDescriptions.Add(new PropertyGroupDescription("GroupKey"));
            }, DispatcherPriority.Render);
        }

        public void PlayAlbum(int albumId)
        {
            var allTracks = _library.Tracks
                .Where(t => t.AlbumId == albumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)
                .ToArray();

            _player.SetQueue(allTracks);
            _player.PlayIndex(0);
        }

        private void LoadAlbumsForEntry(LibraryEntry entry)
        {
            List<int> albumIds = entry.Type switch
            {
                "Album" => [entry.Id],
                "Artist" => _library.Albums.Where(a => a.ArtistIds.Contains(entry.Id)).Select(a => a.Id).ToList(),
                "Genre" => _library.Tracks.Where(t => t.GenreIds.Contains(entry.Id)).Select(t => t.AlbumId).Distinct().ToList(),
                "Decade" => GetAlbumIdsForDecade(entry.Title),
                _ => []
            };

            var albums = new List<AlbumData>();
            var coverLookup = _library.CoverAssets.ToDictionary(c => c.Id, c => c);

            foreach (var albumId in albumIds)
            {
                var album = _library.Albums.FirstOrDefault(a => a.Id == albumId);
                if (album == null) continue;

                var trackCount = _library.Tracks.Count(t => t.AlbumId == albumId);
                if (trackCount == 0) continue;

                string imagePath = null;

                string dominantColorHex = "#808080";
                string foregroundColorHex = "#ffffff";

                if (album.CoverId > 0 && coverLookup.TryGetValue(album.CoverId, out var asset))
                {
                    imagePath = Path.Combine(CoversBasePath, asset.FileName);
                    dominantColorHex = asset.DominantColorHex ?? "#808080";
                    foregroundColorHex = asset.ForegroundColorHex ?? "#ffffff";
                }
                var artistNames = string.Join(", ", _library.Artists.Where(ar => album.ArtistIds.Contains(ar.Id)).Select(ar => ar.Name));


                albums.Add(new AlbumData
                {
                    Id = albumId,
                    Title = album.Title,
                    Year = album.Year,
                    ArtistNames = artistNames,
                    DominantColor = dominantColorHex,
                    ForegroundColor = foregroundColorHex,
                    ImagePath = imagePath
                });
            }

            AlbumsForSelectedEntry = albums.OrderByDescending(a => a.Year).ThenBy(a => a.Title).Cast<object>().ToList();
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
                    catch (OperationCanceledException){}
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

        private List<int> GetAlbumIdsForDecade(string decadeTitle)
        {
            if (!int.TryParse(decadeTitle.TrimEnd('s'), out int decade))
                return [];

            return _library.Albums
                .Where(a => a.Year >= decade && a.Year < decade + 10)
                .Select(a => a.Id)
                .ToList();
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
            public string Title { get; set; }
            public int Year { get; set; }
            public string ArtistNames { get; set; }
            public string? ImagePath { get; set; }
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
            public string DominantColor { get; set; } = "#1a1a1a";
            public string ForegroundColor { get; set; } = "#ffffff";
        }
    }
}
