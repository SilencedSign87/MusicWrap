using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Services.Lyrics;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;

using LyricsModel = MusicWrap.Data.Library.Models.Lyrics;

namespace MusicWrap.UI.Features.Lyrics.Viewmodel
{
    public partial class LyricsViewModel : ObservableObject, IDisposable
    {
        private readonly LyricsProviderService _provider;
        private readonly MusicPlayerService _player;
        private readonly IUIDispatcher _dispatcher;
        private readonly TrackActionService _trackActionService;

        // lyrics sync state
        private readonly DispatcherTimer _scheduler;
        private double _lastEnginePosition;
        private DateTime _lastEngineAtUtc = DateTime.MinValue;
        private double _duration;
        private bool _isPlaying;

        // Versioning state
        private bool _suppressSelection;
        private int _appliedSelectionId;
        private int _loadVersion;


        private bool _disposed;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(Lines))]
        public partial ParsedLyrics? Lyrics { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSeek))]
        public partial int ActiveIndex { get; set; } = -1;
        [ObservableProperty]
        public partial bool HasLyrics { get; set; }

        [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSeek))]
        public partial bool HasSyncedLyrics { get; set; }

        [ObservableProperty]
        public partial LyricsModel? SelectedLyrics { get; set; } = null;

        [ObservableProperty]
        public partial IReadOnlyList<LyricsModel> AvailableLyrics { get; set; } = [];

        public bool CanSeek => HasSyncedLyrics && Lyrics != null && Lyrics.Lines.Count > 0;

        public IReadOnlyList<LyricLine> Lines => Lyrics?.Lines ?? [];

        public LyricsViewModel(LyricsProviderService provider, MusicPlayerService player, TrackActionService trackActionService, IUIDispatcher dispatcher)
        {
            _provider = provider;
            _player = player;
            _dispatcher = dispatcher;
            _trackActionService = trackActionService;

            _scheduler = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _scheduler.Tick += OnSchedulerTick; ;
            _player.PositionChanged += OnPositionChanged;
            _player.TrackChanged += OnTrackChanged;
            _player.PlaybackStateChanged += OnPlaybackStateChanged;

            _isPlaying = _player.IsPlaying;
            _duration = _player.Duration;
            SyncBaseline();

            _ = LoadAsync();
        }
        public void SeekToIndex(int index)
        {
            if (Lyrics == null || !Lyrics.IsSynced) return;
            if ((uint)index >= (uint)Lyrics.Lines.Count) return;
            var target = Lyrics.Lines[index].Timestamp.TotalSeconds;
            _player.Seek(target);
            ActiveIndex = index;
        }

        #region Relay Commands
        [RelayCommand]
        private void SeekToLine(int index) => SeekToIndex(index);
        [RelayCommand]
        private async Task Refresh()
        {
            _provider.Invalidate();
            await LoadAsync();
        }
        [RelayCommand]
        private void OpenLyricsEditorForCurrent()
        {
            var currentTrack = _player.CurrentTrackId;
            if (currentTrack > 0)
            {
                _trackActionService.ShowTrackInformationDialog([currentTrack]);
            }
        }
        #endregion

        #region Internal

        private async Task LoadAsync()
        {
            var version = ++_loadVersion;
            var result = await _provider.GetCurrentAsync();

            _dispatcher.Invoke(() =>
            {
                if (version != _loadVersion || _disposed) return;

                Apply(result);
            });
        }

        private void OnTrackChanged(object? s, string e) => _dispatcher.Invoke(async () =>
        {
            _dispatcher.Invoke(() =>
            {
                _duration = _player.Duration;
                SyncBaseline();
                ScheduleNext();
            });
            _ = LoadAsync();
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
        private void Apply(TrackLyricsResult result)
        {
            _suppressSelection = true;
            try
            {
                // Update the available lyrics list, only if it has changed
                if (!AvailableLyrics.SequenceEqual(result.AvailableLyrics))
                {
                    Debug.WriteLine($"AvailableLyrics changed. Count: {result.AvailableLyrics.Count}");
                    AvailableLyrics = [.. result.AvailableLyrics];
                }

                // Update the selected lyrics
                _appliedSelectionId = result.ActiveLyrics?.Id ?? 0;
                SelectedLyrics = result.ActiveLyrics;

                // update the parsed lyrics
                var p = result.ParsedLyrics;
                Lyrics = p;
                HasLyrics = p.HasContent;
                HasSyncedLyrics = p.IsSynced;
                ActiveIndex = -1;
                SyncBaseline();
                UpdateActiveIndexImmediate();
                ScheduleNext();
            }
            finally
            {
                _suppressSelection = false;
            }
           
        }
        private void UpdateActiveIndexImmediate()
        {
            OnPropertyChanged(nameof(CanSeek));
            if (Lyrics == null || !Lyrics.IsSynced || Lyrics.Lines.Count == 0) return;
            var idx = LyricsParser.FindActiveIndex(Lyrics.Lines, TimeSpan.FromSeconds(PredictedPosition()));
            if (idx != ActiveIndex) ActiveIndex = idx;
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

        #endregion
        #region Partials
        partial void OnSelectedLyricsChanged(LyricsModel? value)
        {
            if (_suppressSelection) return;
            if (value is null || value.Id == _appliedSelectionId) return;

            _provider.SelectLyrics(_player.CurrentTrackId, value.Id);
            _ = LoadAsync();
        }
        #endregion  
        public void Dispose()
        {
            if (_disposed) return;
            _loadVersion++;
            _disposed = true;
            _scheduler.Stop(); _scheduler.Tick -= OnSchedulerTick; 
            _player.PositionChanged -= OnPositionChanged;
            _player.TrackChanged -= OnTrackChanged;
            _player.PlaybackStateChanged -= OnPlaybackStateChanged;
            //_messenger.UnregisterAll(this);
        }
    }
}
