using MusicWrap.Data.Playlist.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Playlist
{
    public interface IPlaylistService
    {
        void CreatePlaylist(string name);
        List<PlaylistMenuItemModel> GetMenuItems(IEnumerable<int> trackIds);
        void SetTracksInPlaylist(IEnumerable<int> trackIds, int playlistId, bool shouldBeInPlaylist);
    }
    public class PlaylistService : IPlaylistService
    {
        private readonly PlaylistData _playlists;

        public PlaylistService(PlaylistData playlist)
        {
            _playlists = playlist;
        }

        public List<PlaylistMenuItemModel> GetMenuItems(IEnumerable<int> trackIds)
        {
            var result = new List<PlaylistMenuItemModel>();
            if (_playlists == null || !trackIds.Any())
                return result;

            _playlists.Playlists.ForEach(p =>
            {
                var requestedTrackIds = trackIds.Distinct().ToArray();
                var playlistTrackIds = p.Items.Select(i => i.TrackId).ToHashSet();

                var isChecked = requestedTrackIds.Length > 0 && requestedTrackIds.All(id => playlistTrackIds.Contains(id));

                result.Add(new PlaylistMenuItemModel
                {
                    PlaylistId = p.Id,
                    Name = p.Name,
                    IsChecked = isChecked,
                    UpdatedatUtcTicks = p.UpdatedAtUtcTicks,
                });
            });

            return result;
        }

        public void SetTracksInPlaylist(IEnumerable<int> trackIds, int playlistId, bool shouldBeInPlaylist)
        {
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null) return;

            var selectedIds = trackIds.Distinct().ToHashSet();
            if (selectedIds.Count == 0) return;

            var now = DateTime.UtcNow.Ticks;
            var changed = false;

            if (shouldBeInPlaylist)
            {
                var existing = playlist.Items.Select(i => i.TrackId).ToHashSet();
                var list = playlist.Items.ToList();

                foreach (var id in selectedIds)
                {
                    if (existing.Add(id))
                    {
                        list.Add(new PlaylistItem { TrackId = id, AddedAtUtcTicks = now });
                        changed = true;
                    }
                }

                if (changed)
                {
                    playlist.Items = list;
                }
            }
            else
            {
                var filtered = playlist.Items.Where(i => !selectedIds.Contains(i.TrackId)).ToList();
                if (filtered.Count != playlist.Items.Count)
                {
                    playlist.Items = filtered;
                    changed = true;
                }
            }

            if (changed)
            {
                playlist.UpdatedAtUtcTicks = now;
            }
        }

        public void CreatePlaylist(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            _playlists.Playlists.Add(new Models.Playlist
            {
                Id = _playlists.GenerateNextPlaylistId(),
                Name = name,
                CreatedAtUtcTicks = DateTime.UtcNow.Ticks,
            });
        }
    }

    public class PlaylistMenuItemModel
    {
        public int PlaylistId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = false;
        public long UpdatedatUtcTicks { get; set; } = 0;
    }
}
