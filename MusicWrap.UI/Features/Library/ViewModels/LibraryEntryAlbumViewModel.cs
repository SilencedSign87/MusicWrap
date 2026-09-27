using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Core.Services.Search;
using MusicWrap.UI.Services;
using System.Collections.ObjectModel;
using MusicWrap.UI.Features.Library.Services;
using static MusicWrap.UI.Features.Library.ViewModels.LibraryViewModel;
using System.Diagnostics;

namespace MusicWrap.UI.Features.Library.ViewModels
{
    public partial class LibraryEntryAlbumViewModel : ObservableObject, IDisposable
    {
        private readonly ILibraryService _libraryService;
        private readonly IwindowsImageService _imageService;
        private readonly SearchService _searchService;
        private readonly LibraryWorkspace _workspace;

        // Props
        [ObservableProperty] private int layoutColumns = 1;

        // View State
        public ObservableCollection<AlbumGridRowModel> GridRows { get; } = [];
        private int? _expandedAlbumId;
        private AlbumTracksViewModel? _expandedTracks;
        private AlbumGridRowModel? _expandedRow;

        // internal state
        private bool _isHibernating = true;
        private List<AlbumData> _rawAlbums = [];
        private List<AlbumData> _sortedAlbums = [];

        private CancellationTokenSource? _imageCTS;

        private const int IMAGE_BATCH = 5;
        private bool _isDisposed;

        public LibraryWorkspace Workspace => _workspace;
        public ILibraryService LibraryService => _libraryService;
        public LibraryEntryAlbumViewModel(
            ILibraryService cacheService,
            IwindowsImageService imageService,
            SearchService searchService,
            LibraryWorkspace workspace
            )
        {
            _libraryService = cacheService;
            _imageService = imageService;
            _searchService = searchService;
            _workspace = workspace;

            _searchService.SearchSubmitted += OnSearchSubmitted;
            _workspace.PropertyChanged += OnWorkspaceChanged;
        }

        private void OnWorkspaceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(LibraryWorkspace.SelectedEntry):
                case nameof(LibraryWorkspace.SelectedTab):
                    SyncWithWorkspace();
                    break;
                case nameof(LibraryWorkspace.TrackSortMode):
                case nameof(LibraryWorkspace.SortAscending):
                    if (!_isHibernating) Reshuffle();
                    break;
            }
        }

        #region Public
        public void ExpandAlbum(int albumId)
        {
            //var row = GridRows.FirstOrDefault(r => r.Albums.Any(a => a.Id == albumId));
            if (_expandedAlbumId == albumId) { CollapseAlbum(); return; }
            CollapseAlbum();
            var row = GridRows.FirstOrDefault(r => r.Albums.Any(a => a.Id == albumId));

            if (row is null) return;

            _expandedTracks = CreateTracksViewModel(row, albumId);
            row.ExpandedAlbumId = albumId;
            row.TracksViewModel = _expandedTracks;
            _expandedRow = row;
            _expandedAlbumId = albumId;
        }
        public void CollapseAlbum()
        {
            DetachExpandedRow();
            _expandedAlbumId = null;
            DisposeExpandedTracks();
        }
        #endregion
        #region Partial functions
        partial void OnLayoutColumnsChanged(int value) => Reflow();


        #endregion

        #region Internal
        private AlbumTracksViewModel CreateTracksViewModel(AlbumGridRowModel row, int albumId)
        {
            int[]? filteredTracks = null;
            var entry = _workspace.SelectedEntry;
            if (entry is not null)
            {
                filteredTracks = _libraryService.GetTrackIdsForEntryAlbum(entry, albumId, useSearchQuery: true);
            }
            return new AlbumTracksViewModel(
                _libraryService,
                albumId,
                filteredTracks);
        }
        private void SyncWithWorkspace()
        {
            bool isActive = _workspace.SelectedTab?.Key == LibraryDetailTabKey.Albums && _workspace.SelectedEntry is not null;
            if (isActive)
            {
                _isHibernating = false;
                ReloadFromEntry();
            }
            else
            {
                Hibernate();
            }
        }
        private void OnSearchSubmitted(object? sender, string e)
        {
            if (_isHibernating) return;
            ReloadFromEntry();
        }
        private void ReloadFromEntry()
        {
            var entry = _workspace.SelectedEntry;

            if (_isHibernating || entry is null) return;

            CancelImageLoading();
            DisposeExpandedTracks();

            var fresh = _libraryService
               .GetAlbumsForEntry(entry, useSearchQuery: true)
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

            var columns = ClampColumns(LayoutColumns);
            var rowCount = (_sortedAlbums.Count + columns - 1) / columns;
            DetachExpandedRow();
            while (GridRows.Count > rowCount)
            {
                GridRows.RemoveAt(GridRows.Count - 1);
            }

            for (int r = 0; r < rowCount; r++)
            {
                var slice = _sortedAlbums.GetRange(
             r * columns, Math.Min(columns, _sortedAlbums.Count - r * columns));

                if (r < GridRows.Count)
                {
                    var row = GridRows[r];
                    if (SameAlbums(row.Albums, slice)) continue;   // no-op: cero notificaciones
                    row.Albums = slice;
                }
                else
                {
                    GridRows.Add(new AlbumGridRowModel { Albums = slice });
                }
            }
            AttachExpandedRow();
        }
        private static bool SameAlbums(List<AlbumData> a, List<AlbumData> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }
        private int ClampColumns(int available) => Math.Clamp(available, 1, Math.Max(1, _sortedAlbums.Count));
        private void DetachExpandedRow()
        {
            //if (_expandedAlbumId is not { } id) return;
            if (_expandedRow is null) return;
            _expandedRow.ExpandedAlbumId = null;
            _expandedRow.TracksViewModel = null;
            _expandedRow = null;
        }
        private void AttachExpandedRow()
        {
            if (_expandedAlbumId is not { } id) return;

            var album = GridRows.SelectMany(r => r.Albums).FirstOrDefault(a => a.Id == id);
            if (album is null)
            {
                _expandedAlbumId = null;
                DisposeExpandedTracks();
                return;
            }

            var row = GridRows.First(r => r.Albums.Contains(album));
            _expandedTracks ??= CreateTracksViewModel(row, album.Id);
            row.ExpandedAlbumId = album.Id;
            row.TracksViewModel = _expandedTracks;
            _expandedRow = row;
        }

        private void DisposeExpandedTracks()
        {
            _expandedTracks?.Dispose();
            _expandedTracks = null;
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
            _imageCTS = cts;
            var pending = albums.Where(a => a.ImagePath is not null && a.CoverImage is null).ToList();
            if (pending.Count > 0)
                _ = LoadCoverImagesAsync(pending, cts.Token);
        }
        private List<AlbumData> ApplySort(List<AlbumData> source)
        {
            var sortMode = _workspace.TrackSortMode;
            var ascending = _workspace.SortAscending;
            IEnumerable<AlbumData> sorted;

            switch (sortMode)
            {
                case TrackSortMode.Title:
                    sorted = ascending
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
                    sorted = ascending
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
                    sorted = ascending
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
                    sorted = ascending
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
            CoverImage = null,
            DominantColor = album.DominantColorHex,
            ForegroundColor = album.ForegroundColorHex,
            HighlightColor = album.HighlightColorHex,
            HighlightForeground = album.HighlightForegroundHex
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
            CollapseAlbum();
            GridRows.Clear();
        }
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _workspace.PropertyChanged -= OnWorkspaceChanged;
            _searchService.SearchSubmitted -= OnSearchSubmitted;
            CancelImageLoading();
        }
        #endregion

    }
}

