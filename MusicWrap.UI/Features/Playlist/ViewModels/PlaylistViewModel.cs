using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Playlist.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.Core.Services.Playlists;

namespace MusicWrap.UI.Features.Playlist.ViewModels
{
    public partial class PlaylistViewModel : ObservableObject
    {
        [ObservableProperty] ObservableCollection<PlaylistEntry> entries = [];
        [ObservableProperty] bool compactMode = true;
        [ObservableProperty] PlaylistEntry? selectedEntry;
        [ObservableProperty] ObservableCollection<TrackRowItem> tracks = [];
        [ObservableProperty] List<int> allTrackIds = [];
        [ObservableProperty] List<int> selectedTrackIds = [];
        [ObservableProperty] List<string> allTabs = ["Tracks", "Stats"];
        [ObservableProperty] string selectedTab = "Tracks";

        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILibraryCacheService _libraryCacheService;
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly IPlaylistService _playlistService;
        private NewPlaylistWindow? _newPlaylistWindow;

        public PlaylistViewModel(ILibraryCacheService cache, ISaveCoordinator saveCoordinator, IMusicPlayerService musicPlayerService, IPlaylistService playlistService)
        {
            _libraryCacheService = cache;
            _saveCoordinator = saveCoordinator;
            _musicPlayerService = musicPlayerService;
            _playlistService = playlistService;
            selectedTrackIds = [];
            _playlistService.PlaylistsChanged += _playlistService_PlaylistsChanged;
            _playlistService.PlaylistItemsChanged += _playlistService_PlaylistItemsChanged;

            ConstructEntries();
        }

        private void _playlistService_PlaylistItemsChanged(object? sender, PlaylistItemsChangedEventArgs e)
        {
            ConstructEntries();
        }

        private void _playlistService_PlaylistsChanged(object? sender, EventArgs e)
        {
            ConstructEntries();
        }
        #region Commands

        [RelayCommand]
        private void NewPlaylist()
        {
            if (_newPlaylistWindow is null)
            {
                _newPlaylistWindow = new NewPlaylistWindow();
                WindowHelper.LauchFromParent(App.Current.MainWindow!, _newPlaylistWindow, true);
                _newPlaylistWindow = null;
                ConstructEntries();
            }
            else
            {
                _newPlaylistWindow.Activate();
            }
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

            _playlistService.ReorderTrack(SelectedEntry.id, request.SourceTrackId, request.TargetTrackId, request.PlaceAfterTarget);

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
            _musicPlayerService.PlayPlaybackIndex(0);
        }
        [RelayCommand]
        private void RemoveSelectedTracks(List<int> trackIds)
        {
            if (SelectedEntry is null || trackIds.Count == 0)
            {
                return;
            }

            _playlistService.RemoveTracksFromPlaylist(trackIds, SelectedEntry.id);

            _saveCoordinator.Enqueue(SaveKind.Playlist);
        }
        [RelayCommand]
        private void PlaySelected()
        {
            if (SelectedEntry is null) return;
            var tracks = _playlistService.GetTracksByPlaylistId(SelectedEntry.id);
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
            var playlists = _playlistService.GetPlaylists();
            
            foreach (var entry in playlists)
            {
                var tracks =  entry.TrackIds;

                Entries.Add(
                    new PlaylistEntry(
                        entry.Id,
                        entry.Name,
                        _libraryCacheService.FindCover(trackIds: tracks) ?? string.Empty,
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

            var selectedPlaylist = _playlistService.GetPlaylistById(SelectedEntry?.id ?? 0);
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
    }
    public record PlaylistEntry(
        int id,
        string Title,
        string ImagePath,
        string Description
        );
}





