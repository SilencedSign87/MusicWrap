using MusicWrap.Core.Services.Playlists;
using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicWrap.UI.Shared.Services
{
    public enum ContextMenuType
    {
        Standard,
        Queue
    }
    public sealed record ExtraMenuItem(string Header, string IconGlyph, ICommand Command);
    public sealed class ContextMenuFactory
    {
        private readonly TrackActionService _actions;
        private readonly IPlaylistService _playlistService;
        private readonly WindowManagerService _windowManager;

        public ContextMenuFactory(TrackActionService actions, IPlaylistService playlistService, WindowManagerService windowManager)
        {
            _actions = actions;
            _playlistService = playlistService;
            _windowManager = windowManager;
        }

        public ContextMenu Create(TracksView view, ContextMenuType type, IReadOnlyList<ExtraMenuItem>? extras = null)
        {
            var menu = new ContextMenu();
            bool isQueue = type == ContextMenuType.Queue;
            var queue = () => view.AllTrackIds?.ToList();

            Add(menu.Items, "Play now", "\uE768", () => WithSelection(view, ids =>
            {
                if (isQueue)
                    _actions.PlayNowInQueue(ids);
                else
                    _actions.PlayNow(ids, queue());
            }));

            Add(menu.Items, "Play next", "\uE893", () => WithSelection(view, ids =>
            {
                if (isQueue)
                    _actions.PlayNextInQueue(ids);
                else
                    _actions.PlayNext(ids, queue());
            }));

            if (!isQueue)
                Add(menu.Items, "Add to queue", "\uE710", () => WithSelection(view, _actions.AddToQueue));

            menu.Items.Add(new Separator());

            menu.Items.Add(CreateAddToPlaylistMenuItem(view.GetSelectedTrackIds));

            if (!isQueue)
            {
                menu.Items.Add(new Separator());
                Add(menu.Items, "Properties", "\uE90F", () => WithSelection(view, _actions.ShowTrackInformationDialog));
                Add(menu.Items, "Show in file explorer", "\uEC50", () => WithSelection(view, _actions.ShowInFileExplorer));
            }

            if (extras is { Count: > 0 })
            {
                menu.Items.Add(new Separator());
                foreach (var extra in extras)
                    menu.Items.Add(new MenuItem { Header = extra.Header, Icon = Icon(extra.IconGlyph), Command = extra.Command });
            }

            return menu;
        }

        public MenuItem CreateAddToPlaylistMenuItem(Func<List<int>> selectedIds)
        {
            var menu = new MenuItem { Header = "Add to Playlist", Icon = Icon("\uE142") };

            var newPlaylistItem = new MenuItem { Header = "New Playlist", Icon = Icon("\uE710") };
            newPlaylistItem.Click += (_, _) => _windowManager.LaunchNewPlaylistWindow(selectedIds());
            menu.Items.Add(newPlaylistItem);
            menu.Items.Add(new Separator());

            menu.SubmenuOpened += (_, _) => PopulatePlaylists(menu, selectedIds);

            return menu;

        }

        #region internal
        private void PopulatePlaylists(MenuItem menu, Func<List<int>> selectedIds)
        {
            while (menu.Items.Count > 2)
                menu.Items.RemoveAt(menu.Items.Count - 1);

            var trackids = selectedIds();

            if (trackids.Count == 0)
                return;

            foreach (var item in _playlistService.GetMenuItems(trackids))
            {
                var checkItem = new MenuItem
                {
                    Header = item.Name,
                    IsCheckable = true,
                    IsChecked = item.IsChecked,
                    StaysOpenOnClick = true,
                };
                checkItem.Checked += (_, _) =>
                {
                    _playlistService.SetTracksInPlaylist(trackids, item.PlaylistId, true);
                    PopulatePlaylists(menu, selectedIds); // trigger a refresh
                };
                checkItem.Unchecked += (_, _) =>
                {
                    _playlistService.SetTracksInPlaylist(trackids, item.PlaylistId, false);
                    PopulatePlaylists(menu, selectedIds); // trigger a refresh
                };

                menu.Items.Add(checkItem);
            }

        }
        private static void WithSelection(TracksView view, Action<List<int>> action)
        {
            var ids = view.GetSelectedTrackIds();

            if (ids.Count > 0)
                action(ids);
        }
        private static void Add(ItemCollection items, string header, string glyph, Action onClick)
        {
            var item = new MenuItem { Header = header, Icon = Icon(glyph) };

            item.Click += (_, _) => onClick();

            items.Add(item);
        }
        private static TextBlock Icon(string glyph, double fontSize = 16, VerticalAlignment verticalAlignment = VerticalAlignment.Center, HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center) => new()
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = fontSize,
            VerticalAlignment = verticalAlignment,
            HorizontalAlignment = horizontalAlignment,
        };
        #endregion
    }
}
