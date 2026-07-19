using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library;
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
    }
}
