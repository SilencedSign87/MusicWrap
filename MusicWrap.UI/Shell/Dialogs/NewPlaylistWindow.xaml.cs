using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Data.Playlist;
using MusicWrap.Data.Playlist.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para NewPlaylistWindow.xaml
    /// </summary>
    public partial class NewPlaylistWindow : Window
    {
        private readonly IPlaylistService _playlistService;
        private readonly PlaylistData _playlist;
        private IEnumerable<int> _trackIds = [];
        public NewPlaylistWindow(IPlaylistService playlistService, PlaylistData playlistData)
        {
            InitializeComponent();
            _playlistService = playlistService;
            _playlist = playlistData;
        }
        public void Initialize(IEnumerable<int>? trackIds = null)
        {
            _trackIds = trackIds ?? [];
            if (trackIds is not null && trackIds.Count() > 0)
            {
                Title = $"Create Playlist - {trackIds.Count()} tracks";
            }else
            {
                Title = "Create Playlist";
            }
        }
        public void AddTracks(IEnumerable<int> tracksId)
        {
            _trackIds = _trackIds.Concat(tracksId).Distinct();

            if (_trackIds.Any())
            {
                Title = $"Create Playlist - {_trackIds.Count()} tracks";
            }
            else
            {
                Title = "Create Playlist";
            }
        }
        private void PlaylistNameInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var playlistName = PlaylistNameInput.Text;
                TryToCreatePlaylist(playlistName);
                e.Handled = true;
            }
        }

        private void SavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            TryToCreatePlaylist(PlaylistNameInput.Text);
        }

        private void CancelPlaylist_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TryToCreatePlaylist(string playlistName)
        {
            var existing = _playlist.Playlists.Any(p => p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));
            if (existing)
            {
                MessageBox.Show(
                     $"A playlist with the name '{playlistName}' already exists.",
                     "Duplicate Playlist Name",
                     MessageBoxButton.OK,
                     MessageBoxImage.Error
                    );

            }
            else
            {
                _playlistService.CreatePlaylist(playlistName, _trackIds);

                Close();

            }
        }

    }
}

