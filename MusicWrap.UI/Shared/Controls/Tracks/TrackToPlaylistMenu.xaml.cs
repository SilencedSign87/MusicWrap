using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.Playlist;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace MusicWrap.UI.Controls.Models
{
    /// <summary>
    /// Lógica de interacción para TrackToPlaylistMenu.xaml
    /// </summary>
    public partial class TrackToPlaylistMenu : MenuItem
    {
        private NewPlaylistWindow? _dialogPlaylist;
        private readonly IPlaylistService _playlistService;
        public ObservableCollection<PlaylistMenuItemModel> PlaylistItems { get; } = new();
        public TrackToPlaylistMenu()
        {
            InitializeComponent();
            _playlistService = App.Services.GetRequiredService<IPlaylistService>();
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
        private static void OnTrackIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TrackToPlaylistMenu)d).ReloadPlaylists();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadPlaylists();
        }

        private void OnPlaylistsChanged()
        {
            ReloadPlaylists();
        }

        private void ReloadPlaylists()
        {
            //PlaylistItems.Clear();

            //var trackIds = TrackIds?.ToArray() ?? [];
            //var items = _playlistService.GetMenuItems(trackIds);

            //foreach (var item in items)
            //{
            //    PlaylistItems.Add(item);
            //}}
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
            var mainwindow = App.CurrentWindow;
            if (mainwindow is null) return;

            if (_dialogPlaylist is null)
            {
                _dialogPlaylist = new NewPlaylistWindow();
                bool? result = WindowHelper.LauchFromParent(mainwindow, _dialogPlaylist, true);
                if (result.HasValue)
                {
                    _dialogPlaylist = null;
                }
            }
            else
            {
                _dialogPlaylist.Activate();
            }

        }
    }
}




