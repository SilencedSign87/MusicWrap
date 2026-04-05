using MusicWrap.UI.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MusicWrap.Data.Playlist.Models;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.UI.Services
{
    public interface IPlaylistManagerCoordinator
    {
        event Action<int[]>? OnTracksAdded;
        TrackToAdd? GetTrackMetadata(int trackId);
        int[] GetTracksInManager();
        void AddToManager(int[] trackIds);
        void RemoveFromManager(int[] trackIds);
        void AddTrackToPlatlist(int trackId, int playlistId);
        void RemoveTrackToPlatlist(int trackId, int playlistId);

    }

    public sealed class PlaylistManagerCoordinator : IPlaylistManagerCoordinator
    {
        private PlaylistManagerWindow? _managerWindow;
        private readonly PlaylistData _playlist;
        private readonly MusicLibrary _library;
        private readonly ILibraryCacheService _cacheService;
        // private readonly object _lock = new();

        private readonly Dictionary<int, TrackToAdd> _tracksInManager = new();
        public event Action<int[]>? OnTracksAdded;

        public PlaylistManagerCoordinator(PlaylistData playlist, MusicLibrary library, ILibraryCacheService cacheService)
        {
            _playlist = playlist;
            _library = library;
            _cacheService = cacheService;
        }

        public TrackToAdd? GetTrackMetadata(int trackId) =>
            _tracksInManager.TryGetValue(trackId, out var track) ? track : null;
        public int[] GetTracksInManager() => _tracksInManager.Keys.ToArray();

        public void AddToManager(int[] trackIds)
        {
            // remove duplicates
            trackIds = trackIds.Where(id => !_tracksInManager.ContainsKey(id)).ToArray();
            if (trackIds.Length == 0) return;

            var tracksToAdd = GetTracksToAdd(trackIds);
            foreach (var track in tracksToAdd)
            {
                _tracksInManager[track.Id] = track;
            }

            if (_managerWindow is null)
            {
                var mainWindow = App.CurrentWindow;
                _managerWindow = new PlaylistManagerWindow
                {
                    Owner = mainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                _managerWindow.Show();
            }
            _managerWindow.Activate();
            OnTracksAdded?.Invoke(trackIds);
        }

        public void RemoveFromManager(int[] trackIds)
        {
            foreach (var id in trackIds)
            {
                _tracksInManager.Remove(id);
            }
        }

        public void AddTrackToPlatlist(int trackId, int playlistId)
        {
            throw new NotImplementedException();
        }

        public void RemoveTrackToPlatlist(int trackId, int playlistId)
        {
            throw new NotImplementedException();
        }
        #region Internal
        private TrackToAdd[] GetTracksToAdd(int[] trackIds)
        {

            TrackToAdd[] tracks = [];
            for (int i = 0; i < trackIds.Length; i++)
            {
                var track = _library.Tracks.FirstOrDefault(t => t.Id == trackIds[i]);

                if (track is not null)
                {
                    var album = _library.Albums.FirstOrDefault(a => a.Id == track.AlbumId);

                    tracks[i] = new TrackToAdd
                    {
                        Id = track.Id,
                        Title = track.Title,
                        ArtistNames = _cacheService.GetArtistNamesForTrack(track.Id),
                        AlbumTitle = album?.Title ?? "Unknown Album",
                        ImagePath = _cacheService.FindCover([track.AlbumId], [track.Id]) ?? string.Empty,
                        PlaylistIn = GetPlaylistIdsWhereTrackIs(track.Id)
                    };
                }

            }
            return tracks;
        }

        private int[] GetPlaylistIdsWhereTrackIs(int trackId)
        {
            return [.. _playlist
                .Playlists
                .Where(p => p.Items.Any(i=>i.TrackId == trackId))
                .Select(p => p.Id)];
        }

        #endregion
    }

    public sealed class TrackToAdd
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistNames { get; set; } = string.Empty;
        public string AlbumTitle { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public int[] PlaylistIn { get; set; } = Array.Empty<int>();
    }
}