using MessagePack;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using MusicWrap.Data.Services;
using MusicWrap.UI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace MusicWrap.UI.Services
{
    public interface ILibraryCacheService
    {
        Task InitializeAsync(string initialView, bool ascending);
        Task<List<LibraryEntry>> GetEntriesAsync(string viewType, bool ascending);
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

        private List<LibraryEntry>? _artistCache;
        private List<LibraryEntry>? _albumCache;
        private List<LibraryEntry>? _genreCache;
        private List<LibraryEntry>? _decadeCache;

        private Dictionary<int, CoverAsset> _coverLookUp = [];
        public LibraryCacheService(MusicLibrary library, IKeyValueStore settings)
        {
            _library = library;
            _settings = settings;
            _coversPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MusicWrap",
                "covers"
                );
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
                    _artistCache = library._ByArtists != null ? LibraryEntryToRecord(library._ByArtists).ToList() : null;
                    _albumCache = library._ByAlbums != null ? LibraryEntryToRecord(library._ByAlbums).ToList() : null;
                    _genreCache = library._ByGenres != null ? LibraryEntryToRecord(library._ByGenres).ToList() : null;
                    _decadeCache = library._ByDecades != null ? LibraryEntryToRecord(library._ByDecades).ToList() : null;
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
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                var data = MessagePackSerializer.Serialize(new LibraryCache
                {
                    _ByArtists = _artistCache != null ? LibraryEntryToClass(_artistCache).ToArray() : null,
                    _ByAlbums = _albumCache != null ? LibraryEntryToClass(_albumCache).ToArray() : null,
                    _ByGenres = _genreCache != null ? LibraryEntryToClass(_genreCache).ToArray() : null,
                    _ByDecades = _decadeCache != null ? LibraryEntryToClass(_decadeCache).ToArray() : null
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

        public async Task<List<LibraryEntry>> GetEntriesAsync(string viewType, bool ascending)
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
            SaveUserPreference(viewType, ascending);
            return ascending ? entries.OrderBy(e => e.Title).ToList() : entries.OrderByDescending(e => e.Title).ToList();
        }
        private void SaveUserPreference(string listBy, bool ascending)
        {
            _settings.SetValue("library_list_by", listBy);
            _settings.SetValue("library_list_ascending", ascending);

            _settings.SaveToDisk();
        }
        public void InvalidateCache()
        {
            _artistCache = null;
            _albumCache = null;
            _genreCache = null;
            _decadeCache = null;
        }

        private void BuildCoverLookUp()
        {
            _coverLookUp = _library.CoverAssets.ToDictionary(c => c.Id, c => c);
        }

        private List<LibraryEntry> ConstructAlbumEntries()
        {
            var albums = _library.Albums;
            var entries = new List<LibraryEntry>(albums.Count);

            for (int i = 0; i < albums.Count; i++)
            {
                var trackCount = _library.Tracks.Count(t => t.AlbumId == albums[i].Id);
                if (trackCount <= 0) continue;

                string? imagePath = null;
                if (_coverLookUp.TryGetValue(albums[i].CoverId, out var cover))
                {
                    imagePath = Path.Combine(_coversPath, cover.FileName);
                }

                entries.Add(new LibraryEntry(
                    Id: albums[i].Id,
                    Type: "Album",
                    ImagePath: imagePath,
                    Image: ImageHelper.LoadThumbnail(imagePath),
                    Title: albums[i].Title,
                    Description: $"{trackCount} track{(trackCount > 1 ? "s" : "")}",
                    GroupKey: GetInitialGroup(albums[i].Title)
                    ));
            }

            return entries;
        }
        private List<LibraryEntry> ConstructArtistEntries()
        {
            var artists = _library.Artists;
            var entries = new List<LibraryEntry>(artists.Count);
            for (int i = 0; i < artists.Count; i++)
            {
                var artist = artists[i];
                var albumsId = _library.Albums.Where(a => a.ArtistIds.Contains(artist.Id)).Select(a => a.Id).ToList();
                var albumCount = albumsId.Count;
                if (albumCount <= 0) continue;

                var imagePath = FindCover(albumsId);

                if (imagePath != null)
                    imagePath = Path.Combine(_coversPath, imagePath);

                entries.Add(new LibraryEntry(
                    Id: artist.Id,
                    Type: "Artist",
                    ImagePath: imagePath,
                    Image: ImageHelper.LoadThumbnail(imagePath),
                    Title: artist.Name,
                    Description: $"{albumCount} album{(albumCount > 1 ? "s" : "")}",
                    GroupKey: GetInitialGroup(artist.Name)
                    ));
            }
            return entries;
        }
        private List<LibraryEntry> ConstructGenreEntries()
        {
            var genres = _library.Genres;
            var entries = new List<LibraryEntry>(genres.Count);
            for (int i = 0; i < genres.Count; i++)
            {
                var albums = _library.Tracks
                     .Where(t => t.GenreIds.Contains(genres[i].Id))
                     .Select(t => t.AlbumId)
                     .Distinct()
                     .ToList();
                var albumCount = albums.Count;
                if (albumCount <= 0) continue;

                var imagePath = FindCover(albums);

                if (imagePath != null)
                    imagePath = Path.Combine(_coversPath, imagePath);

                entries.Add(new LibraryEntry(
                    Id: genres[i].Id,
                    Type: "Genre",
                    ImagePath: imagePath,
                    Image: ImageHelper.LoadThumbnail(imagePath),
                    Title: genres[i].Name,
                    Description: $"{albumCount} album{(albumCount > 1 ? "s" : "")}",
                    GroupKey: GetInitialGroup(genres[i].Name)
                    ));
            }
            return entries;
        }
        private List<LibraryEntry> ConstructDecadeEntries()
        {
            var albums = _library.Albums.Where(a => a.Year > 0).ToList();
            if (albums.Count == 0) _decadeCache = [];

            var decadeGroups = albums
                .GroupBy(a => (a.Year / 10) * 10)
                .OrderBy(g => g.Key)
                .ToList();

            var entries = new List<LibraryEntry>(decadeGroups.Count);
            int entryId = 1;

            for (int i = 0; i < decadeGroups.Count; i++)
            {
                var decadeGroup = decadeGroups[i];

                var decade = decadeGroup.Key;
                var albumIds = decadeGroup.Select(a => a.Id).ToList();
                var albumCount = decadeGroup.Count();
                if (albumCount <= 0) continue;

                string? imagePath = FindCover(albumIds);

                if (imagePath != null)
                    imagePath = Path.Combine(_coversPath, imagePath);

                entries.Add(new LibraryEntry(
                    Id: entryId++,
                    Type: "Decade",
                    ImagePath: imagePath,
                    Image: ImageHelper.LoadThumbnail(imagePath),
                    Title: $"{decade}s",
                    Description: $"{albumCount} album{(albumCount > 1 ? "s" : "")}",
                    GroupKey: $"{decade}s"
                    ));
            }

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
            for (int i = 0; i < AlbumIds.Count(); i++)
            {
                var albumcoverId = _library.Albums.First(a => a.Id == AlbumIds.ElementAt(i)).CoverId;
                if (albumcoverId != 0 && _coverLookUp.TryGetValue(albumcoverId, out var cover))
                {
                    return Path.Combine(_coversPath, cover.FileName);
                }
            }

            if (trackIds != null)
            {
                for (int i = 0; i < trackIds.Count(); i++)
                {
                    var trackcoverId = _library.Tracks.First(t => t.Id == trackIds.ElementAt(i)).CoverId;
                    if (trackcoverId != 0 && _coverLookUp.TryGetValue(trackcoverId, out var cover))
                    {
                        return Path.Combine(_coversPath, cover.FileName);
                    }
                }
            }
            else
            {
                for (int i = 0; i < AlbumIds.Count(); i++)
                {
                    var tracks = _library.Tracks.Where(t => t.AlbumId == AlbumIds.ElementAt(i));
                    for (int j = 0; j < tracks.Count(); j++)
                    {
                        var trackcoverId = tracks.ElementAt(j).CoverId;
                        if (trackcoverId != 0 && _coverLookUp.TryGetValue(trackcoverId, out var cover))
                        {
                            return Path.Combine(_coversPath, cover.FileName);
                        }
                    }
                }
            }

            return null;
        }

        private static IEnumerable<LibraryEntryClass> LibraryEntryToClass(IEnumerable<LibraryEntry> entry)
        {
            var result = new LibraryEntryClass[entry.Count()];
            for (int i = 0; i < entry.Count(); i++)
            {
                var item = entry.ElementAt(i);
                result[i] = new LibraryEntryClass
                {
                    Id = item.Id,
                    Type = item.Type,
                    ImagePath = item.ImagePath,
                    Title = item.Title,
                    Description = item.Description,
                    GroupKey = item.GroupKey
                };
            }
            return result;
        }

        private static IEnumerable<LibraryEntry> LibraryEntryToRecord(IEnumerable<LibraryEntryClass> entry)
        {
            var result = new LibraryEntry[entry.Count()];
            for (int i = 0; i < entry.Count(); i++)
            {
                var item = entry.ElementAt(i);
                result[i] = new LibraryEntry(
                    Id: item.Id,
                    Type: item.Type,
                    ImagePath: item.ImagePath,
                    Image: ImageHelper.LoadThumbnail(item.ImagePath),
                    Title: item.Title,
                    Description: item.Description,
                    GroupKey: item.GroupKey
                    );
            }
            return result;
        }
    }



    public record LibraryEntry(
        int Id,
        string Type,
        string? ImagePath,
        BitmapImage? Image,
        string Title,
        string Description,
        string GroupKey
        );
    public record AlbumData(
        int Id,
        string Title,
        int Year,
        string ArtistNames,
        string? ImagePath,
        string DominantColor,
        string ForegroundColor
        );

    [MessagePackObject]
    public sealed class LibraryCache
    {
        [Key(0)] public LibraryEntryClass[]? _ByArtists { get; set; } = null;
        [Key(1)] public LibraryEntryClass[]? _ByAlbums { get; set; } = null;
        [Key(2)] public LibraryEntryClass[]? _ByGenres { get; set; } = null;
        [Key(3)] public LibraryEntryClass[]? _ByDecades { get; set; } = null;
    }
    [MessagePackObject]
    public sealed class LibraryEntryClass
    {
        [Key(0)] public int Id;
        [Key(1)] public string Type;
        [Key(2)] public string? ImagePath;
        [Key(3)] public string Title;
        [Key(4)] public string Description;
        [Key(5)] public string GroupKey;
    }
}
