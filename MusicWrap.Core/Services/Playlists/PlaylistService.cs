using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Data.Playlist.Models;

namespace MusicWrap.Core.Services.Playlists
{
    public interface IPlaylistService
    {
        // READ
        IReadOnlyList<PlaylistDto> GetPlaylists(bool useSearchQuery = false);
        PlaylistDto? GetPlaylistById(int playlistId);
        List<int> GetTracksByPlaylistId(int playlistId);

        // Services
        void RenamePlaylist(int playlistId, string newName);
        void DeletePlaylist(int playlistId);
        void CreatePlaylist(string name, IEnumerable<int>? trackIds = null);
        void SetTracksInPlaylist(IEnumerable<int> trackIds, int playlistId, bool shouldBeInPlaylist);
        void RemoveTracksFromPlaylist(IEnumerable<int> trackIds, int playlistId);
        void ReorderTrack(int playlistId, int sourceTrackId, int targetTrackId, bool placeAfterTarget);
        void ReloadCache();

        List<PlaylistMenuItemModel> GetMenuItems(IEnumerable<int> trackIds);
    }
    public class PlaylistService : IPlaylistService
    {
        private readonly PlaylistData _playlists;
        private readonly ISearchQueryProvider searchQueryProvider;
        private readonly IMessenger _messenger;

        private Dictionary<int, int[]>? TrackIdsByPlaylistId = null;

        public PlaylistService(PlaylistData playlist, ISearchQueryProvider searchQueryProvider, IMessenger messenger)
        {
            _playlists = playlist;
            this.searchQueryProvider = searchQueryProvider;
            this._messenger = messenger;
            EnsureCache();
        }

        public IReadOnlyList<PlaylistDto> GetPlaylists(bool useSearchQuery = false)
        {
            EnsureCache();

            var allData = _playlists.Playlists.Select(p => new PlaylistDto(
                p.Id,
                p.Name,
                p.UpdatedAtUtcTicks,
                TrackIdsByPlaylistId!.TryGetValue(p.Id, out var trackIds) ? trackIds : []
                )).ToList();

            if (useSearchQuery)
            {
                return allData.Where(p => p.Name.Contains(searchQueryProvider.ActiveQuery ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                return allData;
            }
        }
        public PlaylistDto? GetPlaylistById(int playlistId)
        {
            EnsureCache();
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null) return null;

            var trackIds = TrackIdsByPlaylistId!.TryGetValue(playlistId, out var ids) ? ids : [];

            return new PlaylistDto(
                playlist.Id,
                playlist.Name,
                playlist.UpdatedAtUtcTicks,
                trackIds);
        }
        public List<int> GetTracksByPlaylistId(int playlistId)
        {
            EnsureCache();
            if (TrackIdsByPlaylistId!.TryGetValue(playlistId, out var trackIds))
            {
                return [.. trackIds];
            }
            return [];
        }
        public void RenamePlaylist(int playlistId, string newName)
        {
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null) return;
            if (string.IsNullOrWhiteSpace(newName) || playlist.Name == newName)
                return;
            playlist.Name = newName;
            playlist.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
            _messenger.Send(new PlaylistListChangedMessage());
        }
        public void DeletePlaylist(int playlistId)
        {
            var removed = _playlists.Playlists.RemoveAll(p => p.Id == playlistId) > 0;
            if (removed)
            {
                if (TrackIdsByPlaylistId is not null)
                {
                    TrackIdsByPlaylistId.Remove(playlistId);
                }
                _messenger.Send(new PlaylistListChangedMessage());
            }
        }
        public void RemoveTracksFromPlaylist(IEnumerable<int> trackIds, int playlistId)
        {
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null) return;

            var removeset = trackIds.Distinct().ToHashSet();
            if (removeset.Count == 0) return;

            var removedAny = playlist.Items.RemoveAll(i => removeset.Contains(i.TrackId)) > 0;
            if (!removedAny) return;

            playlist.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;

            if (TrackIdsByPlaylistId is not null)
            {
                TrackIdsByPlaylistId[playlistId] = playlist.Items.Select(i => i.TrackId).ToArray();
            }
            else
            {
                EnsureCache();
            }

            _messenger.Send(new PlaylistContentChangedMessage(playlistId, removeset));
        }
        public void ReorderTrack(int playlistId, int sourceTrackId, int targetTrackId, bool placeAfterTarget)
        {
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist == null) return;

            var items = playlist.Items;
            var sourceIndex = items.FindIndex(i => i.TrackId == sourceTrackId);
            var targetIndex = items.FindIndex(i => i.TrackId == targetTrackId);

            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
                return;

            var item = items[sourceIndex];
            items.RemoveAt(sourceIndex);
            // Adjust target index if source item was before target

            if (sourceIndex < targetIndex)
                targetIndex--;

            var insertIndex = placeAfterTarget ? targetIndex + 1 : targetIndex;
            insertIndex = Math.Clamp(insertIndex, 0, items.Count);
            items.Insert(insertIndex, item);

            playlist.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
            if (TrackIdsByPlaylistId is not null)
            {
                TrackIdsByPlaylistId[playlistId] = items.Select(i => i.TrackId).ToArray();
            }
            else
            {
                EnsureCache();
            }
            _messenger.Send(new PlaylistContentChangedMessage(playlistId, [sourceTrackId]));
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
                if (TrackIdsByPlaylistId is not null)
                {
                    TrackIdsByPlaylistId[playlistId] = playlist.Items.Select(i => i.TrackId).ToArray();
                }
                else
                {
                    EnsureCache();
                }
                _messenger.Send(new PlaylistContentChangedMessage(playlistId, selectedIds));
            }
        }

        public void CreatePlaylist(string name, IEnumerable<int>? trackIds = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            var playlist = new Playlist
            {
                Id = _playlists.GenerateNextPlaylistId(),
                Name = name,
                CreatedAtUtcTicks = DateTime.UtcNow.Ticks,
            };

            if (trackIds != null)
            {
                var now = DateTime.UtcNow.Ticks;
                playlist.Items = trackIds.Distinct().Select(id => new PlaylistItem { TrackId = id, AddedAtUtcTicks = now }).ToList();
            }

            _playlists.Playlists.Add(playlist);


            if (TrackIdsByPlaylistId is not null)
            {
                TrackIdsByPlaylistId[playlist.Id] = playlist.Items.Select(i => i.TrackId).ToArray();
            }
            else
            {
                EnsureCache();
            }
            _messenger.Send(new PlaylistListChangedMessage());
        }
        public void ReloadCache()
        {
            TrackIdsByPlaylistId = null;
            EnsureCache();
            _messenger.Send(new PlaylistListChangedMessage());
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
        private void EnsureCache()
        {
            TrackIdsByPlaylistId ??= _playlists.Playlists.ToDictionary(p => p.Id, p => p.Items.Select(i => i.TrackId).ToArray());
        }

    }
    public sealed record PlaylistDto(
        int Id,
        string Name,
        long UpdatedAtUtcTicks,
        IReadOnlyList<int> TrackIds
        );

    public sealed class PlaylistMenuItemModel
    {
        public int PlaylistId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = false;
        public long UpdatedatUtcTicks { get; set; } = 0;
    }
    public sealed class PlaylistItemsChangedEventArgs : EventArgs
    {
        public int PlaylistId { get; }
        public IEnumerable<int> TrackIds { get; }
        public bool ShouldBeInPlaylist { get; }
        public PlaylistItemsChangedEventArgs(int playlistId, IEnumerable<int> trackIds, bool shouldBeInPlaylist)
        {
            PlaylistId = playlistId;
            TrackIds = trackIds;
            ShouldBeInPlaylist = shouldBeInPlaylist;
        }
    }
}
