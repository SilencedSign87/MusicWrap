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
        //private readonly TracksContextMenuService _tracksContextMenuService;

        public QueueListPage(QueueViewModel queueViewModel)
        {
            InitializeComponent();
            //_tracksContextMenuService = tracksContextMenuService;
            DataContext = queueViewModel;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;
            if (DataContext is not QueueViewModel vm)
                return;

            TrackToPlaylistMenu.AttachTo(contextMenu, index: 3);
            TrackToPlaylistMenu.Shared.TrackIds = vm.SelectedTrackIds?.ToList();
        }
    }
}




