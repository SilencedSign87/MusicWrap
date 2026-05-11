using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryTracksView : UserControl
    {
        private readonly IEditMetadataService _editMetadataService;
        private readonly ILibraryService _libraryService;
        private readonly CommandPaletteViewModel _commandPaletteViewModel;
        private bool _isCommandPaletteSubscribed;

        public LibraryEntryTracksView()
        {
            InitializeComponent();
            _editMetadataService = App.Services.GetRequiredService<IEditMetadataService>();
            _libraryService = App.Services.GetRequiredService<ILibraryService>();
            _commandPaletteViewModel = App.Services.GetRequiredService<CommandPaletteViewModel>();

            Loaded += LibraryEntryTracksView_Loaded;
            Unloaded += LibraryEntryTracksView_Unloaded;
        }

        private void LibraryEntryTracksView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isCommandPaletteSubscribed)
            {
                return;
            }

            _commandPaletteViewModel.QuerySubmitted += CommandPaletteViewModel_QuerySubmitted;
            _isCommandPaletteSubscribed = true;
        }

        private void LibraryEntryTracksView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_isCommandPaletteSubscribed)
            {
                return;
            }

            _commandPaletteViewModel.QuerySubmitted -= CommandPaletteViewModel_QuerySubmitted;
            _isCommandPaletteSubscribed = false;
        }

        private void CommandPaletteViewModel_QuerySubmitted(object? sender, string query)
        {
            if (!IsVisible)
            {
                return;
            }

            if (DataContext is not LibraryEntryDetailPanelViewModel vm)
            {
                return;
            }

            vm.TrackSearchQuery = query?.Trim() ?? string.Empty;
        }

        private void TracksContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
            {
                return;
            }

            if (DataContext is not LibraryEntryDetailPanelViewModel vm)
            {
                return;
            }

            if (contextMenu.Items.OfType<TrackToPlaylistMenu>().FirstOrDefault() is TrackToPlaylistMenu playlistMenu)
            {
                playlistMenu.TrackIds = vm.SelectedTrackIds.ToList();
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryDetailPanelViewModel vm || vm.SelectedTrackIds.Count == 0)
            {
                return;
            }

            _editMetadataService.OpenMetadataWindow(vm.SelectedTrackIds);
        }

        private void ShowInFileExplorerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryDetailPanelViewModel vm || vm.SelectedTrackIds.Count == 0)
            {
                return;
            }

            var track = _libraryService.GetTrackById(vm.SelectedTrackIds[0]);
            if (track is null || string.IsNullOrWhiteSpace(track.FilePath))
            {
                return;
            }

            if (!File.Exists(track.FilePath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{track.FilePath}\"")
            {
                UseShellExecute = true
            });
        }
    }
}
