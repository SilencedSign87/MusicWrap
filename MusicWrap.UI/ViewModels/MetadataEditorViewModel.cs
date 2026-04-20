using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.UI.Features.Library.Services;
using System;
using System.Collections.Generic;
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


        private readonly string variousMetadata = "< mixed >";
        private readonly ILibraryCacheService _libraryCache;
        public MetadataEditorViewModel(ILibraryCacheService libraryCacheService)
        {
            _libraryCache = libraryCacheService;
        }
        public void LoadTracks(List<int> trackIds)
        {

        }
    }
}
