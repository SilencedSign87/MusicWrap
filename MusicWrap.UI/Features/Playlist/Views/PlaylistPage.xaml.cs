using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Playlist.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace MusicWrap.UI.Features.Playlist.Views
{
    /// <summary>
    /// Lógica de interacción para PlaylistPage.xaml
    /// </summary>
    public partial class PlaylistPage : UserControl
    {
        private readonly TracksContextMenuService _tracksContextMenuService;
        private PlaylistViewModel _vm;

        public PlaylistPage()
        {
            InitializeComponent();
            _tracksContextMenuService = App.Services.GetRequiredService<TracksContextMenuService>();
            _vm = App.Services.GetRequiredService<PlaylistViewModel>();
            DataContext = _vm;
        }

        private void PlaylistTracksContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
            {
                return;
            }

            if (DataContext is not PlaylistViewModel vm)
            {
                return;
            }

            if (contextMenu.Items.OfType<TrackToPlaylistMenu>().FirstOrDefault() is TrackToPlaylistMenu playlistMenu)
            {
                playlistMenu.TrackIds = vm.SelectedTrackIds.ToList();
            }
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

        private void PlaylistRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var tracksView = ResolveTracksViewFromMenuSender(sender);
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            if (selected.Count == 0)
            {
                return;
            }

            if (DataContext is PlaylistViewModel vm)
            {
                if (vm.RemoveSelectedTracksCommand.CanExecute(selected))
                {
                    vm.RemoveSelectedTracksCommand.Execute(selected);
                }
            }
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
    }
}




