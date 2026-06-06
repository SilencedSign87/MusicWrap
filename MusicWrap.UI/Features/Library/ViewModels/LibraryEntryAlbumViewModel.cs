using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using static MusicWrap.UI.Features.Library.ViewModels.LibraryViewModel;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryEntryAlbumViewModel : ObservableObject, IDisposable
    {
        private readonly ILibraryService _libraryCache;
        private readonly IImageService _imageService;
        private readonly SearchService _searchService;

        [ObservableProperty] private ObservableCollection<AlbumGridRowModel> gridRows = [];
        [ObservableProperty] private int layoutColumns = 1;
        [ObservableProperty] private int? expandedAlbumId = 0;
        [ObservableProperty] private LibraryEntry? selectedEntry;
        [ObservableProperty] private TrackSortMode? sortMode;
        [ObservableProperty] private bool sortAscending;

        private List<AlbumData> _visibleAlbums = [];


        private CancellationTokenSource? _imageCTS;
        private const int IMAGE_BATCH = 5;
        private bool _disposed = false;

        public LibraryEntryAlbumViewModel(
            ILibraryService cacheService,
            MusicLibrary library,
            IImageService imageService,
            SearchService searchService
            )
        {
            _libraryCache = cacheService;
            _imageService = imageService;
            _searchService = searchService;
        }
        #region Public
        public ILibraryService LibraryCache { get { return _libraryCache; } }
        public void SetLayoutColumns(int columns)
        {
            columns = Math.Max(1, columns);
            if (columns != LayoutColumns)
            {
                LayoutColumns = columns;
            }
        }
        public void ExpandAlbum(int albumId)
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
        #endregion
        #region Partial functions
        partial void OnSelectedEntryChanged(LibraryEntry? value)
        {
            _imageCTS?.Cancel();
            _imageCTS = null;

            if (value is null)
            {
                _visibleAlbums.Clear();
                GridRows.Clear();
                ExpandedAlbumId = null;
                return;
            }

            LoadAlbumData();
        }
        partial void OnSortModeChanged(TrackSortMode? value)
        {
            LoadAlbumData();
        }
        partial void OnSortAscendingChanged(bool value)
        {
            LoadAlbumData();
        }
        partial void OnLayoutColumnsChanged(int value)
        {
            ReflowRows();
        }
        partial void OnGridRowsChanged(ObservableCollection<AlbumGridRowModel> value)
        {
            Debug.WriteLine($"GridRows changed: {value.Count} rows ");
            Debug.WriteLine($"       {LayoutColumns} columns ");
            Debug.WriteLine($"       {_visibleAlbums.Count} visible albums ");
        }

        #endregion
        #region Internal

        private void LoadAlbumData()
        {
            if (SelectedEntry is null)
            {
                _visibleAlbums.Clear();
                GridRows.Clear();
                ExpandedAlbumId = null;
                return;
            }

            var albums = _libraryCache.GetAlbumsForEntry(SelectedEntry, true)
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

            albums = ApplySorting(albums);

            _visibleAlbums = albums;
            ReflowRows();
            _imageCTS?.Cancel();
            _imageCTS = new CancellationTokenSource();
            var pending = _visibleAlbums.Where(a => a.ImagePath is not null && a.CoverImage is null).ToList();
            if (pending.Count != 0)
            {
                _ = LoadCoverImagesAsync(pending, _imageCTS.Token);
            }
        }
        private void ReflowRows()
        {
            var columns = Math.Max(1, LayoutColumns);
            int? expandedAlbumIdSnapshot = ExpandedAlbumId;

            var rows = new ObservableCollection<AlbumGridRowModel>();
            for (int i = 0; i < _visibleAlbums.Count; i += columns)
            {
                rows.Add(new AlbumGridRowModel
                {
                    Albums = [.. _visibleAlbums.Skip(i).Take(columns)],
                });

            }

            GridRows = rows;
            ExpandedAlbumId = null;
            if (expandedAlbumIdSnapshot.HasValue)
            {
                var expandedRow = GridRows
                    .FirstOrDefault(r =>
                        r.Albums.Any(a => a.Id == expandedAlbumIdSnapshot.Value)
                    );
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
        private List<AlbumData> ApplySorting(List<AlbumData> source)
        {
            IEnumerable<AlbumData> sorted;

            switch (SortMode)
            {
                case TrackSortMode.Title:
                    sorted = SortAscending
                            ? source
                                .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(s => s.Year)
                            : source
                                .OrderByDescending(s => s.Title, StringComparer.OrdinalIgnoreCase)
                                .ThenByDescending(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                                .ThenByDescending(s => s.Year);
                    break;
                case TrackSortMode.ArtistName:
                    sorted = SortAscending
                        ? source
                            .OrderBy(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.Year)
                        : source
                            .OrderByDescending(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.Year);

                    break;
                case TrackSortMode.Year:
                default:
                    sorted = SortAscending
                        ? source
                            .OrderBy(s => s.Year)
                            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                        : source
                            .OrderByDescending(s => s.Year)
                            .ThenByDescending(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase);

                    break;
                case TrackSortMode.Duration:
                    sorted = SortAscending
                        ? source
                            .OrderBy(s => _libraryCache.GetAlbumDuration(s.Id))
                            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.Year)
                        : source
                            .OrderByDescending(s => _libraryCache.GetAlbumDuration(s.Id))
                            .ThenByDescending(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.Year);
                    break;
            }

            return [.. sorted];
        }

        private async Task LoadCoverImagesAsync(List<AlbumData> albums, CancellationToken ct)
        {
            foreach (var batch in albums.Chunk(IMAGE_BATCH))
            {
                ct.ThrowIfCancellationRequested();

                using var sem = new SemaphoreSlim(3);
                var tasks = batch.Select(async album =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);

                    try
                    {
                        if (ct.IsCancellationRequested) return;

                        var bmp = await _imageService.LoadAsync(
                            album.ImagePath,
                            ImageVariant.Medium,
                            150,
                            ct
                            );

                        if (bmp is not null && !ct.IsCancellationRequested)
                        {
                            album.CoverImage = bmp;
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        sem.Release();
                    }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _imageCTS?.Cancel();
            _imageCTS?.Dispose();
            _imageCTS = null;
            foreach (var album in _visibleAlbums)
            {
                album.CoverImage = null;
            }
            _visibleAlbums.Clear();
            GridRows.Clear();
        }
        #endregion

    }
}

