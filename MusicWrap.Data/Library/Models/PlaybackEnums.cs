using System;

namespace MusicWrap.Data.Library.Models
{
    public enum PlaybackState
    {
        Stopped = 0,
        Playing = 1,
        Paused = 2
    }
    public enum RepeatMode
    {
        None = 0,
        RepeatAll = 1,
        RepeatOne = 2
    }
    public enum ContinueMode
    {
        None = 0,
        DJEnd = 1
    }
}
