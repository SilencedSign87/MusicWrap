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
using System.Reflection.Emit;

namespace MusicWrap.UI.ViewModels
{
    public partial class PlayerViewModel : ObservableObject, IDisposable
    {
        private bool _disposed = false;
        private readonly IMusicPlayerService _playerService;
        private readonly ILibraryService _libraryService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayPauseIcon))]
        private bool isPlaying = false;

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
        [NotifyPropertyChangedFor(nameof(ShuffleIcon))]
        private bool isShuffleEnabled = false;

        public string ShuffleIcon => IsShuffleEnabled ? "\xE8B1" : "\xE73C";

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
        
        //[ObservableProperty] 
        public string PlayPauseIcon => IsPlaying ? "\ue769" : "\ue768" ; 

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
           
            // Load initial states
            UpdateDJButtonIcon();
            UpdateRepeatModeIcon();

            UpdateCurrentTrackInfo();

            // Initialize state
            IsPlaying = _playerService.IsPlaying;
            IsShuffleEnabled = _playerService.IsShuffleEnabled;
        }

        private void _playerService_ShuffleStateChanged(object? sender, bool enabled)
        {
            Application.Current?.Dispatcher.Invoke(()=>
            {
                IsShuffleEnabled = enabled;
            });
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

        partial void OnIsShuffleEnabledChanged(bool value)
        {
            if (_playerService.IsShuffleEnabled == value) return;
            _playerService.SetShuffle(value);
        }
        private void UpdateDJButtonIcon()
        {
            IsDJOn = _playerService.ContinueMode == ContinueMode.DJEnd;
            DjButtonIcon = IsDJOn ? "\ue7f6" : "\ue738"; // DJ On : DJ Off
            DjTooltip = IsDJOn ? "DJ mode is ON" : "DJ mode is OFF";
        }
        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsPlaying = state == PlaybackState.Playing;
            });
        }

        private void OnTrackChanged(object? sender, string trackPath)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                SyncCurrentTrackStateFromPlayer();
            });
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playerService.TrackChanged -= OnTrackChanged;
            _playerService.ShuffleStateChanged -= _playerService_ShuffleStateChanged;
        }
    }
}


