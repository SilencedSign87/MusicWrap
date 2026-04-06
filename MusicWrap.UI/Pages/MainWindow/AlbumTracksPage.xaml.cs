using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data.Library;
using MusicWrap.UI.ViewModels.Library;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para AlbumTracksPage.xaml
    /// </summary>
    public partial class AlbumTracksPage : UserControl
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly MusicLibrary _library;

        public AlbumTracksPage()
        {
            InitializeComponent();
            _musicPlayerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _library = App.Services.GetRequiredService<MusicLibrary>();
        }

        private void PlayPauseAlbum_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AlbumTracksViewModel vm)
            {
                return;
            }

            var trackIds = _library.Tracks
                .Where(t => t.AlbumId == vm.AlbumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)
                .ToList();

            if (trackIds.Count == 0)
            {
                return;
            }

            _musicPlayerService.SetQueue(trackIds);
            _musicPlayerService.PlayTrack(trackIds[0]);
        }
    }
}
