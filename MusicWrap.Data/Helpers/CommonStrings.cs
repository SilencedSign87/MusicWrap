using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Helpers
{
    public static class CommonStrings
    {
        // fallback display values
        public static readonly string UnknownArtist = AppStringPool.Intern("Unknown Artist")!;
        public static readonly string UnknownAlbum = AppStringPool.Intern("Unknown Album")!;
        public static readonly string UnknownGenre = AppStringPool.Intern("Unknown Genre")!;
        public static readonly string UnknownTrack = AppStringPool.Intern("Unknown Track")!;
        public static readonly string NoTrackPlaying = AppStringPool.Intern("No track playing")!;

        // join/delimiter strings
        public static readonly string ArtistNameSeparator = "; ";
        public static readonly string ListSeparator = ", ";
        public static readonly string CacheKeyDelimiter = "|";

        //Data file names

        public static readonly string LibraryFile = "library.dat";
        public static readonly string LibraryBackupFile = "library.bak";
        public static readonly string PlaylistFile = "playlist.dat";
        public static readonly string PlaylistBackupFile = "playlist.bak";
        public static readonly string QueueFile = "queue.dat";
        public static readonly string QueueBackupFile = "queue.bak";

        // pack uris

        public static readonly string ResourceUriBase = "pack://application:,,,/Resources/";
        public static readonly string IconUriBase = ResourceUriBase + "Icons/";
        public static readonly string DefaultTrackImage = ResourceUriBase + "DefaultTrack.png";
        public static readonly string DefaultBlurImage = ResourceUriBase + "BlurDefault.jpg";

        // image cache prefix
        public static readonly string DefaultImageKeyPrefix = "default";

        // mimetypes
        public static readonly string MimeJpeg = "image/jpeg";
        public static readonly string MimePng = "image/png";
    }

    public static class CommonColors
    {
        public static readonly string DominantColorFallback = "#7f2b19";
        public static readonly string HighlightColorFallback = "#fee6a0";
        public static readonly string ForegroundOnFallback = "#FFFFFF";
    }
}
