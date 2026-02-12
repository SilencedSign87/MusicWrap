using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using MusicWrap.Data.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Data;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class LibraryViewModel : ObservableObject
    {
        [ObservableProperty]
        private string listBy = "Artist"; // Album, Artist, Genre, Year

        [ObservableProperty]
        private bool ascending = true;

        [ObservableProperty]
        private List<LibraryEntry> entries = [];

        [ObservableProperty]
        private CollectionViewSource entriesViewSource = new();

        [ObservableProperty]
        private bool isLoading = false;

        private MusicLibrary _library;
        private ILibraryScanner _scanner;

        private static readonly string CoversBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicWrap",
            "covers"
            );

        public LibraryViewModel(MusicLibrary library, ILibraryScanner scanner)
        {
            _library = library;
            _scanner = scanner;

            LoadEntries();
        }

        public bool IsAlbumView => ListBy == "Album";
        public bool IsArtistView => ListBy == "Artist";
        public bool IsGenreView => ListBy == "Genre";
        public bool IsDecadeView => ListBy == "Decade";

        partial void OnListByChanged(string value)
        {
            OnPropertyChanged(nameof(IsAlbumView));
            OnPropertyChanged(nameof(IsArtistView));
            OnPropertyChanged(nameof(IsGenreView));
            OnPropertyChanged(nameof(IsDecadeView));

            LoadEntries();
        }

        partial void OnAscendingChanged(bool value)
        {
            LoadEntries();
        }
        private string GetInitialGroup(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "#";

            char firstChar = char.ToUpperInvariant(text[0]);

            if (char.IsLetter(firstChar))
                return firstChar.ToString();

            return "#"; // numbers
        }

        [RelayCommand]
        private void AddFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Music Folder",
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                var selectedPath = dialog.FolderName;
                if (selectedPath is not null)
                {
                    _scanner.AddDirectory(selectedPath, true);
                }

                _scanner.ScanAllDirectories(null, null);
                LoadEntries();
            }
        }

        [RelayCommand]
        private void AddFiles()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Music Files",
                Multiselect = true,
            };
            if (dialog.ShowDialog() == true)
            {
                var selectedFiles = dialog.FileNames;
                _scanner.ScanFiles(selectedFiles, null, null);
                LoadEntries();
            }

        }

        [RelayCommand]
        private void Refresh()
        {
            LoadEntries();
        }

        [RelayCommand]
        private void SetViewMode(string mode)
        {
            if (!string.IsNullOrEmpty(mode))
            {
                ListBy = mode;
            }
        }

        [RelayCommand]
        private void SetAscending()
        {
            Ascending = true;
        }

        [RelayCommand]
        private void SetDescending()
        {
            Ascending = false;
        }

        private async void LoadEntries()
        {
            IsLoading = true;


            List<LibraryEntry> entries = await Task.Run(() =>
            {
                return ListBy switch
                {
                    "Album" => ConstructAlbumEntries(),
                    "Artist" => ConstructArtistEntries(),
                    "Genre" => ConstructGenreEntries(),
                    "Decade" => ConstructDecadeEntries(),
                    _ => ConstructArtistEntries()
                };
            });

            ApplyGrouping(entries);
            IsLoading = false;
        }
        private CoverAsset? FindCoverAsset(IEnumerable<int> albumIds, IEnumerable<int>? trackIds = null)
        {
            foreach (var albumId in albumIds)
            {
                var album = _library.Albums.FirstOrDefault(a => a.Id == albumId);
                if (album?.CoverId > 0)
                {
                    var coverAsset = _library.CoverAssets.FirstOrDefault(c => c.Id == album.CoverId);
                    if (coverAsset != null)
                        return coverAsset;
                }
            }

            if (trackIds != null)
            {
                foreach (var trackId in trackIds)
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == trackId);
                    if (track?.CoverId > 0)
                    {
                        var coverAsset = _library.CoverAssets.FirstOrDefault(c => c.Id == track.CoverId);
                        if (coverAsset != null)
                            return coverAsset;
                    }
                }
            }
            else
            {
                foreach (var albumId in albumIds)
                {
                    var tracks = _library.Tracks.Where(t => t.AlbumId == albumId);
                    foreach (var track in tracks)
                    {
                        if (track.CoverId > 0)
                        {
                            var coverAsset = _library.CoverAssets.FirstOrDefault(c => c.Id == track.CoverId);
                            if (coverAsset != null)
                                return coverAsset;
                        }
                    }
                }
            }

            return null;
        }

        private List<LibraryEntry> ConstructAlbumEntries()
        {
            var albums = _library.Albums;
            var entries = new List<LibraryEntry>();

            foreach (var album in albums)
            {
                var trackCount = _library.Tracks.Count(t => t.AlbumId == album.Id);
                if (trackCount == 0)
                {
                    continue;
                }

                var imageAsset = FindCoverAsset(new[] { album.Id });

                LibraryEntry newEntry = new LibraryEntry
                {
                    Id = album.Id,
                    Type = "Album",
                    Title = album.Title,
                    Description = $"{trackCount} track{(trackCount > 1 ? "s" : "")}",
                    ImagePath = imageAsset != null ? Path.Combine(CoversBasePath, imageAsset.FileName) : null,
                    GroupKey = GetInitialGroup(album.Title)
                };
                entries.Add(newEntry);
            }

            return Ascending ? entries.OrderBy(e => e.Title).ToList() : entries.OrderByDescending(e => e.Title).ToList();
        }

        private List<LibraryEntry> ConstructArtistEntries()
        {
            var artists = _library.Artists;
            var entries = new List<LibraryEntry>();

            foreach (var artist in artists)
            {
                var albumIds = _library.Albums.Where(a => a.ArtistIds.Contains(artist.Id)).Select(a => a.Id).ToList();
                var albumCount = albumIds.Count;

                if (albumCount == 0)
                {
                    continue;
                }

                var imageAsset = FindCoverAsset(albumIds);

                LibraryEntry newEntry = new LibraryEntry
                {
                    Id = artist.Id,
                    Type = "Artist",
                    Title = artist.Name,
                    Description = $"{albumCount} Album{(albumCount != 1 ? "s" : "")}",
                    ImagePath = imageAsset != null ? Path.Combine(CoversBasePath, imageAsset.FileName) : null,
                    GroupKey = GetInitialGroup(artist.Name)
                };
                entries.Add(newEntry);
            }

            return Ascending ? entries.OrderBy(e => e.Title).ToList() : entries.OrderByDescending(e => e.Title).ToList();
        }

        private List<LibraryEntry> ConstructGenreEntries()
        {
            var genres = _library.Genres;
            var entries = new List<LibraryEntry>();

            foreach (var genre in genres)
            {
                // Obtener álbumes únicos de forma eficiente
                var albumIds = _library.Tracks
                    .Where(t => t.GenreIds.Contains(genre.Id))
                    .Select(t => t.AlbumId)
                    .Distinct()
                    .ToList();

                var albumCount = albumIds.Count;

                if (albumCount == 0)
                {
                    continue;
                }

                var imageAsset = FindCoverAsset(albumIds);

                LibraryEntry newEntry = new LibraryEntry
                {
                    Id = genre.Id,
                    Type = "Genre",
                    Title = genre.Name,
                    Description = $"{albumCount} Album{(albumCount != 1 ? "s" : "")}",
                    ImagePath = imageAsset != null ? Path.Combine(CoversBasePath, imageAsset.FileName) : null,
                    GroupKey = GetInitialGroup(genre.Name)
                };
                entries.Add(newEntry);
            }

            return Ascending ? entries.OrderBy(e => e.Title).ToList() : entries.OrderByDescending(e => e.Title).ToList();
        }

        private List<LibraryEntry> ConstructDecadeEntries()
        {
            var albums = _library.Albums.Where(a => a.Year > 0).ToList();

            if (albums.Count == 0)
            {
                return [];
            }

            var decadeGroups = albums
                .GroupBy(a => (a.Year / 10) * 10)
                .OrderBy(g => g.Key)
                .ToList();

            var entries = new List<LibraryEntry>();
            int entryId = 1;

            foreach (var decadeGroup in decadeGroups)
            {
                var decade = decadeGroup.Key;
                var albumCount = decadeGroup.Count();
                var albumIds = decadeGroup.Select(a => a.Id).ToList();

                var imageAsset = FindCoverAsset(albumIds);

                var decadeTitle = $"{decade}s";
                var description = $"{albumCount} Album{(albumCount != 1 ? "s" : "")}";

                LibraryEntry newEntry = new LibraryEntry
                {
                    Id = entryId++,
                    Type = "Decade",
                    Title = decadeTitle,
                    Description = description,
                    ImagePath = imageAsset != null ? Path.Combine(CoversBasePath, imageAsset.FileName) : null,
                    GroupKey = decadeTitle
                };
                entries.Add(newEntry);
            }

            return Ascending ? entries.OrderBy(e => e.Title).ToList() : entries.OrderByDescending(e => e.Title).ToList();
        }
        private void ApplyGrouping(List<LibraryEntry> entries)
        {
            Entries = entries;

            EntriesViewSource.Source = entries;
            EntriesViewSource.GroupDescriptions.Clear();
            EntriesViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LibraryEntry.GroupKey)));
        }

        public class LibraryEntry
        {
            public int Id { get; set; }
            public string Type { get; set; } = ""; // Album, Artist, Genre, Year
            public string? ImagePath { get; set; }
            public required string Title { get; set; }
            public required string Description { get; set; }
            public string GroupKey { get; set; } = "";
        }
    }
}
