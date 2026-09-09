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
        private readonly WindowManagerService _windowManager;
        private readonly ILibraryService _libraryCacheService;
        private bool _isCommandPaletteSubscribed;

        public LibraryEntryTracksView()
        {
            InitializeComponent();
            _windowManager = App.Services.GetRequiredService<WindowManagerService>();
            _libraryCacheService = App.Services.GetRequiredService<ILibraryService>();

            var menuFactory = App.Services.GetRequiredService<ContextMenuFactory>();
            EntryTracksView.ContextMenu = menuFactory.Create(EntryTracksView, ContextMenuType.Standard);

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
    }
}
