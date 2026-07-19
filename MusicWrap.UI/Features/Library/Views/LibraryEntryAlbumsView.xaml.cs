using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Search;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryAlbumsView : UserControl
    {
        private const int MinTileWidth = 160;
        private const int Gutter = 0;
        private const int MinColumns = 1;

        public LibraryEntryAlbumsView()
        {
            InitializeComponent();
        }

        private void AlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryAlbumViewModel viewModel)
            {
                return;
            }

            if (sender is Button button && button.DataContext is LibraryViewModel.AlbumData albumData)
            {
                viewModel.ExpandAlbum(albumData.Id);
            }
        }

        private void CloseTracksButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryEntryAlbumViewModel viewModel)
            {
                return;

            }
            viewModel.CollapseAlbum();
        }

        private void TracksContentPlaceholder_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ContentControl contentControl && contentControl.DataContext is LibraryViewModel.AlbumGridRowModel row)
            {
                void RefreshTracksContent()
                {
                    if (row.ExpandedAlbumId == null)
                    {
                        contentControl.Content = null;
                        return;
                    }

                    if (DataContext is not LibraryEntryAlbumViewModel viewModel)
                    {
                        contentControl.Content = null;
                        return;
                    }
                    var libraryCacheService = viewModel.LibraryCache;
                    var tracksContextMenuService = App.Services.GetRequiredService<TrackActionService>();
                    var searchService = App.Services.GetRequiredService<SearchService>();

                    int[]? filteredTrackIds = null;
                    var entry = viewModel.SelectedEntry;
                    if (entry is not null)
                    {
                        filteredTrackIds = libraryCacheService.GetTrackIdsForEntryAlbum(
                            entry, row.ExpandedAlbumId.Value, useSearchQuery: true);
                    }

                    var tracksViewModel = new AlbumTracksViewModel(
                        libraryCacheService,
                        searchService,
                        tracksContextMenuService,
                        row.ExpandedAlbumId.Value,
                        row.ExpandedDominantColor,
                        row.ExpandedForegroundColor,
                        "",
                        viewModel.SortMode ?? TrackSortMode.Year,
                        filteredTrackIds
                    );
                    var tracksPage = new AlbumTracksPage { DataContext = tracksViewModel };
                    contentControl.Content = tracksPage;
                }

                RefreshTracksContent();

                PropertyChangedEventHandler onRowChanged = (_, args) =>
                {
                    if (args.PropertyName == nameof(LibraryViewModel.AlbumGridRowModel.ExpandedAlbumId) ||
                        args.PropertyName == nameof(LibraryViewModel.AlbumGridRowModel.ExpandedDominantColor) ||
                        args.PropertyName == nameof(LibraryViewModel.AlbumGridRowModel.ExpandedForegroundColor))
                    {
                        RefreshTracksContent();
                    }
                };

                row.PropertyChanged += onRowChanged;
                contentControl.Unloaded += (_, __) => row.PropertyChanged -= onRowChanged;
            }
        }

        private void AlbumsViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                UpdateColumnsForViewportWidth();
            }
        }

        private void AlbumsViewport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is not null)
            {
                listBox.SelectedItem = null;
            }
        }

        private int GetCurrentViewportWidth() => Math.Max(1, (int)AlbumsViewport.ActualWidth);

        private int CalculateColumns(int width) => Math.Max(MinColumns, Math.Max(1, width)/(MinTileWidth + Gutter));

        private void UpdateColumnsForViewportWidth(bool force = false)
        {
            if (DataContext is not LibraryEntryAlbumViewModel viewModel)
                return;
            
            int width = GetCurrentViewportWidth();

            if (width <= 0)
                return;

            int columns = CalculateColumns(width);
            
            viewModel.LayoutColumns = columns;
        }
    }
}
