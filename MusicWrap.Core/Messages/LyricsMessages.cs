using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Messages
{
    public enum LyricsChangeType
    {
        Reload,
        SelectionChanged,
        EntryAdded,
        EntryUpdated,
        EntryRemoved
    }

    public sealed class TrackLyricsChangedMessage(int trackId, LyricsChangeType changeType)
    {
        public int TrackId { get; } = trackId;
        public LyricsChangeType ChangeType { get; } = changeType;
    }
}
