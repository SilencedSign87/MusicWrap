using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Playback.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Playback.Views
{
    /// <summary>
    /// Lógica de interacción para QueueListPage.xaml
    /// </summary>
    public partial class QueueListPage : UserControl
    {
        private readonly TracksContextMenuService _tracksContextMenuService;

        public QueueListPage()
        {
            InitializeComponent();
            _tracksContextMenuService = App.Services.GetRequiredService<TracksContextMenuService>();
            DataContext = App.Services.GetRequiredService<QueueViewModel>();
        }

        private void QueueTracksContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
            {
                return;
            }

            var tracksView = contextMenu.PlacementTarget as TracksView;
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            if (selected.Count == 0 && contextMenu.Items.OfType<TrackToPlaylistMenu>().FirstOrDefault() is TrackToPlaylistMenu emptyMenu)
            {
                emptyMenu.TrackIds = [];
                return;
            }

            if (contextMenu.Items.OfType<TrackToPlaylistMenu>().FirstOrDefault() is TrackToPlaylistMenu playlistMenu)
            {
                playlistMenu.TrackIds = selected;
            }
        }

        private void QueuePlayNow_Click(object sender, RoutedEventArgs e)
        {
            var tracksView = ResolveTracksViewFromMenuSender(sender);
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            _tracksContextMenuService.PlayNowInQueue(selected);
        }

        private void QueuePlayNext_Click(object sender, RoutedEventArgs e)
        {
            var tracksView = ResolveTracksViewFromMenuSender(sender);
            if (tracksView == null)
            {
                return;
            }

            var selected = tracksView.GetSelectedTrackIds();
            _tracksContextMenuService.PlayNextInQueue(selected);
        }

        private void QueueRemove_Click(object sender, RoutedEventArgs e)
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

            if (DataContext is QueueViewModel vm)
            {
                vm.RemoveSelectedTracksFromQueue(selected);
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
    }
}




