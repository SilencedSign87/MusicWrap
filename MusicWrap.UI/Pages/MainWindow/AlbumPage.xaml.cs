using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.ViewModels.Library;
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

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para AlbumPage.xaml
    /// </summary>
    public partial class AlbumPage : UserControl
    {
        private readonly IMusicPlayerService _playerService;
        private readonly MusicLibrary _library;
        private int[] TracksId = [];
        public AlbumPage()
        {
            InitializeComponent();

            _playerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _library = App.Services.GetRequiredService<MusicLibrary>();
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

        private void AlbumPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Debug.WriteLine("AlbumPage DataContext changed: " + (e.NewValue?.GetType().Name ?? "null")); // DATACONTEXT : ALbumData
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

        private int[] GetAllAlbumTracksId(int albumId)
        {
            return [.. _library.Tracks.Where(t => t.AlbumId == albumId).Select(t => t.Id)];
        }
    }
}
