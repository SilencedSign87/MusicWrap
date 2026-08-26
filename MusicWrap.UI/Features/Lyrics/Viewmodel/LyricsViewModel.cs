using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Lyrics;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Lyrics.Viewmodel
{
    public partial class LyricsViewModel : ObservableObject, IDisposable
    {
        private readonly LyricsProviderService _provider;
        private readonly IMusicPlayerService _player;
        private readonly IUIDispatcher _dispatcher;
        // lyrics sync state
        private readonly DispatcherTimer _scheduler;
        private double _lastEnginePosition;
        private DateTime _lastEngineAtUtc = DateTime.MinValue;
        private double _duration;
        private bool _isPlaying;

        private bool _disposed;

        [ObservableProperty] public partial ParsedLyrics? Lyrics { get; set; }
        [ObservableProperty] public partial int ActiveIndex { get; set; } = -1;
        [ObservableProperty] public partial bool HasLyrics { get; set; }
        [ObservableProperty] public partial bool HasSyncedLyrics { get; set; }

        public bool CanSeek => HasSyncedLyrics && Lyrics != null && Lyrics.Lines.Count > 0;

        public IReadOnlyList<LyricLine> Lines => Lyrics?.Lines ?? Array.Empty<LyricLine>();

        public LyricsViewModel(LyricsProviderService provider, IMusicPlayerService player, IUIDispatcher dispatcher)
        {
            _provider = provider;
            _player = player;
            _dispatcher = dispatcher;
            _scheduler = new DispatcherTimer { Interval = TimeSpan.FromMicroseconds(100) };
            _scheduler.Tick += OnSchedulerTick; ;
            _provider.LyricsChanged += OnLyricsChanged;
            _player.PositionChanged += OnPositionChanged;
            _player.TrackChanged += OnTrackChanged;
            _player.PlaybackStateChanged += OnPlaybackStateChanged;

            _isPlaying = _player.IsPlaying;
            _duration = _player.Duration;
            SyncBaseline();

            _ = LoadAsync();
        }
        private async Task LoadAsync()
        {
            var p = await _provider.GetCurrentAsync();
            _dispatcher.Invoke(() => Apply(p));
        }
        private void OnLyricsChanged(object? s, ParsedLyrics e) => _dispatcher.Invoke(() => Apply(e));
        private void OnTrackChanged(object? s, string e) => _dispatcher.Invoke(() =>
        {
            _duration = _player.Duration;
            SyncBaseline();
            ScheduleNext();
        });
        private void OnPositionChanged(object? s, double pos) => _dispatcher.Invoke(() =>
        {
            _lastEnginePosition = pos;
            _lastEngineAtUtc = DateTime.UtcNow;
            UpdateActiveIndexImmediate();
            ScheduleNext();
        });
        private void OnPlaybackStateChanged(object? s, ManagedBass.PlaybackState st)
        {
            _dispatcher.Invoke(() =>
            {
                _isPlaying = st == ManagedBass.PlaybackState.Playing;
                if (_isPlaying) SyncBaseline();
                ScheduleNext();
            });
        }
        private void SyncBaseline()
        {
            _lastEnginePosition = _player.CurrentPosition;
            _lastEngineAtUtc = DateTime.UtcNow;
            _duration = _player.Duration;
            _isPlaying = _player.IsPlaying;
        }
        private double PredictedPosition()
        {
            if (!_isPlaying || _lastEngineAtUtc == DateTime.MinValue) return _lastEnginePosition;
            var elapsed = (DateTime.UtcNow - _lastEngineAtUtc).TotalSeconds;
            return Math.Clamp(_lastEnginePosition + elapsed, 0, _duration > 0 ? _duration : double.MaxValue);
        }
        private void Apply(ParsedLyrics p)
        {
            Lyrics = p;
            HasLyrics = p.HasContent;
            HasSyncedLyrics = p.IsSynced;
            OnPropertyChanged(nameof(CanSeek));
            OnPropertyChanged(nameof(Lines));
            ActiveIndex = -1;
            SyncBaseline();
            UpdateActiveIndexImmediate();
            ScheduleNext();
        }
        private void UpdateActiveIndexImmediate()
        {
            OnPropertyChanged(nameof(CanSeek));
            if (Lyrics == null || !Lyrics.IsSynced || Lyrics.Lines.Count == 0) return;
            var idx = LyricsParser.FindActiveIndex(Lyrics.Lines, TimeSpan.FromSeconds(PredictedPosition()));
            if (idx != ActiveIndex) ActiveIndex = idx;
        }
        public void SeekToIndex(int index)
        {
            if (Lyrics == null || !Lyrics.IsSynced) return;
            if ((uint)index >= (uint)Lyrics.Lines.Count) return;
            var target = Lyrics.Lines[index].Timestamp.TotalSeconds;
            _player.Seek(target);
            ActiveIndex = index;
        }
        private void ScheduleNext()
        {
            _scheduler.Stop();
            if (!HasSyncedLyrics || !_isPlaying || Lyrics == null || Lyrics.Lines.Count == 0) return;
            var now = PredictedPosition();
            int nextIdx = ActiveIndex + 1;
            if (nextIdx < Lyrics.Lines.Count && Lyrics.Lines[nextIdx].Timestamp.TotalSeconds <= now)
            {
                UpdateActiveIndexImmediate();
                nextIdx = ActiveIndex + 1;
            }
            if (nextIdx >= Lyrics.Lines.Count) return;
            var delta = Lyrics.Lines[nextIdx].Timestamp.TotalSeconds - now;
            var interval = TimeSpan.FromSeconds(Math.Clamp(delta, 0.01, 5.0));
            _scheduler.Interval = interval;
            _scheduler.Start();
        }
        private void OnSchedulerTick(object? s, EventArgs e)
        {
            _scheduler.Stop();
            UpdateActiveIndexImmediate();
            ScheduleNext();
        }
        #region Relay Commands
        [RelayCommand]
        private void SeekToLine(int index) => SeekToIndex(index);
        [RelayCommand]
        private async Task Refresh()
        {
            _provider.Invalidate();
            var p = await _provider.GetCurrentAsync();
            Apply(p);
        }
        #endregion
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _scheduler.Stop(); _scheduler.Tick -= OnSchedulerTick;
            _provider.LyricsChanged -= OnLyricsChanged;
            _player.PositionChanged -= OnPositionChanged;
            _player.TrackChanged -= OnTrackChanged;
            _player.PlaybackStateChanged -= OnPlaybackStateChanged;
        }
    }
}
