using MessagePack;
using MusicWrap.Data.Library.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicWrap.Data.User.Models
{
    [MessagePackObject]
    public sealed class PlaybackSettings : ObservableSettings
    {
        private float _preferredVolume = 1.0f;
        [Key(0)]
        public float PreferredVolume
        {
            get => _preferredVolume;
            set => SetProperty(ref _preferredVolume, value);
        }

        private RepeatMode _repeatMode = RepeatMode.None;
        [Key(1)]
        public RepeatMode RepeatMode
        {
            get => _repeatMode;
            set => SetProperty(ref _repeatMode, value);
        }
        
        private bool _isShuffleEnabled = false;
        [Key(2)]
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set => SetProperty(ref _isShuffleEnabled, value);
        }
        
        private ContinueMode _continueMode = ContinueMode.None;
        [Key(3)]
        public ContinueMode ContinueMode
        {
            get => _continueMode;
            set => SetProperty(ref _continueMode, value);
        }

        private int _preferredDeviceIndex = -1;
        [Key(4)]
        public int PreferredDeviceIndex
        {
            get => _preferredDeviceIndex;
            set => SetProperty(ref _preferredDeviceIndex, value);
        }
        private SampleRatePreference _preferredSampleRate = SampleRatePreference.Auto;
        [Key(5)]
        public SampleRatePreference PreferredSampleRate
        {
            get => _preferredSampleRate;
            set => SetProperty(ref _preferredSampleRate, value);
        }

        private OutputMode _preferredOutputMode = OutputMode.WasapiShared;
        [Key(6)]
        public OutputMode PreferredOutputMode
        {
            get => _preferredOutputMode;
            set => SetProperty(ref _preferredOutputMode, value);
        }
    }

    public enum OutputMode
    {
        WasapiShared = 0,
        WasapiExclusive = 1,
        Direct = 2 // for android
    }
    public enum SampleRatePreference
    {
        Auto = -1,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000
    }
}
