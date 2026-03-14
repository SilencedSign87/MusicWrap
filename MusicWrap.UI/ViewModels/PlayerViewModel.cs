using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core;
using MusicWrap.Data;
using MusicWrap.Data.Library;
using MusicWrap.UI.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using TagLib.IFD;

namespace MusicWrap.UI.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        private readonly IMusicPlayerService _playerService;
        private readonly MusicLibrary _library;

        private readonly DispatcherTimer _uiPositionTmer;
        private double _lastEnginePosition = 0;
        private DateTime _lastEnginePositionAtUTC = DateTime.UtcNow;

        private double? _pendingSeekTarget = null;
        private DateTime _pendingSeekUntilUtc = DateTime.MinValue;

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

        [ObservableProperty]
        private RepeatMode selectedRepeatMode = RepeatMode.None;
        [ObservableProperty]
        private string repeatModeIcon = "";
        [ObservableProperty]
        private string repeatModeTooltip = "";

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
        private BitmapImage? currentTrackImage;
        [ObservableProperty]
        private string? currentTrackDominantColorHex;
        [ObservableProperty]
        private string? currentTrackForegroundColorHex;

        [ObservableProperty]
        private double currentPosition = 0;

        [ObservableProperty]
        private double duration = 0;

        [ObservableProperty] private float[] waveform;

        [ObservableProperty]
        private string formattedPosition = "0:00";

        [ObservableProperty]
        private string formattedDuration = "0:00";

        [ObservableProperty]
        private float volume = 1.0f;

        [ObservableProperty]
        private string playPauseIcon = "\ue768"; // Play icon

        private bool _isSeekingPosition = false;
        private string ArtworkPath = "";

        private static readonly string CoversBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicWrap",
            "covers"
        );

        public void OpenArtworkOnDefaultApp()
        {
            Process.Start(new ProcessStartInfo(ArtworkPath) { UseShellExecute = true });
        }

        public PlayerViewModel(IMusicPlayerService service, MusicLibrary library)
        {
            _playerService = service;
            _library = library;

            // Subscribe to player events
            _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
            _playerService.TrackChanged += OnTrackChanged;
            _playerService.PositionChanged += OnPositionChanged;
            _playerService.WaveformDataChanged += _playerService_WaveformDataChanged;
            Waveform = [];

            // Load initial states
            UpdateDJButtonIcon();
            UpdateRepeatModeIcon();

            _uiPositionTmer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _uiPositionTmer.Tick += UiPositionTimerOnTick;
            _uiPositionTmer.Start();

            // Initialize state
            UpdatePlaybackState(_playerService.IsPlaying);
        }

        private void _playerService_WaveformDataChanged(object? sender, float[] e)
        {
            Waveform = e.Length == 0 ? Array.Empty<float>() : [..e];
        }

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

        [RelayCommand]
        private void Seek(double position)
        {
            _playerService.Seek(position);
        }

        [RelayCommand]
        private void StartSeeking()
        {
            _isSeekingPosition = true;
        }

        [RelayCommand]
        private void EndSeeking(double position)
        {
            _playerService.Seek(position);

            _lastEnginePosition = position;
            _lastEnginePositionAtUTC = DateTime.UtcNow;

            _pendingSeekTarget = position;
            _pendingSeekUntilUtc = DateTime.UtcNow.AddMilliseconds(200);

            CurrentPosition = position;
            UpdateFormattedPosition(position);
            //FormattedPosition = FormatTime(position);
            _isSeekingPosition = false;
        }
        [RelayCommand]
        private void CicleRepeatMode()
        {
            _playerService.RepeatMode = SelectedRepeatMode switch
            {
                RepeatMode.None => RepeatMode.RepeatTrack,
                RepeatMode.RepeatTrack => RepeatMode.RepeatQueue,
                RepeatMode.RepeatQueue => RepeatMode.None,
                _ => RepeatMode.None
            };
            UpdateRepeatModeIcon();
        }
        [RelayCommand]
        private void ToggleDJMode()
        {
            _playerService.ContinueMode = IsDJOn ? ContinueMode.None : ContinueMode.DJEnd;
            UpdateDJButtonIcon();
        }

        private void UpdateRepeatModeIcon()
        {
            SelectedRepeatMode = _playerService.RepeatMode;
            switch (SelectedRepeatMode)
            {
                case RepeatMode.None:
                    RepeatModeIcon = "\uebe7"; // No repeat
                    RepeatModeTooltip = "No repeat";
                    break;
                case RepeatMode.RepeatTrack:
                    RepeatModeIcon = "\ue8ed"; // Repeat one
                    RepeatModeTooltip = "Repeat current track";
                    break;
                case RepeatMode.RepeatQueue:
                    RepeatModeIcon = "\ue8ee"; // Repeat all
                    RepeatModeTooltip = "Repeat entire queue";
                    break;
            }
        }
        private void UpdateDJButtonIcon()
        {
            IsDJOn = _playerService.ContinueMode == ContinueMode.DJEnd;
            DjButtonIcon = IsDJOn ? "\ue7f6" : "\ue738"; // DJ On : DJ Off
            DjTooltip = IsDJOn ? "DJ mode is ON" : "DJ mode is OFF";
        }
        partial void OnVolumeChanged(float value)
        {
            _playerService.SetVolume(value);
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsPlaying = state == PlaybackState.Playing;
                IsPaused = state == PlaybackState.Paused;
                UpdatePlaybackState(IsPlaying);
            });
        }

        private void OnTrackChanged(object? sender, string trackPath)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                CurrentPosition = 0;
                _lastEnginePosition = 0;
                _lastEnginePositionAtUTC = DateTime.UtcNow;
                UpdateFormattedPosition(0);

                UpdateCurrentTrackInfo();
                Duration = _playerService.Duration;
                FormattedDuration = FormatTime(Duration);
            });
        }

        private void OnPositionChanged(object? sender, double position)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_pendingSeekTarget.HasValue)
                {
                    if (DateTime.UtcNow<= _pendingSeekUntilUtc && Math.Abs(position-_pendingSeekTarget.Value) > 0.15)
                    {
                        return; // we already have a seek in progress
                    }
                    _pendingSeekTarget = null;
                }

                _lastEnginePosition = position;
                _lastEnginePositionAtUTC = DateTime.UtcNow;

                if (!_isSeekingPosition)
                {
                    CurrentPosition = position;
                    //FormattedPosition = FormatTime(position);
                    UpdateFormattedPosition(position);
                }
            });
        }

        private void UpdatePlaybackState(bool playing)
        {
            PlayPauseIcon = playing ? "\ue769" : "\ue768"; // Pause : Play
        }

        private void UpdateCurrentTrackInfo()
        {
            var trackId = _playerService.CurrentTrackId;
            if (trackId == 0)
            {
                CurrentTrackTitle = "No track playing";
                CurrentTrackArtists = "";
                CurrentTrackImage = ImageHelper.GetDefaultAlbumImage(275);
                return;
            }

            var track = _library.Tracks.FirstOrDefault(t => t.Id == trackId);
            CurrentTrackSampleRate = track != null ? $"{track.SamplingRate / 1000.0:F1} kHz" : "";
            CurrentTrackFormat = track != null ? Path.GetExtension(track.Path).TrimStart('.').ToUpper() : "";
            CurrentTrackBitrate = track != null ? $"{track.Bitrate} kbps" : "";
            CurrentTrackBitDepth = track != null ? $"{track.BitDeph} bit" : "";
            CurrentTrackChannels = track != null ? $"{track.Channels} channel{(track.Channels > 1 ? "s" : "")}" : "";

            if (track == null)
            {
                CurrentTrackTitle = "Unknown track";
                CurrentTrackArtists = "";
                CurrentTrackImage = ImageHelper.GetDefaultAlbumImage(275);
                return;
            }

            CurrentTrackTitle = track.Title;

            // Get Album
            var album = _library.Albums.FirstOrDefault(a => a.Id == track.AlbumId);
            CurrentTrackAlbum = album != null ? album.Title : "Unknown Album";
            CurrentTrackYear = album != null ? album.Year.ToString() : "?";

            // Get artists
            var artists = _library.Artists
                .Where(a => track.ArtistIds.Contains(a.Id))
                .Select(a => a.Name);
            CurrentTrackArtists = string.Join(", ", artists);

            // Get cover
            int coverId = track.CoverId;
            string? coverPath = null;

            if (coverId == 0)
            {
                if (album != null)
                {
                    coverId = album.CoverId;
                }
            }

            if (coverId > 0)
            {
                var coverAsset = _library.CoverAssets.FirstOrDefault(c => c.Id == coverId);
                if (coverAsset != null)
                {
                    coverPath = Path.Combine(CoversBasePath, coverAsset.FileName);
                    ArtworkPath = coverPath;
                    CurrentTrackDominantColorHex = coverAsset.DominantColorHex;
                    CurrentTrackForegroundColorHex = coverAsset.ForegroundColorHex;
                }
            }
            else
            {
                CurrentTrackDominantColorHex = null;
                CurrentTrackForegroundColorHex = null;
            }
            CurrentTrackImage = ImageHelper.LoadThumbnail(
                       coverPath,
                       "album",
                       300
                   )!;
        }

        private static string FormatTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            if (time.TotalHours >= 1)
            {
                return time.ToString(@"h\:mm\:ss");
            }
            return time.ToString(@"m\:ss");
        }

        private void UiPositionTimerOnTick(object? sender, EventArgs e)
        {
            if (!IsPlaying || _isSeekingPosition || Duration <= 0) return;

            var elapsed = (DateTime.UtcNow - _lastEnginePositionAtUTC).TotalSeconds;
            var predicted = _lastEnginePosition + elapsed;
            if (predicted < 0) predicted = 0;
            if (predicted > Duration) predicted = Duration;

            if (Math.Abs(predicted - CurrentPosition ) >= 0.01)
            {
                CurrentPosition = predicted;
                UpdateFormattedPosition(predicted);
            }
        }
        private void UpdateFormattedPosition(double position)
        {
            var time = TimeSpan.FromSeconds(position);
            if (time.TotalHours >= 1)
            {
                FormattedPosition = time.ToString(@"h\:mm\:ss");
            }
            else
            {
                FormattedPosition = time.ToString(@"m\:ss");
            }
        }
    }
}
