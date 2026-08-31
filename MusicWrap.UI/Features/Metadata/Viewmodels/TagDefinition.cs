using MusicWrap.Core.Metadata;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public sealed record TagDefinition(
        string Key,
        string DisplayName,
        bool IsMultipleValue,
        MetadataType AutocompleteType,
        Func<TagLib.Tag, string?> GetValue,
        Action<TagLib.Tag, string> SetValue
        );
    public static class TagDefinitions
    {
        public const string ValueSeparator = "; ";
        public static readonly TagDefinition Title = Single("TITLE", "Title", t => t.Title, (t, v) => t.Title = v);
        public static readonly TagDefinition Artist = Multi("ARTIST", "Artist", MetadataType.ArtistName, t => t.Performers, (t, v) => t.Performers = v);
        public static readonly TagDefinition Album = Single("ALBUM", "Album", t => t.Album, (t, v) => t.Album = v);
        public static readonly TagDefinition AlbumArtist = Multi("ALBUMARTIST", "Album Artist", MetadataType.ArtistName, t => t.AlbumArtists, (t, v) => t.AlbumArtists = v);
        public static readonly TagDefinition Genre = Multi("GENRE", "Genre", MetadataType.GenreName, t => t.Genres, (t, v) => t.Genres = v);
        public static readonly TagDefinition Composer = Multi("COMPOSER", "Composer", MetadataType.ArtistName, t => t.Composers, (t, v) => t.Composers = v);
        public static readonly TagDefinition Conductor = Single("CONDUCTOR", "Conductor", t => t.Conductor, (t, v) => t.Conductor = v);
        public static readonly TagDefinition Copyright = Single("COPYRIGHT", "Copyright", t => t.Copyright, (t, v) => t.Copyright = v);
        public static readonly TagDefinition Publisher = Single("PUBLISHER", "Publisher", t => t.Publisher, (t, v) => t.Publisher = v);
        public static readonly TagDefinition Year = Single("YEAR", "Year", t => FormatUInt(t.Year), (t, v) => t.Year = ParseUInt(v));
        public static readonly TagDefinition TrackNumber = Single("TRACKNUMBER", "Track Number", t => FormatUInt(t.Track), (t, v) => t.Track = ParseUInt(v));
        public static readonly TagDefinition TrackCount = Single("TRACKCOUNT", "Track Count", t => FormatUInt(t.TrackCount), (t, v) => t.TrackCount = ParseUInt(v));
        public static readonly TagDefinition DiscNumber = Single("DISCNUMBER", "Disc Number", t => FormatUInt(t.Disc), (t, v) => t.Disc = ParseUInt(v));
        public static readonly TagDefinition DiscCount = Single("DISCTOTAL", "Disc Count", t => FormatUInt(t.DiscCount), (t, v) => t.DiscCount = ParseUInt(v));
        public static readonly TagDefinition Bpm = Single("BPM", "BPM", t => FormatUInt(t.BeatsPerMinute), (t, v) => t.BeatsPerMinute = ParseUInt(v));
        public static readonly TagDefinition Comment = Single("COMMENT", "Comment", t => t.Comment, (t, v) => t.Comment = v);
        public static readonly TagDefinition Isrc = Single("ISRC", "ISRC", t => t.ISRC, (t, v) => t.ISRC = v);

        public static IReadOnlyList<TagDefinition> All { get; } = [
            Title, Artist, Album, AlbumArtist, Genre, Composer,
            Year,
            TrackNumber, TrackCount, DiscNumber, DiscCount,
            //Conductor, Copyright, Publisher, 
            //Bpm, Comment, Isrc
        ];

        #region Internal
        private static TagDefinition Single(string key, string displayName, Func<TagLib.Tag, string?> getValue, Action<TagLib.Tag, string> setValue)
            => new(key, displayName, false, MetadataType.ArtistName, getValue, setValue);
        private static TagDefinition Multi(string key, string displayName, MetadataType autocompleteType, Func<TagLib.Tag, string[]> getValues, Action<TagLib.Tag, string[]> setValues)
            => new(key, displayName, true, autocompleteType,
                t => string.Join(ValueSeparator, getValues(t).Where(v => !string.IsNullOrWhiteSpace(v))),
                (t, v) => setValues(t, SplitValues(v)));
        private static string[] SplitValues(string value)
            => value.Split(ValueSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        private static string FormatUInt(uint value)
            => value == 0 ? string.Empty : value.ToString();
        private static uint ParseUInt(string value)
            => uint.TryParse(value, out var parsed) ? parsed : 0;
        #endregion
    }
}
