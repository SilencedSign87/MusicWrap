using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using ICommand = System.Windows.Input.ICommand;

namespace MusicWrap.UI.Controls.Models
{
    /// <summary>
    /// Lógica de interacción para TracksView.xaml
    /// </summary>
    public partial class TracksView : UserControl
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly IEditMetadataService _editMetadataService;
        private readonly MusicLibrary _library;
        private Point _dragStartPoint;
        private TrackRowItem? _draggedItem;
        private IEnumerable<TrackRowItem>? _itemsSource;
        private INotifyCollectionChanged? _itemsSourceCollection;
        private bool _playerEventsAttached;

        public TracksView()
        {
            InitializeComponent();
            _musicPlayerService = App.Services.GetRequiredService<IMusicPlayerService>();
            _editMetadataService = App.Services.GetRequiredService<IEditMetadataService>();
            _library = App.Services.GetRequiredService<MusicLibrary>();

            Loaded += TracksView_Loaded;
            Unloaded += TracksView_Unloaded;

            RefreshPlaybackIndicator();

        }

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable<TrackRowItem>),
                typeof(TracksView),
                new PropertyMetadata(null, ItemsSource_PropertyChanged));

        public IEnumerable<TrackRowItem>? ItemsSource
        {
            get => (IEnumerable<TrackRowItem>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        private static void ItemsSource_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TracksView tracksView)
                return;

            tracksView.UnsubscribeFromItemsSource();
            tracksView._itemsSource = e.NewValue as IEnumerable<TrackRowItem>;
            tracksView._itemsSourceCollection = tracksView._itemsSource as INotifyCollectionChanged;

            if (tracksView._itemsSourceCollection != null)
            {
                tracksView._itemsSourceCollection.CollectionChanged += tracksView.ItemsSource_CollectionChanged;
            }

            tracksView.RefreshItemsSource();
        }

        private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshItemsSource();
        }

        private void RefreshItemsSource()
        {
            TracksList.ItemsSource = _itemsSource;
        }

        private void UnsubscribeFromItemsSource()
        {
            if (_itemsSourceCollection != null)
            {
                _itemsSourceCollection.CollectionChanged -= ItemsSource_CollectionChanged;
            }

            _itemsSourceCollection = null;
            _itemsSource = null;
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

        public static readonly DependencyProperty IsReorderEnabledProperty =
            DependencyProperty.Register(
                nameof(IsReorderEnabled),
                typeof(bool),
                typeof(TracksView),
                new PropertyMetadata(false));

        public bool IsReorderEnabled
        {
            get => (bool)GetValue(IsReorderEnabledProperty);
            set => SetValue(IsReorderEnabledProperty, value);
        }

        public static readonly DependencyProperty ReorderRequestedCommandProperty =
            DependencyProperty.Register(
                nameof(ReorderRequestedCommand),
                typeof(ICommand),
                typeof(TracksView),
                new PropertyMetadata(null));

        public ICommand? ReorderRequestedCommand
        {
            get => (ICommand?)GetValue(ReorderRequestedCommandProperty);
            set => SetValue(ReorderRequestedCommandProperty, value);
        }

        public static readonly DependencyProperty RemoveRequestedCommandProperty =
            DependencyProperty.Register(
                nameof(RemoveRequestedCommand),
                typeof(ICommand),
                typeof(TracksView),
                new PropertyMetadata(null));

        public ICommand? RemoveRequestedCommand
        {
            get => (ICommand?)GetValue(RemoveRequestedCommandProperty);
            set => SetValue(RemoveRequestedCommandProperty, value);
        }

        public static readonly DependencyProperty TrackActivatedCommandProperty =
            DependencyProperty.Register(
                nameof(TrackActivatedCommand),
                typeof(ICommand),
                typeof(TracksView),
                new PropertyMetadata(null));

        public ICommand? TrackActivatedCommand
        {
            get => (ICommand?)GetValue(TrackActivatedCommandProperty);
            set => SetValue(TrackActivatedCommandProperty, value);
        }

        public static readonly DependencyProperty CurrentTrackIdProperty =
            DependencyProperty.Register(
                nameof(CurrentTrackId),
                typeof(int),
                typeof(TracksView),
                new PropertyMetadata(0));

        public int CurrentTrackId
        {
            get => (int)GetValue(CurrentTrackIdProperty);
            set => SetValue(CurrentTrackIdProperty, value);
        }

        public static readonly DependencyProperty IsPlaybackActiveProperty =
            DependencyProperty.Register(
                nameof(IsPlaybackActive),
                typeof(bool),
                typeof(TracksView),
                new PropertyMetadata(false));

        public bool IsPlaybackActive
        {
            get => (bool)GetValue(IsPlaybackActiveProperty);
            set => SetValue(IsPlaybackActiveProperty, value);
        }

        public static readonly DependencyProperty IsMouseWheelScrollEnabledProperty =
            DependencyProperty.Register(
                nameof(IsMouseWheelScrollEnabled),
                typeof(bool),
                typeof(TracksView),
                new PropertyMetadata(false));

        public bool IsMouseWheelScrollEnabled
        {
            get => (bool)GetValue(IsMouseWheelScrollEnabledProperty);
            set => SetValue(IsMouseWheelScrollEnabledProperty, value);
        }

        public static readonly DependencyProperty AllTrackIdsProperty =
            DependencyProperty.Register(
                nameof(AllTrackIds),
                typeof(IEnumerable<int>),
                typeof(TracksView),
                new PropertyMetadata(null));

        public IEnumerable<int>? AllTrackIds
        {
            get => (IEnumerable<int>?)GetValue(AllTrackIdsProperty);
            set => SetValue(AllTrackIdsProperty, value);
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

            if (TrackActivatedCommand?.CanExecute(row.Id) == true)
            {
                TrackActivatedCommand.Execute(row.Id);
                return;
            }

            var alltracks = TracksList.Items.OfType<TrackRowItem>().Select(t => t.Id).ToList();

            _musicPlayerService.SetQueue(alltracks);
            _musicPlayerService.PlayTrack(row.Id);
        }

        private void TracksList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsMouseWheelScrollEnabled)
            {
                return;
            }

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

            //_playlistCoordinator.AddToManager(selectedTrackIds.ToArray());
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
            var selectedTrackIds = GetSelectedTrackIds();
            if (selectedTrackIds.Count == 0)
            {
                return;
            }

            if (RemoveRequestedCommand?.CanExecute(selectedTrackIds) == true)
            {
                RemoveRequestedCommand.Execute(selectedTrackIds);
            }
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

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Use AllTrackIds if provided (e.g., for album context), otherwise use selected tracks
            var trackIds = AllTrackIds != null ? AllTrackIds.ToList() : GetSelectedTrackIds();
            TrackToPlaylistMenuItem.TrackIds = trackIds;
        }

        private void TracksList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsReorderEnabled)
            {
                return;
            }

            _dragStartPoint = e.GetPosition(TracksList);
            _draggedItem = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject)?.DataContext as TrackRowItem;
        }

        private void TracksList_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsReorderEnabled || e.LeftButton != MouseButtonState.Pressed || _draggedItem is null)
            {
                return;
            }

            var current = e.GetPosition(TracksList);
            var delta = _dragStartPoint - current;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DragDrop.DoDragDrop(TracksList, _draggedItem, DragDropEffects.Move);
        }

        private void TracksList_DragOver(object sender, DragEventArgs e)
        {
            if (!IsReorderEnabled)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = e.Data.GetDataPresent(typeof(TrackRowItem))
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void TracksList_Drop(object sender, DragEventArgs e)
        {
            if (!IsReorderEnabled)
            {
                return;
            }

            if (!e.Data.GetDataPresent(typeof(TrackRowItem)))
            {
                return;
            }

            var source = e.Data.GetData(typeof(TrackRowItem)) as TrackRowItem;
            var targetRow = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            var target = targetRow?.DataContext as TrackRowItem;

            if (source is null || targetRow is null || target is null || source.Id == target.Id)
            {
                return;
            }

            var position = e.GetPosition(targetRow);
            var placeAfter = position.Y > (targetRow.ActualHeight / 2d);

            var request = new TrackReorderRequest(source.Id, target.Id, placeAfter);
            if (ReorderRequestedCommand?.CanExecute(request) == true)
            {
                ReorderRequestedCommand.Execute(request);
            }

            _draggedItem = null;
        }

        private void TracksView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_playerEventsAttached)
            {
                return;
            }

            _musicPlayerService.TrackChanged += MusicPlayerService_TrackChanged;
            _musicPlayerService.PlaybackStateChanged += MusicPlayerService_PlaybackStateChanged;
            _playerEventsAttached = true;

            RefreshPlaybackIndicator();
        }

        private void TracksView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_playerEventsAttached)
            {
                UnsubscribeFromItemsSource();
                return;
            }

            _musicPlayerService.TrackChanged -= MusicPlayerService_TrackChanged;
            _musicPlayerService.PlaybackStateChanged -= MusicPlayerService_PlaybackStateChanged;
            _playerEventsAttached = false;
            UnsubscribeFromItemsSource();
        }

        private void MusicPlayerService_TrackChanged(object? sender, string e)
        {
            RefreshPlaybackIndicator();
        }

        private void MusicPlayerService_PlaybackStateChanged(object? sender, PlaybackState e)
        {
            RefreshPlaybackIndicator();
        }

        private void RefreshPlaybackIndicator()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RefreshPlaybackIndicator);
                return;
            }

            CurrentTrackId = _musicPlayerService.CurrentTrackId;
            IsPlaybackActive = _musicPlayerService.IsPlaying;
        }
    }
}
