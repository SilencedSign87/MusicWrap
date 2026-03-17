using MessagePack;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using MusicWrap.Data.Services;
using MusicWrap.UI.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace MusicWrap.UI.Services
{
    public interface ILibraryCacheService
    {
        Task InitializeAsync(string initialView, bool ascending);
        Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync(string viewType, bool ascending);
        IReadOnlyList<AlbumSummary> GetAlbumsForEntry(LibraryEntry entry);
        int[] GetTrackQueueForAlbum(int albumId);
        void SaveToDisk();
        void InvalidateCache();
    }
    public class LibraryCacheService : ILibraryCacheService
    {
        private static readonly string _libraryFileName = Path.Combine(AppPaths.CacheDir, "library.dat");
        private static readonly object _diskLock = new();

        private readonly MusicLibrary _library;
        private readonly IKeyValueStore _settings;
        private readonly string _coversPath;

        private LibraryEntry[]? _artistCache;
        private LibraryEntry[]? _albumCache;
        private LibraryEntry[]? _genreCache;
        private LibraryEntry[]? _decadeCache;

        private Dictionary<int, Album> _albumById = [];
        private Dictionary<int, int[]> _albumIdsByArtistId = [];
        private Dictionary<int, int[]> _albumIdsByGenreId = [];
        private Dictionary<int, int[]> _albumIdsByDecade = [];
        private Dictionary<int, int> _trackCountByAlbumId = [];
        private Dictionary<int, string> _artistNamesByAlbumId = [];
        private Dictionary<int, string> _artistNameById = [];

        public const string AllEntryType = "all";
        public const int AllEntryId = -1;

        private Dictionary<int, CoverAsset> _coverLookUp = [];
        public LibraryCacheService(MusicLibrary library, IKeyValueStore settings)
        {
            _library = library;
            _settings = settings;
            _coversPath = MusicWrapDirectories.CoverDirectory;

            BuildIndexes();
        }

        #region Interface Methods
        private void LoadFromDisk()
        {
            lock (_diskLock)
            {
                if (!File.Exists(_libraryFileName)) return;
                try
                {
                    var data = File.ReadAllBytes(_libraryFileName);
                    var library = MessagePackSerializer.Deserialize<LibraryCache>(data);
                    _artistCache = library._ByArtists;
                    _albumCache = library._ByAlbums;
                    _genreCache = library._ByGenres;
                    _decadeCache = library._ByDecades;
                }
                catch
                {
                    // If deserialization fails, we can choose to ignore it and rebuild the cache.
                    _artistCache = null;
                    _albumCache = null;
                    _genreCache = null;
                    _decadeCache = null;
                }
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
                    _ByArtists = _artistCache,
                    _ByAlbums = _albumCache,
                    _ByGenres = _genreCache,
                    _ByDecades = _decadeCache
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

        public async Task InitializeAsync(string initialView, bool ascending)
        {
            await Task.Run(() =>
            {
                LoadFromDisk();
                BuildCoverLookUp();

                switch (initialView)
                {
                    case "Album":
                        _albumCache ??= ConstructAlbumEntries();
                        break;
                    case "Artist":
                    default:
                        _artistCache ??= ConstructArtistEntries();
                        break;
                    case "Genre":
                        _genreCache ??= ConstructGenreEntries();
                        break;
                    case "Decade":
                        _decadeCache ??= ConstructDecadeEntries();
                        break;
                }
            });
        }

        public async Task<IReadOnlyList<LibraryEntry>> GetEntriesAsync(string viewType, bool ascending)
        {
            var entries = await Task.Run(() =>
            {
                return viewType switch
                {
                    "Album" => _albumCache ??= ConstructAlbumEntries(),
                    "Artist" => _artistCache ??= ConstructArtistEntries(),
                    "Genre" => _genreCache ??= ConstructGenreEntries(),
                    "Decade" => _decadeCache ??= ConstructDecadeEntries(),
                    _ => _albumCache = ConstructAlbumEntries(),
                };
            });
            entries = EnsureAllLibraryAlbum(entries);
            
            switch(viewType){
                case "Album":
                    _albumCache = entries;
                    break;
                case "Artist":
                    _artistCache = entries;
                    break;
                case "Genre":
                    _genreCache = entries;
                    break;
                case "Decade":
                    _decadeCache = entries;
                    break;
                default:
                    _albumCache = entries;
                    break;
            }

            SaveUserPreference(viewType, ascending);
            return entries;
        }

        public void InvalidateCache()
        {
            _artistCache = null;
            _albumCache = null;
            _genreCache = null;
            _decadeCache = null;

            _albumById = [];
            _albumIdsByArtistId = [];
            _albumIdsByGenreId = [];
            _albumIdsByDecade = [];
            _trackCountByAlbumId = [];
            _artistNamesByAlbumId = [];
            _artistNameById = [];
        }

        public IReadOnlyList<AlbumSummary> GetAlbumsForEntry(LibraryEntry entry)
        {
            EnsureIndexes();

            int[] albumIds = entry.Type switch
            {
                "Album" => [entry.Id],
                "Artist" => _albumIdsByArtistId.TryGetValue(entry.Id, out var byArtist) ? byArtist : [],
                "Genre" => _albumIdsByGenreId.TryGetValue(entry.Id, out var byGenre) ? byGenre : [],
                "Decade" => TryGetDecadeAlbumIds(entry.Title, out var byDecade) ? byDecade : [],
                _ => []
            };

            var result = new List<AlbumSummary>(albumIds.Length);
            foreach (var albumId in albumIds)
            {
                if (!_albumById.TryGetValue(albumId, out var album)) continue;

                if (!_trackCountByAlbumId.TryGetValue(albumId, out var trackCount) || trackCount <= 0) continue;

                string? imagePath = null;
                string DominantColorHex = "#808080";
                string ForegroundColorHex = "#FFFFFF";

                if (album.CoverId > 0 && _coverLookUp.TryGetValue(album.CoverId, out var cover))
                {
                    imagePath = Path.Combine(_coversPath, cover.FileName);
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

        #endregion

        private void SaveUserPreference(string listBy, bool ascending)
        {
            _settings.SetValue("library_list_by", listBy);
            _settings.SetValue("library_list_ascending", ascending);

            _settings.SaveToDisk();
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
                    imagePath = Path.Combine(_coversPath, cover.FileName);
                }

                entries[w++] = new LibraryEntry
                {
                    Id = albums[i].Id,
                    Type = "Album",
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
        private LibraryEntry[] ConstructArtistEntries()
        {
            EnsureIndexes();

            var artists = _library.Artists;
            var entries = new LibraryEntry[artists.Count];
            int w = 0;
            for (int i = 0; i < artists.Count; i++)
            {
                if (!_albumIdsByArtistId.TryGetValue(artists[i].Id, out var albumsId) || albumsId.Length == 0) continue;

                var imagePath = FindCover(albumsId);

                if (imagePath != null)
                    imagePath = Path.Combine(_coversPath, imagePath);

                entries[w++] = new LibraryEntry
                {
                    Id = artists[i].Id,
                    Type = "Artist",
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

                if (imagePath != null)
                    imagePath = Path.Combine(_coversPath, imagePath);

                entries[w++] = new LibraryEntry
                {
                    Id = genres[i].Id,
                    Type = "Genre",
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
                if (imagePath != null)
                    imagePath = Path.Combine(_coversPath, imagePath);

                entries[w++] = new LibraryEntry
                {
                    Id = decade,
                    Type = "Decade",
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

        private string? FindCover(IEnumerable<int> AlbumIds, IEnumerable<int>? trackIds = null)
        {
            foreach (var albumId in AlbumIds)
            {
                var album = _library.Albums.FirstOrDefault(a => a.Id == albumId);
                if (album is null) continue;

                if (album.CoverId != 0 && _coverLookUp.TryGetValue(album.CoverId, out var cover))
                {
                    return cover.FileName;
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

            foreach (var albumId in AlbumIds)
            {
                foreach (var track in _library.Tracks.Where(t => t.AlbumId == albumId))
                {
                    if (track.CoverId != 0 && _coverLookUp.TryGetValue(track.CoverId, out var cover))
                    {
                        return cover.FileName;
                    }
                }
            }
            return null;
        }
        private void EnsureIndexes()
        {
            if (_albumById.Count == 0)
                BuildIndexes();

            if (_coverLookUp.Count == 0)
                BuildCoverLookUp();
        }

        private void BuildIndexes()
        {
            _albumById = _library.Albums.ToDictionary(a => a.Id);
            _artistNameById = _library.Artists.ToDictionary(ar => ar.Id, ar => ar.Name);

            _trackCountByAlbumId = _library.Tracks
                .GroupBy(t => t.AlbumId)
                .ToDictionary(g => g.Key, g => g.Count());

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
        }

        private bool TryGetDecadeAlbumIds(string decadeTitle, out int[] albumIds)
        {
            albumIds = [];

            if (!int.TryParse(decadeTitle.TrimEnd('s'), out int decade))
            {
                return false;
            }

            return _albumIdsByDecade.TryGetValue(decade, out albumIds);
        }

        private static LibraryEntry CreateAllAlbumsEntry()
        {
            return new LibraryEntry
            {
                Id = AllEntryId,
                Type = AllEntryType,
                ImagePath = null,
                Title = "All Albums",
                Description = "Search across your library",
                GroupKey = "",
            };
        }

        private static LibraryEntry[] EnsureAllLibraryAlbum(LibraryEntry[] entries)
        {
            if (entries.Length > 0 && entries[0].Id == AllEntryId && entries[0].Type == AllEntryType)
                return entries;

            int existingIndex = -1;
            for (int i = 0;  i <entries.Length; i++)
            {
                var e = entries[i];
                if (e.Id == AllEntryId && e.Type == AllEntryType)
                {
                    existingIndex = i;
                    break;
                }
            }
            if (existingIndex == -1)
            {
                var result = new LibraryEntry[entries.Length + 1];
                result[0] = CreateAllAlbumsEntry();
                Array.Copy(entries, 0, result, 1, entries.Length);
                return result;
            }
            var reordered = new LibraryEntry[entries.Length];
            reordered[0] = entries[existingIndex];
            int w = 1;
            for (int i = 0; i < entries.Length; i++)
            {
                if (i == existingIndex) continue;
                reordered[w++] = entries[i];
            }
            return reordered;
        }

    }

    [MessagePackObject]
    public sealed class LibraryEntry
    {
        [Key(0)] public int Id { get; set; }
        [Key(1)] public required string Type { get; set; }
        [Key(2)] public string? ImagePath { get; set; }
        [Key(3)] public required string Title { get; set; }
        [Key(4)] public required string Description { get; set; }
        [Key(5)] public required string GroupKey { get; set; }
    }

    [MessagePackObject]
    public sealed class LibraryCache
    {
        [Key(0)] public LibraryEntry[]? _ByArtists { get; set; } = null;
        [Key(1)] public LibraryEntry[]? _ByAlbums { get; set; } = null;
        [Key(2)] public LibraryEntry[]? _ByGenres { get; set; } = null;
        [Key(3)] public LibraryEntry[]? _ByDecades { get; set; } = null;
    }

    public sealed class AlbumSummary
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Year { get; set; }
        public string ArtistNames { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string DominantColorHex { get; set; } = "#808080";
        public string ForegroundColorHex { get; set; } = "#FFFFFF";
    }
}
