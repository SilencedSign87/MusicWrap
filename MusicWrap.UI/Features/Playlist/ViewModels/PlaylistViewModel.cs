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

        private readonly PlaylistData _playlist;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILibraryCacheService _libraryCacheService;
        private NewPlaylistWindow? _newPlaylistWindow;

        public PlaylistViewModel(PlaylistData playlist, ILibraryCacheService cache, ISaveCoordinator saveCoordinator)
        {
            _playlist = playlist;
            _libraryCacheService = cache;
            _saveCoordinator = saveCoordinator;
            selectedTrackIds = [];

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

            var playlist = _playlist.Playlists.FirstOrDefault(p => p.Id == SelectedEntry.id);
            if (playlist is null) return;

            var sourceIndex = playlist.Items.FindIndex(i => i.TrackId == request.SourceTrackId);
            var targetIndex = playlist.Items.FindIndex(i => i.TrackId == request.TargetTrackId);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex) return;

            var moved = playlist.Items[sourceIndex];
            playlist.Items.RemoveAt(sourceIndex);

            if (sourceIndex < targetIndex) targetIndex--;

            var insertIndex = request.PlaceAfterTarget ? targetIndex + 1 : targetIndex;
            insertIndex = Math.Clamp(insertIndex, 0, playlist.Items.Count);
            playlist.Items.Insert(insertIndex, moved);

            playlist.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

            LoadPlaylistTracks();
            _saveCoordinator.Enqueue(SaveKind.Playlist);
        }

        [RelayCommand]
        private void RemoveSelectedTracks(List<int> trackIds)
        {
            if (SelectedEntry is null || trackIds.Count == 0)
            {
                return;
            }

            var playlist = _playlist.Playlists.FirstOrDefault(p => p.Id == SelectedEntry.id);
            if (playlist is null)
            {
                return;
            }

            var removeSet = trackIds.ToHashSet();
            var removedAny = playlist.Items.RemoveAll(item => removeSet.Contains(item.TrackId)) > 0;
            if (!removedAny)
            {
                return;
            }

            playlist.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

            LoadPlaylistTracks();
            _saveCoordinator.Enqueue(SaveKind.Playlist);
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
            var playlists = _playlist.Playlists;

            foreach (var entry in playlists)
            {
                Entries.Add(
                    new PlaylistEntry(
                        entry.Id,
                        entry.Name,
                        string.Empty,
                        $"{entry.Items.Count} items"
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

            var selectedPlaylist = _playlist.Playlists.FirstOrDefault(p => p.Id == SelectedEntry?.id);
            if (selectedPlaylist != null)
            {
                var ids = selectedPlaylist.Items.Select(i => i.TrackId).ToList();
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





