using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Library.Components
{
    /// <summary>
    /// Lógica de interacción para AlbumPage.xaml
    /// </summary>
    public partial class AlbumPage : UserControl
    {
        private readonly ILibraryService _libraryService;
        private readonly ContextMenuFactory _menuFactory;
        private int[] TracksId = [];
        public AlbumPage()
        {
            InitializeComponent();

            _libraryService = App.Services.GetRequiredService<ILibraryService>();
            _menuFactory = App.Services.GetRequiredService<ContextMenuFactory>();

            Loaded += AlbumPage_Loaded;
        }

        private void AlbumPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel.AlbumData data)
            {
                TracksId = GetAllAlbumTracksId(data.Id);

                RootBorder.ContextMenu = _menuFactory.Create(
                    () => [.. TracksId],
                    ContextMenuType.Playback | ContextMenuType.AddToQueue | ContextMenuType.AddToPlaylist | ContextMenuType.TrackProperties,
                    TracksId
                    );
            }
        }

        private int[] GetAllAlbumTracksId(int albumId)
        {
            return _libraryService.GetTrackQueueForAlbum(albumId).ToArray();
        }
    }
}




