using MusicWrap.Data;
using MusicWrap.Data.Library;
using System.Timers;
using System.Linq;
using Un4seen.Bass;

namespace MusicWrap.Core
{
    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused
    }
    public enum RepeatMode
    {
        None, // When queue ends stop playback
        RepeatTrack, // 
        RepeatQueue
    }
    public enum ContinueMode
    {
        None, // when ends stop playback
        DJEnd // add tracks following DJ parameters
    }
    public interface IMusicPlayerService
    {
        bool IsPlaying { get; }
        bool IsPaused { get; }
        double CurrentPosition { get; }
        double Duration { get; }
        float Volume { get; set; }
        int CurrentTrackId { get; }
        string CurrentTrackPath { get; }
        int QueueCount { get; }
        int CurrentDeviceIndex { get; }
        int CurrentSampleRate { get; }

        RepeatMode RepeatMode { get; set; }
        ContinueMode ContinueMode { get; set; }

        event EventHandler<string>? TrackChanged;
        event EventHandler? TrackEnded;
        event EventHandler<PlaybackState>? PlaybackStateChanged;
        event EventHandler<double>? PositionChanged;
        event EventHandler<int[]>? QueueChanged;
        event EventHandler<int>? DeviceIndexChanged;
        event EventHandler<SampleRateChangedEventArgs>? SampleRateChanged;
        void Play();
        void Pause();
        void Stop();
        void Next();
        void Previous();
        void Seek(double seconds);
        void SetVolume(float volume);
        void PlayIndex(int index);

        void SetSilentIndex(int index);
        void AddToQueue(int TrackId);
        void AddToQueue(IEnumerable<int> TrackIds);
        void SetQueue(IEnumerable<int> TrackIds, bool CalculateNewIndex = false);
        void RemoveFromQueue(int index);
        void ClearQueue();
        int[] GetQueue();
        void PlayTrack(int TrackId);

        void ChangeOutputDevice(int deviceIndex);
        void ChangeSampleRate(int sampleRate);
        (int Index, string Name)[] GetAvailableDevices();
    }
    public class MusicPlayerService : IMusicPlayerService, IDisposable
    {
        private readonly MusicLibrary _library;
        private readonly AudioEngine _audioEngine;

        private readonly List<int> _queue = [];

        private int _mixerStream = 0;

        private int _currentIndex = -1;
        private int _currentStream;

        private int _preloadedStream = 0;
        private int _preloadedTrackId = 0;
        private readonly SYNCPROC _preloadSync;

        private PlaybackState _playbackState = PlaybackState.Stopped;
        private float _volume = 1.0f;
        private readonly SYNCPROC _endCallback;
        private readonly System.Timers.Timer _positionTimer;

        private int _currentDeviceIndex = -1;
        public int CurrentDeviceIndex
        {
            get => _currentDeviceIndex;
            private set
            {
                _currentDeviceIndex = value;
            }
        }
        private int _currentSampleRate = -1; // Auto
        public int CurrentSampleRate
        {
            get => _currentSampleRate;
            private set
            {
                _currentSampleRate = value;
            }
        }

        public bool IsPlaying => _playbackState == PlaybackState.Playing;
        public bool IsPaused => _playbackState == PlaybackState.Paused;
        public double CurrentPosition => _currentStream != 0 ? _audioEngine.GetMixerPosition(_currentStream) : 0.0;
        public double Duration => _currentStream != 0 ? _audioEngine.GetDuration(_currentStream) : 0.0;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                if (_currentStream != 0)
                {
                    _audioEngine.SetVolume(_currentStream, _volume);
                }
            }
        }

        public int CurrentTrackId => (_currentIndex >= 0 && _currentIndex < _queue.Count) ? _queue[_currentIndex] : 0;

        public string CurrentTrackPath
        {
            get
            {
                var track = GetCurrentTrack();
                return track?.Path ?? string.Empty;
            }
        }

        public int QueueCount => _queue.Count;
        public RepeatMode RepeatMode { get; set; } = RepeatMode.None;
        public ContinueMode ContinueMode { get; set; } = ContinueMode.None;

        public MusicPlayerService(MusicLibrary library)
        {
            _library = library;

            _audioEngine = new AudioEngine();

            _audioEngine.Initialize(CurrentDeviceIndex);

            _endCallback = OnTrackEndedInternal;
            _preloadSync = OnPreloadSync;

            _positionTimer = new System.Timers.Timer(500)
            {
                AutoReset = true
            };
            _positionTimer.Elapsed += PositionTimerOnElapsed;
            _positionTimer.Start();
        }

        public event EventHandler<string>? TrackChanged;
        public event EventHandler? TrackEnded;
        public event EventHandler<PlaybackState>? PlaybackStateChanged;
        public event EventHandler<double>? PositionChanged;
        public event EventHandler<int[]>? QueueChanged;
        public event EventHandler<int>? DeviceIndexChanged;
        public event EventHandler<SampleRateChangedEventArgs>? SampleRateChanged;

        public void Play()
        {
            if (_currentStream == 0)
            {
                if (_queue.Count == 0) return;

                if (_currentIndex < 0 || _currentIndex >= _queue.Count)
                    _currentIndex = 0;

                StartPlaybackOfCurrent();
                return;
            }

            _audioEngine.Play(_mixerStream, false);
            SetPlaybackState(PlaybackState.Playing);
        }

        public void Pause()
        {
            if (_currentStream == 0)
                return;

            _audioEngine.Pause(_mixerStream);
            SetPlaybackState(PlaybackState.Paused);
        }

        public void Stop()
        {
            if (_currentStream != 0) _audioEngine.RemoveFromMixer(_currentStream);
            FreeStream(_currentStream);
            _currentStream = 0;

            if (_preloadedStream != 0) FreeStream(_preloadedStream);
            _preloadedStream = 0;
            _preloadedTrackId = 0;

            _mixerStream = 0;

            SetPlaybackState(PlaybackState.Stopped);
        }

        public void Next()
        {
            if (_queue.Count == 0)
            {
                Stop();
                return;
            }

            int nextIndex = _currentIndex + 1;

            if (nextIndex >= _queue.Count)
            {
                if (RepeatMode == RepeatMode.RepeatQueue)
                {
                    _currentIndex = 0;
                    StartPlaybackOfCurrent();
                }
                else
                {
                    Stop();
                }
            }
            else
            {
                _currentIndex = nextIndex;
                StartPlaybackOfCurrent();
            }
        }

        public void Previous()
        {
            if (_queue.Count == 0)
                return;

            int prevIndex = _currentIndex - 1;
            if (prevIndex < 0)
                prevIndex = 0;

            _currentIndex = prevIndex;
            StartPlaybackOfCurrent();
        }

        public void Seek(double seconds)
        {
            if (_currentStream == 0)
                return;

            _audioEngine.SetPosition(_currentStream, seconds);
        }

        public void SetVolume(float volume)
        {
            Volume = volume;
        }

        public void PlayIndex(int index)
        {
            if (index < 0 || index >= _queue.Count)
                return;

            _currentIndex = index;
            StartPlaybackOfCurrent();
        }

        public void SetSilentIndex(int index)
        {
            if (index < 0 || index >= _queue.Count)
                return;

            _currentIndex = index;
        }

        public void AddToQueue(int TrackId)
        {
            _queue.Add(TrackId);
            InvokeUI(() => QueueChanged?.Invoke(this, [.. _queue]));
        }

        public void AddToQueue(IEnumerable<int> TrackIds)
        {
            _queue.AddRange(TrackIds);
            InvokeUI(() => QueueChanged?.Invoke(this, [.. _queue]));
        }

        public void SetQueue(IEnumerable<int> TrackIds, bool CalculateNewIndex = false)
        {
            int newIndex = TrackIds.Any() ? 0 : -1;
            if (CalculateNewIndex)
            {
                for (int i = 0; i < TrackIds.Count(); i++)
                {
                    if (TrackIds.ElementAt(i) == CurrentTrackId)
                    {
                        newIndex = i;
                        break;
                    }
                }
            }

            _queue.Clear();
            _queue.AddRange(TrackIds);
            _currentIndex = newIndex;
            InvokeUI(() => QueueChanged?.Invoke(this, [.. _queue]));
        }

        public void RemoveFromQueue(int index)
        {
            if (index < 0 || index >= _queue.Count)
                return;

            bool removingCurrent = index == _currentIndex;

            _queue.RemoveAt(index);

            if (removingCurrent)
            {
                Stop();
                _currentIndex = -1;
            }
            else if (index < _currentIndex)
            {
                _currentIndex--;
            }
            InvokeUI(() => QueueChanged?.Invoke(this, [.. _queue]));
        }

        public void ClearQueue()
        {
            Stop();
            _queue.Clear();
            _currentIndex = -1;
            InvokeUI(() => QueueChanged?.Invoke(this, [.. _queue]));
        }

        public int[] GetQueue()
        {
            return [.. _queue];
        }

        public void PlayTrack(int TrackId)
        {
            int index = _queue.IndexOf(TrackId);
            if (index < 0)
            {
                _queue.Add(TrackId);
                index = _queue.Count - 1;
            }

            _currentIndex = index;
            StartPlaybackOfCurrent();
        }

        public void ChangeOutputDevice(int deviceIndex)
        {
            if (deviceIndex == _currentDeviceIndex) return;
            if (_currentStream != 0)
            {
                FreeStream(_currentStream);
                _currentStream = 0;
            }

            CurrentDeviceIndex = deviceIndex;
            if (_queue.Count > 0 && _currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                StartPlaybackOfCurrent();
            }
            InvokeUI(() => DeviceIndexChanged?.Invoke(this, deviceIndex));
        }

        public void ChangeSampleRate(int sampleRate)
        {
            if (sampleRate == _currentSampleRate) return;
            if (_currentStream != 0)
            {
                FreeStream(_currentStream);
                _currentStream = 0;
            }
            CurrentSampleRate = sampleRate;
            InvokeUI(() =>
            SampleRateChanged?.Invoke(this, new SampleRateChangedEventArgs { PreferedSampleRate = sampleRate, EffectiveSampleRate = sampleRate > 0 ? sampleRate : 0 }));
            if (_queue.Count > 0 && _currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                StartPlaybackOfCurrent();
            }
        }

        public (int Index, string Name)[] GetAvailableDevices()
        {
            return [.. _audioEngine
                .GetOutputDevices()
                .Select(d => (d.Index, d.Info.name))];
        }

        public void Dispose()
        {
            _positionTimer.Elapsed -= PositionTimerOnElapsed;
            _positionTimer.Stop();
            _positionTimer.Dispose();

            Stop();
            FreeStream(_mixerStream);
            _audioEngine.Dispose();
        }

        private Track? GetCurrentTrack()
        {
            var id = CurrentTrackId;
            if (id == 0)
                return null;

            return _library.Tracks.FirstOrDefault(t => t.Id == id);
        }

        private void StartPlaybackOfCurrent()
        {
            var track = GetCurrentTrack();
            if (track == null)
            {
                Next();
                return;
            }

            int effectiveSampleRate = CurrentSampleRate > 0 ? CurrentSampleRate : track.SamplingRate;

            // Initialize mixer on first playback
            if (_mixerStream == 0)
            {
                _mixerStream = _audioEngine.CreateMixer(effectiveSampleRate);
                _audioEngine.Play(_mixerStream, false);
            }

            int previousStream = _currentStream;

            // Use preloaded stream if available and matches
            if (_preloadedStream != 0 && _preloadedTrackId == track.Id)
            {
                _currentStream = _preloadedStream;
                _preloadedTrackId = 0;
                _preloadedStream = 0;
            }
            else
            {
                if (_preloadedStream != 0)
                {
                    FreeStream(_preloadedStream);
                    _preloadedStream = 0;
                }

                _currentStream = _audioEngine.CreateDecodeStream(track.Path);
            }

            // Remove previous stream if it exists
            if (previousStream != 0)
            {
                _audioEngine.RemoveFromMixer(previousStream);
                FreeStream(previousStream);
            }

            _audioEngine.SetVolume(_currentStream, _volume);
            _audioEngine.AddToMixer(_mixerStream, _currentStream, BASSFlag.BASS_MIXER_CHAN_NORAMPIN);
            _audioEngine.SetEndCallback(_currentStream, _endCallback, false);

            // Setup preload for next track
            double duration = _audioEngine.GetDuration(_currentStream);
            const double preloadLeadSeconds = 0.75;
            if (duration > preloadLeadSeconds)
            {
                _audioEngine.SetPositionSync(_currentStream, duration - preloadLeadSeconds, _preloadSync);
            }

            _audioEngine.Play(_mixerStream, false);
            SetPlaybackState(PlaybackState.Playing);

            InvokeUI(() =>
            {
                SampleRateChanged?.Invoke(this, new SampleRateChangedEventArgs { PreferedSampleRate = CurrentSampleRate, EffectiveSampleRate = effectiveSampleRate });
                TrackChanged?.Invoke(this, track.Path);
                QueueChanged?.Invoke(this, [.. _queue]);
            });
        }

        private void SetPlaybackState(PlaybackState state)
        {
            if (_playbackState == state)
                return;

            _playbackState = state;

            InvokeUI(() => PlaybackStateChanged?.Invoke(this, _playbackState));
        }

        private void PositionTimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsPlaying || _currentStream == 0)
                return;

            var position = _audioEngine.GetMixerPosition(_currentStream);
            InvokeUI(() => PositionChanged?.Invoke(this, position));
        }

        private void OnTrackEndedInternal(int handle, int channel, int data, IntPtr user)
        {
            if (channel != _currentStream) return;

            InvokeUI(() => TrackEnded?.Invoke(this, EventArgs.Empty));

            // Handle RepeatTrack mode
            if (RepeatMode == RepeatMode.RepeatTrack)
            {
                _audioEngine.SetPosition(_currentStream, 0.0);

                // Re-setup callbacks for the repeated track
                double duration = _audioEngine.GetDuration(_currentStream);
                const double preloadLeadSeconds = 0.75;
                if (duration > preloadLeadSeconds)
                {
                    _audioEngine.SetPositionSync(_currentStream, duration - preloadLeadSeconds, _preloadSync);
                }

                return;
            }

            int nextIndex = _currentIndex + 1;
            if (nextIndex >= _queue.Count)
            {
                if (RepeatMode == RepeatMode.RepeatQueue) nextIndex = 0;
                else
                {
                    Stop();
                    return;
                }
            }

            int nextTrackId = _queue[nextIndex];

            // If preloaded stream is available and matches next track, use it
            if (_preloadedStream != 0 && _preloadedTrackId == nextTrackId)
            {
                int previousStream = _currentStream;
                _currentStream = _preloadedStream;
                _preloadedStream = 0;
                _preloadedTrackId = 0;
                _currentIndex = nextIndex;

                // Remove previous and add preloaded stream to mixer immediately
                _audioEngine.RemoveFromMixer(previousStream);
                FreeStream(previousStream);

                _audioEngine.SetVolume(_currentStream, _volume);
                _audioEngine.AddToMixer(_mixerStream, _currentStream, BASSFlag.BASS_MIXER_CHAN_NORAMPIN);
                _audioEngine.SetEndCallback(_currentStream, _endCallback, false);

                // Setup preload for next track
                double duration = _audioEngine.GetDuration(_currentStream);
                const double preloadLeadSeconds = 0.75;
                if (duration > preloadLeadSeconds)
                {
                    _audioEngine.SetPositionSync(_currentStream, duration - preloadLeadSeconds, _preloadSync);
                }

                var track = GetCurrentTrack();
                if (track != null)
                {
                    InvokeUI(() =>
                    {
                        TrackChanged?.Invoke(this, track.Path);
                        QueueChanged?.Invoke(this, [.. _queue]);
                    });
                }
                return;
            }

            // Fallback: if no preloaded stream, load next track normally
            _currentIndex = nextIndex;
            StartPlaybackOfCurrent();
        }

        private void OnPreloadSync(int handle, int channel, int data, IntPtr user)
        {
            // Don't preload if we're in RepeatTrack mode
            if (RepeatMode == RepeatMode.RepeatTrack) return;

            Task.Run(() =>
            {
                int nextIndex = _currentIndex + 1;
                if (nextIndex >= _queue.Count)
                {
                    if (RepeatMode == RepeatMode.RepeatQueue) nextIndex = 0;
                    else return;
                }

                int nextTrackId = _queue[nextIndex];
                if (_preloadedTrackId == nextTrackId) return; // Already preloaded

                var nextTrack = _library.Tracks.FirstOrDefault(t => t.Id == nextTrackId);
                if (nextTrack == null) return;

                // Clear previous preloaded stream if any
                if (_preloadedStream != 0)
                {
                    FreeStream(_preloadedStream);
                    _preloadedStream = 0;
                    _preloadedTrackId = 0;
                }

                // Create decode stream and prepare it (but don't add to mixer yet)
                int nextStream = _audioEngine.CreateDecodeStream(nextTrack.Path);
                if (nextStream == 0) return;

                _preloadedStream = nextStream;
                _preloadedTrackId = nextTrackId;
            });
        }

        private void FreeStream(int streamHandle)
        {
            if (streamHandle != 0)
            {
                _audioEngine.Stop(streamHandle);
                _audioEngine.Free(streamHandle);
            }
        }

        private static void InvokeUI(Action action)
        {
            if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send, action);
            }
        }
    }

    public class QueuedTrack
    {
        public int TrackId { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
    public class SampleRateChangedEventArgs
    {
        public int PreferedSampleRate { get; set; }
        public int EffectiveSampleRate { get; set; }
    }

    public class OutputDeviceState
    {
        public int SampleRate { get; set; }
        public int Bitrate { get; set; }
        public int Channels { get; set; }
        public int BitDepth { get; set; }
        public string Codec { get; set; } = string.Empty;
    }
}
