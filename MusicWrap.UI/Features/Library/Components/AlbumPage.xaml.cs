using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.ViewModels;
using System.Windows;
using System.Windows.Controls;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Library;

namespace MusicWrap.UI.Features.Library.Components
{
    /// <summary>
    /// Lógica de interacción para AlbumPage.xaml
    /// </summary>
    public partial class AlbumPage : UserControl
    {
        private readonly IMusicPlayerService _playerService;
        private readonly ILibraryService _libraryService;
        private readonly TrackActionService _trackActions;
        private int[] TracksId = [];
        public AlbumPage()
        {
            InitializeComponent();

            _playerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _trackActions = App.Services.GetRequiredService<TrackActionService>();
            _libraryService = App.Services.GetRequiredService<ILibraryService>();

            Loaded += AlbumPage_Loaded;
        }

        private void AlbumPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                TracksId = GetAllAlbumTracksId(data.Id);
            }
        }

        private void PlayAlbum(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                _playerService.ClearQueue();
                _playerService.SetQueue(TracksId);
                _playerService.Play();
            }
        }

        private void AddAlbumToNext(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                // get queue tracks
                var currentQueue = _playerService.GetQueue();
                var currentTrackPlaying = _playerService.CurrentTrackId;
                List<int> newQueue = [];
                for (int i = 0; currentQueue != null && i < currentQueue.Length; i++)
                {
                    newQueue.Add(currentQueue[i]);
                    if (currentQueue[i] == currentTrackPlaying)
                    {
                        newQueue.AddRange(TracksId);
                    }
                }
                _playerService.SetQueue(newQueue, true);
            }
        }

        private void AddToQueue(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                var currentQueue = _playerService.GetQueue() ?? [];
                List<int> newQueue = [.. currentQueue];
                newQueue.AddRange(TracksId);
                _playerService.SetQueue(newQueue, true);
            }
        }

        private void AlbumContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Find and set track IDs on the TrackToPlaylistMenu in the context menu
            if (sender is ContextMenu contextMenu)
            {
                var trackToPlaylistMenu = contextMenu.Items.OfType<MusicWrap.UI.Controls.Models.TrackToPlaylistMenu>().FirstOrDefault();
                if (trackToPlaylistMenu != null)
                {
                    trackToPlaylistMenu.TrackIds = [.. TracksId];
                }
            }
        }

        private int[] GetAllAlbumTracksId(int albumId)
        {
            return _libraryService.GetTrackQueueForAlbum(albumId).ToArray();
        }

        private void EditTracks_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                var tracks = _libraryService.GetTracksForAlbum(data.Id).ToList();
                _trackActions.EditMetadata(tracks);
            }

        }
    }
}




