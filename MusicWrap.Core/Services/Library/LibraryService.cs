using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

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
    private LibraryEntry[]? _albumArtistEntries;
    private LibraryEntry[]? _trackArtistEntries;
    private LibraryEntry[]? _genreEntries;
    private LibraryEntry[]? _decadeEntries;

    private Dictionary<int, Track> _trackById = [];
    private Dictionary<int, CoverAsset> _coverById = [];
    private Dictionary<int, AlbumGroup> _albumGroupsById = [];
    private Dictionary<int, int[]> _trackIdsByAlbumId = [];
    private Dictionary<int, int[]> _trackIdsByAlbumArtistId = [];
    private Dictionary<int, int[]> _trackIdsByGenreId = [];
    private Dictionary<int, int[]> _trackIdsByTrackArtistId = [];

    private Dictionary<int, string> _albumArtistNameById = [];
    private Dictionary<int, string> _trackArtistNameById = [];
    private Dictionary<int, string> _genreNameById = [];

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

        int[] trackIds = GetDirectTrackIdsForEntry(entry);

       
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
            _albumArtistEntries = null;
            _trackArtistEntries = null;
            _genreEntries = null;
            _decadeEntries = null;
            _entriesCache.Clear();
        }
    }

    private int[] GetDirectTrackIdsForEntry(LibraryEntry entry)
    {
        return entry.Type switch
        {
            "Album" => _trackIdsByAlbumId.TryGetValue(entry.Id, out var ids) ? ids : [],
            "AlbumArtist" => _trackIdsByAlbumArtistId.TryGetValue(entry.Id, out var ids) ? ids : [],
            "TrackArtist" => _trackIdsByTrackArtistId.TryGetValue(entry.Id, out var ids) ? ids : [],
            "Genre" => _trackIdsByGenreId.TryGetValue(entry.Id, out var ids) ? ids : [],
            "Decade" => [.. _library.Tracks
            .Where(t => t.ReleaseYear.HasValue && (t.ReleaseYear.Value / 10) * 10 == entry.Id)
            .Select(t => t.Id)],
            _ => []
        };
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
            "AlbumArtist" or "Artist" => GetAlbumArtistEntries(ascending),
            "TrackArtist" => GetTrackArtistEntries(ascending),
            "Genre" => GetGenreEntries(ascending),
            "Decade" => GetDecadeEntries(ascending),
            _ => GetAlbumEntries(ascending)
        };
        if (viewType != _userSettings.LibraryListBy || ascending != _userSettings.LibraryAscending)
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

    private LibraryEntry[] GetAlbumArtistEntries(bool ascending)
    {
        if (_albumArtistEntries is null)
        {
            _albumArtistEntries = _trackIdsByAlbumArtistId.Select(kvp =>
            {
                var artistId = kvp.Key;
                var trackIds = kvp.Value;
                var displayName = _albumArtistNameById.TryGetValue(artistId, out var name) ? name : "Unknown Artist";

                return new LibraryEntry
                {
                    Id = artistId,
                    Type = "AlbumArtist",
                    Title = displayName,
                    ImagePath = FindCover(trackIds: trackIds),
                    Description = $"{GetAlbumIdsForTracks(trackIds).Distinct().Count()} album{(GetAlbumIdsForTracks(trackIds).Distinct().Count() == 1 ? string.Empty : "s")}",
                    GroupKey = GetInitialGroup(displayName)
                };
            }).ToArray();
        }

        return ascending
            ? [.. _albumArtistEntries.OrderBy(entry => entry.GroupKey).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)]
            : [.. _albumArtistEntries.OrderByDescending(entry => entry.GroupKey).ThenByDescending(entry => entry.Title, StringComparer.OrdinalIgnoreCase)];
    }
    private LibraryEntry[] GetTrackArtistEntries(bool ascending)
    {
        if (_trackArtistEntries is null)
        {
            _trackArtistEntries = _trackIdsByTrackArtistId.Select(kvp =>
            {
                var artistId = kvp.Key;
                var trackIds = kvp.Value;
                var displayName = _trackArtistNameById.TryGetValue(artistId, out var name) ? name : "Unknown Artist";

                return new LibraryEntry
                {
                    Id = artistId,
                    Type = "TrackArtist",
                    Title = displayName,
                    ImagePath = FindCover(trackIds: trackIds),
                    Description = $"{trackIds.Length} track{(trackIds.Length == 1 ? string.Empty : "s")}",
                    GroupKey = GetInitialGroup(displayName)
                };
            }).ToArray();
        }

        return ascending
            ? [.. _trackArtistEntries.OrderBy(entry => entry.GroupKey).ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)]
            : [.. _trackArtistEntries.OrderByDescending(entry => entry.GroupKey).ThenByDescending(entry => entry.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private LibraryEntry[] GetGenreEntries(bool ascending)
    {
        if (_genreEntries is null)
        {
            _genreEntries = _trackIdsByGenreId.Select(kvp =>
            {
                var genreId = kvp.Key;
                var trackIds = kvp.Value;
                var displayName = _genreNameById.TryGetValue(genreId, out var name) ? name : "Unknown Genre";

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
            "AlbumArtist" => _trackIdsByAlbumArtistId.TryGetValue(entry.Id, out var artistTrackIds)
                ? GetAlbumIdsForTracks(artistTrackIds)
                : [],
            "TrackArtist" => _trackIdsByTrackArtistId.TryGetValue(entry.Id, out var trackArtistTrackIds)
                ? GetAlbumIdsForTracks(trackArtistTrackIds)
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
        var trackArtists = BuildArtistGroupInternal(false);
        var albumArtists = BuildArtistGroupInternal(true);

        _trackIdsByTrackArtistId = trackArtists.ToDictionary(g => g.Id, g => g.TrackIds);
        _trackIdsByAlbumArtistId = albumArtists.ToDictionary(g => g.Id, g => g.TrackIds);
        _trackArtistNameById = trackArtists.ToDictionary(g => g.Id, g => g.Name);
        _albumArtistNameById = albumArtists.ToDictionary(g => g.Id, g => g.Name);
    }

    private (int Id, string Name, int[] TrackIds)[] BuildArtistGroupInternal(bool isAlbumArtist)
    {
        return _library.Tracks
            .SelectMany(track => GetArtistTokens(track, isAlbumArtist)
                .Select(artist => (Artist: artist, TrackId: track.Id)))
            .GroupBy(item => NormalizeArtistKey(item.Artist))
            .Select(group =>
            {
                var artistId = StableHash(group.Key);
                var displayName = group.Select(x => x.Artist)
                                       .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a))
                                    ?? "Unknown Artist";
                return (Id: artistId, Name: displayName, TrackIds: group.Select(x => x.TrackId).Distinct().ToArray());
            })
            .ToArray();
    }

    private void BuildGenreGroups()
    {
        var groups = _library.Tracks
        .SelectMany(track => track.Genres.Select(genre => (Genre: genre, TrackId: track.Id)))
        .GroupBy(item => NormalizeKey(item.Genre))
        .Select(group =>
        {
            var genreId = StableHash(group.Key);
            var displayName = group.Select(x => x.Genre)
                                   .FirstOrDefault(g => !string.IsNullOrWhiteSpace(g))
                                ?? "Unknown Genre";
            return new
            {
                Id = genreId,
                Name = displayName,
                TrackIds = group.Select(x => x.TrackId).Distinct().ToArray()
            };
        })
        .ToArray();
        _trackIdsByGenreId = groups.ToDictionary(g => g.Id, g => g.TrackIds);
        _genreNameById = groups.ToDictionary(g => g.Id, g => g.Name);
    }

    private static string CreateAlbumKey(Track track)
    {
        var album = string.IsNullOrWhiteSpace(track.AlbumName) ? "Unknown Album" : track.AlbumName.Trim();

        //var artists = GetArtistTokens(track)
        //    .Select(artist => artist.Trim())
        //    .Where(artist => !string.IsNullOrWhiteSpace(artist))
        //    .OrderBy(artist => artist, StringComparer.OrdinalIgnoreCase)
        //    .ToArray();

        //if (artists.Length == 0)
        //{
        //    artists = ["Unknown Artist"];
        //}

        return $"{NormalizeKey(album)}";
    }

    private static ImmutableArray<string> GetArtistTokens(Track track, bool isAlbumArtist)
    {
        ImmutableArray<string> artists;
        if (isAlbumArtist && track.AlbumArtists.Length > 0)
        {
            artists = track.AlbumArtists;
        }
        else if (track.Artists.Length > 0)
        {
            artists = track.Artists;
        }
        else
        {
            artists = ImmutableArray<string>.Empty;
        }
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

    private static string GetInitialGroup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "#";
        }

        var firstChar = char.ToUpperInvariant(text[0]);
        return char.IsLetter(firstChar) ? firstChar.ToString() : "#";
    }

    private static string NormalizeArtistKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);

            if (cat == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
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
