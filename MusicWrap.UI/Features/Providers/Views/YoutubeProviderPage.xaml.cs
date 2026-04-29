using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using MusicWrap.UI.Features.Providers.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System.Linq;

namespace MusicWrap.UI.Features.Providers.Views
{
    /// <summary>
    /// Lógica de interacción para YoutubeProviderPage.xaml
    /// </summary>
    public partial class YoutubeProviderPage : UserControl
    {
        private readonly YoutubeProviderViewModel _viewModel;
        private readonly CommandPaletteViewModel _commandPaletteViewModel;
        private IndexingWindow? _indexingWindow;
        private bool _isSubscribed;
        private bool _isClearingTrackSelections;

        public event EventHandler? BackRequested;

        public YoutubeProviderPage()
        {
            InitializeComponent();

            _viewModel = App.Services.GetRequiredService<YoutubeProviderViewModel>();
            _commandPaletteViewModel = App.Services.GetRequiredService<CommandPaletteViewModel>();

            DataContext = _viewModel;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribed)
            {
                return;
            }

            _commandPaletteViewModel.QuerySubmitted += OnQuerySubmitted;
            _isSubscribed = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                return;
            }

            _commandPaletteViewModel.QuerySubmitted -= OnQuerySubmitted;
            _isSubscribed = false;
        }

        private async void OnQuerySubmitted(object? sender, string query)
        {
            if (!IsVisible)
            {
                return;
            }

            await _viewModel.SearchAsync(query);
        }

        private async void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox listBox)
            {
                return;
            }

            if (listBox.SelectedItem is YoutubeSearchLeafNode selected)
            {
                await _viewModel.SelectItemAsync(selected);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private void TrackListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DetailsScrollViewer is null)
            {
                return;
            }

            e.Handled = true;
            var parentEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            DetailsScrollViewer.RaiseEvent(parentEvent);
        }

        private void TrackListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isClearingTrackSelections)
            {
                return;
            }

            if (sender is not ListBox sourceListBox || sourceListBox.SelectedItems.Count == 0)
            {
                return;
            }

            if (DetailsScrollViewer is null)
            {
                return;
            }

            _isClearingTrackSelections = true;
            try
            {
                foreach (var listBox in FindVisualDescendants<ListBox>(DetailsScrollViewer))
                {
                    if (ReferenceEquals(listBox, sourceListBox))
                    {
                        continue;
                    }

                    if (listBox.SelectedItems.Count > 0)
                    {
                        listBox.UnselectAll();
                    }
                }
            }
            finally
            {
                _isClearingTrackSelections = false;
            }
        }

        private void GroupAddToIndexing_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not YoutubeDetailGroupNode group)
            {
                return;
            }

            int added = _viewModel.AddGroupToIndexing(group);
            if (added > 0)
            {
                ShowIndexingWindow();
            }
        }

        private void TrackAddToIndexing_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.DataContext is not YoutubeDetailTrackNode track)
            {
                return;
            }

            var group = menuItem.CommandParameter as YoutubeDetailGroupNode;
            int added = 0;

            if (menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget is DependencyObject placementTarget)
            {
                var listBox = FindVisualAncestor<ListBox>(placementTarget);
                var selectedTracks = listBox?.SelectedItems
                    .OfType<YoutubeDetailTrackNode>()
                    .ToArray();

                if (selectedTracks is not null && selectedTracks.Length > 1)
                {
                    added = _viewModel.AddTracksToIndexing(selectedTracks, group);
                }
            }

            if (added == 0 && _viewModel.AddTrackToIndexing(track, group))
            {
                added = 1;
            }

            if (added > 0)
            {
                ShowIndexingWindow();
            }
        }

        private void ShowIndexingWindow()
        {
            if (_indexingWindow is not null)
            {
                _indexingWindow.Activate();
                return;
            }

            _indexingWindow = App.Services.GetRequiredService<IndexingWindow>();
            _indexingWindow.Owner = Window.GetWindow(this);
            _indexingWindow.Closed += (_, _) => _indexingWindow = null;
            _indexingWindow.Show();
            _indexingWindow.Activate();
        }

        private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is null)
            {
                yield break;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in FindVisualDescendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static T? FindVisualAncestor<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child is not null)
            {
                if (child is T typed)
                {
                    return typed;
                }

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }
    }
}



