using System.Timers;
using System.Linq;
using Un4seen.Bass;
using System.Diagnostics;
using MusicWrap.Data.Library.Models;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.User.Models;
using System.Net;
using Microsoft.Extensions.Logging;

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
        int CurrentQueueIndex { get; }
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
        OutputMode CurrentOutputMode { get; }

        RepeatMode RepeatMode { get; set; }
        ContinueMode ContinueMode { get; set; }

        event EventHandler<string>? TrackChanged;
        event EventHandler? TrackEnded;
        event EventHandler<PlaybackState>? PlaybackStateChanged;
        event EventHandler<double>? PositionChanged;
        event EventHandler<int[]>? QueueChanged;
        event EventHandler<int>? DeviceIndexChanged;
        event EventHandler<SampleRateChangedEventArgs>? SampleRateChanged;
        event EventHandler<OutputMode>? OutputModeChanged;
        event EventHandler<float[]>? WaveformDataChanged;
        void LoadIndex(int index, bool autoPlay);
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
        void ChangeOutputMode(OutputMode mode);
        (int Index, string Name)[] GetAvailableDevices();
    }
    public class MusicPlayerService : IMusicPlayerService, IDisposable
    {
        private readonly MusicLibrary _library;
        private readonly AudioEngine _audioEngine;
        private readonly ILogger<MusicPlayerService> _logger;

        private readonly List<int> _queue = [];

        private int _mixerStream = 0;
        private int _mixerSampleRate = 0;
        private int _mixerChannels = 2;

        private int _currentIndex = -1;
        public int CurrentQueueIndex => _currentIndex;

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
        private OutputMode _currentOutputMode = OutputMode.WasapiShared;
        public OutputMode CurrentOutputMode
        {
            get => _currentOutputMode;
            private set => _currentOutputMode = value;
        }

        // States

        public bool IsPlaying => _playbackState == PlaybackState.Playing;
        public bool IsPaused => _playbackState == PlaybackState.Paused;
        public double CurrentPosition => _currentStream != 0 ? _audioEngine.GetMixerPosition(_currentStream) : 0.0;
        public double Duration => _currentStream != 0 ? _audioEngine.GetDuration(_currentStream) : 0.0;

        private const double PositionTimerPlayingIntervalMs = 50;
        private const double PositionTimerIdleIntervalMs = 500;

        // Waveform

        private const int WaveformDataPoints = 2000;
        private readonly Dictionary<int, float[]> _waveformCache = [];
        private readonly object _waveformCacheLock = new object();
        private int _waveformVersion = 0;

        private DateTime _suppressPositionUntilUtc = DateTime.MinValue;

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

        // Providers
        private readonly ITrackPlaybackResolver _trackPlaybackResolver;

        public MusicPlayerService(MusicLibrary library, ITrackPlaybackResolver trackPlaybackResolver, ILogger<MusicPlayerService> logger)
        {
            _library = library;
            _trackPlaybackResolver = trackPlaybackResolver;
            _logger = logger;

            _audioEngine = new AudioEngine();

            _audioEngine.Initialize(CurrentDeviceIndex, 44100, CurrentOutputMode);

            _endCallback = OnTrackEndedInternal;
            _preloadSync = OnPreloadSync;

            _positionTimer = new System.Timers.Timer(PositionTimerIdleIntervalMs)
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
        public event EventHandler<float[]>? WaveformDataChanged;
        public event EventHandler<OutputMode>? OutputModeChanged;

        public void LoadIndex(int index, bool autoplay)
        {
            if (index < 0 || index >= _queue.Count)
                return;
            _currentIndex = index;
            StartPlaybackOfCurrent(autoplay);
        }
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

            if (_mixerStream != 0)
            {
                _audioEngine.Stop(_mixerStream);
                FreeStream(_mixerStream);
            }

            _mixerStream = 0;
            _mixerSampleRate = 0;
            _mixerChannels = 2;

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

            if (_audioEngine.SetPosition(_currentStream, seconds))
            {
                var target = Math.Clamp(seconds, 0.0, Duration);

                int latencyMs = _audioEngine.GetDeviceLatencyMs();
                int suppressMs = Math.Clamp(latencyMs * 2 + 40, 250, 900);
                _suppressPositionUntilUtc = DateTime.UtcNow.AddMilliseconds(suppressMs);

                //var pos = _audioEngine.GetMixerPosition(_currentStream);
                InvokeUI(() => PositionChanged?.Invoke(this, target)); // update position immediately after seek
            }
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
            NotifyQueueChanged();
        }

        public void AddToQueue(IEnumerable<int> TrackIds)
        {
            _queue.AddRange(TrackIds);
            NotifyQueueChanged();
        }

        public void SetQueue(IEnumerable<int> TrackIds, bool CalculateNewIndex = false)
        {
            var list = TrackIds as IList<int> ?? TrackIds.ToList();

            int newIndex = list.Count > 0 ? 0 : -1;

            if (CalculateNewIndex && list.Count > 0)
            {
                int currentTrackId = CurrentTrackId;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == currentTrackId)
                    {
                        newIndex = i;
                        break;
                    }
                }
            }
            _queue.Clear();
            if (list.Count > 0)
            {
                _queue.AddRange(list);
            }
            _currentIndex = newIndex;

            NotifyQueueChanged();
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

            NotifyQueueChanged();
        }

        public void ClearQueue()
        {
            Stop();
            _queue.Clear();
            _currentIndex = -1;
            NotifyQueueChanged();
        }

        public int[] GetQueue()
        {
            return _queue.Count == 0 ? Array.Empty<int>() : _queue.ToArray();
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

            bool shouldResume = IsPlaying;

            double position = CurrentPosition;

            Stop();

            CurrentDeviceIndex = deviceIndex;
            int sr = CurrentSampleRate > 0 ? CurrentSampleRate : 44100;
            _audioEngine.Reinitialize(CurrentDeviceIndex, sr, CurrentOutputMode);

            if (_queue.Count > 0 && _currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                _mixerStream = 0;
                StartPlaybackOfCurrent(shouldResume);
                if (position > 0 && !shouldResume)
                {
                    Seek(position);
                }
            }
            InvokeUI(() => DeviceIndexChanged?.Invoke(this, deviceIndex));
        }

        public void ChangeSampleRate(int sampleRate)
        {
            if (sampleRate == _currentSampleRate) return;

            bool shouldResume = IsPlaying;
            var position = CurrentPosition;

            Stop();
            CurrentSampleRate = sampleRate;
            int sr = CurrentSampleRate > 0 ? CurrentSampleRate : 44100;
            _audioEngine.Reinitialize(CurrentDeviceIndex, sr, CurrentOutputMode);

            if (_queue.Count > 0 && _currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                _mixerStream = 0;
                StartPlaybackOfCurrent(shouldResume);
                if (!shouldResume && position > 0) Seek(position);
            }

            InvokeUI(() => SampleRateChanged?.Invoke(this, new SampleRateChangedEventArgs
            {
                PreferedSampleRate = sampleRate,
                EffectiveSampleRate = sampleRate > 0 ? sampleRate : 0
            }));
        }

        public void ChangeOutputMode(OutputMode outputMode)
        {
            if (outputMode == _currentOutputMode) return;

            bool shouldResume = IsPlaying;
            double position = CurrentPosition;
            Stop();
            CurrentOutputMode = outputMode;
            int sr = CurrentSampleRate > 0 ? CurrentSampleRate : 44100;
            _audioEngine.Reinitialize(CurrentDeviceIndex, sr, CurrentOutputMode);
            if (_queue.Count > 0 && _currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                _mixerStream = 0;
                StartPlaybackOfCurrent(shouldResume);
                if (position > 0 && !shouldResume)
                {
                    Seek(position);
                }
            }

            InvokeUI(() => OutputModeChanged?.Invoke(this, outputMode));
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

        private void StartPlaybackOfCurrent(bool autoplay = true)
        {
            var track = GetCurrentTrack();

            if (track == null)
            {
                Next();
                return;
            }

            int requestedSampleRate = CurrentSampleRate > 0 ? CurrentSampleRate : track.SamplingRate;
            int requestedChannels = track.Channels > 0 ? track.Channels : 2;

            int effectiveSampleRate = _audioEngine.PrepareOutputForTrack(requestedSampleRate, requestedChannels);
            int effectiveChannels = _audioEngine.CurrentOutputChannels > 0 ? _audioEngine.CurrentOutputChannels : requestedChannels;

            bool shouldRecreateMixer = _mixerStream == 0
                || _mixerSampleRate != effectiveSampleRate
                || _mixerChannels != effectiveChannels;

            // Initialize mixer on first playback
            if (shouldRecreateMixer)
            {
                if (_mixerStream != 0)
                {
                    _audioEngine.Stop(_mixerStream);
                    FreeStream(_mixerStream);
                    _mixerStream = 0;
                }

                _mixerStream = _audioEngine.CreateMixer(effectiveSampleRate);
                _audioEngine.AttachOutputToMixer(_mixerStream, effectiveSampleRate, effectiveChannels);
                _mixerSampleRate = effectiveSampleRate;
                _mixerChannels = effectiveChannels;

                if (autoplay) _audioEngine.Play(_mixerStream, false);
                else _audioEngine.Pause(_mixerStream);

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
                if (!_trackPlaybackResolver.TryResolve(track, out var resolvedCurrent))
                {
                    _logger.LogWarning("Failed to resolve playback source for track: {TrackId}, {TrackOrigin}", track.Id, track.Origin);
                    Next();
                    return;
                }

                int createdStream;
                switch (resolvedCurrent.Kind)
                {
                    case PlaybackSourceKind.LocalFile:
                        createdStream = _audioEngine.CreateDecodeStream(resolvedCurrent.Input);
                        break;
                    case PlaybackSourceKind.RemoteUrl:
                        createdStream = _audioEngine.CreateDecodeStreamFromUrl(resolvedCurrent.Input);
                        break;
                    default:
                        _logger.LogWarning("Unsupported playback source kind {PlaybackSourceKind} for track {TrackId}, {TrackOrigin}", resolvedCurrent.Kind, track.Id, track.Origin);
                        Next();
                        return;
                }

                _currentStream = createdStream;
                if (_currentStream == 0)
                {
                    var err = _audioEngine.GetLastError();
                    _logger.LogWarning("Failed to create decode stream for track {TrackId}, {TrackOrigin}, error code: {ErrorCode}", track.Id, track.Origin, err);
                    Next();
                    return;
                }
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

            if (autoplay)
            {

                _audioEngine.Play(_mixerStream, false);
                SetPlaybackState(PlaybackState.Playing);
            }
            else
            {
                _audioEngine.Pause(_mixerStream);
                SetPlaybackState(PlaybackState.Paused);
            }
            var inmidiatePos = _audioEngine.GetMixerPosition(_currentStream);
            InvokeUI(() => PositionChanged?.Invoke(this, inmidiatePos)); // update position immediately on track change

            var snapshot = CreateQueueSnapshot();
            InvokeUI(() =>
            {
                SampleRateChanged?.Invoke(this, new SampleRateChangedEventArgs { PreferedSampleRate = CurrentSampleRate, EffectiveSampleRate = effectiveSampleRate });
                var trackRef = string.IsNullOrWhiteSpace(track.Path) ? (track.SourceUri ?? string.Empty) : track.Path;
                TrackChanged?.Invoke(this, trackRef);
                QueueChanged?.Invoke(this, snapshot);
            });
            BeginWaveformPipeline(track);
        }

        private void SetPlaybackState(PlaybackState state)
        {
            if (_playbackState == state)
                return;

            _playbackState = state;

            _positionTimer.Interval = _playbackState == PlaybackState.Playing
                ? PositionTimerPlayingIntervalMs
                : PositionTimerIdleIntervalMs;

            InvokeUI(() => PlaybackStateChanged?.Invoke(this, _playbackState));
        }

        private void PositionTimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsPlaying || _currentStream == 0)
                return;
            if (DateTime.UtcNow < _suppressPositionUntilUtc)
                return; // Suppress position updates for a short time after seeking to avoid UI jitter

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
                var snapshot = CreateQueueSnapshot();
                if (track != null)
                {
                    InvokeUI(() =>
                    {
                        var trackRef = string.IsNullOrWhiteSpace(track.Path) ? (track.SourceUri ?? string.Empty) : track.Path;
                        TrackChanged?.Invoke(this, trackRef);
                        QueueChanged?.Invoke(this, snapshot);
                    });
                    BeginWaveformPipeline(track);
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

                if (!_trackPlaybackResolver.TryResolve(nextTrack, out var resolvedNext))
                {
                    _logger.LogWarning("Failed to resolve playback source for preload of track : {TrackId}, {TrackOrigin}, {TrackPath}", nextTrack.Id, nextTrack.Origin, nextTrack.Path);
                    return;
                }

                int nextStream;

                switch(resolvedNext.Kind){
                    case PlaybackSourceKind.LocalFile:
                        nextStream = _audioEngine.CreateDecodeStream(resolvedNext.Input);
                        break;
                    case PlaybackSourceKind.RemoteUrl:
                        nextStream = _audioEngine.CreateDecodeStreamFromUrl(resolvedNext.Input);
                        break;
                    default:
                        _logger.LogWarning("Unsupported playback source kind {PlaybackSourceKind} for preload of track {TrackId}, {TrackOrigin}, {TrackPath}", resolvedNext.Kind, nextTrack.Id, nextTrack.Origin, nextTrack.Path);
                        return;
                }

                if (nextStream == 0)
                {
                    var err = _audioEngine.GetLastError();
                    _logger.LogWarning("Failed to create decode stream for preload of track {TrackId}, {TrackOrigin}, {TrackPath}, error code: {ErrorCode}", nextTrack.Id, nextTrack.Origin, nextTrack.Path, err);
                    return;
                }

                _preloadedStream = nextStream;
                _preloadedTrackId = nextTrackId;
                _ = PreloadWaveformCacheAsync(nextTrack);
            });
        }

        private void FreeStream(int streamHandle)
        {
            if (streamHandle != 0)
            {
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

        private int[] CreateQueueSnapshot()
        {
            return _queue.Count == 0 ? Array.Empty<int>() : [.. _queue];
        }
        private void BeginWaveformPipeline(Track track)
        {
            int requestVersion = Interlocked.Increment(ref _waveformVersion);
            // inmidiate fallback
            PublishWaveformIfCurrent(track.Id, requestVersion, CreateFallbackWaveForm(WaveformDataPoints));

            if (TryGetWaveformFromCache(track.Id, out var cached))
            {
                PublishWaveformIfCurrent(track.Id, requestVersion, cached ?? Array.Empty<float>());
                return;
            }

            _ = Task.Run(async () =>
            {
                var waveform = await AudioEngine.GetWaveFromDataAsync(track.Path, WaveformDataPoints);
                if (waveform.Length == 0)
                {
                    waveform = CreateFallbackWaveForm(WaveformDataPoints);
                }
                CacheWaveform(track.Id, waveform);
                PublishWaveformIfCurrent(track.Id, requestVersion, waveform);
            });
        }
        private async Task PreloadWaveformCacheAsync(Track track)
        {
            if (TryGetWaveformFromCache(track.Id, out _)) return; // Already cached
            var waveform = await AudioEngine.GetWaveFromDataAsync(track.Path, WaveformDataPoints);
            if (waveform.Length == 0) return;
            CacheWaveform(track.Id, waveform);
        }
        private bool TryGetWaveformFromCache(int trackId, out float[]? data)
        {
            lock (_waveformCacheLock)
            {
                return _waveformCache.TryGetValue(trackId, out data);
            }
        }
        private void CacheWaveform(int trackId, float[] data)
        {
            lock (_waveformCacheLock)
            {
                _waveformCache[trackId] = data;
            }
        }
        private void PublishWaveformIfCurrent(int trackId, int requestVersion, float[] data)
        {
            if (trackId <= 0) return;


            InvokeUI(() =>
            {
                if (requestVersion != Volatile.Read(ref _waveformVersion)) return; // Stale request, ignore

                if (CurrentTrackId != trackId) return; // Not current track anymore, ignore

                WaveformDataChanged?.Invoke(this, data);

            });
        }
        private static float[] CreateFallbackWaveForm(int points = 2000)
        {
            var data = new float[points];
            Array.Fill(data, 1f);
            return data;
        }
        private void NotifyQueueChanged()
        {
            var snapshot = CreateQueueSnapshot();
            InvokeUI(() => QueueChanged?.Invoke(this, snapshot));
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
