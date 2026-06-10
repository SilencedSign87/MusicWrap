using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Infrastructure.Saving
{
    [Flags]
    public enum SaveKind
    {
        None = 0,
        Settings = 1,
        Playback = 2,
        Library = 4,
        Cache = 8,
        Playlist = 16,
    }

}
