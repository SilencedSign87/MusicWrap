using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using System.Windows;
using System.Windows.Controls;
using MusicWrap.Data.Library.Models;
using System.IO;
using System.Diagnostics;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Contracts;

namespace MusicWrap.UI.Features.Library.Views
{
    /// <summary>
    /// Lógica de interacción para AlbumTracksPage.xaml
    /// </summary>
    public partial class AlbumTracksPage : UserControl
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly ILibraryService _libraryCacheService;
        private readonly IEditMetadataService _editMetadataService;
        private bool _playerEventsAttached;

        public AlbumTracksPage()
        {
            InitializeComponent();
            _musicPlayerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _libraryCacheService = App.Services.GetRequiredService<ILibraryService>();
            _editMetadataService = App.Services.GetRequiredService<IEditMetadataService>();

            Loaded += AlbumTracksPage_Loaded;
            Unloaded += AlbumTracksPage_Unloaded;
            DataContextChanged += AlbumTracksPage_DataContextChanged;
        }

        private void PlayPauseAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AlbumTracksViewModel vm)
            {
                return;
            }

            if (vm.IsAlbumPlaying)
            {
                _musicPlayerService.Pause();
                return;
            }

            if (vm.ContainsTrack(_musicPlayerService.CurrentTrackId) && _musicPlayerService.IsPaused)
            {
                _musicPlayerService.Play();
                return;
            }

            //var trackIds = _libraryCacheService.GetTrackQueueForAlbum(vm.AlbumId).ToList();
            var trackIds = vm.GetPlayableTrackIds().ToList();
            if (trackIds.Count == 0)
            {
                return;
            }

            _musicPlayerService.SetQueue(trackIds);
            _musicPlayerService.PlayTrack(trackIds[0]);
        }

        private void AlbumTracksPage_Loaded(object sender, RoutedEventArgs e)
        {
            AttachPlayerEvents();
            RefreshPlaybackState();
        }

        private void AlbumTracksPage_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachPlayerEvents();
        }

        private void AlbumTracksPage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            RefreshPlaybackState();
        }

        private void AttachPlayerEvents()
        {
            if (_playerEventsAttached)
            {
                return;
            }

            _musicPlayerService.TrackChanged += MusicPlayerService_TrackChanged;
            _musicPlayerService.PlaybackStateChanged += MusicPlayerService_PlaybackStateChanged;
            _playerEventsAttached = true;
        }

        private void DetachPlayerEvents()
        {
            if (!_playerEventsAttached)
            {
                return;
            }

            _musicPlayerService.TrackChanged -= MusicPlayerService_TrackChanged;
            _musicPlayerService.PlaybackStateChanged -= MusicPlayerService_PlaybackStateChanged;
            _playerEventsAttached = false;
        }

        private void MusicPlayerService_TrackChanged(object? sender, string e)
        {
            RefreshPlaybackState();
        }

        private void MusicPlayerService_PlaybackStateChanged(object? sender, PlaybackState e)
        {
            RefreshPlaybackState();
        }

        private void RefreshPlaybackState()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RefreshPlaybackState);
                return;
            }

            if (DataContext is AlbumTracksViewModel vm)
            {
                vm.UpdatePlaybackState(_musicPlayerService.CurrentTrackId, _musicPlayerService.IsPlaying);
            }
        }

        private void AlbumTracksContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
            {
                return;
            }

            if (DataContext is not AlbumTracksViewModel vm)
            {
                return;
            }

            if (contextMenu.Items.OfType<TrackToPlaylistMenu>().FirstOrDefault() is TrackToPlaylistMenu playlistMenu)
            {
                // Force DP change with a fresh list instance so the menu reloads current selection.
                playlistMenu.TrackIds = vm.SelectedTrackIds.ToList();
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AlbumTracksViewModel vm || vm.SelectedTrackIds.Count == 0)
            {
                return;
            }

            _editMetadataService.OpenMetadataWindow(vm.SelectedTrackIds);
        }

        private void ShowInFileExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AlbumTracksViewModel vm || vm.SelectedTrackIds.Count == 0)
            {
                return;
            }

            var track = _libraryCacheService.GetTrackById(vm.SelectedTrackIds[0]);
            if (track is null || string.IsNullOrWhiteSpace(track.Path))
            {
                return;
            }

            if (!File.Exists(track.Path))
            {
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{track.Path}\"")
            {
                UseShellExecute = true
            });
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AlbumTracksViewModel vm)
            {
                return;
            }
            //var trackIds = _libraryCacheService.GetTracksForAlbum(vm.AlbumId).ToList();
            var trackIds = vm.GetPlayableTrackIds().ToList();

            if (trackIds.Count == 0)
            {
                return;
            }
            _musicPlayerService.SetQueue(trackIds);
            if (!_musicPlayerService.IsShuffleEnabled)
            {
                _musicPlayerService.ToggleShuffle();
            }
            _musicPlayerService.PlayIndex(0);

        }
    }
}


