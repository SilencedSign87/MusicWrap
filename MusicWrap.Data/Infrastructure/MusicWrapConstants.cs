using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Infrastructure
{
    public static class MusicWrapConstants
    {
        public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".aac", ".ogg", ".opus", ".m4a"
        };

    }
}
