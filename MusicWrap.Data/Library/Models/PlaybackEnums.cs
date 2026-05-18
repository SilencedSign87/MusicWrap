using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Data.Library.Models
{
    public enum PlaybackState
    {
        Paused = 0,
        Playing = 1,
        Stopped = 2
    }
    public enum RepeatMode
    {
        None = 0,
        RepeatAll = 1,
        RepeatOne = 2
    }
    public enum ContinueMode
    {
        None = 0,   // when ends stop playback
        DJEnd = 1   // add tracks following DJ parameters
    }
}
