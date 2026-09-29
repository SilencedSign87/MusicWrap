using MusicWrap.Core.Services.Playlists;
using MusicWrap.Data.Playlist.Models;
using MusicWrap.UI.Helpers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para NewPlaylistWindow.xaml
    /// </summary>
    public partial class PlaylistManagerWindow : Window
    {
        private readonly IPlaylistService _playlistService;
        private readonly PlaylistData _playlist;
        private IEnumerable<int> _trackIds = [];
        public PlaylistManagerMode Mode = new();
        public PlaylistManagerWindow(IPlaylistService playlistService, PlaylistData playlistData)
        {
            InitializeComponent();
            _playlistService = playlistService;
            _playlist = playlistData;
            Loaded += NewPlaylistWindow_Loaded;
        }

        private void NewPlaylistWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PlaylistNameInput.Focus();
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Helper.DwnSetWindowLong(hwnd, Win32Helper.GWL_STYLE, Win32Helper.DwnGetWindowLong(hwnd, Win32Helper.GWL_STYLE) & ~Win32Helper.WS_SYSMENU);
        }

        public void Initialize(PlaylistManagerMode state)
        {
            if (state is PlaylistManagerStateCreate createState)
            {
                Mode = state;
                InitializeForCreate(createState.TrackIds);
            }
            else if (state is PlaylistManagerStateEdit editState)
            {
                Mode = state;
                InitializeForEdit(editState.Playlist);
            }
        }

        private void InitializeForCreate(IEnumerable<int> trackIds)
        {
            _trackIds = trackIds ?? [];
            if (_trackIds.Any())
            {
                Title = $"Create Playlist - {_trackIds.Count()} tracks";
            }
            else
            {
                Title = "Create Playlist";
            }
            TitleBox.Text = $"New playlist";
        }
        private void InitializeForEdit(PlaylistDto playlist)
        {
            PlaylistNameInput.Text = playlist.Name;
            Title = $"Edit Playlist - {playlist.Name}";
            TitleBox.Text = "Playlist information";
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

    public class PlaylistManagerMode { }

    public class PlaylistManagerStateCreate : PlaylistManagerMode
    {
        public IEnumerable<int> TrackIds { get; }
        public PlaylistManagerStateCreate(IEnumerable<int> trackIds)
        {
            TrackIds = trackIds;
        }
    }

    public class PlaylistManagerStateEdit : PlaylistManagerMode
    {
        public PlaylistDto Playlist { get; }
        public PlaylistManagerStateEdit(PlaylistDto playlist)
        {
            Playlist = playlist;
        }
    }
}

