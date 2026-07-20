using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Helpers;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using MusicWrap.Core.Services.Contracts;

namespace MusicWrap.UI.ViewModels
{
    public enum LinkType { Album, Artist, Genre, Year }
    public sealed record LinkItem(string Name, int Id, LinkType Type);
    public partial class TrackInformationViewModel : ObservableObject
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private readonly ILibraryService _libraryService;
        private readonly IwindowsImageService _imageService;
        private readonly IUIDispatcher _uiDispatcher;
        private readonly IMessenger _messenger;
        private readonly TrackActionService _trackActions;

        private string _artworkPath = string.Empty;

        // navigation entities
        private int _currentTrackId;
        private int _currentAlbumId;
        private int[] _currentArtistIds = [];
        private int[] _currentAlbumArtistIds = [];
        private int[] _currentGenreIds = [];

        // view properties

        [ObservableProperty]
        private bool hasTrack;
        [ObservableProperty]
        private string trackTitle = "No track playing";
        [ObservableProperty]
        private string? trackImagePath;
        [ObservableProperty]
        private string? dominantColorHex = "#808080";
        [ObservableProperty]
        private string? foregroundColorHex = "#FFFFFF";
        [ObservableProperty]
        private string albumTitle = "";
        [ObservableProperty]
        private string year = "";
        [ObservableProperty]
        private string trackNumber = "";
        [ObservableProperty]
        private string diskNumber = "";
        [ObservableProperty]
        private string duration = "0:00";

        // technical properties

        [ObservableProperty]
        private string format = "";
        [ObservableProperty]
        private string sampleRate = "";
        [ObservableProperty]
        private string bitDepth = "";
        [ObservableProperty]
        private string bitrate = "";
        [ObservableProperty]
        private string channels = "";
        [ObservableProperty]
        private string fileSize = "";
        [ObservableProperty]
        private string filePath = "";
        [ObservableProperty]
        private string technicalInfoSummary = "";

        // source origin

        [ObservableProperty]
        private string sourceType = "Local";
        [ObservableProperty]
        private string? externalId;
        [ObservableProperty]
        private string? sourceUri;
        [ObservableProperty]
        private bool isFromYouTube;

        // clickable links
        public ObservableCollection<LinkItem> Artists { get; } = [];
        public ObservableCollection<LinkItem> AlbumArtists { get; } = [];
        public ObservableCollection<LinkItem> Genres { get; } = [];


        public TrackInformationViewModel(
             IMusicPlayerService playerService,
            ILibraryService libraryService,
            IUIDispatcher uiDispatcher,
            IwindowsImageService imageService,
            TrackActionService trackActions,
            IMessenger messenger)
        {
            _musicPlayerService = playerService;
            _libraryService = libraryService;
            _imageService = imageService;
            _uiDispatcher = uiDispatcher;
            _trackActions = trackActions;
            _messenger = messenger;

            _musicPlayerService.TrackChanged += OnTrackChanged;

            UpdateCurrentTrackInfo();
        }

        #region Relay Commands

        [RelayCommand]
        private void OpenArtworkDefaultApp()
        {
            if (!string.IsNullOrEmpty(_artworkPath))
                Process.Start(new ProcessStartInfo(_artworkPath) { UseShellExecute = true });
        }

        [RelayCommand]
        private void EditTrackInfo()
        {
            _trackActions.EditMetadata([_currentTrackId]);
        }
        [RelayCommand]
        private void NavigateToAlbum()
        {

        }
        [RelayCommand]
        private void NavigateToArtist(int artistId)
        {

        }
        [RelayCommand]
        private void NavigateToGenre(int genreId)
        {

        }
        [RelayCommand]
        private void OpenInExplorer()
        {
            _trackActions.ShowInFileExplorer([_currentTrackId]);
        }

        #endregion

        #region Events
        private void OnTrackChanged(object? sender, string e)
        {
            UpdateCurrentTrackInfo();
        }

        #endregion
        #region internal
        private void UpdateCurrentTrackInfo()
        {
            ClearCurrentTrackInfo();

            var trackId = _musicPlayerService.CurrentTrackId;
            if (trackId == 0)
            {
                HasTrack = false;
                TrackTitle = "No track playing";
                return;
            }
            var track = _libraryService.GetTrackById(trackId);
            if (track == null)
            {
                HasTrack = false;
                TrackTitle = "Unknown track";
                return;
            }

            HasTrack = true;
            _currentTrackId = track.Id;

            // Basic information

            TrackTitle = track.Title;
            Duration = FormatHelpers.FormatDuration(track.Duration);
            TrackNumber = track.TrackNumber > 0 ? FormatHelpers.FormatTrackNumber(track.TrackNumber) : "Track";
            DiskNumber = track.Disk > 0 ? FormatHelpers.FormatDiscNumber(track.Disk) : "Disk";

            // Album
            Album? album = null;
            if (track.AlbumId > 0)
            {
                _currentAlbumId = track.AlbumId;
                album = _libraryService.GetAlbumById(track.AlbumId);
                AlbumTitle = album?.Title ?? "Unknown Album";
                Year = album?.Year > 0 ? album.Year.ToString() : "";

                var albumArtist = _libraryService.GetArtistNamesByIds(album?.ArtistIds ?? []);
                AlbumArtists.Clear();
                foreach (var artist in albumArtist)
                {
                    AlbumArtists.Add(new LinkItem(artist, 0, LinkType.Artist));
                }
            }
            else
            {
                _currentAlbumId = 0;
                AlbumTitle = "";
                Year = "";
            }

            // artists
            _currentArtistIds = track.ArtistIds ?? [];
            Artists.Clear();
            var artistNames = _libraryService.GetArtistNamesByIds(_currentArtistIds);
            for (int i = 0; i < _currentArtistIds.Length && i < artistNames.Length; i++)
            {
                Artists.Add(new LinkItem(artistNames[i], _currentArtistIds[i], LinkType.Artist));
            }

            // genres
            _currentGenreIds = track.GenreIds ?? [];
            Genres.Clear();
            if (_currentGenreIds.Length > 0)
            {
                var genreList = _libraryService.GetGenreById([.. _currentGenreIds]);
                foreach (var genre in genreList)
                {
                    Genres.Add(new LinkItem(genre.Name, genre.Id, LinkType.Genre));
                }
            }

            // technical info
            Format = FormatHelpers.FormatFileExtension(track.Path);
            SampleRate = FormatHelpers.FormatSampleRate(track.SamplingRate);
            BitDepth = FormatHelpers.FormatBitDepth(track.BitDeph);
            Bitrate = FormatHelpers.FormatBitrate(track.Bitrate);
            Channels = FormatHelpers.FormatChannels(track.Channels);

            // File size
            if (track.FileSize > 0)
            {
                FileSize = FormatHelpers.FormatFileSize(track.FileSize);
            }
            else
            {
                FileSize = "";
            }
            FilePath = track.Path;

            // summary 
            var parts = new[] { Format, SampleRate, BitDepth, Bitrate, Channels }
                .Where(p => !string.IsNullOrEmpty(p));
            TechnicalInfoSummary = string.Join(" • ", parts);

            // cover
            int coverId = track.CoverId;
            if (coverId == 0 && album != null)
                coverId = album.CoverId;
            if (coverId > 0)
            {
                var coverAsset = _libraryService.GetCoverAsset(coverId);
                if (coverAsset != null)
                {
                    TrackImagePath = coverAsset.FileName;
                    _artworkPath = _imageService.ResolvePath(coverAsset.FileName, ImageVariant.Original) ?? string.Empty;
                    DominantColorHex = coverAsset.DominantColorHex;
                    ForegroundColorHex = coverAsset.ForegroundColorHex;
                }
            }

            // source
            IsFromYouTube = track.Origin == TrackOrigin.Youtube;
            SourceType = IsFromYouTube ? "YouTube" : "Local";
            ExternalId = track.ExternalId;
            SourceUri = track.SourceUri;

            // Fallbacks
            DominantColorHex ??= "#808080";
            ForegroundColorHex ??= "#FFFFFF";
        }
        private void ClearCurrentTrackInfo()
        {
            _currentTrackId = 0;
            _currentAlbumId = 0;
            _currentArtistIds = [];
            _currentAlbumArtistIds = [];
            _currentGenreIds = [];
            TrackTitle = string.Empty;
            TrackImagePath = null;
            DominantColorHex = "#808080";
            ForegroundColorHex = "#FFFFFF";
            AlbumTitle = string.Empty;
            Year = string.Empty;
            TrackNumber = string.Empty;
            DiskNumber = string.Empty;
            Duration = "0:00";
            Format = string.Empty;
            SampleRate = string.Empty;
            BitDepth = string.Empty;
            Bitrate = string.Empty;
            Channels = string.Empty;
            FileSize = string.Empty;
            FilePath = string.Empty;
            TechnicalInfoSummary = string.Empty;
            SourceType = "Local";
            ExternalId = null;
            SourceUri = null;
            IsFromYouTube = false;
            Artists.Clear();
            AlbumArtists.Clear();
            Genres.Clear();
            _artworkPath = string.Empty;
        }
        #endregion
    }
}
