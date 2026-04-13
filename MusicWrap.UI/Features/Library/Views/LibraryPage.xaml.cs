using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.ViewModels;
using MusicWrap.UI.Features.Library.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryPage : UserControl
    {
        public LibraryViewModel vm;
        private readonly CommandPaletteViewModel _commandPaletteViewModel;
        private readonly ILibraryCacheService _libraryCacheService;
        private bool _isCommandPaletteSubscribed;
        private DispatcherTimer? _resizeThrottleTimer;

        private int _lastColumns = -1;
        private const int MinTileWidth = 150;
        private const int Gutter = 16;
        private const int MinColumns = 1;

        public LibraryPage()
        {
            InitializeComponent();

            vm = App.Services.GetRequiredService<LibraryViewModel>();
            _libraryCacheService = App.Services.GetRequiredService<ILibraryCacheService>();
            DataContext = vm;

            _commandPaletteViewModel = App.Services.GetRequiredService<CommandPaletteViewModel>();
            Loaded += LibraryPage_Loaded;
            Unloaded += LibraryPage_Unloaded;

            // Subscribe to property changes
            vm.PropertyChanged += Vm_PropertyChanged;

            // Initialize throttle timer (50ms to reduce excessive recalcs during drag resize)
            _resizeThrottleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _resizeThrottleTimer.Tick += (_, _) =>
            {
                _resizeThrottleTimer.Stop();
                UpdateColumnsForViewportWidth();
            };
        }

        private void LibraryPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isCommandPaletteSubscribed)
                return;

            _commandPaletteViewModel.QuerySubmitted += CommandPaletteViewModel_QuerySubmitted;
            _isCommandPaletteSubscribed = true;
        }

        private void LibraryPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_isCommandPaletteSubscribed)
                return;

            _commandPaletteViewModel.QuerySubmitted -= CommandPaletteViewModel_QuerySubmitted;
            _isCommandPaletteSubscribed = false;
        }

        private void CommandPaletteViewModel_QuerySubmitted(object? sender, string query)
        {
            if (!IsVisible)
                return;

            vm.ApplySearchFilter(query);
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(vm.SelectedEntry))
            {
                HandleSelectionChanged();
            }
        }

        private void HandleSelectionChanged()
        {
            var selected = vm.SelectedEntry;
            if (selected == null)
            {
                vm.CollapseAlbum();
                _lastColumns = -1;
                return;
            }

            // Keep collapsed by default. Album expansion is user-driven.
            vm.CollapseAlbum();
        }

        private void AlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is LibraryViewModel.AlbumData albumData)
            {
                // Use the new ViewModel method to expand/collapse
                vm.ExpandAlbum(albumData.Id, GetCurrentViewportWidth());
            }
        }

        private void CloseTracksButton_Click(object sender, RoutedEventArgs e)
        {
            vm.CollapseAlbum();
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

                    var library = vm.GetLibrary();
                    var libCache = App.Services.GetRequiredService<ILibraryCacheService>();
                    var tracksContextMenuService = App.Services.GetRequiredService<TracksContextMenuService>();

                    var tracksViewModel = new AlbumTracksViewModel(
                        library,
                        libCache,
                        tracksContextMenuService,
                        row.ExpandedAlbumId.Value,
                        row.ExpandedDominantColor,
                        row.ExpandedForegroundColor,
                        vm.ActiveSearchQuery
                    );
                    var tracksPage = new AlbumTracksPage { DataContext = tracksViewModel };
                    contentControl.Content = tracksPage;
                }

                RefreshTracksContent();

                System.ComponentModel.PropertyChangedEventHandler onRowChanged = (_, args) =>
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

        private void EntriesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                char keyChar = (char)('A' + (e.Key - Key.A));
                ScrollToGroup(keyChar.ToString());
                e.Handled = true;
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                ScrollToGroup("#");
                e.Handled = true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                ScrollToGroup("#");
                e.Handled = true;
            }
        }

        private void ScrollToGroup(string groupKey)
        {
            var view = EntriesListView.Items;

            if (view.Groups == null || view.Groups.Count == 0)
                return;

            foreach (CollectionViewGroup group in view.Groups)
            {
                if (group.Name?.ToString()?.Equals(groupKey, StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (group.ItemCount > 0)
                    {
                        var firstItem = group.Items[0];

                        EntriesListView.ScrollIntoView(firstItem);

                        EntriesListView.SelectedItem = firstItem;

                        break;
                    }
                }
            }
        }

        private void AlbumsViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                // Throttle resize events with 50ms timer: restart if already running
                // (multiple rapid SizeChanged events will only result in one recalc after 50ms quiet)
                if (_resizeThrottleTimer?.IsEnabled != true)
                {
                    _resizeThrottleTimer?.Start();
                }
            }
        }

        private void AlbumsViewport_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateColumnsForViewportWidth(true);
        }

        private void AlbumsViewport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // This ListBox is used only as a virtualizing host; row selection is intentionally disabled.
            if (sender is ListBox listBox && listBox.SelectedItem is not null)
            {
                listBox.SelectedItem = null;
            }
        }

        private int GetCurrentViewportWidth()
        {
            var estimated = ActualWidth - 280;
            return Math.Max(1, (int)estimated);
        }

        private int CalculateColumns(int width)
        {
            int tileFootprint = MinTileWidth + Gutter;
            return Math.Max(MinColumns, Math.Max(1, width) / tileFootprint);
        }
        private void UpdateColumnsForViewportWidth(bool force = false)
        {
            int width = GetCurrentViewportWidth();
            if (width <= 0) return;
            int columns = CalculateColumns(width);
            if (!force && columns == _lastColumns) return;

            _lastColumns = columns;
            vm.SetLayoutColumns(columns);
        }

        private void LibraryContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;

            // Get the entry from the visual tree (the Grid containing this context menu)
            var grid = contextMenu.PlacementTarget as Grid;
            if (grid?.DataContext is not MusicWrap.UI.Features.Library.Services.LibraryEntry entry)
                return;

            // Get track IDs based on entry type
            var library = vm.GetLibrary();
            List<int> trackIds = [];

            switch (entry.Type)
            {
                case "Album":
                    trackIds = [.. library.Tracks
                        .Where(t => t.AlbumId == entry.Id)
                        .OrderBy(t => t.Disk)
                        .ThenBy(t => t.TrackNumber)
                        .ThenBy(t => t.Title)
                        .Select(t => t.Id)];
                    break;

                case "Artist":
                    var artistAlbumIds = library.Albums
                        .Where(a => a.ArtistIds.Contains(entry.Id))
                        .Select(a => a.Id)
                        .ToHashSet();
                    trackIds = [.. library.Tracks
                        .Where(t => artistAlbumIds.Contains(t.AlbumId))
                        .OrderBy(t => t.Disk)
                        .ThenBy(t => t.TrackNumber)
                        .ThenBy(t => t.Title)
                        .Select(t => t.Id)];
                    break;

                case "Genre":
                    trackIds = [.. library.Tracks
                        .Where(t => t.GenreIds.Contains(entry.Id))
                        .OrderBy(t => t.TrackNumber)
                        .ThenBy(t => t.Title)
                        .Select(t => t.Id)];
                    break;

                case "Decade":
                    if (int.TryParse(entry.Title.TrimEnd('s'), out int decade))
                    {
                        trackIds = [.. library.Tracks
                            .Where(t => (library.Albums.FirstOrDefault(a => a.Id == t.AlbumId)?.Year / 10) * 10 == decade)
                            .OrderBy(t => t.TrackNumber)
                            .ThenBy(t => t.Title)
                            .Select(t => t.Id)];
                    }
                    break;
            }

            // Find and set track IDs on the TrackToPlaylistMenu in the context menu
            var trackToPlaylistMenu = contextMenu.Items.OfType<MusicWrap.UI.Controls.Models.TrackToPlaylistMenu>().FirstOrDefault();
            if (trackToPlaylistMenu != null)
            {
                trackToPlaylistMenu.TrackIds = trackIds;
            }
        }
    }
}







