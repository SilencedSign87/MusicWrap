using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;

namespace MusicWrap.UI.Controls.Models
{
    /// <summary>
    /// Lógica de interacción para TracksView.xaml
    /// </summary>
    public partial class TracksView : UserControl
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly IPlaylistManagerCoordinator _playlistCoordinator;
        private readonly IEditMetadataService _editMetadataService;
        private readonly MusicLibrary _library;

        public TracksView()
        {
            InitializeComponent();
            _musicPlayerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _playlistCoordinator = App.Services.GetRequiredService<IPlaylistManagerCoordinator>();
            _editMetadataService = App.Services.GetRequiredService<IEditMetadataService>();
            _library = App.Services.GetRequiredService<MusicLibrary>();

        }

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable<TrackRowItem>),
                typeof(TracksView),
                new PropertyMetadata(null));

        public IEnumerable<TrackRowItem>? ItemsSource
        {
            get => (IEnumerable<TrackRowItem>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty VisualModeProperty =
            DependencyProperty.Register(
                nameof(VisualMode),
                typeof(TrackVisualMode),
                typeof(TracksView),
                new PropertyMetadata(TrackVisualMode.CompactNoCover));

        public TrackVisualMode VisualMode
        {
            get => (TrackVisualMode)GetValue(VisualModeProperty);
            set => SetValue(VisualModeProperty, value);
        }

        public static readonly DependencyProperty IndexModeProperty =
            DependencyProperty.Register(
                nameof(IndexMode),
                typeof(TrackIndexDisplayMode),
                typeof(TracksView),
                new PropertyMetadata(TrackIndexDisplayMode.TrackNumber));

        public TrackIndexDisplayMode IndexMode
        {
            get => (TrackIndexDisplayMode)GetValue(IndexModeProperty);
            set => SetValue(IndexModeProperty, value);
        }

        public static readonly DependencyProperty UseColumnFlowPanelProperty =
            DependencyProperty.Register(
                nameof(UseColumnFlowPanel),
                typeof(bool),
                typeof(TracksView),
                new PropertyMetadata(false));

        public bool UseColumnFlowPanel
        {
            get => (bool)GetValue(UseColumnFlowPanelProperty);
            set => SetValue(UseColumnFlowPanelProperty, value);
        }

        public static readonly DependencyProperty ColumnFlowColumnsProperty =
            DependencyProperty.Register(
                nameof(ColumnFlowColumns),
                typeof(int),
                typeof(TracksView),
                new PropertyMetadata(2));

        public int ColumnFlowColumns
        {
            get => (int)GetValue(ColumnFlowColumnsProperty);
            set => SetValue(ColumnFlowColumnsProperty, value);
        }

        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(
                nameof(SelectionMode),
                typeof(SelectionMode),
                typeof(TracksView),
                new PropertyMetadata(SelectionMode.Extended));

        public SelectionMode SelectionMode
        {
            get => (SelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public static readonly DependencyProperty SelectedTrackIdsProperty =
            DependencyProperty.Register(
                nameof(SelectedTrackIds),
                typeof(IList<int>),
                typeof(TracksView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public IList<int>? SelectedTrackIds
        {
            get => (IList<int>?)GetValue(SelectedTrackIdsProperty);
            set => SetValue(SelectedTrackIdsProperty, value);
        }

        #endregion

        private void TracksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedTrackIds == null)
            {
                return;
            }

            SelectedTrackIds.Clear();

            foreach (var row in TracksList.SelectedItems.OfType<TrackRowItem>())
            {
                SelectedTrackIds.Add(row.Id);
            }
        }
        private void TracksList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            InvokeSelectedTrack();
        }

        private void TracksList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InvokeSelectedTrack();
                e.Handled = true;
            }
        }

        private void InvokeSelectedTrack()
        {
            if (TracksList.SelectedItem is not TrackRowItem row)
            {
                return;
            }

            _musicPlayerService.SetQueue([row.Id]);
            _musicPlayerService.PlayTrack(row.Id);
        }

        private void TracksList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // avoid scroll lock
            e.Handled = true;

            var forwarded = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = this
            };

            RaiseEvent(forwarded);
        }

        private void TracksList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listBoxItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (listBoxItem == null)
            {
                return;
            }

            if (!listBoxItem.IsSelected)
            {
                if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == ModifierKeys.None)
                {
                    TracksList.SelectedItems.Clear();
                }

                listBoxItem.IsSelected = true;
            }
        }

        private void PlayNowButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTrackIds = GetSelectedTrackIds();
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            _musicPlayerService.SetQueue(selectedTrackIds);
            _musicPlayerService.PlayTrack(selectedTrackIds[0]);
        }

        private void PlayNextButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTrackIds = GetSelectedTrackIds();
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            var currentQueue = _musicPlayerService.GetQueue() ?? [];
            var newQueue = new List<int>();
            bool inserted = false;

            foreach (var trackId in currentQueue)
            {
                newQueue.Add(trackId);
                if (!inserted && trackId == _musicPlayerService.CurrentTrackId)
                {
                    newQueue.AddRange(selectedTrackIds);
                    inserted = true;
                }
            }

            if (!inserted)
            {
                newQueue.AddRange(selectedTrackIds);
            }

            _musicPlayerService.SetQueue(newQueue, true);
        }

        private void AddQueueButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTrackIds = GetSelectedTrackIds();
            foreach (var trackId in selectedTrackIds)
            {
                _musicPlayerService.AddToQueue(trackId);
            }
        }

        private void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var selectedTrackIds = GetSelectedTrackIds();
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            _playlistCoordinator.AddToManager(selectedTrackIds.ToArray());
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTrackIds = GetSelectedTrackIds();
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            _editMetadataService.OpenMetadataWindow(selectedTrackIds[0], MetadataEntityType.Track);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // Not supported yet for library tracks.
        }

        private void ShowExternal_Click(object sender, RoutedEventArgs e)
        {
            var selectedTrackIds = GetSelectedTrackIds();
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            var path = _library.Tracks.FirstOrDefault(t => t.Id == selectedTrackIds[0])?.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                }
            );
        }

        private List<int> GetSelectedTrackIds()
        {
            var selectedTrackIds = TracksList.SelectedItems
                .OfType<TrackRowItem>()
                .Select(track => track.Id)
                .ToList();

            if (SelectedTrackIds is null)
            {
                return selectedTrackIds;
            }

            SelectedTrackIds.Clear();
            foreach (var trackId in selectedTrackIds)
            {
                SelectedTrackIds.Add(trackId);
            }

            return selectedTrackIds;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t)
                {
                    return t;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
