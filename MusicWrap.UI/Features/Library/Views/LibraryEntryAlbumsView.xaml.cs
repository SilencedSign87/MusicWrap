using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryEntryAlbumsView : UserControl
    {
        private readonly ILibraryCacheService _libraryCacheService;
        private DispatcherTimer? _resizeThrottleTimer;
        private LibraryViewModel? _subscribedViewModel;

        private int _lastColumns = -1;
        private const int MinTileWidth = 150;
        private const int Gutter = 16;
        private const int MinColumns = 1;

        public LibraryEntryAlbumsView()
        {
            InitializeComponent();

            _libraryCacheService = App.Services.GetRequiredService<ILibraryCacheService>();

            Loaded += LibraryEntryAlbumsView_Loaded;
            Unloaded += LibraryEntryAlbumsView_Unloaded;

            _resizeThrottleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _resizeThrottleTimer.Tick += (_, _) =>
            {
                _resizeThrottleTimer.Stop();
                UpdateColumnsForViewportWidth();
            };
        }

        public static readonly DependencyProperty LibraryViewModelProperty =
            DependencyProperty.Register(
                nameof(LibraryViewModel),
                typeof(LibraryViewModel),
                typeof(LibraryEntryAlbumsView),
                new PropertyMetadata(null, OnLibraryViewModelChanged));

        public LibraryViewModel? LibraryViewModel
        {
            get => (LibraryViewModel?)GetValue(LibraryViewModelProperty);
            set => SetValue(LibraryViewModelProperty, value);
        }

        private static void OnLibraryViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LibraryEntryAlbumsView view)
            {
                return;
            }

            if (ReferenceEquals(e.OldValue, e.NewValue))
            {
                return;
            }

            if (e.OldValue is LibraryViewModel oldVm)
            {
                oldVm.PropertyChanged -= view.ViewModel_PropertyChanged;
            }

            if (e.NewValue is LibraryViewModel newVm)
            {
                newVm.PropertyChanged += view.ViewModel_PropertyChanged;
                view._subscribedViewModel = newVm;
                view.HandleSelectionChanged();
            }
            else
            {
                view._subscribedViewModel = null;
            }
        }

        private void LibraryEntryAlbumsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_subscribedViewModel is null && LibraryViewModel is not null)
            {
                LibraryViewModel.PropertyChanged += ViewModel_PropertyChanged;
                _subscribedViewModel = LibraryViewModel;
            }

            UpdateColumnsForViewportWidth(true);
            HandleSelectionChanged();
        }

        private void LibraryEntryAlbumsView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_subscribedViewModel is not null)
            {
                _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _subscribedViewModel = null;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryViewModel.SelectedEntry))
            {
                HandleSelectionChanged();
            }
        }

        private void HandleSelectionChanged()
        {
            if (LibraryViewModel is null)
            {
                _lastColumns = -1;
                return;
            }

            var selected = LibraryViewModel.SelectedEntry;
            if (selected == null)
            {
                LibraryViewModel.CollapseAlbum();
                _lastColumns = -1;
                return;
            }

            // Keep collapsed by default when selecting a new entry.
            LibraryViewModel.CollapseAlbum();
        }

        private void AlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryViewModel is null)
            {
                return;
            }

            if (sender is Button button && button.DataContext is LibraryViewModel.AlbumData albumData)
            {
                LibraryViewModel.ExpandAlbum(albumData.Id, GetCurrentViewportWidth());
            }
        }

        private void CloseTracksButton_Click(object sender, RoutedEventArgs e)
        {
            LibraryViewModel?.CollapseAlbum();
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

                    if (LibraryViewModel is null)
                    {
                        contentControl.Content = null;
                        return;
                    }

                    var library = LibraryViewModel.GetLibrary();
                    var tracksContextMenuService = App.Services.GetRequiredService<TracksContextMenuService>();

                    var tracksViewModel = new AlbumTracksViewModel(
                        library,
                        _libraryCacheService,
                        tracksContextMenuService,
                        row.ExpandedAlbumId.Value,
                        row.ExpandedDominantColor,
                        row.ExpandedForegroundColor,
                        LibraryViewModel.ActiveSearchQuery,
                        LibraryViewModel.DetailSortMode
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
            if (e.NewSize.Width > 0 && _resizeThrottleTimer?.IsEnabled != true)
            {
                _resizeThrottleTimer?.Start();
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
            if (LibraryViewModel is null)
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
            LibraryViewModel.SetLayoutColumns(columns);
        }
    }
}
