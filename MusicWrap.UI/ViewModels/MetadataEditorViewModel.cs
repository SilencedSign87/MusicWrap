using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.UI.Features.Library.Services;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace MusicWrap.UI.ViewModels
{
    public partial class MetadataEditorViewModel : ObservableObject
    {
        private string originalTitle = string.Empty;
        private string originalArtist = string.Empty;
        private string originalAlbum = string.Empty;
        private string originalAlbumArtist = string.Empty;
        private string originalYear = string.Empty;
        private string originalTrackNumber = string.Empty;
        private string originalTotalTracks = string.Empty;
        private string originalDiskNumber = string.Empty;
        private string originalAlbumNumber = string.Empty;

        [ObservableProperty] private string title = string.Empty;
        [ObservableProperty] private string artist = string.Empty;
        [ObservableProperty] private string album = string.Empty;
        [ObservableProperty] private string albumArtist = string.Empty;
        [ObservableProperty] private string year = string.Empty;
        [ObservableProperty] private string trackNumber = string.Empty;
        [ObservableProperty] private string totalTracks = string.Empty;
        [ObservableProperty] private string diskNumber = string.Empty;
        [ObservableProperty] private string totalDisk = string.Empty;


        private readonly string variousMetadata = "--mixed--";
        private readonly ILibraryCacheService _libraryCache;
        public MetadataEditorViewModel(ILibraryCacheService libraryCacheService)
        {
            _libraryCache = libraryCacheService;
        }
        public void LoadTracks(List<int> trackIds)
        {
            ResetInputs();
            foreach (var trackId in trackIds)
            {
                var track = _libraryCache.GetTrackById(trackId);

                if (track != null)
                {
                    if (string.IsNullOrEmpty(Title))
                    {
                        Title = track.Title;
                        originalTitle = track.Title;
                    }
                    else if (Title != track.Title)
                    {
                        Title = variousMetadata;
                    }
                    var artistNames = _libraryCache.GetArtistNamesForTrack(trackId);
                    if (string.IsNullOrEmpty(Artist))
                    {
                        Artist = artistNames;
                        originalArtist = artistNames;
                    }
                    else if (Artist != artistNames)
                    {
                        Artist = variousMetadata;
                    }
                    var album = _libraryCache.GetAlbumById(track.AlbumId);
                    var albumName = album != null ? album.Title : string.Empty;
                    Year = album?.Year > 0 ? album.Year.ToString() : string.Empty;

                    if (string.IsNullOrEmpty(Album))
                    {
                        Album = albumName;
                        originalAlbum = albumName;
                    }
                    else if (Album != albumName)
                    {
                        Album = variousMetadata;

                    }
                    var albumArtistNames = _libraryCache.GetArtistNamesForAlbum(track.AlbumId);
                    if (string.IsNullOrEmpty(AlbumArtist))
                    {
                        AlbumArtist = albumArtistNames;
                        originalAlbumArtist = albumArtistNames;
                    }
                    else if (AlbumArtist != albumArtistNames)
                    {
                        AlbumArtist = variousMetadata;
                    }
                    var trackNumber = track.TrackNumber > 0 ? track.TrackNumber.ToString() : string.Empty;
                    if (string.IsNullOrEmpty(TrackNumber) || trackNumber == TrackNumber)
                    {
                        TrackNumber = trackNumber;
                    }
                    else
                    {
                        TrackNumber = variousMetadata;
                    }
                    var diskNumber = track.Disk > 0 ? track.Disk.ToString() : string.Empty;
                    if (string.IsNullOrEmpty(DiskNumber) || diskNumber == DiskNumber)
                    {
                        DiskNumber = diskNumber;
                    }
                    else
                    {
                        DiskNumber = variousMetadata;
                    }
                }
            }
        }
        private void ResetChanges()
        {

        }
        private void ResetInputs()
        {
            Title = string.Empty;
            Album = string.Empty;
            AlbumArtist = string.Empty;
            Artist = string.Empty;
            Album = string.Empty;
            TrackNumber = string.Empty;
            DiskNumber = string.Empty;
            TotalDisk = string.Empty;
            TotalTracks = string.Empty;
        }
    }
}
