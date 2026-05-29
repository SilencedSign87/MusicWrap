using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MusicWrap.UI.Services;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Library;

namespace MusicWrap.UI.ViewModels
{
    public partial class PlayerViewModel : ObservableObject, IDisposable
    {
        private bool _disposed = false;
        private readonly IMusicPlayerService _playerService;
        private readonly ILibraryService _libraryService;

        [ObservableProperty]
        private bool isPlaying = false;

        [ObservableProperty]
        private bool isPaused = false;

        [ObservableProperty]
        private bool isDJOn = false;
        [ObservableProperty]
        private string djButtonIcon = "";
        [ObservableProperty]
        private string djTooltip = "Toggle DJ mode";

        public RepeatMode SelectedRepeatMode => _playerService.RepeatMode;

        [ObservableProperty]
        private string repeatModeIcon = "";
        [ObservableProperty]
        private string repeatModeTooltip = "";

        [ObservableProperty]
        private bool isShuffleEnabled = false;

        [ObservableProperty]
        private string shuffleIcon = "\ue8b1";

        [ObservableProperty]
        private string shuffleTooltip = "Shuffle off";

        [ObservableProperty]
        private string currentTrackTitle = "No track playing";
        [ObservableProperty]
        private string currentTrackAlbum = "";
        [ObservableProperty]
        private string currentTrackArtists = "";
        [ObservableProperty]
        private string currentTrackYear = "";
        [ObservableProperty]
        private string currentTrackSampleRate = "";
        [ObservableProperty]
        private string currentTrackFormat = "";
        [ObservableProperty]
        private string currentTrackBitrate = "";
        [ObservableProperty]
        private string currentTrackBitDepth = "";
        [ObservableProperty]
        private string currentTrackChannels = "";
        [ObservableProperty]
        private string currentTrackImagePath = "";
        [ObservableProperty]
        private string? currentTrackDominantColorHex;
        [ObservableProperty]
        private string? currentTrackForegroundColorHex;

        //[ObservableProperty] private double currentPosition = 0;

        //[ObservableProperty] private double duration = 0;

        //[ObservableProperty] private float[] waveform;

        //[ObservableProperty] private string formattedPosition = "0:00";

        //[ObservableProperty] private string formattedDuration = "0:00";

        //[ObservableProperty] private float volume = 1.0f;

        [ObservableProperty] private string playPauseIcon = "\ue768"; // Play icon

        //[ObservableProperty] private bool isMuted = false;

        //private float previousVolume = 1.0f;

        //[ObservableProperty] private string muteButtonIcon = "\xE767"; // Volume on icon

        //private bool _isSeekingPosition = false;
        private string ArtworkPath = "";

        private readonly IImageService _imageService;

        public void OpenArtworkOnDefaultApp()
        {
            Process.Start(new ProcessStartInfo(ArtworkPath) { UseShellExecute = true });
        }

        public PlayerViewModel(IMusicPlayerService service, ILibraryService libraryService, IImageService imageService)
        {
            _playerService = service;
            _libraryService = libraryService;
            _imageService = imageService;


            // Subscribe to player events
            _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
            _playerService.TrackChanged += OnTrackChanged;
            _playerService.ShuffleStateChanged += _playerService_ShuffleStateChanged;
            //_playerService.WaveformDataChanged += _playerService_WaveformDataChanged;
            //_playerService.PositionChanged += OnPositionChanged;
            //_playerService.VolumeChanged += _playerService_VolumeChanged;

            // Load initial states
            UpdateDJButtonIcon();
            UpdateRepeatModeIcon();
            UpdateShuffleState();

            UpdateCurrentTrackInfo();
            //Waveform = _playerService.CurrentWaveformData;
            //Volume = _playerService.Volume;
            //RestorePlaybackState();

            //_uiPositionTmer = new DispatcherTimer(DispatcherPriority.Render)
            //{
            //    Interval = TimeSpan.FromMilliseconds(33)
            //};
            //_uiPositionTmer.Tick += UiPositionTimerOnTick;
            //_uiPositionTmer.Start();

            // Initialize state
            UpdatePlaybackState(_playerService.IsPlaying);
        }

        //private void RestorePlaybackState()
        //{
        //    var settings = App.Services.GetRequiredService<UserSettings>();
        //    if (settings == null)
        //    {
        //        SyncCurrentTrackStateFromPlayer();
        //        return;
        //    }

        //    _playerService.LoadInitialState();

        //    SyncCurrentTrackStateFromPlayer();
        //}

        private void _playerService_ShuffleStateChanged(object? sender, bool enabled)
        {
            Application.Current?.Dispatcher.Invoke(UpdateShuffleState);
        }

        //private void _playerService_WaveformDataChanged(object? sender, float[] e)
        //{
        //    Waveform = e.Length == 0 ? Array.Empty<float>() : [.. e];
        //}

        //[RelayCommand]
        //private void ToggleMute()
        //{
        //    if (IsMuted)
        //    {
        //        Volume = previousVolume;
        //        IsMuted = false;
        //        UpdateVolumeIcon(Volume);
        //    }
        //    else
        //    {
        //        previousVolume = Volume;
        //        Volume = 0;
        //        IsMuted = true;
        //        MuteButtonIcon = "\xE74F";
        //    }
        //}

        [RelayCommand]
        private void PlayPause()
        {
            if (IsPlaying)
            {
                _playerService.Pause();
            }
            else
            {
                _playerService.Play();
            }
        }

        [RelayCommand]
        private void Next()
        {
            _playerService.Next();
        }

        [RelayCommand]
        private void Previous()
        {
            _playerService.Previous();
        }

        //[RelayCommand]
        //private void Seek(double position)
        //{
        //    _playerService.Seek(position);
        //}

        //[RelayCommand]
        //private void StartSeeking()
        //{
        //    _isSeekingPosition = true;
        //}

        //[RelayCommand]
        //private void EndSeeking(double position)
        //{
        //    var target = Math.Clamp(position, 0, Duration > 0 ? Duration : position);

        //    _lastEnginePosition = target;
        //    _lastEnginePositionAtUTC = DateTime.UtcNow;

        //    CurrentPosition = target;
        //    UpdateFormattedPosition(target);

        //    _isSeekingPosition = false;

        //    _playerService.Seek(target);
        //}

        //[RelayCommand]
        //private void CancelSeeking()
        //{

        //    double enginePosition = _playerService.CurrentPosition;
        //    double target = Math.Clamp(enginePosition, 0, Duration > 0 ? Duration : enginePosition);

        //    _lastEnginePosition = target;
        //    _lastEnginePositionAtUTC = DateTime.UtcNow;

        //    CurrentPosition = target;
        //    UpdateFormattedPosition(target);

        //    _isSeekingPosition = false;
        //}

        [RelayCommand]
        private void CicleRepeatMode()
        {
            _playerService.RepeatMode = SelectedRepeatMode switch
            {
                RepeatMode.None => RepeatMode.RepeatOne,
                RepeatMode.RepeatOne => RepeatMode.RepeatAll,
                RepeatMode.RepeatAll => RepeatMode.None,
                _ => RepeatMode.None
            };
            UpdateRepeatModeIcon();
        }

        [RelayCommand]
        private void ToggleShuffle()
        {
            _playerService.ToggleShuffle();
            UpdateShuffleState();
        }
        [RelayCommand]
        private void ToggleDJMode()
        {
            _playerService.ContinueMode = IsDJOn ? ContinueMode.None : ContinueMode.DJEnd;
            UpdateDJButtonIcon();
        }

        private void UpdateRepeatModeIcon()
        {
            switch (SelectedRepeatMode)
            {
                case RepeatMode.None:
                    RepeatModeIcon = "\uebe7"; // No repeat
                    RepeatModeTooltip = "No repeat";
                    break;
                case RepeatMode.RepeatOne:
                    RepeatModeIcon = "\ue8ed"; // Repeat one
                    RepeatModeTooltip = "Repeat current track";
                    break;
                case RepeatMode.RepeatAll:
                    RepeatModeIcon = "\ue8ee"; // Repeat all
                    RepeatModeTooltip = "Repeat entire queue";
                    break;
            }
            OnPropertyChanged(nameof(SelectedRepeatMode));
        }

        private void UpdateShuffleState()
        {
            IsShuffleEnabled = _playerService.IsShuffleEnabled;
            ShuffleIcon = IsShuffleEnabled ? "\xE8B1" : "\xE73C";
            ShuffleTooltip = IsShuffleEnabled ? "Shuffle on" : "Shuffle off";
        }

        partial void OnIsShuffleEnabledChanged(bool value)
        {
            if (_playerService.IsShuffleEnabled == value) return;
            _playerService.SetShuffle(value);
            UpdateShuffleState();
        }
        private void UpdateDJButtonIcon()
        {
            IsDJOn = _playerService.ContinueMode == ContinueMode.DJEnd;
            DjButtonIcon = IsDJOn ? "\ue7f6" : "\ue738"; // DJ On : DJ Off
            DjTooltip = IsDJOn ? "DJ mode is ON" : "DJ mode is OFF";
        }
        //partial void OnVolumeChanged(float value)
        //{
        //    if (IsMuted && value > 0)
        //    {
        //        IsMuted = false;
        //    }
        //    if (!IsMuted)
        //    {
        //        UpdateVolumeIcon(value);
        //    }
        //    if (Math.Abs(_playerService.Volume - value) > 0.0001f)
        //        _playerService.SetVolume(value);
        //    //_playerService.SetVolume(value);
        //}
        //private void UpdateVolumeIcon(float value)
        //{
        //    switch (value)
        //    {
        //        case 0:
        //            MuteButtonIcon = "\xE992";
        //            break;
        //        case < 0.35f:
        //            MuteButtonIcon = "\xE993";
        //            break;
        //        case < 0.75f:
        //            MuteButtonIcon = "\xE994";
        //            break;
        //        default:
        //            MuteButtonIcon = "\xE767";
        //            break;
        //    }
        //}

        //private void SyncPredictionBaselineToEngine()
        //{
        //    _lastEnginePosition = _playerService.CurrentPosition;
        //    _lastEnginePositionAtUTC = DateTime.UtcNow;
        //}
        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsPlaying = state == PlaybackState.Playing;
                IsPaused = state == PlaybackState.Paused;
                UpdatePlaybackState(IsPlaying);

                //SyncPredictionBaselineToEngine();
            });
        }

        private void OnTrackChanged(object? sender, string trackPath)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                SyncCurrentTrackStateFromPlayer();

                //CurrentPosition = 0;
                //_lastEnginePosition = 0;
                //_lastEnginePositionAtUTC = DateTime.UtcNow;
                //UpdateFormattedPosition(0);
                //Duration = _playerService.Duration;
                //FormattedDuration = FormatTime(Duration);
            });
        }

        //private void OnPositionChanged(object? sender, double position)
        //{
        //    Application.Current?.Dispatcher.Invoke(() =>
        //    {
        //        _lastEnginePosition = position;
        //        _lastEnginePositionAtUTC = DateTime.UtcNow;

        //        if (!_isSeekingPosition)
        //        {
        //            CurrentPosition = position;
        //            //FormattedPosition = FormatTime(position);
        //            UpdateFormattedPosition(position);
        //        }
        //    });
        //}

        private void UpdatePlaybackState(bool playing)
        {
            PlayPauseIcon = playing ? "\ue769" : "\ue768"; // Pause : Play
        }

        private void UpdateCurrentTrackInfo()
        {
            ClearCurrentTrackInfo();

            var currentIndex = _playerService.CurrentIndex;
            if (currentIndex < 0)
            {
                CurrentTrackTitle = "No track playing";
                return;
            }

            var trackId = _playerService.CurrentTrackId;
            if (trackId == 0)
            {
                CurrentTrackTitle = "Unknown track";
                CurrentTrackDominantColorHex = "#808080";
                CurrentTrackForegroundColorHex = "#FFFFFF";
                return;
            }

            var track = _libraryService.GetTrackById(trackId);
            CurrentTrackSampleRate = track != null ? $"{track.SamplingRate / 1000.0:F1} kHz" : "";
            CurrentTrackFormat = track != null ? Path.GetExtension(track.Path).TrimStart('.').ToUpper() : "";
            CurrentTrackBitrate = track != null ? $"{track.Bitrate} kbps" : "";
            CurrentTrackBitDepth = track != null ? $"{track.BitDeph} bit" : "";
            CurrentTrackChannels = track != null ? $"{track.Channels} channel{(track.Channels > 1 ? "s" : "")}" : "";

            if (track == null)
            {
                CurrentTrackTitle = "Unknown track";
                CurrentTrackDominantColorHex = "#808080";
                CurrentTrackForegroundColorHex = "#FFFFFF";
                return;
            }

            CurrentTrackTitle = track.Title;

            // Get Album
            var album = _libraryService.GetAlbumById(track.AlbumId);
            CurrentTrackAlbum = album != null ? album.Title : "Unknown Album";
            CurrentTrackYear = album != null ? album.Year.ToString() : "?";

            // Get artists
            var artists = _libraryService.GetArtistNamesByIds(track.ArtistIds);

            CurrentTrackArtists = string.Join(", ", artists);

            // Get cover
            int coverId = track.CoverId;

            if (coverId == 0)
            {
                if (album != null)
                {
                    coverId = album.CoverId;
                }
            }

            if (coverId > 0)
            {
                var coverAsset = _libraryService.GetCoverAsset(coverId);
                if (coverAsset != null)
                {
                    CurrentTrackImagePath = coverAsset.FileName;
                    ArtworkPath = _imageService.ResolvePath(coverAsset.FileName, ImageVariant.Original) ?? string.Empty;
                    CurrentTrackDominantColorHex = coverAsset.DominantColorHex;
                    CurrentTrackForegroundColorHex = coverAsset.ForegroundColorHex;
                }
                else
                {
                    CurrentTrackImagePath = string.Empty;
                    ArtworkPath = string.Empty;
                    CurrentTrackDominantColorHex = "#808080";
                    CurrentTrackForegroundColorHex = "#FFFFFF";
                }
            }
            else
            {
                CurrentTrackDominantColorHex = "#808080";
                CurrentTrackForegroundColorHex = "#FFFFFF";
            }
        }

        private void SyncCurrentTrackStateFromPlayer()
        {
            UpdateCurrentTrackInfo();

            if (_playerService.CurrentIndex <= 0)
            {
                UpdatePlaybackState(false);
            }

            //var currentIndex = _playerService.CurrentIndex;
            //if (currentIndex < 0)
            //{
            //    CurrentPosition = 0;
            //    Duration = 0;
            //    FormattedPosition = "0:00";
            //    FormattedDuration = "0:00";
            //    UpdatePlaybackState(false);
            //    return;
            //}

            //Duration = _playerService.Duration;
            //FormattedDuration = FormatTime(Duration);

            //var position = Math.Clamp(_playerService.CurrentPosition, 0, Duration > 0 ? Duration : _playerService.CurrentPosition);
            //CurrentPosition = position;
            //UpdateFormattedPosition(position);
            //_lastEnginePosition = position;
            //_lastEnginePositionAtUTC = DateTime.UtcNow;
            //_isSeekingPosition = false;
        }

        private void ClearCurrentTrackInfo()
        {
            CurrentTrackTitle = string.Empty;
            CurrentTrackAlbum = string.Empty;
            CurrentTrackArtists = string.Empty;
            CurrentTrackYear = string.Empty;
            CurrentTrackSampleRate = string.Empty;
            CurrentTrackFormat = string.Empty;
            CurrentTrackBitrate = string.Empty;
            CurrentTrackBitDepth = string.Empty;
            CurrentTrackChannels = string.Empty;
            CurrentTrackImagePath = string.Empty;
            CurrentTrackDominantColorHex = "#808080";
            CurrentTrackForegroundColorHex = "#FFFFFF";
            ArtworkPath = string.Empty;
        }

        //private static string FormatTime(double seconds)
        //{
        //    var time = TimeSpan.FromSeconds(seconds);
        //    if (time.TotalHours >= 1)
        //    {
        //        return time.ToString(@"h\:mm\:ss");
        //    }
        //    return time.ToString(@"m\:ss");
        //}

        //private void UiPositionTimerOnTick(object? sender, EventArgs e)
        //{
        //    if (_isSeekingPosition || Duration <= 0) return;

        //    if (!IsPlaying || _playerService.IsPaused || !_playerService.IsPlaying)
        //    {
        //        return;
        //    }

        //    var elapsed = (DateTime.UtcNow - _lastEnginePositionAtUTC).TotalSeconds;
        //    var predicted = _lastEnginePosition + elapsed;
        //    if (predicted < 0) predicted = 0;
        //    if (predicted > Duration) predicted = Duration;

        //    if (Math.Abs(predicted - CurrentPosition) >= 0.01)
        //    {
        //        CurrentPosition = predicted;
        //        UpdateFormattedPosition(predicted);
        //    }
        //}
        //private void UpdateFormattedPosition(double position)
        //{
        //    var time = TimeSpan.FromSeconds(position);
        //    if (time.TotalHours >= 1)
        //    {
        //        FormattedPosition = time.ToString(@"h\:mm\:ss");
        //    }
        //    else
        //    {
        //        FormattedPosition = time.ToString(@"m\:ss");
        //    }
        //}
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            //_uiPositionTmer.Stop();
            //_uiPositionTmer.Tick -= UiPositionTimerOnTick;

            _playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playerService.TrackChanged -= OnTrackChanged;
            //_playerService.PositionChanged -= OnPositionChanged;
            //_playerService.WaveformDataChanged -= _playerService_WaveformDataChanged;
            _playerService.ShuffleStateChanged -= _playerService_ShuffleStateChanged;

            //Waveform = Array.Empty<float>();
        }
    }
}


