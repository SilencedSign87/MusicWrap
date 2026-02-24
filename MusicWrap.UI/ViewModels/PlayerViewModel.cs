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
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        private readonly IMusicPlayerService _playerService;
        private readonly MusicLibrary _library;

        [ObservableProperty]
        private bool isPlaying = false;

        [ObservableProperty]
        private bool isPaused = false;

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
            Process.Start(new ProcessStartInfo(ArtworkPath) { UseShellExecute = true});
        }

        public PlayerViewModel(IMusicPlayerService service, MusicLibrary library)
        {
            _playerService = service;
            _library = library;

            // Subscribe to player events
            _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
            _playerService.TrackChanged += OnTrackChanged;
            _playerService.PositionChanged += OnPositionChanged;

            // Initialize state
            UpdatePlaybackState(_playerService.IsPlaying);
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
            _isSeekingPosition = false;
            _playerService.Seek(position);
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
                UpdateCurrentTrackInfo();
                Duration = _playerService.Duration;
                FormattedDuration = FormatTime(Duration);
            });
        }

        private void OnPositionChanged(object? sender, double position)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (!_isSeekingPosition)
                {
                    CurrentPosition = position;
                    FormattedPosition = FormatTime(position);
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
                       275
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
    }
}
