using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Features.Library.Services;
using MusicWrap.UI.Features.Library.ViewModels;
using MusicWrap.UI.Services;
using MusicWrap.UI.Shared.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryPage : UserControl, IDisposable
    {
        public LibraryViewModel vm;
        private readonly ILibraryService _libraryService;
        private readonly ContextMenuFactory _menuFactory;
        private readonly TrackActionService _trackActions;

        private MenuItem? _playlistMenu;

        private  bool _disposed;

        public LibraryPage(LibraryViewModel viewmodel, ILibraryService libraryService, ContextMenuFactory menuFactory, TrackActionService trackActions)
        {
            InitializeComponent();

            _libraryService = libraryService;
            _menuFactory = menuFactory;
            _trackActions = trackActions;


            vm = viewmodel;
            DataContext = vm;
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

        private void LibraryContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu) return;

            var grid = contextMenu.PlacementTarget as Grid;
            
            if (grid?.DataContext is not LibraryEntry entry) return;

            var trackIds = _libraryService.GetTrackIdsForEntry(entry).ToList();

            _playlistMenu ??= _menuFactory.CreateAddToPlaylistMenuItem(() => [.. trackIds]);

            if (_playlistMenu.Parent is ItemsControl parent)
                parent.Items.Remove(_playlistMenu);

            contextMenu.Items.Add(_playlistMenu);

        }
        private void EntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView lv && lv.SelectedItem is LibraryEntry entry)
            {
                // scroll to the selected item
                lv.ScrollIntoView(entry);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            vm.Dispose();
        }
    }
}







