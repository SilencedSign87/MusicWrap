using MusicWrap.Core.Services.Playlists;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace MusicWrap.UI.Shell.Dialogs
{
    /// <summary>
    /// Lógica de interacción para NewPlaylistWindow.xaml
    /// </summary>
    public partial class PlaylistManagerWindow : Window
    {
        private readonly IPlaylistService _playlistService;
        private readonly IwindowsImageService _imageService;

        private readonly List<int> _trackIds = [];
        private readonly HashSet<int> _trackIdSet = [];

        public PlaylistManagerMode? Mode { get; private set; }
        private const int ArtworkSize = 300;
        private string? _pendingArtwork;
        private bool _artworkChanged = false;

        public PlaylistManagerWindow(IPlaylistService playlistService, IwindowsImageService imageService)
        {
            InitializeComponent();
            _playlistService = playlistService;
            _imageService = imageService;
            SourceInitialized += OnSourceInitialized;
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = Win32Helper.DwnGetWindowLong(hwnd, Win32Helper.GWL_STYLE) & ~Win32Helper.WS_SYSMENU;
            Win32Helper.DwnSetWindowLong(hwnd, Win32Helper.GWL_STYLE, style);
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            PlaylistNameInput.Focus();
        }

        public void Initialize(PlaylistManagerMode state)
        {
            Mode = state;

            _pendingArtwork = null;
            _artworkChanged = false;
            SetArtworkPreview(null);

            switch (state)
            {
                case PlaylistManagerStateCreate create:
                    InitializeForCreate(create.TrackIds);
                    break;
                case PlaylistManagerStateEdit edit:
                    InitializeForEdit(edit.Playlist);
                    break;
            }
        }

        private void InitializeForCreate(IEnumerable<int> trackIds)
        {
            _trackIds.Clear();
            _trackIdSet.Clear();
            AddTracks(trackIds);

            TitleBox.Text = $"New playlist";
            PlaylistNameInput.Text = string.Empty;
        }
        private void UpdateTitle()
        {
            Title = _trackIds.Count > 0 ? $"Create Playlist - {_trackIds.Count} tracks" : "Create Playlist";
        }
        private void InitializeForEdit(PlaylistDto playlist)
        {
            TitleBox.Text = "Playlist information";
            PlaylistNameInput.Text = playlist.Name;
            SetArtworkPreview(playlist.CoverPath);
            Title = $"Edit Playlist - {playlist.Name}";
        }
        public void AddTracks(IEnumerable<int> tracksIds)
        {
            if (Mode is not PlaylistManagerStateCreate || tracksIds is null) return;

            foreach (var id in tracksIds)
                if (_trackIdSet.Add(id))
                    _trackIds.Add(id);

            UpdateTitle();
        }

        private void SavePlaylist_Click(object sender, RoutedEventArgs e) => Save();

        private void CancelPlaylist_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Save()
        {
            var name = PlaylistNameInput.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                System.Windows.MessageBox.Show("Please enter a valid playlist name.", "Invalid Playlist Name",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                PlaylistNameInput.Focus();
                return;
            }

            var editMode = Mode as PlaylistManagerStateEdit;
            if (_playlistService.PlaylistNameExists(name, editMode?.Playlist.Id))
            {
                System.Windows.MessageBox.Show($"A playlist with the name '{name}' already exists.", "Duplicate Playlist Name",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (editMode is not null)
                {
                    _playlistService.RenamePlaylist(editMode.Playlist.Id, name);
                    if (_artworkChanged)
                        _playlistService.SetPlaylistArtwork(editMode.Playlist.Id, _pendingArtwork);
                }
                else if (Mode is PlaylistManagerStateCreate)
                {
                    _playlistService.CreatePlaylist(name, _trackIds, _artworkChanged ? _pendingArtwork : null);
                }
                else
                {
                    throw new InvalidOperationException("Initialize() debe llamarse antes de guardar.");
                }

                Close();
            }
            catch (Exception ex)
            {
                // TODO: log
                System.Windows.MessageBox.Show($"No se pudo guardar la playlist.\n\n{ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetArtworkPreview(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                PlaylistArtworkBorder.SetResourceReference(Border.BackgroundProperty, "BackgroundLayer");
                return;
            }

            PlaylistArtworkBorder.Background = new ImageBrush
            {
                ImageSource = _imageService.LoadForSize(path, ArtworkSize),
                Stretch = Stretch.UniformToFill
            };
        }

        private void SwapArtwork_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png",
                Title = "Select playlist artwork",
                Multiselect = false,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true) return;

            _pendingArtwork = dialog.FileName;
            _artworkChanged = true;
            SetArtworkPreview(_pendingArtwork);
        }

        private void RemoveArtwork_click(object sender, RoutedEventArgs e)
        {
            _pendingArtwork = null;
            _artworkChanged = true;
            SetArtworkPreview(null);
        }

        private void PlaylistNameInput_EnterPressed(object sender, RoutedEventArgs e) => Save();
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

