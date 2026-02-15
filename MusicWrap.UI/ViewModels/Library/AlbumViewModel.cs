using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MusicWrap.UI.ViewModels.Library
{
    public partial class AlbumViewModel : ObservableObject
    {
        [ObservableProperty]
        private int albumId;

        [ObservableProperty]
        private string title = "";

        [ObservableProperty]
        private int year;

        [ObservableProperty]
        private string? coverImagePath;

        [ObservableProperty]
        private string artistNames = "";

        private MusicLibrary _library;

        private static readonly string CoversBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicWrap",
            "covers"
        );

        public AlbumViewModel(MusicLibrary library, int albumId)
        {
            _library = library;
            this.albumId = albumId;
            LoadAlbumData();
        }

        private void LoadAlbumData()
        {
            var album = _library.Albums.FirstOrDefault(a => a.Id == albumId);
            if (album == null) return;

            Title = album.Title;
            Year = album.Year;

            // Get artist names
            var artists = _library.Artists.Where(a => album.ArtistIds.Contains(a.Id)).Select(a => a.Name);
            ArtistNames = string.Join(", ", artists);

            // Get cover image
            if (album.CoverId > 0)
            {
                var coverAsset = _library.CoverAssets.FirstOrDefault(c => c.Id == album.CoverId);
                if (coverAsset != null)
                {
                    CoverImagePath = Path.Combine(CoversBasePath, coverAsset.FileName);
                }
            }
        }
    }
}

