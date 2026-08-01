using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Shared.Services;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryTracksView : UserControl
    {
        private readonly WindowManager _windowManager;
        private readonly ILibraryService _libraryCacheService;
        private bool _isCommandPaletteSubscribed;

        public LibraryEntryTracksView()
        {
            InitializeComponent();
            _windowManager = App.Services.GetRequiredService<WindowManager>();
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

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;
            if (DataContext is not LibraryEntryTracksViewModel vm)
                return;

            TrackToPlaylistMenu.AttachTo(contextMenu, index: 4);
            TrackToPlaylistMenu.Shared.TrackIds = vm.SelectedTrackIds.ToList();
        }
    }
}
