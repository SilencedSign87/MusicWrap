
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
        None,
        RepeatTrack,
        RepeatQueue
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

        RepeatMode RepeatMode { get; set; }

        event EventHandler<string>? TrackChanged;
        event EventHandler? TrackEnded;
        event EventHandler<PlaybackState>? PlaybackStateChanged;
        event EventHandler<double>? PositionChanged;
        event EventHandler<int[]>? QueueChanged;
        void Play();
        void Pause();
        void Stop();
        void Next();
        void Previous();
        void Seek(double seconds);
        void SetVolume(float volume);
        void PlayIndex(int index);

        void AddToQueue(int TrackId);
        void AddToQueue(IEnumerable<int> TrackIds);
        void SetQueue(IEnumerable<int> TrackIds);
        void RemoveFromQueue(int index);
        void ClearQueue();
        int[] GetQueue();
        void PlayTrack(int TrackId);

        void ChangeOutputDevice(int deviceIndex);
        (int Index, string Name)[] GetAvailableDevices();
    }
    public class MusicPlayerService : IMusicPlayerService, IDisposable
    {
        private readonly MusicLibrary _library;
        private readonly AudioEngine _audioEngine;

        private readonly List<int> _queue = [];
        private int _currentIndex = -1;
        private int _currentStream;
        private PlaybackState _playbackState = PlaybackState.Stopped;
        private float _volume = 1.0f;
        private readonly SYNCPROC _endCallback;
        private readonly System.Timers.Timer _positionTimer;

        public bool IsPlaying => _playbackState == PlaybackState.Playing;
        public bool IsPaused => _playbackState == PlaybackState.Paused;
        public double CurrentPosition => _currentStream != 0 ? _audioEngine.GetPosition(_currentStream) : 0.0;
        public double Duration => _currentStream != 0 ? _audioEngine.GetDuration(_currentStream) : 0.0;

        public float Volume
        {
            get => _currentStream != 0 ? _audioEngine.GetVolume(_currentStream) : _volume;
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

        public MusicPlayerService(MusicLibrary library)
        {
            _library = library;

            _audioEngine = new AudioEngine();
            _audioEngine.Initialize();

            _endCallback = OnTrackEndedInternal;

            _positionTimer = new System.Timers.Timer(500);
            _positionTimer.AutoReset = true;
            _positionTimer.Elapsed += PositionTimerOnElapsed;
            _positionTimer.Start();
        }

        public event EventHandler<string>? TrackChanged;
        public event EventHandler? TrackEnded;
        public event EventHandler<PlaybackState>? PlaybackStateChanged;
        public event EventHandler<double>? PositionChanged;
        public event EventHandler<int[]>? QueueChanged;

        public void Play()
        {
            if (_currentStream == 0)
            {
                if (_queue.Count == 0)
                    return;

                if (_currentIndex < 0 || _currentIndex >= _queue.Count)
                    _currentIndex = 0;

                StartPlaybackOfCurrent();
                return;
            }

            _audioEngine.Play(_currentStream, false);
            SetPlaybackState(PlaybackState.Playing);
        }

        public void Pause()
        {
            if (_currentStream == 0)
                return;

            _audioEngine.Pause(_currentStream);
            SetPlaybackState(PlaybackState.Paused);
        }

        public void Stop()
        {
            if (_currentStream != 0)
            {
                _audioEngine.Stop(_currentStream);
                _audioEngine.Free(_currentStream);
                _currentStream = 0;
            }

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

        public void AddToQueue(int TrackId)
        {
            _queue.Add(TrackId);
            QueueChanged?.Invoke(this, [.. _queue]);
        }

        public void AddToQueue(IEnumerable<int> TrackIds)
        {
            _queue.AddRange(TrackIds);
            QueueChanged?.Invoke(this, [.. _queue]);
        }

        public void SetQueue(IEnumerable<int> TrackIds)
        {
            _queue.Clear();
            _queue.AddRange(TrackIds);
            _currentIndex = _queue.Count > 0 ? 0 : -1;
            QueueChanged?.Invoke(this, [.. _queue]);
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
            QueueChanged?.Invoke(this, [.. _queue]);
        }

        public void ClearQueue()
        {
            Stop();
            _queue.Clear();
            _currentIndex = -1;
            QueueChanged?.Invoke(this, [.. _queue]);
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
            // Detenemos y liberamos el stream actual antes de cambiar de dispositivo
            if (_currentStream != 0)
            {
                _audioEngine.Stop(_currentStream);
                _audioEngine.Free(_currentStream);
                _currentStream = 0;
            }

            _audioEngine.ChangeOutputDevice(deviceIndex);

            // Opcionalmente reanudamos la reproducción del track actual en el nuevo dispositivo
            if (_queue.Count > 0 && _currentIndex >= 0 && _currentIndex < _queue.Count)
            {
                StartPlaybackOfCurrent();
            }
        }

        public (int Index, string Name)[] GetAvailableDevices()
        {
            return _audioEngine
                .GetOutputDevices()
                .Select(d => (d.Index, d.Info.name))
                .ToArray();
        }

        public void Dispose()
        {
            _positionTimer.Elapsed -= PositionTimerOnElapsed;
            _positionTimer.Stop();
            _positionTimer.Dispose();

            Stop();
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

            if (_currentStream != 0)
            {
                _audioEngine.Stop(_currentStream);
                _audioEngine.Free(_currentStream);
                _currentStream = 0;
            }

            _currentStream = _audioEngine.CreateStream(track.Path);
            _audioEngine.SetVolume(_currentStream, _volume);
            _audioEngine.SetEndCallback(_currentStream, _endCallback);
            _audioEngine.Play(_currentStream, false);

            SetPlaybackState(PlaybackState.Playing);

            TrackChanged?.Invoke(this, track.Path);
            QueueChanged?.Invoke(this, [.. _queue]);
        }

        private void SetPlaybackState(PlaybackState state)
        {
            if (_playbackState == state)
                return;

            _playbackState = state;
            PlaybackStateChanged?.Invoke(this, _playbackState);
        }

        private void PositionTimerOnElapsed(object? sender, ElapsedEventArgs e)
        {
            if (!IsPlaying || _currentStream == 0)
                return;

            var position = _audioEngine.GetPosition(_currentStream);
            PositionChanged?.Invoke(this, position);
        }

        private void OnTrackEndedInternal(int handle, int channel, int data, IntPtr user)
        {
            // Se llama desde el callback de BASS
            TrackEnded?.Invoke(this, EventArgs.Empty);

            if (RepeatMode == RepeatMode.RepeatTrack)
            {
                // Repetir el mismo track
                StartPlaybackOfCurrent();
            }
            else
            {
                Next();
            }
        }
    }
    
    public class QueuedTrack
    {
        public int TrackId { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
