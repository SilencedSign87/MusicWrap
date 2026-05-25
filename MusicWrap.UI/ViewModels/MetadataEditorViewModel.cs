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
        private readonly ILibraryService _libraryCache;
        public MetadataEditorViewModel(ILibraryService libraryCacheService)
        {
            _libraryCache = libraryCacheService;
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
                var track = _libraryCache.GetTrackById(trackId);

                if (track is not null && track.Id != 0)
                {
                    // Track Properties

                    // title
                    if (string.IsNullOrEmpty(Title) && TitlePlaceholder != variousMetadata)
                    {
                        Title = track.Title;
                        originalTitle = track.Title;
                        TitlePlaceholder = string.Empty;
                    }
                    else if (!Title.Equals(track.Title))
                    {
                        originalTitle = string.Empty;
                        TitlePlaceholder = variousMetadata;
                        Title = string.Empty;
                    }

                    // track artist
                    var trackArtist = _libraryCache.GetArtistNamesForTrack(trackId);
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
                        TrackNumber = track.TrackNumber.ToString();
                        originalTrackNumber = track.TrackNumber.ToString();
                        TrackNumberPlaceholder = string.Empty;
                    }
                    else if (!TrackNumber.Equals(track.TrackNumber))
                    {
                        originalTrackNumber = string.Empty;
                        TrackNumberPlaceholder = variousMetadata;
                        TrackNumber = string.Empty;
                    }

                    if (string.IsNullOrEmpty(DiskNumber) && DiskNumberPlaceholder != variousMetadata)
                    {
                        DiskNumber = track.Disk.ToString();
                        originalDiskNumber = track.Disk.ToString();
                        DiskNumberPlaceholder = string.Empty;
                    }
                    else if (!DiskNumber.Equals(track.Disk))
                    {
                        originalDiskNumber = string.Empty;
                        DiskNumberPlaceholder = variousMetadata;
                        DiskNumber = string.Empty;
                    }

                    var genres = _libraryCache.GetGenreById([.. track.GenreIds]);
                    var genreNames = string.Join(", ", genres.Select(g => g.Name));
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
                    var album = _libraryCache.GetAlbumById(track.AlbumId);

                    if (album is null)
                        continue;

                    if (string.IsNullOrEmpty(Album) && AlbumPlaceholder != variousMetadata)
                    {
                        Album = album.Title;
                        originalAlbum = album.Title;
                        AlbumPlaceholder = string.Empty;
                    }
                    else if (!Album.Equals(album.Title))
                    {
                        originalAlbum = string.Empty;
                        AlbumPlaceholder = variousMetadata;
                        Album = string.Empty;
                    }

                    var albumArtists = _libraryCache.GetArtistNamesForAlbum(album.Id);
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
                        Year = album.Year.ToString();
                        originalYear = album.Year.ToString();
                        YearPlaceholder = string.Empty;
                    }
                    else if (!Year.Equals(album.Year.ToString()))
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
