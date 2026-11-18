using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;


namespace MusicWrap.Core.Services.Lyrics
{
    public class LyricsService
    {
        private readonly ILibraryService _library;
        private readonly LyricsProviderService _lyricsProvider;
        private readonly LyricsData _libraryLyrics;

        public LyricsService(ILibraryService library, LyricsProviderService lyricsProvider, LyricsData libraryLyrics)
        {
            _library = library;
            _lyricsProvider = lyricsProvider;
            _libraryLyrics = libraryLyrics;
        }

        #region Public Methods
        public List<Data.Library.Models.Lyrics> LoadLyricsByTrackId(int id)
        {
            var lyrics = _libraryLyrics.Lyrics.FindAll(x => x.TrackId == id).OrderByDescending(x => x.Default).ThenByDescending(x => x.Type).ToList();
            return lyrics;
        }
        #endregion
        #region Internals
        #endregion
    }
}
