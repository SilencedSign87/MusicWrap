using Microsoft.Extensions.Logging;
using MusicWrap.Core.Saving;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Library
{
    public enum LocalTagKey
    {
        Title,
        Artist,
        Album,
        AlbumArtist,
        Genre,
        Year,
        TrackNumber,
        TrackCount,
        DiscNumber,
        DiscCount,
        Rating,
        PlayCount
    }

    public sealed record LocalTagChange(LocalTagKey Key, IReadOnlyList<string> Values);

    public class LocalMetadataEditorService
    {
        private readonly ILibraryService _library;
        private readonly LibraryIndexer _indexer;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILogger<LocalMetadataEditorService> _logger;

        public LocalMetadataEditorService(
           ILibraryService library,
           LibraryIndexer indexer,
           ISaveCoordinator saveCoordinator,
           ILogger<LocalMetadataEditorService> logger)
        {
            _library = library;
            _indexer = indexer;
            _saveCoordinator = saveCoordinator;
            _logger = logger;
        }

        public IReadOnlyList<string> GetValues(LocalTagKey key, Track track, Album? album)
        {
            return key switch
            {
                LocalTagKey.Title => [track.Title],
                LocalTagKey.Artist => _library.GetArtistNamesByIds(track.ArtistIds),
                LocalTagKey.Album => album is null ? [] : [album.Title],
                LocalTagKey.AlbumArtist => album is null ? [] : _library.GetArtistNamesByIds(album.ArtistIds),
                LocalTagKey.Genre => _library.GetGenreById([.. track.GenreIds]).Select(g => g.Name).ToArray(),
                LocalTagKey.Year => album is { Year: > 0 } ? [album.Year.ToString()] : [],
                LocalTagKey.TrackNumber => track.TrackNumber > 0 ? [track.TrackNumber.ToString()] : [],
                LocalTagKey.TrackCount => track.TrackCount > 0 ? [track.TrackCount.ToString()] : [],
                LocalTagKey.DiscNumber => track.Disk > 0 ? [track.Disk.ToString()] : [],
                LocalTagKey.DiscCount => track.DiscCount > 0 ? [track.DiscCount.ToString()] : [],
                LocalTagKey.Rating => track.Rating > 0 ? [track.Rating.ToString()] : [],
                LocalTagKey.PlayCount => track.PlayCount > 0 ? [track.PlayCount.ToString()] : [],
                _ => []
            };
        }

        public int Apply(IReadOnlyList<int> trackIds, IReadOnlyList<LocalTagChange> changes)
        {
            var tracks = trackIds
                .Select(_library.GetTrackById)
                .Where(t => t is { Origin: TrackOrigin.Local })
                .Cast<Track>()
                .ToList();

            if (tracks.Count == 0 || changes.Count == 0)
                return 0;

            foreach (var track in tracks)
            {
                var album = _library.GetAlbumById(track.AlbumId);

                foreach (var change in changes)
                    ApplyChange(change, track, album);
            }

            _library.RefreshLibrary();
            _saveCoordinator.Enqueue(SaveKind.Library);

            _logger.LogInformation("Updated local metadata on {Count} tracks.", tracks.Count);
            return tracks.Count;
        }

        private void ApplyChange(LocalTagChange change, Track track, Album? album)
        {
            var values = change.Values;

            switch (change.Key)
            {
                case LocalTagKey.Title:
                    track.Title = First(values);
                    break;

                case LocalTagKey.Artist:
                    track.ArtistIds = _indexer.GetOrCreateArtistIds(values);
                    break;

                case LocalTagKey.Genre:
                    track.GenreIds = _indexer.GetOrCreateGenreIds(values);
                    break;

                case LocalTagKey.TrackNumber:
                    track.TrackNumber = ParseInt(values);
                    break;

                case LocalTagKey.TrackCount:
                    track.TrackCount = ParseInt(values);
                    break;

                case LocalTagKey.DiscNumber:
                    track.Disk = ParseInt(values);
                    break;

                case LocalTagKey.DiscCount:
                    track.DiscCount = ParseInt(values);
                    break;

                case LocalTagKey.Rating:
                    track.Rating = ParseInt(values);
                    break;

                case LocalTagKey.PlayCount:
                    track.PlayCount = ParseInt(values);
                    break;

                case LocalTagKey.Album:
                    if (album is null) break;
                    album.Title = First(values);
                    break;

                case LocalTagKey.AlbumArtist:
                    if (album is null) break;
                    album.ArtistIds = _indexer.GetOrCreateArtistIds(values);
                    break;

                case LocalTagKey.Year:
                    if (album is null) break;
                    album.Year = ParseInt(values);
                    break;
            }
        }

        private static string First(IReadOnlyList<string> values)
            => values.Count > 0 ? values[0] : string.Empty;

        private static int ParseInt(IReadOnlyList<string> values)
            => values.Count > 0 && int.TryParse(values[0], out var parsed) ? parsed : 0;

    }
}
