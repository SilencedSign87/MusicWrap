using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Library.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MusicWrap.Core.Services.Playback;

namespace MusicWrap.UI.Features.Library.Components
{
    /// <summary>
    /// Lógica de interacción para AlbumPage.xaml
    /// </summary>
    public partial class AlbumPage : UserControl
    {
        private readonly IMusicPlayerService _playerService;
        private readonly IEditMetadataService _editMetadataService;
        private readonly MusicLibrary _library;
        private int[] TracksId = [];
        public AlbumPage()
        {
            InitializeComponent();

            _playerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _library = App.Services.GetRequiredService<MusicLibrary>();
            _editMetadataService = App.Services.GetRequiredService<IEditMetadataService>();
            //this.DataContextChanged += AlbumPage_DataContextChanged;

            Loaded += AlbumPage_Loaded;
        }

        private void AlbumPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                TracksId = GetAllAlbumTracksId(data.Id);
                //Debug.WriteLine("AlbumPage loaded with AlbumData: " + data.Title + " with " + TracksId.Length + " tracks.");
            }
        }

        private void PlayAlbum(object sender, RoutedEventArgs e)
        {
            //Debug.WriteLine("Trying to play album");
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                //Debug.WriteLine("Searching tracks...");
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
            return [.. _library.Tracks.Where(t => t.AlbumId == albumId).Select(t => t.Id)];
        }

        private void EditTracks_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                var tracks = _library.Tracks.Where(t => t.AlbumId == data.Id).Select(t => t.Id).ToList();
                _editMetadataService.OpenMetadataWindow(tracks);
            }

        }
    }
}




