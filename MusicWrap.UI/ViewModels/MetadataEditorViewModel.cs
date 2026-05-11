using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Library;

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
        private string originalGenre = string.Empty;

        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string title = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string artist = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string album = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string albumArtist = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string year = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string trackNumber = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string diskNumber = string.Empty;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(HasChanges))] private string genre = string.Empty;

        [ObservableProperty] private string titlePlaceholder = string.Empty;
        [ObservableProperty] private string artistPlaceholder = string.Empty;
        [ObservableProperty] private string albumPlaceholder = string.Empty;
        [ObservableProperty] private string albumArtistPlaceholder = string.Empty;
        [ObservableProperty] private string yearPlaceholder = string.Empty;
        [ObservableProperty] private string trackNumberPlaceholder = string.Empty;
        [ObservableProperty] private string totalTracksPlaceholder = string.Empty;
        [ObservableProperty] private string diskNumberPlaceholder = string.Empty;
        [ObservableProperty] private string totalDiskPlaceholder = string.Empty;
        [ObservableProperty] private string genrePlaceholder = string.Empty;


        public bool HasChanges =>
            !string.IsNullOrEmpty(Title) && !Title.Equals(originalTitle) ||
            !string.IsNullOrEmpty(Artist) && !Artist.Equals(originalArtist) ||
            !string.IsNullOrEmpty(Album) && !Album.Equals(originalAlbum) ||
            !string.IsNullOrEmpty(AlbumArtist) && !AlbumArtist.Equals(originalAlbumArtist) ||
            !string.IsNullOrEmpty(Year) && !Year.Equals(originalYear) ||
            !string.IsNullOrEmpty(TrackNumber) && !TrackNumber.Equals(originalTrackNumber) ||
            !string.IsNullOrEmpty(DiskNumber) && !DiskNumber.Equals(originalDiskNumber) ||
            !string.IsNullOrEmpty(Genre) && !Genre.Equals(originalGenre)
            ;

        private readonly string variousMetadata = "--mixed--";
        private readonly ILibraryService _libraryService;
        public MetadataEditorViewModel(ILibraryService libraryService)
        {
            _libraryService = libraryService;
        }

        [RelayCommand]
        private void CancelChanges()
        {
            ResetChanges();
        }
        public void LoadTracks(List<int> trackIds)
        {
            ResetInputs();

            foreach (var trackId in trackIds)
            {
                var track = _libraryService.GetTrackById(trackId);

                if (track is not null && track.Id != 0)
                {
                    // Track Properties

                    // title
                    if (string.IsNullOrEmpty(Title) && TitlePlaceholder != variousMetadata)
                    {
                        Title = track?.Title ?? "";
                        originalTitle = track?.Title ?? "";
                        TitlePlaceholder = string.Empty;
                    }
                    else if (!Title.Equals(track.Title))
                    {
                        originalTitle = string.Empty;
                        TitlePlaceholder = variousMetadata;
                        Title = string.Empty;
                    }

                    // track artist
                    var trackArtist = string.Join(", ", track?.AlbumArtists.Length > 0 ? track.AlbumArtists : track?.Artists ?? Array.Empty<string>());
                    if (string.IsNullOrEmpty(Artist) && ArtistPlaceholder != variousMetadata)
                    {

                        Artist = trackArtist;
                        originalArtist = trackArtist;
                        ArtistPlaceholder = string.Empty;
                    }
                    else if (!Artist.Equals(trackArtist))
                    {
                        originalArtist = string.Empty;
                        ArtistPlaceholder = variousMetadata;
                        Artist = string.Empty;
                    }

                    if (string.IsNullOrEmpty(TrackNumber) && TrackNumberPlaceholder != variousMetadata)
                    {
                        TrackNumber = track?.TrackNumber.ToString() ?? string.Empty;
                        originalTrackNumber = track?.TrackNumber.ToString() ?? string.Empty;
                        TrackNumberPlaceholder = string.Empty;
                    }
                    else if (!TrackNumber.Equals(track?.TrackNumber.ToString() ?? string.Empty))
                    {
                        originalTrackNumber = string.Empty;
                        TrackNumberPlaceholder = variousMetadata;
                        TrackNumber = string.Empty;
                    }

                    if (string.IsNullOrEmpty(DiskNumber) && DiskNumberPlaceholder != variousMetadata)
                    {
                        DiskNumber = track?.DiskNumber.ToString() ?? string.Empty;
                        originalDiskNumber = track?.DiskNumber.ToString() ?? string.Empty;
                        DiskNumberPlaceholder = string.Empty;
                    }
                    else if (!DiskNumber.Equals(track?.DiskNumber.ToString() ?? string.Empty))
                    {
                        originalDiskNumber = string.Empty;
                        DiskNumberPlaceholder = variousMetadata;
                        DiskNumber = string.Empty;
                    }

                    var genreNames = string.Join(", ", track?.Genres ?? Array.Empty<string>());
                    if (string.IsNullOrEmpty(Genre) && GenrePlaceholder != variousMetadata)
                    {
                        Genre = genreNames;
                        originalGenre = genreNames;
                        GenrePlaceholder = string.Empty;
                    }
                    else if (!Genre.Equals(genreNames))
                    {
                        originalGenre = string.Empty;
                        GenrePlaceholder = variousMetadata;
                        Genre = string.Empty;
                    }

                    // Album Properties
                    if (string.IsNullOrEmpty(Album) && AlbumPlaceholder != variousMetadata)
                    {
                        Album = track?.AlbumName ?? string.Empty;
                        originalAlbum = track?.AlbumName ?? string.Empty;
                        AlbumPlaceholder = string.Empty;
                    }
                    else if (!Album.Equals(track?.AlbumName ?? string.Empty))
                    {
                        originalAlbum = string.Empty;
                        AlbumPlaceholder = variousMetadata;
                        Album = string.Empty;
                    }

                    var albumArtists = string.Join(", ", track?.AlbumArtists.Length > 0 ? track.AlbumArtists : track?.Artists ?? Array.Empty<string>());
                    if (string.IsNullOrEmpty(AlbumArtist) && AlbumArtistPlaceholder != variousMetadata)
                    {
                        AlbumArtist = albumArtists;
                        originalAlbumArtist = albumArtists;
                        AlbumArtistPlaceholder = string.Empty;
                    }
                    else if (!AlbumArtist.Equals(albumArtists))
                    {
                        originalAlbumArtist = string.Empty;
                        AlbumArtist = string.Empty;
                        AlbumArtistPlaceholder = variousMetadata;
                    }

                    if (string.IsNullOrEmpty(Year) && YearPlaceholder != variousMetadata)
                    {
                        Year = track?.ReleaseYear?.ToString() ?? string.Empty;
                        originalYear = track?.ReleaseYear?.ToString() ?? string.Empty;
                        YearPlaceholder = string.Empty;
                    }
                    else if (!Year.Equals(track?.ReleaseYear?.ToString() ?? string.Empty))
                    {
                        originalYear = string.Empty;
                        Year = string.Empty;
                        YearPlaceholder = variousMetadata;
                    }
                }
            }
        }
        private void ResetChanges()
        {
            if (!string.Equals(Title, originalTitle))
                Title = originalTitle;
            if (!string.Equals(Artist, originalArtist))
                Artist = originalArtist;
            if (!string.Equals(Album, originalAlbum))
                Album = originalAlbum;
            if (!string.Equals(AlbumArtist, originalAlbumArtist))
                AlbumArtist = originalAlbumArtist;
            if (!string.Equals(Year, originalYear))
                Year = originalYear;
            if (!string.Equals(TrackNumber, originalTrackNumber))
                TrackNumber = originalTrackNumber;
            if (!string.Equals(DiskNumber, originalDiskNumber))
                DiskNumber = originalDiskNumber;
            if (!string.Equals(Genre, originalGenre))
                Genre = originalGenre;
        }
        private void ResetInputs()
        {
            Title = string.Empty;
            Artist = string.Empty;
            Album = string.Empty;
            AlbumArtist = string.Empty;
            TrackNumber = string.Empty;
            DiskNumber = string.Empty;
            Year = string.Empty;
            Genre = string.Empty;

            TitlePlaceholder = string.Empty;
            ArtistPlaceholder = string.Empty;
            AlbumPlaceholder = string.Empty;
            AlbumArtistPlaceholder = string.Empty;
            TrackNumberPlaceholder = string.Empty;
            DiskNumberPlaceholder = string.Empty;
            YearPlaceholder = string.Empty;
            GenrePlaceholder = string.Empty;
        }
    }
}
