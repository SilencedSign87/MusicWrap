using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Library.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;

namespace MusicWrap.UI.Features.Library.Views
{
    public partial class LibraryPage : UserControl, IDisposable
    {
        public LibraryViewModel vm;
        private readonly ILibraryService _libraryCacheService;
        private bool _disposed = false;

        public LibraryPage(LibraryViewModel viewmodel, ILibraryService libraryService)
        {
            InitializeComponent();

            vm = viewmodel;
            _libraryCacheService = libraryService;
            DataContext = vm;

            // Subscribe to property changes
            vm.PropertyChanged += Vm_PropertyChanged;

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
            if (selected is not null && EntriesListView.SelectedItem != selected)
            {
                EntriesListView.SelectedItem = selected;
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

        private void LibraryContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;

            // Get the entry from the visual tree (the Grid containing this context menu)
            var grid = contextMenu.PlacementTarget as Grid;
            if (grid?.DataContext is not LibraryEntry entry)
                return;

            // Get track IDs based on entry type
            List<int> trackIds = _libraryCacheService.GetTrackIdsForEntry(entry).ToList();

            // Find and set track IDs on the TrackToPlaylistMenu in the context menu
            var trackToPlaylistMenu = contextMenu.Items.OfType<MusicWrap.UI.Controls.Models.TrackToPlaylistMenu>().FirstOrDefault();
            if (trackToPlaylistMenu != null)
            {
                trackToPlaylistMenu.TrackIds = trackIds;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            vm.PropertyChanged -= Vm_PropertyChanged;
            vm.Dispose();

            DataContext = null;
        }

        private void EntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView lv && lv.SelectedItem is LibraryEntry entry && vm.SetSelectionCommand.CanExecute(entry))
            {
                vm.SetSelectionCommand.Execute(entry);
            }
        }
    }
}







