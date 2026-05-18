using Microsoft.Extensions.Logging;
using MusicWrap.Core.Queue;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Core.Sources.Providers.Queue;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Player;
using MusicWrap.Data.Player.Models;
using MusicWrap.Data.User.Models;
using System.Timers;
using Un4seen.Bass;

namespace MusicWrap.Core.Services.Playback
{
    public interface IMusicPlayerService
    {
        int CurrentQueueIndex { get; }
        int CurrentPlaybackIndex { get; }
        int[] GetPlaybackOrder();
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
        float[] CurrentWaveformData { get; }

        RepeatMode RepeatMode { get; set; }
        ContinueMode ContinueMode { get; set; }
        bool IsShuffleEnabled { get; }
        event EventHandler<bool>? ShuffleStateChanged;

        event EventHandler<string>? TrackChanged;
        event EventHandler? TrackEnded;
        event EventHandler<PlaybackState>? PlaybackStateChanged;
        event EventHandler<double>? PositionChanged;
        event EventHandler<int[]>? QueueChanged;
        event EventHandler<int>? DeviceIndexChanged;
        event EventHandler<SampleRateChangedEventArgs>? SampleRateChanged;
        event EventHandler<OutputMode>? OutputModeChanged;
        event EventHandler<float[]>? WaveformDataChanged;
        event EventHandler<float>? VolumeChanged;
        void LoadIndex(int index, bool autoPlay);
        void LoadPlaybackIndex(int index, bool autoPlay);
        void Play();
        void Pause();
        void Stop();
        void Next();
        void Previous();
        void Seek(double seconds);
        void FlushPlaybackState();
        void SetVolume(float volume);
        void PlayIndex(int index);
        void PlayPlaybackIndex(int index);
        void ToggleShuffle();
        void SetShuffle(bool enabled);

        void SetSilentIndex(int index);
        void SetSilentPlaybackIndex(int index);
        void AddToQueue(int TrackId);
        void AddToQueue(IEnumerable<int> TrackIds);
        void SetQueue(IEnumerable<int> TrackIds, bool CalculateNewIndex = false);
        void RemoveFromQueue(int index);
        void ClearQueue();
        int[] GetQueue();
        void PlayTrack(int TrackId);
        void SetPlaybackOrder(int[] playbackOrderIndices);

        void ChangeOutputDevice(int deviceIndex);
        void ChangeSampleRate(int sampleRate);
        void ChangeOutputMode(OutputMode mode);
        (int Index, string Name)[] GetAvailableDevices();
        PlaybackQueueSnapshot BuildPlaybackSnapshot();
        void LoadInitialState(UserSettings settings);
    }
    public class MusicPlayerService : IMusicPlayerService, IDisposable
    {
        // Providers
        private readonly IQueueManager _queue;
        private readonly IQueueItemPlaybackResolver _queueItemResolver;
        private readonly MusicLibrary _library;
        private readonly AudioEngine _audioEngine;
        private readonly ILogger<MusicPlayerService> _logger;
        private readonly IPlaybackRepository _playbackRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly IUIDispatcher _dispatcher;
        private readonly UserSettings _userSettings;
        private ISaveCoordinator? _saveCoordinator;

        private const int MaxErrorCount = 5;
        private int _errorCount;

        //private readonly List<int> _queue = [];

        private int _mixerStream = 0;
        private int _mixerSampleRate = 0;
        private int _mixerChannels = 2;

        public int CurrentQueueIndex => _queue.CurrentIndex;
        public int CurrentPlaybackIndex => _queue.CurrentPlaybackIndex;

        private int _currentStream;
        private double _selectedTrackDuration;
        private float[] _currentWaveform = Array.Empty<float>();

        private int _preloadedStream = 0;
        private PlaybackQueueItem? _preloadedQueueItem;
        private readonly SYNCPROC _preloadSync;

        private PlaybackState _playbackState = PlaybackState.Stopped;
        //private float _volume = 1.0f;
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
        public double CurrentPosition => _currentStream != 0 ? GetEffectivePosition() : 0.0;
        public double Duration => _currentStream != 0 ? _currentTrackDuration : _selectedTrackDuration;
        public float[] CurrentWaveformData => _currentWaveform;

        private double _currentTrackDuration;

        private const double PositionTimerPlayingIntervalMs = 50;
        private const double PositionTimerIdleIntervalMs = 500;

        // Waveform
        private const int MaxWaveformCacheEntries = 256;
        private const int WaveformDataPoints = 2000;
        private readonly Dictionary<int, float[]> _waveformCache = [];
        private readonly Dictionary<int, LinkedListNode<int>> _waveformCacheNodes = [];
        private readonly LinkedList<int> _waveformCacheLru = [];
        private readonly object _waveformCacheLock = new object();
        private int _waveformVersion = 0;

        private DateTime _suppressPositionUntilUtc = DateTime.MinValue;

        private int _positionDebugRemaining = 5;

        private double? _pendingResumePositionSeconds;

        private bool _isRestoringInitialState;

        public float Volume
        {
            get => _userSettings.PreferredVolume;
            set
            {
                var v = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(_userSettings.PreferredVolume - v) < 0.0001f) return;
                _userSettings.PreferredVolume = v;
                if (_currentStream != 0) _audioEngine.SetVolume(_currentStream, _userSettings.PreferredVolume);
                _dispatcher.Invoke(() => VolumeChanged?.Invoke(this, _userSettings.PreferredVolume));
                EnqueueSave(SaveKind.Settings);
            }
        }

        public int CurrentTrackId => _queue.CurrentItem?.LibraryId ?? 0;

        public string CurrentTrackPath => _queue.CurrentItem?.Source ?? string.Empty;

        public int QueueCount => _queue.Items.Count;
        //private RepeatMode _repeatMode = RepeatMode.None;
        public RepeatMode RepeatMode
        {
            get => _userSettings.RepeatMode;
            set
            {
                if (_userSettings.RepeatMode == value)
                {
                    return;
                }

                _userSettings.RepeatMode = value;
                //SyncQueueRepeatMode();
                EnqueueSave(SaveKind.Settings);
            }
        }

        public bool IsShuffleEnabled => _userSettings.IsShuffleEnabled;

        //private ContinueMode _continueMode = ContinueMode.None;
        public ContinueMode ContinueMode
        {
            get => _userSettings.ContinueMode;
            set
            {
                if (_userSettings.ContinueMode == value)
                {
                    return;
                }

                _userSettings.ContinueMode = value;
                EnqueueSave(SaveKind.Settings);
            }
        }

        public MusicPlayerService(
            MusicLibrary library,
            IQueueManager queueManager,
            IQueueItemPlaybackResolver queueItemPlaybackResolver,
            IPlaybackRepository playbackRepository,
            IServiceProvider serviceProvider,
            ILogger<MusicPlayerService> logger,
            IUIDispatcher dispatcher,
            UserSettings userSettings
            )
        {
            _library = library;
            _queue = queueManager;
            _queueItemResolver = queueItemPlaybackResolver;
            _playbackRepository = playbackRepository;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dispatcher = dispatcher;
            _userSettings = userSettings;

            _audioEngine = new AudioEngine();

            var result = _audioEngine.Initialize(CurrentDeviceIndex, 44100, CurrentOutputMode);
            if (!result)
            {
                var err = _audioEngine.GetLastError();
                _logger.LogCritical("Failed to initialize audio engine with device index {DeviceIndex}, error code: {ErrorCode}", CurrentDeviceIndex, err);
            }

            _endCallback = OnTrackEndedInternal;
            _preloadSync = OnPreloadSync;

            _positionTimer = new System.Timers.Timer(PositionTimerIdleIntervalMs)
            {
                AutoReset = true
            };
            _positionTimer.Elapsed += PositionTimerOnElapsed;
            _positionTimer.Start();

            _queue.QueueChanged += QueueOnQueueChanged;
            _queue.CurrentChanged += QueueOnCurrentChanged;
            //SyncQueueRepeatMode();
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
        public event EventHandler<float>? VolumeChanged;
        public event EventHandler<bool>? ShuffleStateChanged;

        public void LoadIndex(int index, bool autoplay)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;
            _queue.Jump(index);
            StartPlaybackOfCurrent(autoplay);
        }

        public void LoadPlaybackIndex(int index, bool autoplay)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;
            _queue.JumpPlaybackIndex(index);
            StartPlaybackOfCurrent(autoplay);
        }
        public void Play()
        {
            if (_currentStream == 0)
            {
                if (_queue.Items.Count == 0) return;

                if (_queue.CurrentIndex < 0 || _queue.CurrentIndex >= _queue.Items.Count)
                    _queue.Jump(0);

                StartPlaybackOfCurrent();
                return;
            }

            _audioEngine.Play(_mixerStream, false);
            SetPlaybackState(PlaybackState.Playing);
            _positionDebugRemaining = 5;
            var mixerPos = _audioEngine.GetMixerPosition(_currentStream);
            var streamPos = _audioEngine.GetPosition(_currentStream);
            var effectivePos = GetEffectivePosition();
            _logger.LogDebug("Play: mixerPos={MixerPos:0.000}s, streamPos={StreamPos:0.000}s, effectivePos={EffectivePos:0.000}s",
                mixerPos, streamPos, effectivePos);
            if (_pendingResumePositionSeconds.HasValue)
            {
                _ = ApplyPendingResumeSeekAsync("Play");
            }
            EnqueueSave(SaveKind.Playback);
        }

        public void Pause()
        {
            if (_currentStream == 0)
                return;

            _audioEngine.Pause(_mixerStream);
            SetPlaybackState(PlaybackState.Paused);
            EnqueueSave(SaveKind.Playback);
        }

        public void Stop()
        {
            if (_currentStream != 0) _audioEngine.RemoveFromMixer(_currentStream);
            FreeStream(_currentStream);
            _currentStream = 0;
            _currentTrackDuration = 0.0;

            if (_preloadedStream != 0) FreeStream(_preloadedStream);
            _preloadedStream = 0;
            _preloadedQueueItem = null;

            if (_mixerStream != 0)
            {
                _audioEngine.Stop(_mixerStream);
                FreeStream(_mixerStream);
            }

            _mixerStream = 0;
            _mixerSampleRate = 0;
            _mixerChannels = 2;

            SetPlaybackState(PlaybackState.Stopped);
            EnqueueSave(SaveKind.Playback);
        }

        public void Next()
        {
            var next = _queue.Next();
            if (next == null)
            {
                Stop();
                return;
            }
            StartPlaybackOfCurrent();
        }

        public void Previous()
        {
            var prev = _queue.Previous();
            if (prev == null) return;
            StartPlaybackOfCurrent();
        }

        public void Seek(double seconds)
        {
            if (_currentStream == 0)
                return;

            var seekOk = _audioEngine.SetPosition(_currentStream, seconds);
            if (seekOk)
            {
                var target = Math.Clamp(seconds, 0.0, Duration);
                var mixerPos = _audioEngine.GetMixerPosition(_currentStream);
                var streamPos = _audioEngine.GetPosition(_currentStream);
                _logger.LogDebug("Seek applied: target={Target:0.000}s, mixerPos={MixerPos:0.000}s, streamPos={StreamPos:0.000}s", target, mixerPos, streamPos);

                int latencyMs = _audioEngine.GetDeviceLatencyMs();
                int suppressMs = Math.Clamp(latencyMs * 2 + 40, 250, 900);
                _suppressPositionUntilUtc = DateTime.UtcNow.AddMilliseconds(suppressMs);

                //var pos = _audioEngine.GetMixerPosition(_currentStream);
                _dispatcher.Invoke(() => PositionChanged?.Invoke(this, target)); // update position immediately after seek
                EnqueueSave(SaveKind.Playback);
            }
            else
            {
                var mixerPos = _audioEngine.GetMixerPosition(_currentStream);
                var streamPos = _audioEngine.GetPosition(_currentStream);
                _logger.LogWarning("Seek failed: target={Target:0.000}s, mixerPos={MixerPos:0.000}s, streamPos={StreamPos:0.000}s", seconds, mixerPos, streamPos);
            }
        }

        public void FlushPlaybackState()
        {
            _playbackRepository.Save(BuildPlaybackSnapshot());
        }

        public void SetVolume(float volume) => Volume = volume;

        public void PlayIndex(int index)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;
            _queue.Jump(index);
            StartPlaybackOfCurrent();
        }

        public void PlayPlaybackIndex(int index)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;
            _queue.JumpPlaybackIndex(index);
            StartPlaybackOfCurrent();
        }

        public void ToggleShuffle()
        {
            SetShuffle(!_userSettings.IsShuffleEnabled);
        }

        public void SetShuffle(bool enabled)
        {
            if (_userSettings.IsShuffleEnabled == enabled) return;
            _queue.SetShuffle(enabled); // delegate shuffle state management to queue
            _dispatcher.Invoke(() => ShuffleStateChanged?.Invoke(this, enabled));
            EnqueueSave(SaveKind.Settings);
        }

        public void SetSilentIndex(int index)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;
            _queue.Jump(index);
            UpdateSelectedTrackState();
            EnqueueSave(SaveKind.Playback);
        }

        public void SetSilentPlaybackIndex(int index)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;
            _queue.JumpPlaybackIndex(index);
            UpdateSelectedTrackState();
            EnqueueSave(SaveKind.Playback);
        }

        public void AddToQueue(int TrackId)
        {
            var item = CreateQueueItemFromTrackId(TrackId);
            if (item == null) return;
            _queue.AddLast([item]);
            NotifyQueueChanged();
            EnqueueSave(SaveKind.Playback);
        }

        public void AddToQueue(IEnumerable<int> TrackIds)
        {
            var items = TrackIds
                .Select(CreateQueueItemFromTrackId)
                .Where(item => item != null)
                .ToList();
            if (items.Count == 0) return;
            _queue.AddLast(items!);
            NotifyQueueChanged();
            EnqueueSave(SaveKind.Playback);
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
            var items = list
                .Select(CreateQueueItemFromTrackId)
                .Where(item => item != null)
                .ToList();
            _queue.Set(items!, newIndex);
            UpdateSelectedTrackState();

            NotifyQueueChanged();
            EnqueueSave(SaveKind.Playback);
        }

        public void RemoveFromQueue(int index)
        {
            if (index < 0 || index >= _queue.Items.Count)
                return;

            bool removingCurrent = index == _queue.CurrentIndex;

            _queue.RemoveAt(index);

            if (removingCurrent)
            {
                Stop();
            }

            UpdateSelectedTrackState();

            NotifyQueueChanged();
            EnqueueSave(SaveKind.Playback);
        }

        public void ClearQueue()
        {
            Stop();
            _queue.Clear();
            UpdateSelectedTrackState();
            NotifyQueueChanged();
            EnqueueSave(SaveKind.Playback);
        }

        public int[] GetQueue()
        {
            return _queue.Items.Count == 0
                ? Array.Empty<int>()
                : _queue.Items.Select(item => item.LibraryId ?? 0).ToArray();
        }

        public int[] GetPlaybackOrder()
        {
            return _queue.GetPlaybackOrderIndices();
        }

        public void SetPlaybackOrder(int[] playbackOrderIndices)
        {
            _queue.SetPlaybackOrder(playbackOrderIndices);
        }

        public void PlayTrack(int TrackId)
        {
            int index = _queue.Items.ToList().FindIndex(item => item.LibraryId == TrackId);
            if (index < 0)
            {
                var item = CreateQueueItemFromTrackId(TrackId);
                if (item == null) return;
                _queue.AddLast([item]);
                index = _queue.Items.Count - 1;
            }

            _queue.Jump(index);
            StartPlaybackOfCurrent();
            EnqueueSave(SaveKind.Playback);
        }

        private void UpdateSelectedTrackState()
        {
            var item = _queue.CurrentItem;
            if (item == null)
            {
                _selectedTrackDuration = 0;
                _currentWaveform = Array.Empty<float>();
                _dispatcher.Invoke(() =>
                {
                    TrackChanged?.Invoke(this, string.Empty);
                    WaveformDataChanged?.Invoke(this, Array.Empty<float>());
                });
                return;
            }

            Track? track = null;
            if (item.LibraryId.HasValue)
            {
                track = _library.Tracks.FirstOrDefault(t => t.Id == item.LibraryId.Value);
            }

            if (track == null)
            {
                _selectedTrackDuration = 0;
                _currentWaveform = CreateFallbackWaveForm(WaveformDataPoints);
                _dispatcher.Invoke(() =>
                {
                    TrackChanged?.Invoke(this, item.DisplayTitle ?? item.Source);
                    WaveformDataChanged?.Invoke(this, _currentWaveform);
                });
                return;
            }

            _selectedTrackDuration = track.DurationSeconds > 0 ? track.DurationSeconds : 0;
            var trackRef = string.IsNullOrWhiteSpace(track.FilePath) ? string.Empty : track.FilePath;
            _dispatcher.Invoke(() => TrackChanged?.Invoke(this, trackRef));
            BeginWaveformPipeline(track);
        }

        public void ChangeOutputDevice(int deviceIndex)
        {
            if (deviceIndex == _currentDeviceIndex) return;

            bool shouldResume = IsPlaying;

            double position = CurrentPosition;

            Stop();

            int sr = CurrentSampleRate > 0 ? CurrentSampleRate : 44100;
            bool appliedPreferred = TryReinitializeOutput(deviceIndex, sr, CurrentOutputMode);
            if (!appliedPreferred && !TryReinitializeOutput(-1, 44100, OutputMode.WasapiShared))
            {
                return;
            }

            CurrentDeviceIndex = appliedPreferred ? deviceIndex : -1;
            CurrentOutputMode = _audioEngine.GetCurrentOutputMode();

            if (_queue.Items.Count > 0 && _queue.CurrentIndex >= 0 && _queue.CurrentIndex < _queue.Items.Count)
            {
                _mixerStream = 0;
                StartPlaybackOfCurrent(shouldResume);
                if (position > 0 && !shouldResume)
                {
                    Seek(position);
                }
            }
            _dispatcher.Invoke(() => DeviceIndexChanged?.Invoke(this, CurrentDeviceIndex));
            EnqueueSave(SaveKind.Playback);
        }

        public void ChangeSampleRate(int sampleRate)
        {
            if (sampleRate == _currentSampleRate) return;

            bool shouldResume = IsPlaying;
            var position = CurrentPosition;

            Stop();
            int currentSampleRate = CurrentSampleRate > 0 ? CurrentSampleRate : 44100;
            int targetSampleRate = sampleRate > 0 ? sampleRate : currentSampleRate;
            bool appliedPreferred = TryReinitializeOutput(CurrentDeviceIndex, targetSampleRate, CurrentOutputMode);
            if (!appliedPreferred && !TryReinitializeOutput(-1, 44100, OutputMode.WasapiShared))
            {
                return;
            }

            CurrentSampleRate = appliedPreferred ? sampleRate : -1;
            CurrentOutputMode = _audioEngine.GetCurrentOutputMode();
            if (!appliedPreferred)
            {
                CurrentDeviceIndex = -1;
            }

            if (_queue.Items.Count > 0 && _queue.CurrentIndex >= 0 && _queue.CurrentIndex < _queue.Items.Count)
            {
                _mixerStream = 0;
                StartPlaybackOfCurrent(shouldResume);
                if (!shouldResume && position > 0) Seek(position);
            }

            _dispatcher.Invoke(() => SampleRateChanged?.Invoke(this, new SampleRateChangedEventArgs
            {
                PreferedSampleRate = CurrentSampleRate,
                EffectiveSampleRate = _audioEngine.CurrentOutputSampleRate
            }));
            EnqueueSave(SaveKind.Playback);
        }

        public void ChangeOutputMode(OutputMode outputMode)
        {
            if (outputMode == _currentOutputMode) return;

            bool shouldResume = IsPlaying;
            double position = CurrentPosition;
            Stop();
            int sr = CurrentSampleRate > 0 ? CurrentSampleRate : 44100;
            bool appliedPreferred = TryReinitializeOutput(CurrentDeviceIndex, sr, outputMode);
            if (!appliedPreferred && !TryReinitializeOutput(-1, 44100, OutputMode.WasapiShared))
            {
                return;
            }

            CurrentOutputMode = _audioEngine.GetCurrentOutputMode();
            if (!appliedPreferred)
            {
                CurrentDeviceIndex = -1;
                CurrentSampleRate = -1;
            }
            if (_queue.Items.Count > 0 && _queue.CurrentIndex >= 0 && _queue.CurrentIndex < _queue.Items.Count)
            {
                _mixerStream = 0;
                StartPlaybackOfCurrent(shouldResume);
                if (position > 0 && !shouldResume)
                {
                    Seek(position);
                }
            }

            _dispatcher.Invoke(() => OutputModeChanged?.Invoke(this, CurrentOutputMode));
            EnqueueSave(SaveKind.Playback);
        }

        public (int Index, string Name)[] GetAvailableDevices()
        {
            return [.. _audioEngine
                .GetOutputDevices()
                .Select(d => (d.Index, d.Info.name))];
        }

        public PlaybackQueueSnapshot BuildPlaybackSnapshot()
        {
            return new PlaybackQueueSnapshot
            {
                TrackIds = GetQueue(),
                CurrentIndex = CurrentQueueIndex,
                CurrentPlaybackIndex = CurrentPlaybackIndex,
                PlaybackOrderIndices = GetPlaybackOrder(),
                PositionInSeconds = CurrentPosition,
                PlaybackState = _playbackState
            };
        }

        public void LoadInitialState(UserSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            _isRestoringInitialState = true;
            try
            {
                ApplyPreferredAudioSettings(settings);
                Volume = Math.Clamp(settings.PreferredVolume, 0f, 1f);

                var startupBehavior = settings.StartupBehavior;
                if (startupBehavior == StartupBehavior.StartClean)
                {
                    _playbackRepository.Clear();
                    ClearQueue();
                    return;
                }

                var snapshot = _playbackRepository.Load();
                if (snapshot.TrackIds == null || snapshot.TrackIds.Length == 0)
                {
                    ClearQueue();
                    return;
                }

                var validIds = new HashSet<int>(_library.Tracks.Select(t => t.Id));
                var queue = snapshot.TrackIds.Where(validIds.Contains).ToArray();
                if (queue.Length == 0)
                {
                    _playbackRepository.Clear();
                    ClearQueue();
                    return;
                }

                SetQueue(queue, false);
                //SetShuffle(snapshot.IsShuffleEnabled);
                //if (snapshot.IsShuffleEnabled && snapshot.PlaybackOrderIndices is { Length: > 0 })
                //{
                //    SetPlaybackOrder(snapshot.PlaybackOrderIndices);
                //}
                //RepeatMode = (RepeatMode)snapshot.RepeatMode;
                //ContinueMode = (ContinueMode)snapshot.ContinueMode;

                int index = snapshot.CurrentIndex;
                if (index < 0 || index >= queue.Length)
                {
                    index = 0;
                }

                switch (startupBehavior)
                {
                    case StartupBehavior.RestoreQueueOnly:
                        Stop();
                        break;
                    case StartupBehavior.RestoreQueueAndIndexOnly:
                        SetSilentPlaybackIndex(snapshot.CurrentPlaybackIndex >= 0 ? snapshot.CurrentPlaybackIndex : index);
                        Stop();
                        break;
                    case StartupBehavior.ResumePlayback:
                        {
                            var playbackIndex = snapshot.CurrentPlaybackIndex >= 0
                                ? snapshot.CurrentPlaybackIndex
                                : index;
                            SetSilentPlaybackIndex(playbackIndex);
                            LoadPlaybackIndex(playbackIndex, autoplay: false);

                            _logger.LogDebug("ResumePlayback: index={Index}, playbackIndex={PlaybackIndex}, savedPos={SavedPos:0.000}s", index, playbackIndex, snapshot.PositionInSeconds);

                            var position = Math.Clamp(snapshot.PositionInSeconds, 0, Duration);
                            if (position > 0)
                            {
                                _pendingResumePositionSeconds = position;
                                Seek(position);
                            }

                            switch (snapshot.PlaybackState)
                            {
                                case PlaybackState.Playing:
                                    _logger.LogDebug("ResumePlayback: restoring state -> Play");
                                    Play();
                                    if (_pendingResumePositionSeconds.HasValue)
                                    {
                                        _ = ApplyPendingResumeSeekAsync("ResumePlayback:Play");
                                    }
                                    break;
                                case PlaybackState.Paused:
                                    _logger.LogDebug("ResumePlayback: restoring state -> Pause");
                                    Pause();
                                    break;
                                default:
                                    _logger.LogDebug("ResumePlayback: restoring state -> Stop");
                                    Stop();
                                    break;
                            }
                            break;
                        }
                    default:
                        Stop();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore initial player state, starting clean");
                ClearQueue();
            }
            finally
            {
                _isRestoringInitialState = false;
            }

            EnqueueSave(SaveKind.Playback);
        }

        private void ApplyPreferredAudioSettings(UserSettings settings)
        {
            int requestedDevice = settings.PreferredDeviceIndex;
            var availableDevices = GetAvailableDevices();
            bool requestedDeviceExists = requestedDevice < 0 || availableDevices.Any(d => d.Index == requestedDevice);
            int safeDevice = requestedDeviceExists ? requestedDevice : -1;

            var preferredMode = settings.PreferredOutputMode;
            int preferredSampleRate = (int)settings.PreferredSampleRate;
            int safeSampleRate = preferredSampleRate > 0 ? preferredSampleRate : 44100;

            bool appliedPreferred = TryReinitializeOutput(safeDevice, safeSampleRate, preferredMode);
            if (!appliedPreferred)
            {
                _logger.LogError("Failed to apply preferred audio settings, falling back to default output");
                if (!TryReinitializeOutput(-1, 44100, OutputMode.WasapiShared))
                {
                    _logger.LogCritical("Failed to initialize fallback audio output");
                }
            }

            CurrentDeviceIndex = appliedPreferred ? safeDevice : -1;
            CurrentOutputMode = _audioEngine.GetCurrentOutputMode();
            CurrentSampleRate = preferredSampleRate;
        }

        private bool TryReinitializeOutput(int deviceIndex, int sampleRate, OutputMode outputMode)
        {
            int initSampleRate = sampleRate > 0 ? sampleRate : 44100;
            var ok = _audioEngine.Reinitialize(deviceIndex, initSampleRate, outputMode);
            if (!ok)
            {
                var err = _audioEngine.GetLastError();
                _logger.LogWarning(
                    "Audio reinitialize failed for device {DeviceIndex}, sample rate {SampleRate}, mode {OutputMode}. Error: {ErrorCode}",
                    deviceIndex,
                    initSampleRate,
                    outputMode,
                    err);
            }

            return ok;
        }
        private void EnqueueSave(SaveKind kind)
        {
            if (!_isRestoringInitialState)
            {
                if (_saveCoordinator is null)
                {
                    _saveCoordinator = _serviceProvider.GetService(typeof(ISaveCoordinator)) as ISaveCoordinator;
                }
                _saveCoordinator?.Enqueue(kind);
            }
        }

        private void StartPlaybackOfCurrent(bool autoplay = true)
        {
            var item = _queue.CurrentItem;

            if (item == null)
            {
                _errorCount++;
                if (_errorCount >= MaxErrorCount) Stop();
                else Next();
                return;
            }

            var track = item.LibraryId.HasValue
                ? _library.Tracks.FirstOrDefault(t => t.Id == item.LibraryId.Value)
                : null;

            int requestedSampleRate = CurrentSampleRate > 0
                ? CurrentSampleRate
                : (track?.SampleRate ?? 44100);
            int requestedChannels = track?.Channels > 0 ? track.Channels : 2;

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

            if (_preloadedStream != 0 && _preloadedQueueItem != null && ReferenceEquals(_preloadedQueueItem, item))
            {
                _currentStream = _preloadedStream;
                _preloadedStream = 0;
                _preloadedQueueItem = null;
            }
            else
            {
                if (!_queueItemResolver.TryResolve(item, out var resolved))
                {
                    _errorCount++;
                    if (_errorCount >= MaxErrorCount) Stop();
                    else Next();
                    return;
                }

                int createdStream = resolved.Kind switch
                {
                    PlaybackSourceKind.LocalFile => _audioEngine.CreateDecodeStream(resolved.Input),
                    PlaybackSourceKind.RemoteUrl => _audioEngine.CreateDecodeStreamFromUrl(resolved.Input),
                    _ => 0
                };

                _currentStream = createdStream;
                if (_currentStream == 0)
                {
                    var err = _audioEngine.GetLastError();
                    _logger.LogWarning("Failed to create decode stream for queue item, error code: {ErrorCode}", err);
                    _errorCount++;
                    if (_errorCount >= MaxErrorCount)
                    {
                        _logger.LogError("Exceeded maximum error count of {MaxErrorCount}, stopping playback", MaxErrorCount);
                        Stop();
                        return;
                    }
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

            _audioEngine.SetVolume(_currentStream, _userSettings.PreferredVolume);
            _audioEngine.AddToMixer(_mixerStream, _currentStream, BASSFlag.BASS_MIXER_CHAN_NORAMPIN);
            _audioEngine.SetEndCallback(_currentStream, _endCallback, false);

            // Setup preload for next track
            double duration = _audioEngine.GetDuration(_currentStream);
            _currentTrackDuration = duration;
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
            var inmidiatePos = GetEffectivePosition();
            var mixerPos = _audioEngine.GetMixerPosition(_currentStream);
            var streamPos = _audioEngine.GetPosition(_currentStream);
            _logger.LogDebug("StartPlaybackOfCurrent: autoplay={Autoplay}, mixerPos={MixerPos:0.000}s, streamPos={StreamPos:0.000}s, effectivePos={EffectivePos:0.000}s",
                autoplay, mixerPos, streamPos, inmidiatePos);
            _dispatcher.Invoke(() => PositionChanged?.Invoke(this, inmidiatePos)); // update position immediately on track change

            var snapshot = CreateQueueSnapshot();
            _dispatcher.Invoke(() =>
            {
                SampleRateChanged?.Invoke(this, new SampleRateChangedEventArgs { PreferedSampleRate = CurrentSampleRate, EffectiveSampleRate = effectiveSampleRate });
                var trackRef = track != null
                    ? (string.IsNullOrWhiteSpace(track.FilePath) ? string.Empty : track.FilePath)
                    : (item.DisplayTitle ?? item.Source);
                TrackChanged?.Invoke(this, trackRef);
                QueueChanged?.Invoke(this, snapshot);
            });
            if (track != null)
            {
                BeginWaveformPipeline(track);
            }
            else
            {
                _currentWaveform = CreateFallbackWaveForm(WaveformDataPoints);
                _dispatcher.Invoke(() => WaveformDataChanged?.Invoke(this, _currentWaveform));
            }
            _errorCount = 0; // Reset error count on successful playback
            EnqueueSave(SaveKind.Playback);
        }

        private void SetPlaybackState(PlaybackState state)
        {
            if (_playbackState == state)
                return;

            _playbackState = state;

            _positionTimer.Interval = _playbackState == PlaybackState.Playing
                ? PositionTimerPlayingIntervalMs
                : PositionTimerIdleIntervalMs;

            _dispatcher.Invoke(() => PlaybackStateChanged?.Invoke(this, _playbackState));
        }

        private void PositionTimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsPlaying || _currentStream == 0)
                return;
            if (DateTime.UtcNow < _suppressPositionUntilUtc)
                return; // Suppress position updates for a short time after seeking to avoid UI jitter

            var position = GetEffectivePosition();
            if (_positionDebugRemaining > 0)
            {
                _positionDebugRemaining--;
                var mixerPos = _audioEngine.GetMixerPosition(_currentStream);
                var streamPos = _audioEngine.GetPosition(_currentStream);
                _logger.LogDebug("PositionTick: mixerPos={MixerPos:0.000}s, streamPos={StreamPos:0.000}s, effectivePos={EffectivePos:0.000}s",
                    mixerPos, streamPos, position);
            }
            _dispatcher.Invoke(() => PositionChanged?.Invoke(this, position));
        }

        private void OnTrackEndedInternal(int handle, int channel, int data, IntPtr user)
        {
            if (channel != _currentStream) return;

            _dispatcher.Invoke(() => TrackEnded?.Invoke(this, EventArgs.Empty));

            // Handle RepeatOne mode
            if (RepeatMode == RepeatMode.RepeatOne)
            {
                _audioEngine.SetPosition(_currentStream, 0.0);

                // Re-setup callbacks for the repeated track
                double duration = _audioEngine.GetDuration(_currentStream);
                _currentTrackDuration = duration;
                const double preloadLeadSeconds = 0.75;
                if (duration > preloadLeadSeconds)
                {
                    _audioEngine.SetPositionSync(_currentStream, duration - preloadLeadSeconds, _preloadSync);
                }

                return;
            }

            var nextItem = _queue.Next();
            if (nextItem == null)
            {
                Stop();
                return;
            }

            if (_preloadedStream != 0 && _preloadedQueueItem != null && ReferenceEquals(_preloadedQueueItem, nextItem))
            {
                int previousStream = _currentStream;
                _currentStream = _preloadedStream;
                _preloadedStream = 0;
                _preloadedQueueItem = null;

                _audioEngine.RemoveFromMixer(previousStream);
                FreeStream(previousStream);

                _audioEngine.SetVolume(_currentStream, _userSettings.PreferredVolume);
                _audioEngine.AddToMixer(_mixerStream, _currentStream, BASSFlag.BASS_MIXER_CHAN_NORAMPIN);
                _audioEngine.SetEndCallback(_currentStream, _endCallback, false);

                double duration = _audioEngine.GetDuration(_currentStream);
                const double preloadLeadSeconds = 0.75;
                if (duration > preloadLeadSeconds)
                {
                    _audioEngine.SetPositionSync(_currentStream, duration - preloadLeadSeconds, _preloadSync);
                }

                var snapshot = CreateQueueSnapshot();
                _dispatcher.Invoke(() =>
                {
                    var trackRef = nextItem.Source;
                    TrackChanged?.Invoke(this, trackRef);
                    QueueChanged?.Invoke(this, snapshot);
                });

                UpdateSelectedTrackState();
                return;
            }

            StartPlaybackOfCurrent();
        }

        private void OnPreloadSync(int handle, int channel, int data, IntPtr user)
        {
            // Don't preload if we're in RepeatTrack mode
            if (RepeatMode == RepeatMode.RepeatOne) return;

            Task.Run(() =>
            {
                var nextItem = _queue.PeekNext();
                if (nextItem == null) return;
                if (_preloadedQueueItem != null && ReferenceEquals(_preloadedQueueItem, nextItem)) return;

                if (_preloadedStream != 0)
                {
                    FreeStream(_preloadedStream);
                    _preloadedStream = 0;
                    _preloadedQueueItem = null;
                }

                if (!_queueItemResolver.TryResolve(nextItem, out var resolvedNext))
                {
                    _logger.LogWarning("Failed to resolve playback source for preload of queue item");
                    return;
                }

                int nextStream = resolvedNext.Kind switch
                {
                    PlaybackSourceKind.LocalFile => _audioEngine.CreateDecodeStream(resolvedNext.Input),
                    PlaybackSourceKind.RemoteUrl => _audioEngine.CreateDecodeStreamFromUrl(resolvedNext.Input),
                    _ => 0
                };

                if (nextStream == 0)
                {
                    var err = _audioEngine.GetLastError();
                    _logger.LogWarning("Failed to create decode stream for preload of queue item, error code: {ErrorCode}", err);
                    return;
                }

                _preloadedStream = nextStream;
                _preloadedQueueItem = nextItem;

                if (nextItem.LibraryId.HasValue)
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == nextItem.LibraryId.Value);
                    if (track != null)
                    {
                        _ = PreloadWaveformCacheAsync(track);
                    }
                }
            });
        }

        private void FreeStream(int streamHandle)
        {
            if (streamHandle != 0)
            {
                _audioEngine.Free(streamHandle);
            }
        }

        private int[] CreateQueueSnapshot()
        {
            return _queue.Items.Count == 0
                ? Array.Empty<int>()
                : _queue.Items.Select(item => item.LibraryId ?? 0).ToArray();
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
                var waveform = await AudioEngine.GetWaveFromDataAsync(track.FilePath, WaveformDataPoints);
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
            var waveform = await AudioEngine.GetWaveFromDataAsync(track.FilePath, WaveformDataPoints);
            if (waveform.Length == 0) return;
            CacheWaveform(track.Id, waveform);
        }
        private bool TryGetWaveformFromCache(int trackId, out float[]? data)
        {
            lock (_waveformCacheLock)
            {
                if (!_waveformCache.TryGetValue(trackId, out data))
                {
                    return false;
                }
                TouchWaveformCacheEntry(trackId);
                return true;
            }
        }
        private void CacheWaveform(int trackId, float[] data)
        {
            if (trackId <= 0 || data.Length == 0) return;

            lock (_waveformCacheLock)
            {
                _waveformCache[trackId] = data;
                if (!_waveformCacheNodes.ContainsKey(trackId))
                {
                    var node = _waveformCacheLru.AddLast(trackId);
                    _waveformCacheNodes[trackId] = node;
                }
                else
                {
                    TouchWaveformCacheEntry(trackId);
                }

                while (_waveformCache.Count > MaxWaveformCacheEntries)
                {
                    var oldest = _waveformCacheLru.First;
                    if (oldest is null) break;

                    int evictedTrackId = oldest.Value;
                    _waveformCacheLru.RemoveFirst();
                    _waveformCacheNodes.Remove(evictedTrackId);
                    _waveformCache.Remove(evictedTrackId);
                }
            }
        }
        private void TouchWaveformCacheEntry(int trackId)
        {
            if (!_waveformCacheNodes.TryGetValue(trackId, out var node))
            {
                node = _waveformCacheLru.AddLast(trackId);
                _waveformCacheNodes[trackId] = node;
                return;
            }

            if (!ReferenceEquals(_waveformCacheLru.Last, node))
            {
                _waveformCacheLru.Remove(node);
                _waveformCacheLru.AddLast(node);
            }
        }
        private void PublishWaveformIfCurrent(int trackId, int requestVersion, float[] data)
        {
            if (trackId <= 0) return;


            _dispatcher.Invoke(() =>
            {
                if (requestVersion != Volatile.Read(ref _waveformVersion)) return; // Stale request, ignore

                if (CurrentTrackId != trackId) return; // Not current track anymore, ignore

                _currentWaveform = data;
                WaveformDataChanged?.Invoke(this, data);

            });
        }
        private static float[] CreateFallbackWaveForm(int points = 2000)
        {
            var data = new float[points];
            Array.Fill(data, 1f);
            return data;
        }
        private async Task ApplyPendingResumeSeekAsync(string reason)
        {
            if (!_pendingResumePositionSeconds.HasValue || _currentStream == 0) return;

            var target = _pendingResumePositionSeconds.Value;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                await Task.Delay(150 * attempt).ConfigureAwait(false);
                if (_currentStream == 0) return;

                _audioEngine.SetPosition(_currentStream, target);
                var mixerPos = _audioEngine.GetMixerPosition(_currentStream);
                var streamPos = _audioEngine.GetPosition(_currentStream);
                _logger.LogDebug("ApplyPendingResumeSeek({Reason}) attempt {Attempt}: target={Target:0.000}s, mixerPos={MixerPos:0.000}s, streamPos={StreamPos:0.000}s",
                    reason, attempt, target, mixerPos, streamPos);

                if (Math.Abs(streamPos - target) <= 0.25 || Math.Abs(mixerPos - target) <= 0.25)
                {
                    _pendingResumePositionSeconds = null;
                    _dispatcher.Invoke(() => PositionChanged?.Invoke(this, target));
                    return;
                }
            }
        }
        private double GetEffectivePosition()
        {
            if (_currentStream == 0) return 0.0;
            var pos = _audioEngine.GetMixerPosition(_currentStream);
            if (pos > 0.0001) return pos;
            return _audioEngine.GetPosition(_currentStream);
        }
        private void NotifyQueueChanged()
        {
            var snapshot = CreateQueueSnapshot();
            _dispatcher.Invoke(() => QueueChanged?.Invoke(this, snapshot));
        }
        private void QueueOnQueueChanged(object? sender, EventArgs e)
        {
            NotifyQueueChanged();
        }
        private void QueueOnCurrentChanged(object? sender, EventArgs e)
        {
            UpdateSelectedTrackState();
        }
        private PlaybackQueueItem? CreateQueueItemFromTrackId(int trackId)
        {
            if (trackId <= 0) return null;
            var track = _library.Tracks.FirstOrDefault(t => t.Id == trackId);
            if (track == null) return null;
            return new PlaybackQueueItem
            {
                SourceType = QueueItemSourceType.LocalFile,
                Source = track.FilePath,
                DisplayTitle = track.Title,
                LibraryId = track.Id,
                ExternalId = track.ExternalId
            };
        }
        public void Dispose()
        {
            _positionTimer.Elapsed -= PositionTimerOnElapsed;
            _positionTimer.Stop();
            _positionTimer.Dispose();

            _queue.QueueChanged -= QueueOnQueueChanged;
            _queue.CurrentChanged -= QueueOnCurrentChanged;

            Stop();
            FreeStream(_mixerStream);

            lock (_waveformCacheLock)
            {
                _waveformCache.Clear();
                _waveformCacheNodes.Clear();
                _waveformCacheLru.Clear();
            }

            _audioEngine.Dispose();
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
