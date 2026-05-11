using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using System.Collections.Concurrent;

namespace MusicWrap.Core.Services.Library;

public interface ILibraryService
{
    Track? GetTrackById(int trackId);
    IReadOnlyList<Track> GetAllTracks();

    Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync(string viewType, bool ascending);
    IReadOnlyList<AlbumSummary> GetAlbumsForEntry(LibraryEntry entry);

    int[] GetTrackIdsForEntry(LibraryEntry entry, string? query = null);
    int[] GetTracksForAlbum(int albumId, string? query = null);
    int[] GetTrackQueueForAlbum(int albumId);
    int GetAlbumIdForTrack(int trackId);

    int GetAlbumDuration(int albumId);
    string GetArtistNamesForAlbum(int albumId);
    string GetArtistNamesForTrack(int trackId);

    string? FindCover(IEnumerable<int>? albumIds = null, IEnumerable<int>? trackIds = null);

    void Initialize(string initialView, bool ascending);
    void InvalidateCache();

    event EventHandler? LibraryEntriesChanged;
    event EventHandler? LibraryEntryItemsChanged;
}

public sealed class LibraryService : ILibraryService
{
    private readonly UserSettings _userSettings;
    private readonly ISaveCoordinator _saveCoordinator;
    private readonly ILogger<LibraryService> _logger;
    private readonly MusicLibrary _library;
    private readonly object _indexLock = new();

    private readonly ConcurrentDictionary<string, LibraryEntry[]> _entriesCache = new(StringComparer.OrdinalIgnoreCase);

    private LibraryEntry[]? _albumEntries;
    private LibraryEntry[]? _artistEntries;
    private LibraryEntry[]? _genreEntries;
    private LibraryEntry[]? _decadeEntries;

    private Dictionary<int, Track> _trackById = new();
    private Dictionary<int, CoverAsset> _coverById = new();
    private Dictionary<int, AlbumGroup> _albumGroupsById = new();
    private Dictionary<int, int[]> _trackIdsByAlbumId = new();
    private Dictionary<int, int[]> _trackIdsByArtistId = new();
    private Dictionary<int, int[]> _trackIdsByGenreId = new();

    public event EventHandler? LibraryEntriesChanged;
    public event EventHandler? LibraryEntryItemsChanged;

    public LibraryService(MusicLibrary library, ILogger<LibraryService> logger, UserSettings userSettings, ISaveCoordinator saveCoordinator)
    {
        _library = library;
        _logger = logger;
        _userSettings = userSettings;
        _saveCoordinator = saveCoordinator;
        BuildIndexes();
        Initialize(userSettings.LibraryListBy, userSettings.LibraryAscending);
    }

    public Track? GetTrackById(int trackId)
    {
        EnsureIndexes();
        return _trackById.TryGetValue(trackId, out var track) ? track : null;
    }

    public IReadOnlyList<Track> GetAllTracks() => _library.Tracks;

    public void Initialize(string initialView, bool ascending)
    {

        EnsureIndexes();
        _ = GetEntriesInternal(initialView, ascending);
    }

    public async Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync(string viewType, bool ascending)
    {
        var entries = await Task.Run(() => GetEntriesInternal(viewType, ascending));
        return entries;
    }

    public IReadOnlyList<AlbumSummary> GetAlbumsForEntry(LibraryEntry entry)
    {
        EnsureIndexes();

        var albumIds = GetAlbumIdsForEntry(entry);
        var result = new List<AlbumSummary>(albumIds.Length);

        foreach (var albumId in albumIds)
        {
            if (!_albumGroupsById.TryGetValue(albumId, out var group))
            {
                continue;
            }

            if (group.TrackIds.Length == 0)
            {
                continue;
            }

            var imagePath = FindCover(trackIds: group.TrackIds);
            result.Add(new AlbumSummary
            {
                Id = group.AlbumId,
                Title = group.AlbumName,
                Year = group.ReleaseYear ?? 0,
                ArtistNames = group.ArtistNames,
                ImagePath = imagePath,
                BluredImagePath = imagePath,
                DominantColorHex = group.DominantColorHex,
                ForegroundColorHex = group.ForegroundColorHex
            });
        }

        return [.. result.OrderByDescending(album => album.Year).ThenBy(album => album.Title, StringComparer.OrdinalIgnoreCase)];
    }

    public int[] GetTrackIdsForEntry(LibraryEntry entry, string? query = null)
    {
        EnsureIndexes();

        var albumIds = GetAlbumIdsForEntry(entry);
        var allTrackIds = albumIds.SelectMany(albumId =>
            _trackIdsByAlbumId.TryGetValue(albumId, out var ids) ? ids : []);

        var trackIds = allTrackIds;
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim();
            trackIds = trackIds.Where(id => _trackById.TryGetValue(id, out var track) && TrackMatchesQuery(track, normalizedQuery));
        }

        return [.. trackIds];
    }

    public int[] GetTracksForAlbum(int albumId, string? query = null)
    {
        EnsureIndexes();

        if (!_trackIdsByAlbumId.TryGetValue(albumId, out var trackIds))
        {
            return [];
        }

        var tracks = trackIds.Select(trackId => _trackById[trackId]);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim();
            tracks = tracks.Where(track => TrackMatchesQuery(track, normalizedQuery));
        }

        return [.. tracks
            .OrderBy(track => track.DiskNumber)
            .ThenBy(track => track.TrackNumber)
            .ThenBy(track => track.Title, StringComparer.OrdinalIgnoreCase)
            .Select(track => track.Id)];
    }

    public int[] GetTrackQueueForAlbum(int albumId)
    {
        EnsureIndexes();

        if (!_trackIdsByAlbumId.TryGetValue(albumId, out var trackIds))
        {
            return [];
        }

        return trackIds;
    }
    public int GetAlbumIdForTrack(int trackId)
    {
        EnsureIndexes();
        foreach (var kvp in _trackIdsByAlbumId)
        {
            if (kvp.Value.Contains(trackId))
            {
                return kvp.Key;
            }
        }
        return -1;
    }

    public int GetAlbumDuration(int albumId)
    {
        EnsureIndexes();

        if (!_trackIdsByAlbumId.TryGetValue(albumId, out var trackIds))
        {
            return 0;
        }

        return trackIds.Sum(trackId => _trackById.TryGetValue(trackId, out var track) ? Math.Max(0, track.DurationSeconds) : 0);
    }

    public string GetArtistNamesForAlbum(int albumId)
    {
        EnsureIndexes();

        return _albumGroupsById.TryGetValue(albumId, out var group)
            ? group.ArtistNames
            : "Unknown Artist";
    }

    public string GetArtistNamesForTrack(int trackId)
    {
        EnsureIndexes();

        if (!_trackById.TryGetValue(trackId, out var track))
        {
            return "Unknown Artist";
        }

        var artists = track.AlbumArtists.Length > 0 ? track.AlbumArtists : track.Artists;
        return JoinNames(artists, "Unknown Artist");
    }

    public string? FindCover(IEnumerable<int>? albumIds = null, IEnumerable<int>? trackIds = null)
    {
        EnsureIndexes();

        if (albumIds is not null)
        {
            foreach (var albumId in albumIds)
            {
                if (!_trackIdsByAlbumId.TryGetValue(albumId, out var albumTrackIds))
                {
                    continue;
                }

                var cover = FindCoverByTrackIds(albumTrackIds);
                if (cover is not null)
                {
                    return cover;
                }
            }
        }

        if (trackIds is not null)
        {
            return FindCoverByTrackIds(trackIds);
        }

        return null;
    }

    public void InvalidateCache()
    {
        lock (_indexLock)
        {
            _albumEntries = null;
            _artistEntries = null;
            _genreEntries = null;
            _decadeEntries = null;
            _entriesCache.Clear();
        }
    }

    private LibraryEntry[] GetEntriesInternal(string viewType, bool ascending)
    {
        EnsureIndexes();

        var cacheKey = $"{viewType}:{ascending}";
        if (_entriesCache.TryGetValue(cacheKey, out var cachedEntries))
        {
            return cachedEntries;
        }

        var entries = viewType switch
        {
            "Album" => GetAlbumEntries(ascending),
            "Artist" => GetArtistEntries(ascending),
            "Genre" => GetGenreEntries(ascending),
            "Decade" => GetDecadeEntries(ascending),
            _ => GetAlbumEntries(ascending)
        };
        if(viewType != _userSettings.LibraryListBy || ascending != _userSettings.LibraryAscending)
        {
            _userSettings.LibraryListBy = viewType;
            _userSettings.LibraryAscending = ascending;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }

        _entriesCache[cacheKey] = entries;


        return entries;
    }

    private LibraryEntry[] GetAlbumEntries(bool ascending)
    {
        if (_albumEntries is null)
        {
            _albumEntries = _albumGroupsById.Values
                .Select(group => new LibraryEntry
                {
                    Id = group.AlbumId,
                    Type = "Album",
                    Title = group.AlbumName,
                    ImagePath = FindCover(trackIds: group.TrackIds),
                    Description = $"{group.TrackIds.Length} track{(group.TrackIds.Length == 1 ? string.Empty : "s")}",
                    GroupKey = GetInitialGroup(group.AlbumName)
                })
                .ToArray();
        }

        return ascending
            ? [.. _albumEntries.OrderBy(entry => entry.GroupKey).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)]
            : [.. _albumEntries.OrderByDescending(entry => entry.GroupKey).ThenByDescending(entry => entry.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private LibraryEntry[] GetArtistEntries(bool ascending)
    {
        if (_artistEntries is null)
        {
            _artistEntries = _trackIdsByArtistId.Select(kvp =>
            {
                var artistId = kvp.Key;
                var trackIds = kvp.Value;
                var displayName = ResolveTrackValue(trackIds, track => FirstArtist(track)) ?? "Unknown Artist";

                return new LibraryEntry
                {
                    Id = artistId,
                    Type = "Artist",
                    Title = displayName,
                    ImagePath = FindCover(trackIds: trackIds),
                    Description = $"{GetAlbumIdsForTracks(trackIds).Distinct().Count()} album{(GetAlbumIdsForTracks(trackIds).Distinct().Count() == 1 ? string.Empty : "s")}",
                    GroupKey = GetInitialGroup(displayName)
                };
            }).ToArray();
        }

        return ascending
            ? [.. _artistEntries.OrderBy(entry => entry.GroupKey).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)]
            : [.. _artistEntries.OrderByDescending(entry => entry.GroupKey).ThenByDescending(entry => entry.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private LibraryEntry[] GetGenreEntries(bool ascending)
    {
        if (_genreEntries is null)
        {
            _genreEntries = _trackIdsByGenreId.Select(kvp =>
            {
                var genreId = kvp.Key;
                var trackIds = kvp.Value;
                var displayName = ResolveTrackValue(trackIds, track => track.Genres.FirstOrDefault()) ?? "Unknown Genre";

                return new LibraryEntry
                {
                    Id = genreId,
                    Type = "Genre",
                    Title = displayName,
                    ImagePath = FindCover(trackIds: trackIds),
                    Description = $"{GetAlbumIdsForTracks(trackIds).Distinct().Count()} album{(GetAlbumIdsForTracks(trackIds).Distinct().Count() == 1 ? string.Empty : "s")}",
                    GroupKey = GetInitialGroup(displayName)
                };
            }).ToArray();
        }

        return ascending
            ? [.. _genreEntries.OrderBy(entry => entry.GroupKey).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)]
            : [.. _genreEntries.OrderByDescending(entry => entry.GroupKey).ThenByDescending(entry => entry.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private LibraryEntry[] GetDecadeEntries(bool ascending)
    {
        if (_decadeEntries is null)
        {
            var decadeGroups = _library.Tracks
                .Where(track => track.ReleaseYear.HasValue)
                .GroupBy(track => (track.ReleaseYear!.Value / 10) * 10);

            _decadeEntries = decadeGroups
                .Select(group => new LibraryEntry
                {
                    Id = group.Key,
                    Type = "Decade",
                    Title = $"{group.Key}s",
                    ImagePath = FindCover(trackIds: group.Select(track => track.Id)),
                    Description = $"{group.Select(track => track.AlbumName).Distinct(StringComparer.OrdinalIgnoreCase).Count()} album(s)",
                    GroupKey = GetInitialGroup($"{group.Key}s")
                })
                .ToArray();
        }

        return ascending
            ? [.. _decadeEntries.OrderBy(entry => entry.GroupKey).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)]
            : [.. _decadeEntries.OrderByDescending(entry => entry.GroupKey).ThenByDescending(entry => entry.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private int[] GetAlbumIdsForTracks(IEnumerable<int> trackIds)
    {
        var trackIdSet = trackIds.ToHashSet();
        return [.. _trackIdsByAlbumId
            .Where(kvp => kvp.Value.Any(trackId => trackIdSet.Contains(trackId)))
            .Select(kvp => kvp.Key)
            .Distinct()];
    }

    private int[] GetAlbumIdsForEntry(LibraryEntry entry)
    {
        return entry.Type switch
        {
            "Album" => [entry.Id],
            "Artist" => _trackIdsByArtistId.TryGetValue(entry.Id, out var artistTrackIds)
                ? GetAlbumIdsForTracks(artistTrackIds)
                : [],
            "Genre" => _trackIdsByGenreId.TryGetValue(entry.Id, out var genreTrackIds)
                ? GetAlbumIdsForTracks(genreTrackIds)
                : [],
            "Decade" => GetAlbumIdsForTracks(_library.Tracks
                .Where(track => track.ReleaseYear.HasValue && (track.ReleaseYear.Value / 10) * 10 == entry.Id)
                .Select(track => track.Id)),
            _ => []
        };
    }

    private void EnsureIndexes()
    {
        lock (_indexLock)
        {
            if (_trackById.Count != _library.Tracks.Count)
            {
                BuildIndexes();
            }
        }
    }

    private void BuildIndexes()
    {
        _trackById = _library.Tracks.ToDictionary(track => track.Id);
        _coverById = _library.CoverAssets.ToDictionary(cover => cover.Id);

        BuildAlbumGroups();
        BuildArtistGroups();
        BuildGenreGroups();
        _entriesCache.Clear();
    }

    private void BuildAlbumGroups()
    {

        _albumGroupsById = _library.Tracks
            .GroupBy(track => CreateAlbumKey(track))
            .Select(group =>
            {
                var first = group.FirstOrDefault();
                var albumId = StableHash(group.Key);
                var trackIds = group.Select(track => track.Id).ToArray();
                var artistNames = JoinNames(first?.AlbumArtists.Length > 0 ? first.AlbumArtists : first?.Artists, "Unknown Artist");
                var cover = FindCoverAssetByTrackIds(trackIds);

                return new AlbumGroup
                {
                    AlbumId = albumId,
                    AlbumName = first?.AlbumName ?? "Unknown Album",
                    ArtistNames = artistNames,
                    ReleaseYear = group.Select(track => track.ReleaseYear).FirstOrDefault(year => year.HasValue),
                    TrackIds = [.. trackIds.OrderBy(id => _trackById[id].DiskNumber).ThenBy(id => _trackById[id].TrackNumber).ThenBy(id => _trackById[id].Title, StringComparer.OrdinalIgnoreCase)],
                    DominantColorHex = cover?.DominantColorHex ?? "#808080",
                    ForegroundColorHex = cover?.ForegroundColorHex ?? "#FFFFFF"
                };
            })
            .ToDictionary(group => group.AlbumId);

        _trackIdsByAlbumId = _albumGroupsById.Values.ToDictionary(group => group.AlbumId, group => group.TrackIds);
    }

    private void BuildArtistGroups()
    {
        _trackIdsByArtistId = _library.Tracks
            .SelectMany(track => GetArtistTokens(track).Select(artist => (Artist: artist, TrackId: track.Id)))
            .GroupBy(item => StableHash(NormalizeKey(item.Artist)))
            .ToDictionary(group => group.Key, group => group.Select(item => item.TrackId).Distinct().ToArray());
    }

    private void BuildGenreGroups()
    {
        _trackIdsByGenreId = _library.Tracks
            .SelectMany(track => track.Genres.Select(genre => (Genre: genre, TrackId: track.Id)))
            .GroupBy(item => StableHash(NormalizeKey(item.Genre)))
            .ToDictionary(group => group.Key, group => group.Select(item => item.TrackId).Distinct().ToArray());
    }

    private static string CreateAlbumKey(Track track)
    {
        var album = string.IsNullOrWhiteSpace(track.AlbumName) ? "Unknown Album" : track.AlbumName.Trim();

        var artists = GetArtistTokens(track)
            .Select(artist => artist.Trim())
            .Where(artist => !string.IsNullOrWhiteSpace(artist))
            .OrderBy(artist => artist, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (artists.Length == 0)
        {
            artists = ["Unknown Artist"];
        }

        return $"{NormalizeKey(album)}|{string.Join("|", artists.Select(NormalizeKey))}";
    }

    private static string[] GetArtistTokens(Track track)
    {
        var artists = track.AlbumArtists.Length > 0 ? track.AlbumArtists : track.Artists;
        if (artists.Length == 0 || artists.All(artist => string.IsNullOrWhiteSpace(artist)))
        {
            return ["Unknown Artist"];
        }
        return artists;
    }

    private string? FindCoverByTrackIds(IEnumerable<int> trackIds)
    {
        foreach (var trackId in trackIds)
        {
            if (!_trackById.TryGetValue(trackId, out var track))
            {
                continue;
            }

            if (track.CoverIds.Length == 0)
            {
                continue;
            }

            var coverId = track.CoverIds[0];
            if (_coverById.TryGetValue(coverId, out var cover))
            {
                return cover.FileName;
            }
        }

        return null;
    }

    private CoverAsset? FindCoverAssetByTrackIds(IEnumerable<int> trackIds)
    {
        foreach (var trackId in trackIds)
        {
            if (!_trackById.TryGetValue(trackId, out var track))
            {
                continue;
            }
            if (track.CoverIds.Length == 0)
            {
                continue;
            }
            var coverId = track.CoverIds[0];
            if (_coverById.TryGetValue(coverId, out var cover))
            {
                return cover;
            }
        }
        return null;
    }

    private bool TrackMatchesQuery(Track track, string query)
    {
        if (track.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (track.AlbumName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (track.Artists.Any(artist => artist.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (track.AlbumArtists.Any(artist => artist.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return track.Genres.Any(genre => genre.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static string JoinNames(IEnumerable<string>? names, string fallback)
    {
        if (names is null)
        {
            return fallback;
        }

        var filtered = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToArray();
        return filtered.Length == 0 ? fallback : string.Join(", ", filtered);
    }

    private string? ResolveTrackValue(IEnumerable<int> trackIds, Func<Track, string?> selector)
    {
        foreach (var trackId in trackIds)
        {
            if (_trackById.TryGetValue(trackId, out var track))
            {
                var value = selector(track);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }

    private static string? FirstArtist(Track track)
    {
        var artists = track.AlbumArtists.Length > 0 ? track.AlbumArtists : track.Artists;
        return artists.FirstOrDefault(artist => !string.IsNullOrWhiteSpace(artist));
    }

    private static string GetInitialGroup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "#";
        }

        var firstChar = char.ToUpperInvariant(text[0]);
        return char.IsLetter(firstChar) ? firstChar.ToString() : "#";
    }

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return hash;
        }
    }

    private sealed class AlbumGroup
    {
        public int AlbumId { get; init; }
        public string AlbumName { get; init; } = string.Empty;
        public string ArtistNames { get; init; } = string.Empty;
        public int? ReleaseYear { get; init; }
        public int[] TrackIds { get; init; } = [];
        public string DominantColorHex { get; init; } = "#808080";
        public string ForegroundColorHex { get; init; } = "#FFFFFF";
    }
}
