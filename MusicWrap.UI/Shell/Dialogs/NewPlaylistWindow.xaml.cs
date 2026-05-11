using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.Data.Playlist.Models;
using System.Windows;
using System.Windows.Input;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para NewPlaylistWindow.xaml
    /// </summary>
    public partial class NewPlaylistWindow : Window
    {
        private readonly IPlaylistService _playlistService;
        private readonly PlaylistData _playlist;
        private readonly IEnumerable<int> _trackIds;
        public NewPlaylistWindow(IEnumerable<int>? trackIds = null)
        {
            InitializeComponent();
            _trackIds = trackIds ?? Array.Empty<int>();
            _playlistService = App.Services.GetRequiredService<IPlaylistService>();
            _playlist = App.Services.GetRequiredService<PlaylistData>();

            if (trackIds is not null && trackIds.Count() > 0)
            {
                Title = $"Create Playlist - {trackIds.Count()} tracks";
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

