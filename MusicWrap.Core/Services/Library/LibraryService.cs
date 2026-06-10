using MessagePack;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User;
using MusicWrap.Data.User.Models;
using System.IO;

namespace MusicWrap.Core.Services.Library
{
    public interface ILibraryService
    {
        Track? GetTrackById(int trackId);
        Album? GetAlbumById(int albumId);
        List<Genre> GetGenreById(List<int> genreIds);
        int[] GetTracksForAlbum(int albumId, bool useSearchQuery = false);
        int[] GetTrackIdsForEntryAlbum(LibraryEntry entry, int AlbumId, bool useSearchQuery = false);
        int[] GetTrackIdsForEntry(LibraryEntry entry, bool useSearchQuery = false);
        int GetAlbumDuration(int albumId);
        CoverAsset? GetCoverAsset(int coverId);
        string[] GetArtistNamesByIds(int[] artistIds);
        int[] GetAlbumIdsForArtist(int artistId);
        int[] GetAlbumIdsForGenre(int genreId);
        int[] GetAlbumIdsForDecade(int decadeStart);
        IReadOnlyList<Album> GetAlbumsByIds(IEnumerable<int> albumIds);
        ScanDirectory[] GetDirectories();
        Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync(LibraryEntryType viewType, bool ascending, bool useSearchQuery = false);
        IReadOnlyList<AlbumSummary> GetAlbumsForEntry(LibraryEntry entry, bool useSearchQuery = false);
        List<TrackRowItem> TrackIdsToTrackRowItems(IEnumerable<int> trackIds);
        string GetArtistNamesForAlbum(int albumId);
        string GetArtistNamesForTrack(int trackId);
        string? FindCover(IEnumerable<int>? albumIds = null, IEnumerable<int>? trackIds = null);
        int[] GetTrackQueueForAlbum(int albumId);
        void SaveToDisk();
        void InvalidateCache();
    }
    public class LibraryService : ILibraryService
    {
        private static readonly string _libraryFileName = Path.Combine(MusicWrapDirectories.CacheDirectory, "library.dat");
        private static readonly Lock _diskLock = new();
        private static readonly Lock _indexLock = new();

        private readonly ISearchQueryProvider _searchQueryProvider;
        private readonly MusicLibrary _library;
        private readonly UserSettings _userSettings;

        private LibraryEntry[]? _trackArtistCache;
        private LibraryEntry[]? _albumArtistCache;
        private LibraryEntry[]? _albumCache;
        private LibraryEntry[]? _genreCache;
        private LibraryEntry[]? _decadeCache;

        private Dictionary<int, Track> _trackById = [];
        private Dictionary<int, Album> _albumById = [];

        private Dictionary<int, int[]> _trackIdsByArtistId = [];
        private Dictionary<int, int[]> _albumIdsByArtistId = [];

        private Dictionary<int, int[]> _albumIdsByGenreId = [];
        private Dictionary<int, int[]> _albumIdsByDecade = [];
        private Dictionary<int, int> _trackCountByAlbumId = [];
        private Dictionary<int, string> _artistNamesByAlbumId = [];
        private Dictionary<int, string> _artistNamesByTrackId = [];
        private Dictionary<int, string> _artistNameById = [];
        private Dictionary<int, int>? _albumDurationById = [];


        private Dictionary<int, CoverAsset> _coverLookUp = [];
        public LibraryService(MusicLibrary library, UserSettings userSettings, ISearchQueryProvider searchQueryProvider)
        {
            _library = library;
            _userSettings = userSettings;
            _searchQueryProvider = searchQueryProvider;
            BuildIndexes();
            Initialize();
        }

        private void LoadFromDisk()
        {
            lock (_diskLock)
            {
                if (!File.Exists(_libraryFileName)) return;
                try
                {
                    var data = File.ReadAllBytes(_libraryFileName);
                    var library = MessagePackSerializer.Deserialize<LibraryCache>(data);
                    _trackArtistCache = library.ByTrackArtists;
                    _albumArtistCache = library.ByAlbumArtists;
                    _albumCache = library.ByAlbums;
                    _genreCache = library.ByGenres;
                    _decadeCache = library.ByDecades;
                }
                catch
                {
                    // If deserialization fails, we can choose to ignore it and rebuild the cache.
                    _trackArtistCache = null;
                    _albumArtistCache = null;
                    _albumCache = null;
                    _genreCache = null;
                    _decadeCache = null;
                }
            }
        }
        #region Interface Methods

        public string GetArtistNamesForAlbum(int albumId)
        {
            EnsureIndexes();
            return _artistNamesByAlbumId.TryGetValue(albumId, out var name) ? name : "Unknown Artist";
        }
        public string GetArtistNamesForTrack(int trackId)
        {
            EnsureIndexes();
            return _artistNamesByTrackId.TryGetValue(trackId, out var name) ? name : "Unknown Artist";
        }
        public int GetAlbumDuration(int albumId)
        {
            EnsureIndexes();
            if (_albumDurationById is not null && _albumDurationById.TryGetValue(albumId, out var duration))
            {
                return duration;
            }
            else
            {
                _albumDurationById ??= [];
                // calculate duration
                var tracks = _library.Tracks.Where(t => t.AlbumId == albumId);
                int durationSeconds = 0;
                foreach (var track in tracks)
                {
                    durationSeconds += track.Duration;
                }
                _albumDurationById[albumId] = durationSeconds;
                return durationSeconds;
            }
        }

        public void SaveToDisk()
        {

            lock (_diskLock)
            {
                var directory = Path.GetDirectoryName(_libraryFileName);
                if (directory is null) return;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var data = MessagePackSerializer.Serialize(new LibraryCache
                {
                    ByTrackArtists = _trackArtistCache,
                    ByAlbumArtists = _albumArtistCache,
                    ByAlbums = _albumCache,
                    ByGenres = _genreCache,
                    ByDecades = _decadeCache
                });

                var tmp = _libraryFileName + ".tmp";
                File.WriteAllBytes(tmp, data);

                if (File.Exists(_libraryFileName))
                {
                    File.Replace(tmp, _libraryFileName, null);
                }
                else
                {
                    File.Move(tmp, _libraryFileName);
                }
            }
        }

        public void Initialize()
        {
            LoadFromDisk();
            BuildCoverLookUp();

            switch (_userSettings.LibrarySettings.EntryType)
            {
                case LibraryEntryType.Album:
                    _albumCache ??= ConstructAlbumEntries();
                    break;
                case LibraryEntryType.TrackArtist:
                    _trackArtistCache ??= ConstructTrackArtistEntries();
                    break;
                case LibraryEntryType.AlbumArtist:
                default:
                    _albumArtistCache ??= ConstructAlbumArtistEntries();
                    break;
                case LibraryEntryType.Genre:
                    _genreCache ??= ConstructGenreEntries();
                    break;
                case LibraryEntryType.Decade:
                    _decadeCache ??= ConstructDecadeEntries();
                    break;
            }
        }

        public async Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync(LibraryEntryType viewType, bool ascending, bool useSearchQuery = false)
        {
            var entries = await Task.Run(() =>
            {
                return viewType switch
                {
                    LibraryEntryType.Album => _albumCache ??= ConstructAlbumEntries(),
                    LibraryEntryType.TrackArtist => _trackArtistCache ??= ConstructTrackArtistEntries(),
                    LibraryEntryType.AlbumArtist => _albumArtistCache ??= ConstructAlbumArtistEntries(),
                    LibraryEntryType.Genre => _genreCache ??= ConstructGenreEntries(),
                    LibraryEntryType.Decade => _decadeCache ??= ConstructDecadeEntries(),
                    _ => _albumCache = ConstructAlbumEntries(),
                };
            });

            switch (viewType)
            {
                case LibraryEntryType.Album:
                    _albumCache = entries;
                    break;
                case LibraryEntryType.TrackArtist:
                    _trackArtistCache = entries;
                    break;
                case LibraryEntryType.AlbumArtist:
                    _albumArtistCache = entries;
                    break;
                case LibraryEntryType.Genre:
                    _genreCache = entries;
                    break;
                case LibraryEntryType.Decade:
                    _decadeCache = entries;
                    break;
                default:
                    _albumCache = entries;
                    break;
            }

            if (useSearchQuery)
            {
                return FilterEntries(entries.ToList(), _searchQueryProvider.ActiveQuery);
            }
            else
            {
                return entries;
            }
        }

        public List<TrackRowItem> TrackIdsToTrackRowItems(IEnumerable<int> trackIds)
        {
            var trackRowItems = new List<TrackRowItem>();
            int index = 1;
            foreach (var trackId in trackIds)
            {
                var track = _library.Tracks.FirstOrDefault(t => t.Id == trackId);
                if (track is null) continue;
                var album = _albumById.TryGetValue(track.AlbumId, out var alb) ? alb : null;
                var artistNames = GetArtistNamesForTrack(trackId);
                trackRowItems.Add(new TrackRowItem
                {
                    Id = track.Id,
                    Title = track.Title,
                    ArtistNames = artistNames,
                    AlbumName = album?.Title ?? "Unknow artists",
                    DiskNumber = track.Disk,
                    CoverAssetPath = track.CoverId != 0 && _coverLookUp.TryGetValue(track.CoverId, out var cover) ? cover.FileName : null,
                    DurationText = TimeSpan.FromSeconds(track.Duration).ToString(@"m\:ss"),
                    TrackNumber = track.TrackNumber,
                    ListIndex = index
                });
                index++;
            }
            return trackRowItems;
        }

        public int[] GetTrackIdsForEntry(LibraryEntry entry, bool useSearchQuery = false)
        {
            EnsureIndexes();

            if (entry.Type == LibraryEntryType.TrackArtist)
            {
                if (!_trackIdsByArtistId.TryGetValue(entry.Id, out var byTrackArtist))
                    return [];
                var tracks = _library.Tracks.Where(t => byTrackArtist.Contains(t.Id));
                if (useSearchQuery && !string.IsNullOrWhiteSpace(_searchQueryProvider.ActiveQuery))
                {
                    var q = _searchQueryProvider.ActiveQuery.Trim();
                    tracks = tracks.Where(t => TrackMatchesQuery(t, q));
                }
                return [.. tracks
            .OrderBy(t => t.Disk)
            .ThenBy(t => t.TrackNumber)
            .ThenBy(t => t.Title)
            .Select(t => t.Id)];
            }
            else
            {
                int[] albumIds = entry.Type switch
                {
                    LibraryEntryType.Album => [entry.Id],
                    LibraryEntryType.AlbumArtist => _albumIdsByArtistId.TryGetValue(entry.Id, out var byArtist) ? byArtist : [],
                    LibraryEntryType.Genre => _albumIdsByGenreId.TryGetValue(entry.Id, out var byGenre) ? byGenre : [],
                    LibraryEntryType.Decade => TryGetDecadeAlbumIds(entry.Title, out var byDecade) ? byDecade : [],
                    _ => []
                };
                var tracks = _library.Tracks.Where(t => albumIds.Contains(t.AlbumId));
                if (useSearchQuery && !string.IsNullOrWhiteSpace(_searchQueryProvider.ActiveQuery))
                {
                    var q = _searchQueryProvider.ActiveQuery.Trim();
                    tracks = tracks.Where(t => TrackMatchesQuery(t, q));
                }
                return [.. tracks
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)];
            }
        }

        public void InvalidateCache()
        {
            _trackArtistCache = null;
            _albumArtistCache = null;
            _albumCache = null;
            _genreCache = null;
            _decadeCache = null;

            _trackById = [];
            _albumById = [];
            _trackIdsByArtistId = [];
            _albumIdsByArtistId = [];
            _albumIdsByGenreId = [];
            _albumIdsByDecade = [];
            _trackCountByAlbumId = [];
            _artistNamesByAlbumId = [];
            _artistNameById = [];
            _coverLookUp = [];
            _albumDurationById = [];
        }
        public Track? GetTrackById(int trackId)
        {
            EnsureIndexes();
            return _trackById.TryGetValue(trackId, out var track) ? track : null;
        }
        public Album? GetAlbumById(int albumId)
        {
            EnsureIndexes();
            return _albumById.TryGetValue(albumId, out var album) ? album : null;
        }
        public List<Genre> GetGenreById(List<int> genreIds)
        {
            return genreIds.Select(genreId => _library.Genres.FirstOrDefault(g => g.Id == genreId) ?? new Genre { Id = genreId, Name = "Unknown Genre" }).ToList();
        }
        public IReadOnlyList<AlbumSummary> GetAlbumsForEntry(LibraryEntry entry, bool useSearchQuery = false)
        {
            EnsureIndexes();

            int[] albumIds = entry.Type switch
            {
                LibraryEntryType.Album => [entry.Id],
                LibraryEntryType.TrackArtist => _trackIdsByArtistId.TryGetValue(entry.Id, out var byTrackArtist) ? _library.Tracks.Where(t => byTrackArtist.Contains(t.Id)).Select(t => t.AlbumId).Distinct().ToArray() : [],
                LibraryEntryType.AlbumArtist => _albumIdsByArtistId.TryGetValue(entry.Id, out var byArtist) ? byArtist : [],
                LibraryEntryType.Genre => _albumIdsByGenreId.TryGetValue(entry.Id, out var byGenre) ? byGenre : [],
                LibraryEntryType.Decade => TryGetDecadeAlbumIds(entry.Title, out var byDecade) ? byDecade : [],
                _ => []
            };

            var result = new List<AlbumSummary>(albumIds.Length);
            foreach (var albumId in albumIds)
            {

                if (useSearchQuery && !string.IsNullOrWhiteSpace(_searchQueryProvider.ActiveQuery))
                {
                    var q = _searchQueryProvider.ActiveQuery.Trim();
                    IEnumerable<Track> tracksForAlbum;

                    if (entry.Type == LibraryEntryType.TrackArtist && _trackIdsByArtistId.TryGetValue(entry.Id, out var byTrackArtist))
                    {
                        tracksForAlbum = _library.Tracks
                            .Where(t => t.AlbumId == albumId && byTrackArtist.Contains(t.Id));
                    }
                    else
                    {
                        tracksForAlbum = _library.Tracks.Where(t => t.AlbumId == albumId);
                    }
                    if (!tracksForAlbum.Any(t => TrackMatchesQuery(t, q)))
                    {
                        continue;
                    }
                }

                if (!_albumById.TryGetValue(albumId, out var album)) continue;

                if (!_trackCountByAlbumId.TryGetValue(albumId, out var trackCount) || trackCount <= 0) continue;

                string? imagePath = null;
                string? bluredImagePath = null;
                string DominantColorHex = "#808080";
                string ForegroundColorHex = "#FFFFFF";

                if (album.CoverId > 0 && _coverLookUp.TryGetValue(album.CoverId, out var cover))
                {
                    imagePath = cover.FileName;
                    bluredImagePath = cover.FileName;
                    DominantColorHex = cover.DominantColorHex ?? "#808080";
                    ForegroundColorHex = cover.ForegroundColorHex ?? "#FFFFFF";
                }
                var artistName = _artistNamesByAlbumId.TryGetValue(albumId, out var name) ? name : "Unknown Artist";

                result.Add(new AlbumSummary
                {
                    Id = album.Id,
                    Title = album.Title,
                    Year = album.Year,
                    ArtistNames = artistName,
                    ImagePath = imagePath ?? string.Empty,
                    BluredImagePath = bluredImagePath ?? string.Empty,
                    DominantColorHex = DominantColorHex,
                    ForegroundColorHex = ForegroundColorHex
                });
            }

            return [.. result.OrderByDescending(a => a.Year).ThenBy(a => a.Title)];
        }

        public int[] GetTrackQueueForAlbum(int albumId)
        {
            return [.. _library.Tracks
                .Where(t => t.AlbumId == albumId)
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)];
        }

        public int[] GetTracksForAlbum(int albumId, bool useSearchQuery = false)
        {

            var tracks = _library.Tracks.Where(t => t.AlbumId == albumId);
            if (useSearchQuery && !string.IsNullOrWhiteSpace(_searchQueryProvider.ActiveQuery))
            {
                var q = _searchQueryProvider.ActiveQuery.Trim();
                tracks = tracks.Where(t => TrackMatchesQuery(t, q));
            }
            return [.. tracks
                .OrderBy(t => t.Disk)
                .ThenBy(t => t.TrackNumber)
                .ThenBy(t => t.Title)
                .Select(t => t.Id)];
        }
        public int[] GetTrackIdsForEntryAlbum(LibraryEntry entry, int albumId, bool useSearchQuery = false)
        {
            EnsureIndexes();

            if (entry.Type == LibraryEntryType.TrackArtist)
            {
                if (!_trackIdsByArtistId.TryGetValue(entry.Id, out var artistTrackIds))
                    return [];
                var tracks = _library.Tracks
                    .Where(t => t.AlbumId == albumId && artistTrackIds.Contains(t.Id));
                if (useSearchQuery && !string.IsNullOrWhiteSpace(_searchQueryProvider.ActiveQuery))
                {
                    var q = _searchQueryProvider.ActiveQuery.Trim();
                    tracks = tracks.Where(t => TrackMatchesQuery(t, q));
                }
                return [.. tracks
            .OrderBy(t => t.Disk)
            .ThenBy(t => t.TrackNumber)
            .ThenBy(t => t.Title)
            .Select(t => t.Id)];
            }

            return GetTracksForAlbum(albumId, useSearchQuery);
        }

        public CoverAsset? GetCoverAsset(int coverId)
        {
            EnsureIndexes();
            return _coverLookUp.TryGetValue(coverId, out var cover) ? cover : null;
        }

        public string[] GetArtistNamesByIds(int[] artistIds)
        {
            EnsureIndexes();
            return artistIds.Select(id => _artistNameById.TryGetValue(id, out var name) ? name : "Unknown Artist").ToArray();
        }

        public int[] GetAlbumIdsForArtist(int artistId)
        {
            EnsureIndexes();
            return _albumIdsByArtistId.TryGetValue(artistId, out var albumIds) ? albumIds : Array.Empty<int>();
        }

        public int[] GetAlbumIdsForGenre(int genreId)
        {
            EnsureIndexes();
            return _albumIdsByGenreId.TryGetValue(genreId, out var albumIds) ? albumIds : Array.Empty<int>();
        }

        public int[] GetAlbumIdsForDecade(int decadeStart)
        {
            EnsureIndexes();
            return _albumIdsByDecade.TryGetValue(decadeStart, out var albumIds) ? albumIds : Array.Empty<int>();
        }

        public IReadOnlyList<Album> GetAlbumsByIds(IEnumerable<int> albumIds)
        {
            return _library.Albums.Where(a => albumIds.Contains(a.Id)).ToList();
        }

        public ScanDirectory[] GetDirectories()
        {
            return _library.Directories.ToArray();
        }
        public string? FindCover(IEnumerable<int>? albumIds = null, IEnumerable<int>? trackIds = null)
        {

            if (albumIds is not null)
            {

                foreach (var albumId in albumIds)
                {
                    var album = _library.Albums.FirstOrDefault(a => a.Id == albumId);
                    if (album is null) continue;

                    if (album.CoverId != 0 && _coverLookUp.TryGetValue(album.CoverId, out var cover))
                    {
                        return cover.FileName;
                    }
                }
            }

            if (trackIds is not null)
            {
                foreach (var trackId in trackIds)
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == trackId);
                    if (track is null) continue;

                    if (track.CoverId != 0 && _coverLookUp.TryGetValue(track.CoverId, out var cover))
                    {
                        return cover.FileName;
                    }
                }
                return null;
            }

            return null;
        }
        #endregion

        #region Internal Methods
        private bool TrackMatchesQuery(Track track, string query)
        {
            if (track.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;

            // Track Artists
            foreach (var artistId in track.ArtistIds)
            {
                if (_artistNameById.TryGetValue(artistId, out var artistName) &&
                    artistName.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Album Artists
            if (_artistNamesByAlbumId.TryGetValue(track.AlbumId, out var albumArtistNames) &&
                albumArtistNames.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private List<LibraryEntry> FilterEntries(List<LibraryEntry> entries, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return entries;

            var q = query.Trim();

            var result = entries
                .Where(e => EntryMatchesQuery(e, q))
                .ToList();
            return result;
        }
        private bool EntryMatchesQuery(LibraryEntry entry, string query)
        {

            return GetTrackIdsForEntry(entry, true).Length > 0;
        }

        private void BuildCoverLookUp()
        {
            _coverLookUp = _library.CoverAssets.ToDictionary(c => c.Id, c => c);
        }

        private LibraryEntry[] ConstructAlbumEntries()
        {
            EnsureIndexes();

            var albums = _library.Albums;
            var entries = new LibraryEntry[albums.Count];
            int w = 0;

            for (int i = 0; i < albums.Count; i++)
            {
                if (!_trackCountByAlbumId.TryGetValue(albums[i].Id, out var trackCount) || trackCount <= 0) continue;

                string? imagePath = null;
                if (_coverLookUp.TryGetValue(albums[i].CoverId, out var cover))
                {
                    imagePath = cover.FileName;
                }

                entries[w++] = new LibraryEntry
                {
                    Id = albums[i].Id,
                    Type = LibraryEntryType.Album,
                    ImagePath = imagePath,
                    Title = albums[i].Title,
                    Description = $"{trackCount} track{(trackCount > 1 ? "s" : "")}",
                    GroupKey = GetInitialGroup(albums[i].Title)
                };
            }
            if (w == entries.Length) return entries;

            Array.Resize(ref entries, w);

            return entries;
        }
        private LibraryEntry[] ConstructTrackArtistEntries()
        {
            EnsureIndexes();
            var artists = _library.Artists;
            var entries = new LibraryEntry[artists.Count];
            int w = 0;
            for (int i = 0; i < artists.Count; i++)
            {
                if (!_trackIdsByArtistId.TryGetValue(artists[i].Id, out var trackIds) || trackIds.Length == 0) continue;
                var imagePath = FindCover(null, trackIds);

                entries[w++] = new LibraryEntry
                {
                    Id = artists[i].Id,
                    Type = LibraryEntryType.TrackArtist,
                    ImagePath = imagePath,
                    Title = artists[i].Name,
                    Description = $"{trackIds.Length} track{(trackIds.Length > 1 ? "s" : "")}",
                    GroupKey = GetInitialGroup(artists[i].Name)
                };
            }
            if (w == entries.Length) return entries;
            Array.Resize(ref entries, w);
            return entries;
        }
        private LibraryEntry[] ConstructAlbumArtistEntries()
        {
            EnsureIndexes();

            var artists = _library.Artists;
            var entries = new LibraryEntry[artists.Count];
            int w = 0;
            for (int i = 0; i < artists.Count; i++)
            {
                if (!_albumIdsByArtistId.TryGetValue(artists[i].Id, out var albumsId) || albumsId.Length == 0) continue;

                var imagePath = FindCover(albumsId);

                entries[w++] = new LibraryEntry
                {
                    Id = artists[i].Id,
                    Type = LibraryEntryType.AlbumArtist,
                    ImagePath = imagePath,
                    Title = artists[i].Name,
                    Description = $"{albumsId.Length} album{(albumsId.Length > 1 ? "s" : "")}",
                    GroupKey = GetInitialGroup(artists[i].Name)
                };
            }
            if (w == entries.Length) return entries;
            Array.Resize(ref entries, w);
            return entries;
        }
        private LibraryEntry[] ConstructGenreEntries()
        {
            EnsureIndexes();

            var genres = _library.Genres;
            var entries = new LibraryEntry[genres.Count];
            int w = 0;
            for (int i = 0; i < genres.Count; i++)
            {
                if (!_albumIdsByGenreId.TryGetValue(genres[i].Id, out var albums) || albums.Length == 0) continue;

                var imagePath = FindCover(albums);

                entries[w++] = new LibraryEntry
                {
                    Id = genres[i].Id,
                    Type = LibraryEntryType.Genre,
                    ImagePath = imagePath,
                    Title = genres[i].Name,
                    Description = $"{albums.Length} album{(albums.Length > 1 ? "s" : "")}",
                    GroupKey = GetInitialGroup(genres[i].Name)
                };
            }
            if (w == entries.Length) return entries;
            Array.Resize(ref entries, w);
            return entries;
        }
        private LibraryEntry[] ConstructDecadeEntries()
        {
            EnsureIndexes();

            if (_albumIdsByDecade.Count == 0) return [];

            var entries = new LibraryEntry[_albumIdsByDecade.Count];
            int w = 0;

            foreach (var decadeKvp in _albumIdsByDecade.OrderBy(x => x.Key))
            {
                var decade = decadeKvp.Key;
                var albums = decadeKvp.Value;

                var imagePath = FindCover(albums);

                entries[w++] = new LibraryEntry
                {
                    Id = decade,
                    Type = LibraryEntryType.Decade,
                    ImagePath = imagePath,
                    Title = $"{decade}s",
                    Description = $"{albums.Length} album{(albums.Length > 1 ? "s" : "")}",
                    GroupKey = GetInitialGroup($"{decade}s")
                };
            }

            if (w == entries.Length) return entries;

            Array.Resize(ref entries, w);
            return entries;
        }

        private static string GetInitialGroup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "#";

            char firstChar = char.ToUpperInvariant(text[0]);

            if (char.IsLetter(firstChar))
                return firstChar.ToString();

            return "#"; // numbers
        }

        private void EnsureIndexes()
        {
            lock (_indexLock)
            {
                bool mustRebuildIndexes =
                    _albumById.Count != _library.Albums.Count ||
                    _artistNameById.Count != _library.Artists.Count ||
                    _trackCountByAlbumId.Count != _library.Albums.Count ||
                    _artistNamesByAlbumId.Count != _library.Albums.Count;
                if (mustRebuildIndexes)
                {
                    BuildIndexes();
                }

                bool mustRebuildCoverLookUp =
                    _coverLookUp.Count != _library.CoverAssets.Count ||
                    (_library.CoverAssets.Count > 0 && !_coverLookUp.ContainsKey(_library.CoverAssets[0].Id));
                if (mustRebuildCoverLookUp)
                {
                    BuildCoverLookUp();
                }

            }
        }

        private void BuildIndexes()
        {
            _trackById = _library.Tracks.ToDictionary(t => t.Id);

            _albumById = _library.Albums.ToDictionary(a => a.Id);

            _artistNameById = _library.Artists.ToDictionary(ar => ar.Id, ar => ar.Name);

            _trackCountByAlbumId = _library.Tracks
                .GroupBy(t => t.AlbumId)
                .ToDictionary(g => g.Key, g => g.Count());

            _trackIdsByArtistId = _library.Tracks
                .SelectMany(t => t.ArtistIds.Select(arId => (ArtistId: arId, TrackId: t.Id)))
                .GroupBy(x => x.ArtistId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TrackId).ToArray());

            _albumIdsByArtistId = _library.Albums
                .SelectMany(a => a.ArtistIds.Select(arId => (ArtistId: arId, AlbumId: a.Id)))
                .GroupBy(x => x.ArtistId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.AlbumId).ToArray());

            _albumIdsByGenreId = _library.Tracks
                .SelectMany(t => t.GenreIds.Select(gId => (GenreId: gId, AlbumId: t.AlbumId)))
                .GroupBy(x => x.GenreId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.AlbumId).Distinct().ToArray());

            _albumIdsByDecade = _library.Albums
                .GroupBy(a => (a.Year / 10) * 10)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Id).ToArray());

            _artistNamesByAlbumId = new Dictionary<int, string>(_library.Albums.Count);
            foreach (var album in _library.Albums)
            {
                var names = album.ArtistIds
                    .Where(id => _artistNameById.ContainsKey(id))
                    .Select(id => _artistNameById[id]);
                _artistNamesByAlbumId[album.Id] = string.Join(", ", names);
            }
            _artistNamesByTrackId = _library.Tracks.ToDictionary(t => t.Id, t =>
            {
                if (t.ArtistIds.Length > 0)
                    return string.Join(", ", t.ArtistIds.Where(id => _artistNameById.ContainsKey(id)).Select(id => _artistNameById[id]));
                return _artistNamesByAlbumId.TryGetValue(t.AlbumId, out var albumArtistNames) ? albumArtistNames : "Unknown Artist";
            });
        }

        private bool TryGetDecadeAlbumIds(string decadeTitle, out int[] albumIds)
        {
            albumIds = [];

            if (!int.TryParse(decadeTitle.TrimEnd('s'), out int decade))
            {
                return false;
            }

            return _albumIdsByDecade.TryGetValue(decade, out albumIds!);
        }
        #endregion
    }
    public sealed class TrackRowItem
    {
        public int Id { get; init; }
        public int ListIndex { get; init; }
        public int DiskNumber { get; init; }
        public int TrackNumber { get; init; }
        public string Title { get; init; } = "";
        public string ArtistNames { get; init; } = "";
        public string AlbumName { get; init; } = "";
        public string DurationText { get; init; } = "";
        public string? CoverAssetPath { get; init; }
        public string TrackNumberDisplay => TrackNumber > 0 ? TrackNumber.ToString() : "";
    }
}
