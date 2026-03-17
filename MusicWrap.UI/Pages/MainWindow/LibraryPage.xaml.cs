using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.UI.Services;
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

namespace MusicWrap.UI.Pages.MainWindow
{
    /// <summary>
    /// Lógica de interacción para LibraryPage.xaml
    /// </summary>
    public partial class LibraryPage : UserControl
    {
        public LibraryViewModel vm;

        public LibraryPage()
        {
            InitializeComponent();

            vm = App.Services.GetRequiredService<LibraryViewModel>();
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
            if (selected == null)
            {
                vm.CollapseAlbum();
                return;
            }
            bool isAllEntry = selected.Id == LibraryCacheService.AllEntryId && string.Equals(selected.Type == LibraryCacheService.AllEntryType, StringComparison.OrdinalIgnoreCase);

            if (isAllEntry) {
                vm.CollapseAlbum();
                return;
            }

            // For Album view, auto-expand the single album
            if (vm.IsAlbumView && vm.AlbumsForSelectedEntry.Count > 0)
            {
                // Find the first AlbumData in the collection
                var firstAlbum = vm.AlbumsForSelectedEntry
                    .OfType<LibraryViewModel.AlbumData>()
                    .FirstOrDefault();

                if (firstAlbum != null)
                {
                    vm.ExpandAlbum(firstAlbum.Id);
                }
            }
            else
            {
                // For other views, collapse any expanded tracks
                vm.CollapseAlbum();
            }
        }

        private void AlbumButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int albumId)
            {
                // Use the new ViewModel method to expand/collapse
                vm.ExpandAlbum(albumId, (int)AlbumWrapper.ActualWidth);
            }
        }

        private void CloseTracksButton_Click(object sender, RoutedEventArgs e)
        {
            vm.CollapseAlbum();
        }

        private void TracksContentPlaceholder_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ContentControl contentControl && contentControl.DataContext is LibraryViewModel.TrackListPlaceholder placeholder)
            {
                // Load tracks for the album with colors and player service
                var library = vm.GetLibrary();
                var playerService = App.Services.GetRequiredService<IMusicPlayerService>();

                var tracksViewModel = new AlbumTracksViewModel(
                    library,
                    playerService,
                    placeholder.AlbumId,
                    placeholder.DominantColor,
                    placeholder.ForegroundColor
                );
                var tracksPage = new AlbumTracksPage { DataContext = tracksViewModel };

                contentControl.Content = tracksPage;
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

        private void Album_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is int albumId)
            {
                // Use the new ViewModel method to expand/collapse
                vm.PlayAlbum(albumId);
            }
        }
    }
}



