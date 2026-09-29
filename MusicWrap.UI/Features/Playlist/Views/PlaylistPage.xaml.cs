using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Playlist.ViewModels;
using System.Windows;
using System.Windows.Controls;
using MusicWrap.UI.Shared.Services;

namespace MusicWrap.UI.Features.Playlist.Views
{
    public partial class PlaylistPage : UserControl, IDisposable
    {
        private PlaylistViewModel _vm;
        private readonly ContextMenuFactory _menuFactory;
        private bool _isDisposed = false;

        public PlaylistPage(PlaylistViewModel playlistViewModel, ContextMenuFactory menuFactory)
        {
            InitializeComponent();

            _vm = playlistViewModel;
            DataContext = _vm;

            _menuFactory = menuFactory;

            PlaylistTracksView.ContextMenu = _menuFactory.Create(
                PlaylistTracksView,
                ContextMenuType.Standard,
                extras: [
                    new ExtraMenuItem("Remove from playlist", "\uE74D",_vm.RemoveSelectedTracksCommand)
                ]);
        }

        private void PlaySelectedPlaylist_click(object sender, RoutedEventArgs e)
        {
            var entry = _vm.SelectedEntry;
            if (entry == null) return;

            if (_vm.PlayPlaylistCommand.CanExecute(entry.Id))
            {
                _vm.PlayPlaylistCommand.Execute(entry.Id);
            }

        }

        private void ShufflePlaylist_click(object sender, RoutedEventArgs e)
        {
            var entry = _vm.SelectedEntry;
            if (entry == null) return;
            if (_vm.ShufflePlaylistCommand.CanExecute(entry.Id))
            {
                _vm.ShufflePlaylistCommand.Execute(entry.Id);
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

        private void PlaylistGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: PlaylistEntry entry } target) return;

            var trackIds = _vm.ResolvePlaylistTrackIds(entry);

            var menu = _menuFactory.Create(
                () => [.. trackIds],
                ContextMenuType.Playback | ContextMenuType.AddToQueue,
                extras: [
                    new ExtraMenuItem("Edit playlist", "\xE70F", _vm.OpenPlaylistManagerCommand, entry.Id),
                    new ExtraMenuItem("Delete playlist", "\xE74D", _vm.DeletePlaylistCommand,entry.Id)
                    ]
                );

            e.Handled = true;
            target.ContextMenu = menu;
            menu.IsOpen = true;
        }
    }
}




