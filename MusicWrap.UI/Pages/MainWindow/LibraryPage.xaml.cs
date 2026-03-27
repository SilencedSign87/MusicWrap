using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.UI.Services;
using MusicWrap.UI.ViewModels;
using MusicWrap.UI.ViewModels.Library;
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

namespace MusicWrap.UI.Pages.MainWindow
{
    public partial class LibraryPage : UserControl
    {
        public LibraryViewModel vm;
        private readonly CommandPaletteViewModel _commandPaletteViewModel;
        private bool _isCommandPaletteSubscribed;
        private int _lastViewportWidth = -1;
        private DispatcherTimer? _resizeThrottleTimer;

        public LibraryPage()
        {
            InitializeComponent();

            vm = App.Services.GetRequiredService<LibraryViewModel>();
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
                TryRebuildRowsForCurrentViewport();
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
                _lastViewportWidth = -1;
                return;
            }

            TryRebuildRowsForCurrentViewport(true);

            // Defer once so the viewport is measured and rows are rebuilt with final width.
            Dispatcher.BeginInvoke(() =>
            {
                TryRebuildRowsForCurrentViewport(true);

                if (vm.IsAlbumView && vm.GridRows.Count > 0)
                    {
                    var firstAlbum = vm.GridRows[0].Albums.FirstOrDefault();
                    if (firstAlbum != null)
                    {
                        vm.ExpandAlbum(firstAlbum.Id, GetCurrentViewportWidth());
                    }
                }
                else
                {
                    vm.CollapseAlbum();
                }
            }, DispatcherPriority.Loaded);
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
                    var playerService = App.Services.GetRequiredService<IMusicPlayerService>();

                    var tracksViewModel = new AlbumTracksViewModel(
                        library,
                        playerService,
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
            if (e.NewSize.Width > 0 && vm.SelectedEntry != null)
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
            TryRebuildRowsForCurrentViewport(true);
        }

        private void AlbumsViewport_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // ViewportWidth can change when vertical scrollbar appears/disappears or after window state changes.
            if (e.ViewportWidthChange != 0)
            {
                TryRebuildRowsForCurrentViewport();
            }
        }

        private int GetCurrentViewportWidth()
        {
            if (AlbumsViewport.ViewportWidth > 0)
            {
                return (int)AlbumsViewport.ViewportWidth;
            }

            var padding = AlbumsViewport.Padding;
            var estimated = AlbumsViewport.ActualWidth - padding.Left - padding.Right;
            return Math.Max(1, (int)estimated);
        }

        private void RebuildRowsForCurrentViewport()
        {
            if (vm.SelectedEntry == null)
            {
                return;
            }

            vm.RebuildRows(GetCurrentViewportWidth());
        }

        private void TryRebuildRowsForCurrentViewport(bool force = false)
        {
            if (vm.SelectedEntry == null)
            {
                return;
            }

            int width = GetCurrentViewportWidth();
            if (width <= 0)
            {
                return;
            }

            if (!force && width == _lastViewportWidth)
            {
                return;
            }

            _lastViewportWidth = width;
            vm.RebuildRows(width);
        }
    }
}



