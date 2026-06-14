using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Playlist.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Playlist.Views
{
    public partial class PlaylistPage : UserControl, IDisposable
    {
        private readonly TracksContextMenuService _tracksContextMenuService;
        private PlaylistViewModel _vm;

        private bool _isDisposed = false;

        public PlaylistPage(PlaylistViewModel playlistViewModel, TracksContextMenuService tracksContextMenuService)
        {
            InitializeComponent();
            _tracksContextMenuService = tracksContextMenuService;
            _vm = playlistViewModel;
            DataContext = _vm;
        }

        private void PlaylistPlayNow_Click(object sender, RoutedEventArgs e)
        {
            var tracksView = ResolveTracksViewFromMenuSender(sender);
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            _tracksContextMenuService.PlayNow(selected, tracksView.AllTrackIds?.ToList());
        }

        private void PlaylistPlayNext_Click(object sender, RoutedEventArgs e)
        {
            var tracksView = ResolveTracksViewFromMenuSender(sender);
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            _tracksContextMenuService.PlayNext(selected, tracksView.AllTrackIds?.ToList());
        }

        private void PlaylistAddToQueue_Click(object sender, RoutedEventArgs e)
        {
            var tracksView = ResolveTracksViewFromMenuSender(sender);
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            _tracksContextMenuService.AddToQueue(selected);
        }

        private static TracksView? ResolveTracksViewFromMenuSender(object sender)
        {
            if (sender is not FrameworkElement element)
            {
                return null;
            }

            return (element.Parent as ContextMenu)?.PlacementTarget as TracksView;
        }

        private void PlaySelectedPlaylist_click(object sender, RoutedEventArgs e)
        {
            var entry = _vm.SelectedEntry;
            if (entry == null) return;

            if (_vm.PlayPlaylistCommand.CanExecute(entry.id))
            {
                _vm.PlayPlaylistCommand.Execute(entry.id);
            }


        }

        private void ShufflePlaylist_click(object sender, RoutedEventArgs e)
        {
            var entry = _vm.SelectedEntry;
            if (entry == null) return;
            if (_vm.ShufflePlaylistCommand.CanExecute(entry.id))
            {
                _vm.ShufflePlaylistCommand.Execute(entry.id);
            }
        }

        private void PlayPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.PlaySelectedCommand.CanExecute(null))
            {
                _vm.PlaySelectedCommand.Execute(null);
            }
        }

        private void PlayNext_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.PlayNextSelectedCommand.CanExecute(null))
            {
                _vm.PlayNextSelectedCommand.Execute(null);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _vm.Dispose();
        }
    }
}




