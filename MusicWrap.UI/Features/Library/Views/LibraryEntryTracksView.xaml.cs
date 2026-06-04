using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryTracksView : UserControl
    {
        private readonly IEditMetadataService _editMetadataService;
        private readonly ILibraryService _libraryCacheService;
        private bool _isCommandPaletteSubscribed;

        public LibraryEntryTracksView()
        {
            InitializeComponent();
            _editMetadataService = App.Services.GetRequiredService<IEditMetadataService>();
            _libraryCacheService = App.Services.GetRequiredService<ILibraryService>();

            Loaded += LibraryEntryTracksView_Loaded;
            Unloaded += LibraryEntryTracksView_Unloaded;
        }

        private void LibraryEntryTracksView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isCommandPaletteSubscribed)
            {
                return;
            }
            _isCommandPaletteSubscribed = true;
        }

        private void LibraryEntryTracksView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_isCommandPaletteSubscribed)
            {
                return;
            }
            _isCommandPaletteSubscribed = false;
        }

        //private void TracksContextMenu_Opened(object sender, RoutedEventArgs e)
        //{
        //    if (sender is not ContextMenu contextMenu)
        //    {
        //        return;
        //    }

        //    if (DataContext is not LibraryEntryDetailPanelViewModel vm)
        //    {
        //        return;
        //    }

        //    if (contextMenu.Items.OfType<TrackToPlaylistMenu>().FirstOrDefault() is TrackToPlaylistMenu playlistMenu)
        //    {
        //        playlistMenu.TrackIds = vm.SelectedTrackIds.ToList();
        //    }
        //}

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryTracksViewModel vm || vm.SelectedTrackIds.Count == 0)
            {
                return;
            }

            _editMetadataService.OpenMetadataWindow(vm.SelectedTrackIds);
        }

        private void ShowInFileExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryTracksViewModel vm || vm.SelectedTrackIds.Count == 0)
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
    }
}
