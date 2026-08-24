using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MusicWrap.Core.Services.Playlists;
using MusicWrap.UI.Shared.Services;
using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Threading;
using MusicWrap.Core.Messages;

namespace MusicWrap.UI.Controls.Models
{
    /// <summary>
    /// Lógica de interacción para TrackToPlaylistMenu.xaml
    /// </summary>
    public partial class TrackToPlaylistMenu : MenuItem
    {
        private static TrackToPlaylistMenu? _sharedInstance;
        public static TrackToPlaylistMenu Shared => _sharedInstance ??= CreateShared();
        private bool _isInitialized;

        private readonly WindowManagerService _windowManager;
        private readonly IPlaylistService _playlistService;
        private readonly IMessenger _messenger;
        private readonly IUIDispatcher _uiDispatcher;
        public ObservableCollection<PlaylistMenuItemModel> PlaylistItems { get; } = new();

        private TrackToPlaylistMenu()
        {
            _playlistService = App.Services.GetRequiredService<IPlaylistService>();
            _windowManager = App.Services.GetRequiredService<WindowManagerService>();
            _messenger = App.Services.GetRequiredService<IMessenger>();
            _uiDispatcher = App.Services.GetRequiredService<IUIDispatcher>();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            _isInitialized = true;

            ReloadPlaylists();

            _messenger.Register<PlaylistListChangedMessage>(this, (r, m) =>
            {
                _uiDispatcher.Invoke(() => ReloadPlaylists());
            });
            _messenger.Register<PlaylistContentChangedMessage>(this, (r, m) =>
            {
                _uiDispatcher.Invoke(() =>
                {
                    var currentTrackIds = TrackIds?.ToArray() ?? [];
                    if (m.AffectedTrackIds.Any(id => currentTrackIds.Contains(id)))
                        ReloadPlaylists();
                });
            });
        }

        public static void AttachTo(ContextMenu contextMenu, int index = -1)
        {
            var instance = Shared;

            if (instance.Parent is ItemsControl oldParent && oldParent != contextMenu)
                oldParent.Items.Remove(instance);

            if (!contextMenu.Items.Contains(instance))
            {
                if (index >= 0)
                    contextMenu.Items.Insert(index, instance);
                else
                    contextMenu.Items.Add(instance);
            }
        }

        #region Dependency Properties
        public static readonly DependencyProperty TrackIdsProperty =
         DependencyProperty.Register(
             nameof(TrackIds),
             typeof(IEnumerable<int>),
             typeof(TrackToPlaylistMenu),
             new PropertyMetadata(null, OnTrackIdsChanged));

        public IEnumerable<int>? TrackIds
        {
            get => (IEnumerable<int>?)GetValue(TrackIdsProperty);
            set => SetValue(TrackIdsProperty, value);
        }
        #endregion

        private static TrackToPlaylistMenu CreateShared()
        {
            var instance = new TrackToPlaylistMenu();
            instance.InitializeComponent();
            instance.Initialize();
            return instance;
        }

        private static void OnTrackIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TrackToPlaylistMenu)d).ReloadPlaylists();
        }

        private void ReloadPlaylists()
        {
            while (Items.Count > 2)
            {
                Items.RemoveAt(2);
            }
            var trackIds = TrackIds?.ToArray() ?? [];
            var items = _playlistService.GetMenuItems(trackIds);

            foreach (var item in items)
            {
                var menuItem = new MenuItem
                {
                    Header = item.Name,
                    IsCheckable = true,
                    IsChecked = item.IsChecked,
                    DataContext = item,
                    StaysOpenOnClick = true,
                };
                menuItem.Checked += MenuItem_Checked;
                menuItem.Unchecked += MenuItem_Unchecked;
                Items.Add(menuItem);
            }
        }

        private void MenuItem_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.DataContext is not PlaylistMenuItemModel item) return;
            var trackIds = TrackIds?.ToArray() ?? [];
            _playlistService.SetTracksInPlaylist(trackIds, item.PlaylistId, false);
            ReloadPlaylists();
        }

        private void MenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.DataContext is not PlaylistMenuItemModel item) return;
            var trackIds = TrackIds?.ToArray() ?? [];
            _playlistService.SetTracksInPlaylist(trackIds, item.PlaylistId, true);
            ReloadPlaylists();
        }

        private void NewPlaylist_click(object sender, RoutedEventArgs e)
        {
            _windowManager.LaunchNewPlaylistWindow(TrackIds);
        }
    }
}




