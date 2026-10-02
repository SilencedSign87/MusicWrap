using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.UI.Controls.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Shared.Services;
using MusicWrap.Core.Saving;
using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Threading;
using MusicWrap.Core.Services.Search;
using MusicWrap.Data.Playlist.Models;
using System.Windows;

namespace MusicWrap.UI.Features.Playlist.ViewModels
{
    public partial class PlaylistViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty] ObservableCollection<PlaylistEntry> entries = [];
        [ObservableProperty] bool compactMode = true;
        [ObservableProperty] PlaylistEntry? selectedEntry;
        [ObservableProperty] ObservableCollection<TrackRowItem> tracks = [];
        [ObservableProperty] List<int> allTrackIds = [];
        [ObservableProperty] List<int> selectedTrackIds = [];
        [ObservableProperty] List<string> allTabs = ["Tracks", "Stats"];
        [ObservableProperty] string selectedTab = "Tracks";
        private bool isDisposing = false;

        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILibraryService _libraryCacheService;
        private readonly MusicPlayerService _musicPlayerService;
        private readonly IPlaylistService _playlistService;
        private readonly SearchService _searchService;
        private readonly WindowManagerService _windowManager;
        private readonly IMessenger _messenger;
        private readonly IUIDispatcher _uiDispatcher;

        public PlaylistViewModel(ILibraryService cache, ISaveCoordinator saveCoordinator, MusicPlayerService musicPlayerService, IPlaylistService playlistService, SearchService searchService, WindowManagerService windowManager, IMessenger messenger, IUIDispatcher uiDispatcher)
        {
            _libraryCacheService = cache;
            _saveCoordinator = saveCoordinator;
            _musicPlayerService = musicPlayerService;
            _playlistService = playlistService;
            _windowManager = windowManager;
            _messenger = messenger;
            _uiDispatcher = uiDispatcher;
            selectedTrackIds = [];

            _searchService = searchService;
            _searchService.SearchSubmitted += _searchService_SearchSubmitted;

            ConstructEntries();

            _messenger.Register<PlaylistListChangedMessage>(this, (r, m) =>
            {
                _uiDispatcher.Invoke(() => ConstructEntries());
            });
            _messenger.Register<PlaylistContentChangedMessage>(this, (r, m) =>
            {
                _uiDispatcher.Invoke(() =>
                {
                    if (m.PlaylistId == SelectedEntry?.Id)
                    {
                        LoadPlaylistTracks();
                    }
                });
            });
        }

        public IReadOnlyList<int> ResolvePlaylistTrackIds(PlaylistEntry entry)
        {
            return _playlistService.GetTracksByPlaylistId(entry.Id) ?? [];
        }

        private PlaylistDto? GetPlaylistByEntry(int entryId)
        {
            return _playlistService.GetPlaylistById(entryId);
        }

        private void _searchService_SearchSubmitted(object? sender, string e)
        {
            ConstructEntries();
        }

        private void _playlistService_PlaylistItemsChanged(object? sender, PlaylistItemsChangedEventArgs e)
        {
            if (e.PlaylistId == SelectedEntry?.Id)
            {
                LoadPlaylistTracks();
            }
        }

        private void _playlistService_PlaylistsChanged(object? sender, EventArgs e)
        {
            ConstructEntries();
        }
        #region Commands
        [RelayCommand]
        private void OpenPlaylistManager(int playlistId)
        {
            var playlist = _playlistService.GetPlaylistById(playlistId);
            if (playlist is not null)
            {
                _windowManager.LaunchEditPlaylistWindow(playlist);
            }
        }
        [RelayCommand]
        private void DeletePlaylist(int playlistId)
        {
            var playlist = _playlistService.GetPlaylistById(playlistId);
            if (playlist is null) return;

            var result = MessageBox.Show($"Are you sure you want to delete the playlist '{playlist.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _playlistService.DeletePlaylist(playlist.Id);
            _saveCoordinator.Enqueue(SaveKind.Playlist);
        }

        [RelayCommand]
        private void NewPlaylist()
        {
            _windowManager.LaunchNewPlaylistWindow();
        }

        [RelayCommand]
        private void Refresh()
        {
            ConstructEntries();
        }
        [RelayCommand]
        private void ReorderTrack(TrackReorderRequest request)
        {
            if (SelectedEntry is null) return;

            _playlistService.ReorderTrack(SelectedEntry.Id, request.SourceTrackId, request.TargetTrackId, request.PlaceAfterTarget);

            _saveCoordinator.Enqueue(SaveKind.Playlist);
        }
        [RelayCommand]
        private void PlayPlaylist(int playlistId)
        {
            var tracks = _playlistService.GetTracksByPlaylistId(playlistId);
            if (tracks == null || tracks.Count == 0) return;

            _musicPlayerService.SetQueue(tracks, false);
            _musicPlayerService.PlayIndex(0);
        }
        [RelayCommand]
        private void ShufflePlaylist(int playlistId)
        {
            var tracks = _playlistService.GetTracksByPlaylistId(playlistId);
            if (tracks == null || tracks.Count == 0) return;
            _musicPlayerService.SetQueue(tracks, false);
            if (!_musicPlayerService.IsShuffleEnabled)
            {
                _musicPlayerService.ToggleShuffle();
            }
            _musicPlayerService.PlayIndex(0);
        }
        [RelayCommand]
        private void RemoveSelectedTracks()
        {
            if (SelectedEntry is null || SelectedTrackIds.Count == 0) return;

            _playlistService.RemoveTracksFromPlaylist(SelectedTrackIds, SelectedEntry.Id);
            _saveCoordinator?.Enqueue(SaveKind.Playlist);
        }
        [RelayCommand]
        private void PlaySelected()
        {
            if (SelectedEntry is null) return;
            var tracks = _playlistService.GetTracksByPlaylistId(SelectedEntry.Id);
            if (tracks == null || tracks.Count == 0) return;
            _musicPlayerService.SetQueue(tracks, false);
            _musicPlayerService.PlayIndex(0);
        }
        [RelayCommand]
        private void PlayNextSelected()
        {
            if (SelectedEntry is null) return;

        }
        #endregion

        #region Overrides
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(SelectedEntry))
            {
                LoadPlaylistTracks();
            }
        }
        #endregion

        private void ConstructEntries()
        {
            Entries.Clear();
            var playlists = _playlistService.GetPlaylists(true);

            foreach (var entry in playlists)
            {
                var tracks = entry.TrackIds;

                Entries.Add(
                    new PlaylistEntry(
                        entry.Id,
                        entry.Name,
                        entry.CoverPath is not null
                            ? entry.CoverPath
                            : (_libraryCacheService.FindCover(trackIds: tracks) ?? string.Empty),
                        $"{tracks.Count} items"
                        )
                    );
            }
            if (SelectedEntry is null)
            {
                // select first item
                SelectedEntry = Entries.Count > 0 ? Entries[0] : null;
            }
        }
        private void LoadPlaylistTracks()
        {
            Tracks.Clear();

            var selectedPlaylist = _playlistService.GetPlaylistById(SelectedEntry?.Id ?? 0);
            if (selectedPlaylist != null)
            {
                var ids = selectedPlaylist.TrackIds.ToList();
                AllTrackIds = ids;
                var trackRows = _libraryCacheService.TrackIdsToTrackRowItems(ids);
                foreach (var row in trackRows)
                {
                    Tracks.Add(row);
                }
            }
            else
            {
                AllTrackIds = [];
            }
        }

        public void Dispose()
        {
            if (isDisposing) return;
            isDisposing = true;
            _searchService.SearchSubmitted -= _searchService_SearchSubmitted;
            _messenger.UnregisterAll(this);
        }
    }

    public record PlaylistEntry(
        int Id,
        string Title,
        string ImagePath,
        string Description
        );
}






