using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Search;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.WebSockets;
using static MusicWrap.UI.Features.Library.ViewModels.LibraryViewModel;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryEntryAlbumViewModel : ObservableObject, IDisposable
    {
        private readonly ILibraryService _libraryService;
        private readonly IwindowsImageService _imageService;
        private readonly SearchService _searchService;

        // Props
        [ObservableProperty] private LibraryEntry? selectedEntry;
        [ObservableProperty] private TrackSortMode? sortMode;
        [ObservableProperty] private bool sortAscending;
        [ObservableProperty] private int layoutColumns = 1;

        // View State
        [ObservableProperty] private ObservableCollection<AlbumGridRowModel> gridRows = [];
        [ObservableProperty] private int? expandedAlbumId = 0;

        // internal state
        private bool _isHibernating = true;
        private List<AlbumData> _rawAlbums = [];
        private List<AlbumData> _sortedAlbums = [];
        private CancellationTokenSource? _imageCts;
        private bool _isDisposed;


        private CancellationTokenSource? _imageCTS;
        private const int IMAGE_BATCH = 5;

        public LibraryEntryAlbumViewModel(
            ILibraryService cacheService,
            IwindowsImageService imageService,
            SearchService searchService
            )
        {
            _libraryService = cacheService;
            _imageService = imageService;
            _searchService = searchService;

            _searchService.SearchSubmitted += OnSearchSubmitted;
        }

        #region Public
        public ILibraryService LibraryCache => _libraryService;
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
            if (value is null)
            {
                Hibernate();
            }
            else
            {
                _isHibernating = false;
                ReloadFromEntry();
            }
        }
        partial void OnSortModeChanged(TrackSortMode? value) => Reshuffle();
        partial void OnSortAscendingChanged(bool value) => Reshuffle();
        partial void OnLayoutColumnsChanged(int value) => Reflow();

        #endregion
        #region Internal
        private void OnSearchSubmitted(object? sender, string e)
        {
            if (_isHibernating) return;
            ReloadFromEntry();
        }
        private void ReloadFromEntry()
        {
            if (_isHibernating || SelectedEntry is null) return;

            CancelImageLoading();

            var fresh = _libraryService
               .GetAlbumsForEntry(SelectedEntry, useSearchQuery: true)
               .Select(MapToAlbumData)
               .ToList();

            _rawAlbums = fresh;
            Reshuffle();
            StartImageLoading(_sortedAlbums);
        }
        private void Reshuffle()
        {
            if (_isHibernating) return;
            _sortedAlbums = ApplySort(_rawAlbums);
            Reflow();
        }
        private void Reflow()
        {
            if (_isHibernating) return;

            var columns = Math.Max(1, LayoutColumns);
            var rows = new ObservableCollection<AlbumGridRowModel>();
            for (int i = 0; i < _sortedAlbums.Count; i += columns)
            {
                rows.Add(new AlbumGridRowModel
                {
                    Albums = [.. _sortedAlbums.Skip(i).Take(columns)]
                });
            }
            GridRows = rows;
            RestoreExpandedIfPresent();
        }
        private void RestoreExpandedIfPresent()
        {
            if (ExpandedAlbumId is not { } id) return;

            var album = GridRows.SelectMany(r => r.Albums).FirstOrDefault(a => a.Id == id);
            if (album is null)
            {
                ExpandedAlbumId = null;
                return;
            }

            var row = GridRows.First(r => r.Albums.Contains(album));
            row.ExpandedAlbumId = album.Id;
            row.ExpandedImagePath = album.BlurredImagePath;
            row.ExpandedDominantColor = album.DominantColor;
            row.ExpandedForegroundColor = album.ForegroundColor;
        }
        private void CancelImageLoading()
        {
            _imageCTS?.Cancel();
            _imageCTS?.Dispose();
            _imageCTS = null;
        }
        private void StartImageLoading(List<AlbumData> albums)
        {
            var cts = new CancellationTokenSource();
            _imageCts = cts;
            var pending = albums.Where(a => a.ImagePath is not null && a.CoverImage is null).ToList();
            if (pending.Count > 0)
                _ = LoadCoverImagesAsync(pending, cts.Token);
        }
        private List<AlbumData> ApplySort(List<AlbumData> source)
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
                            .OrderBy(s => _libraryService.GetAlbumDuration(s.Id))
                            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.Year)
                        : source
                            .OrderByDescending(s => _libraryService.GetAlbumDuration(s.Id))
                            .ThenByDescending(s => s.Title, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.ArtistNames, StringComparer.OrdinalIgnoreCase)
                            .ThenByDescending(s => s.Year);
                    break;
            }

            return [.. sorted];
        }
        private AlbumData MapToAlbumData(AlbumSummary album) => new()
        {

            Id = album.Id,
            Title = album.Title,
            Year = album.Year,
            ArtistNames = album.ArtistNames,
            ImagePath = album.ImagePath,
            BlurredImagePath = album.BluredImagePath,
            CoverImage = null,
            DominantColor = album.DominantColorHex,
            ForegroundColor = album.ForegroundColorHex
        };

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

        private void Hibernate()
        {
            _isHibernating = true;
            CancelImageLoading();
            _rawAlbums.Clear();
            _sortedAlbums.Clear();
            GridRows.Clear();
            ExpandedAlbumId = null;
        }
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _searchService.SearchSubmitted -= OnSearchSubmitted;
            CancelImageLoading();
        }
        #endregion

    }
}

