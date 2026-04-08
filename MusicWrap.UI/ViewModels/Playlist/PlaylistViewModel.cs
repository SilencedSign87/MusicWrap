using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Playlist.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;

namespace MusicWrap.UI.ViewModels.Playlist
{
    public partial class PlaylistViewModel : ObservableObject
    {
        [ObservableProperty] ObservableCollection<PlaylistEntry> entries = [];
        [ObservableProperty] bool compactMode = true;
        [ObservableProperty] PlaylistEntry? selectedEntry;
        [ObservableProperty] ObservableCollection<TrackRowItem> tracks = [];

        private readonly PlaylistData _playlist;
        private readonly ILibraryCacheService _libraryCacheService;
        private NewPlaylistWindow? _newPlaylistWindow;

        public PlaylistViewModel(PlaylistData playlist, ILibraryCacheService cache)
        {
            _playlist = playlist;
            _libraryCacheService = cache;

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
                var tracksRows = _libraryCacheService.TrackIdsToTrackRowItems(ids);
                foreach (var track in tracksRows)
                {
                    Tracks.Add(track);
                }
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
