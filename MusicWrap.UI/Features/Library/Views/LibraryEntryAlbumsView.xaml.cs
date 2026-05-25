using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryAlbumsView : UserControl
    {
        private readonly DispatcherTimer? _resizeThrottleTimer;

        private int _lastColumns = -1;
        private const int MinTileWidth = 160;
        private const int Gutter = 0;
        private const int MinColumns = 1;

        public LibraryEntryAlbumsView()
        {
            InitializeComponent();

            _resizeThrottleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _resizeThrottleTimer.Tick += (_, _) =>
            {
                _resizeThrottleTimer.Stop();
                UpdateColumnsForViewportWidth();
            };
        }

        #region Dependency Properties
        private static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(LibraryEntryAlbumViewModel),
                typeof(LibraryEntryAlbumsView),
                new PropertyMetadata(null));
        public LibraryEntryAlbumViewModel ViewModel
        {
            get => (LibraryEntryAlbumViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        #endregion

        private void LibraryEntryAlbumsView_Loaded(object sender, RoutedEventArgs e)
        {

            UpdateColumnsForViewportWidth(true);
        }

        private void AlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            if (sender is Button button && button.DataContext is LibraryViewModel.AlbumData albumData)
            {
                ViewModel.ExpandAlbum(albumData.Id);
            }
        }

        private void CloseTracksButton_Click(object sender, RoutedEventArgs e)
        {
                ViewModel?.CollapseAlbum();
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

                    if (ViewModel is null)
                    {
                        contentControl.Content = null;
                        return;
                    }
                    var libraryCacheService = ViewModel.LibraryCache;
                    var tracksContextMenuService = App.Services.GetRequiredService<TracksContextMenuService>();
                    var searchService = App.Services.GetRequiredService<SearchService>();

                    var tracksViewModel = new AlbumTracksViewModel(
                        libraryCacheService,
                        searchService,
                        tracksContextMenuService,
                        row.ExpandedAlbumId.Value,
                        row.ExpandedDominantColor,
                        row.ExpandedForegroundColor,
                        "",
                        ViewModel.SortMode ?? TrackSortMode.Year
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

        private void AlbumsViewport_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateColumnsForViewportWidth(true);
        }

        private void AlbumsViewport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is not null)
            {
                listBox.SelectedItem = null;
            }
        }

        private int GetCurrentViewportWidth()
        {
            var padding = AlbumsViewport.Padding;
            var estimated = AlbumsViewport.ActualWidth - padding.Left - padding.Right;
            return Math.Max(1, (int)estimated);
        }

        private int CalculateColumns(int width)
        {
            int tileFootprint = MinTileWidth + Gutter;
            return Math.Max(MinColumns, Math.Max(1, width) / tileFootprint);
        }

        private void UpdateColumnsForViewportWidth(bool force = false)
        {
            if (ViewModel is null)
            {
                return;
            }

            int width = GetCurrentViewportWidth();
            if (width <= 0)
            {
                return;
            }

            int columns = CalculateColumns(width);
            if (!force && columns == _lastColumns)
            {
                return;
            }

            _lastColumns = columns;
            ViewModel.SetLayoutColumns(columns);
        }
    }
}
