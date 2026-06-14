using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Messages
{
    public sealed class EntriesReadyMessage
    {
    }
    public sealed class LibraryChangedMessage
    {
        public LibraryChangeType ChangeType { get; }
        public LibraryChangedMessage(LibraryChangeType changeType) => ChangeType = changeType;
    }
    public enum LibraryChangeType
    {
        FullReload,
        EntryReload,
    }
}
