using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Lyrics
{
    public enum LyricsSource
    {
        None,
        Embedded,
        External,
        Remote
    }
    public sealed record LyricLine(TimeSpan Timestamp, string Text);
    public sealed record ParsedLyrics(
        bool IsSynced,
        IReadOnlyList<LyricLine> Lines,
        string RawText,
        LyricsSource Source,
        int OffsetMs = 0
        )
    {
        public static ParsedLyrics Empty => new(false, Array.Empty<LyricLine>(), string.Empty, LyricsSource.None);
        public bool HasContent => Lines.Count > 0 || !string.IsNullOrWhiteSpace(RawText);
    }

}
