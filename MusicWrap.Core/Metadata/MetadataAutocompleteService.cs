using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Metadata
{
    /// <summary>
    /// Implementation of metadata autocomplete service
    /// Provides suggestions from the music library
    /// </summary>
    public class MetadataAutocompleteService : IMetadataAutocompleteService
    {
        private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromSeconds(30);
        private readonly MusicLibrary _library;
        private readonly object _cacheLock = new();

        private readonly Dictionary<MetadataType, IReadOnlyList<string>> _cache = [];
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private int _lastArtistsCount;
        private int _lastAlbumsCount;
        private int _lastGenresCount;

        public MetadataAutocompleteService(MusicLibrary library)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
        }

        public IReadOnlyList<string> GetSuggestions(MetadataType metadataType, string searchTerm, int limit = 20)
        {
            if (limit <= 0)
            {
                return Array.Empty<string>();
            }

            var allValues = GetAllValues(metadataType);
            var term = searchTerm?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(term))
            {
                return allValues.Take(limit).ToList();
            }

            // Ranking:
            // 0: exact match
            // 1: starts with term
            // 2: contains term
            var suggestions = allValues
                .Where(v => v.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => GetRank(v, term))
                .ThenBy(v => v.IndexOf(term, StringComparison.OrdinalIgnoreCase))
                .ThenBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();

            return suggestions;
        }

        public IReadOnlyList<string> GetAllValues(MetadataType metadataType)
        {
            EnsureCacheFresh();

            lock (_cacheLock)
            {
                return _cache.TryGetValue(metadataType, out var values)
                    ? values
                    : Array.Empty<string>();
            }
        }

        private static int GetRank(string candidate, string term)
        {
            if (candidate.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (candidate.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private void EnsureCacheFresh()
        {
            lock (_cacheLock)
            {
                if (!ShouldRefreshCacheUnsafe())
                {
                    return;
                }

                RebuildCacheUnsafe();
            }
        }

        private bool ShouldRefreshCacheUnsafe()
        {
            if (_cache.Count == 0)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            if ((now - _lastRefreshUtc) >= CacheRefreshInterval)
            {
                return true;
            }

            // Fast shape checks for library mutations that change counts.
            //if (_library.Artists.Count != _lastArtistsCount ||
            //    _library.Albums.Count != _lastAlbumsCount ||
            //    _library.Genres.Count != _lastGenresCount)
            //{
            //    return true;
            //}

            return false;
        }

        private void RebuildCacheUnsafe()
        {
            var artists = _library.Tracks
                .SelectMany(t => t.AlbumArtists.Length > 0 ? t.AlbumArtists : t.Artists)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var albums = _library.Tracks
                .Select(t => t.AlbumName)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            var genres = _library.Tracks
                .SelectMany(t => t.Genres)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            _cache[MetadataType.ArtistName] = BuildValues(artists);
            _cache[MetadataType.AlbumTitle] = BuildValues(albums);
            _cache[MetadataType.GenreName] = BuildValues(genres);

            _lastArtistsCount = artists.Count();
            _lastAlbumsCount = albums.Count();
            _lastGenresCount = genres.Count();
            _lastRefreshUtc = DateTime.UtcNow;
        }

        private static IReadOnlyList<string> BuildValues(IEnumerable<string?> values)
        {
            return values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

    }
}
