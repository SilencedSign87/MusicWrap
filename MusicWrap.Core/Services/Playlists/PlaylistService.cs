using AngleSharp.Io;
using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Images;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Playlist.Models;

namespace MusicWrap.Core.Services.Playlists
{
    public interface IPlaylistService
    {
        // READ
        IReadOnlyList<PlaylistDto> GetPlaylists(bool useSearchQuery = false);
        PlaylistDto? GetPlaylistById(int playlistId);
        List<int> GetTracksByPlaylistId(int playlistId);
        bool PlaylistNameExists(string name, int? excludePlaylistId = null);

        // Services
        void RenamePlaylist(int playlistId, string newName);
        void SetPlaylistArtwork(int playlistId, string? artworkPath);
        void DeletePlaylist(int playlistId);
        void CreatePlaylist(string name, IEnumerable<int>? trackIds = null, string? artworkPath = null);
        void SetTracksInPlaylist(IEnumerable<int> trackIds, int playlistId, bool shouldBeInPlaylist);
        void RemoveTracksFromPlaylist(IEnumerable<int> trackIds, int playlistId);
        void ReorderTrack(int playlistId, int sourceTrackId, int targetTrackId, bool placeAfterTarget);
        void ReloadCache();

        List<PlaylistMenuItemModel> GetMenuItems(IEnumerable<int> trackIds);
    }
    public class PlaylistService : IPlaylistService
    {
        private readonly PlaylistData _playlists;
        private readonly ISearchQueryProvider _searchQueryProvider;
        private readonly IMessenger _messenger;
        private readonly LibraryIndexer _indexer;
        private readonly ILibraryService _libraryService;

        private Dictionary<int, int[]>? TrackIdsByPlaylistId = null;

        public PlaylistService(PlaylistData playlist, ISearchQueryProvider searchQueryProvider, IMessenger messenger, LibraryIndexer indexer, ILibraryService libraryService)
        {
            _playlists = playlist;
            _searchQueryProvider = searchQueryProvider;
            _messenger = messenger;
            _indexer = indexer;
            _libraryService = libraryService;
            EnsureCache();
        }

        public IReadOnlyList<PlaylistDto> GetPlaylists(bool useSearchQuery = false)
        {
            EnsureCache();

            var allData = _playlists.Playlists.Select(p => new PlaylistDto(
                p.Id,
                p.CoverId is not null ? _libraryService.GetCoverAsset(p.CoverId.Value)?.FileName : null,
                p.Name,
                p.UpdatedAtUtcTicks,
                TrackIdsByPlaylistId!.TryGetValue(p.Id, out var trackIds) ? trackIds : []
                )).ToList();

            if (useSearchQuery)
            {
                return allData.Where(p => p.Name.Contains(_searchQueryProvider.ActiveQuery ?? string.Empty, StringComparison.OrdinalIgnoreCase)).ToList();
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
                playlist.CoverId is not null ? _libraryService.GetCoverAsset(playlist.CoverId.Value)?.FileName : null,
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
        public bool PlaylistNameExists(string name, int? excludePlaylistId = null)
        {
            return _playlists.Playlists.Any(p =>
                p.Id != excludePlaylistId &&
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public void RenamePlaylist(int playlistId, string newName)
        {
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist is null) return;
            if (string.IsNullOrWhiteSpace(newName)) return;
            if (playlist.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)) return;

            playlist.Name = newName;
            playlist.UpdatedAtUtcTicks = DateTime.UtcNow.Ticks;
            _messenger.Send(new PlaylistListChangedMessage());
        }
        public void SetPlaylistArtwork(int playlistId, string? artworkPath)
        {
            var playlist = _playlists.Playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist is null) return;

            playlist.CoverId = ResolveCoverId(artworkPath);
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

        public void CreatePlaylist(string name, IEnumerable<int>? trackIds = null, string? artworkPath = null)
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

            playlist.CoverId = ResolveCoverId(artworkPath);

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

        private int? ResolveCoverId(string? artworkPath)
        {
            if (string.IsNullOrEmpty(artworkPath) || !File.Exists(artworkPath))
                return null;   // ojo: null, no 0 (ver punto 8)

            var bytes = File.ReadAllBytes(artworkPath);
            var mimeType = LibraryIndexer.GetMimeTypeFromExtension(Path.GetExtension(artworkPath));
            return _indexer.GetOrCreateCoverAsset(bytes, mimeType);
        }

    }
    public sealed record PlaylistDto(
        int Id,
        string? CoverPath,
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
