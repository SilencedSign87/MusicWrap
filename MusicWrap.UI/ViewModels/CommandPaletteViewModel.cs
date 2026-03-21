using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.ViewModels
{
    public partial class CommandPaletteViewModel : ObservableObject
    {
        [ObservableProperty] private string query = string.Empty;
        [ObservableProperty] private bool isSearching;
        [ObservableProperty] private bool isPopupOpen;

        public bool HasQuery => !string.IsNullOrWhiteSpace(Query);
        public bool HasAnyResults => HasTrackResults || HasAlbumResults || HasArtistResults || HasPlaylistResults;

        public bool HasTrackResults => TrackResults.Count > 0;
        public bool HasAlbumResults => AlbumResults.Count > 0;
        public bool HasArtistResults => ArtistResults.Count > 0;
        public bool HasPlaylistResults => PlaylistResults.Count > 0;

        public ObservableCollection<SearchResultItem> TrackResults { get; } = [];
        public ObservableCollection<SearchResultItem> AlbumResults { get; } = [];
        public ObservableCollection<SearchResultItem> ArtistResults { get; } = [];
        public ObservableCollection<SearchResultItem> PlaylistResults { get; } = [];

        private readonly ILibraryCacheService _libraryCache;
        private readonly MusicLibrary _library;
        private CancellationTokenSource? _cts;
        private int _requestId;

        public event EventHandler? OpenFullSearchRequested;
        public event EventHandler<SearchResultItem>? ResultSelected;

        public CommandPaletteViewModel(ILibraryCacheService libraryCache, MusicLibrary library)
        {
            _libraryCache = libraryCache;
            _library = library;
        }

        partial void OnQueryChanged(string value)
        {
            OnPropertyChanged(nameof(HasQuery));

            _ = SearchAsync(value);

        }

        [RelayCommand]
        private void OpenFullSearch()
        {
            OpenFullSearchRequested?.Invoke(this, EventArgs.Empty);
        }
        [RelayCommand]
        private void ClosePopUp() {
            IsPopupOpen = false;
        }
        [RelayCommand]
        private void SelectResult(SearchResultItem? item)
        {
            if (item is null) return;
            ResultSelected?.Invoke(this, item);
            IsPopupOpen = false;
        }

        private async Task SearchAsync(string rawQuery)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            int req = Interlocked.Increment(ref _requestId);

            string q = rawQuery?.Trim() ?? string.Empty;
            if (q.Length  == 0)
            {
                ClearResults();
                IsPopupOpen = false;
                return;
            }

            IsSearching = true;
            try
            {
                await Task.Delay(300, ct); // Debounce

                var tracks = _library.Tracks
                    .Where(t => t.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .Select(t => new SearchResultItem("Track", t.Id, t.Title))
                    .ToArray();

                var albums = _library.Albums
                    .Where(a => a.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .Select(a => new SearchResultItem("Album", a.Id, a.Title))
                    .ToArray();

                var artists = _library.Artists
                    .Where(a => a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .Select(a => new SearchResultItem("Artist", a.Id, a.Name))
                    .ToArray();

                // Placeholder
                var playlists = Array.Empty<SearchResultItem>();

                if (req != Volatile.Read(ref _requestId) || ct.IsCancellationRequested)
                    return;

                Replace(TrackResults, tracks);
                Replace(AlbumResults, albums);
                Replace(ArtistResults, artists);
                Replace(PlaylistResults, playlists);

                OnPropertyChanged(nameof(HasAnyResults));
                OnPropertyChanged(nameof(HasTrackResults));
                OnPropertyChanged(nameof(HasAlbumResults));
                OnPropertyChanged(nameof(HasArtistResults));
                OnPropertyChanged(nameof(HasPlaylistResults));
                IsPopupOpen = HasQuery;
            }
            catch (OperationCanceledException){}
            finally
            {
                if (req == Volatile.Read(ref _requestId))
                    IsSearching = false;
            }
        }
        private void ClearResults()
        {
            TrackResults.Clear();
            AlbumResults.Clear();
            ArtistResults.Clear();
            PlaylistResults.Clear();
            OnPropertyChanged(nameof(HasAnyResults));
            OnPropertyChanged(nameof(HasTrackResults));
            OnPropertyChanged(nameof(HasAlbumResults));
            OnPropertyChanged(nameof(HasArtistResults));
            OnPropertyChanged(nameof(HasPlaylistResults));
        }
        private static void Replace(ObservableCollection<SearchResultItem> target, IEnumerable<SearchResultItem> source)
        {
            target.Clear();
            foreach (var item in source) target.Add(item);
        }
    }

    public sealed record SearchResultItem(
        string Section,
        int Id,
        string Title
        );
}
