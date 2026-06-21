using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using MusicWrap.UI.Services;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Helpers;

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
        private string currentTrackArtists = "";
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
                CurrentTrackTitle = AppStringPool.Intern("No track playing") ?? "No track playing";
                return;
            }

            var trackId = _playerService.CurrentTrackId;
            if (trackId == 0)
            {
                CurrentTrackTitle = AppStringPool.Intern("Unknown track") ?? "Unknown track";
                CurrentTrackDominantColorHex = "#808080";
                CurrentTrackForegroundColorHex = "#FFFFFF";
                return;
            }

            var track = _libraryService.GetTrackById(trackId);

            if (track == null)
            {
                CurrentTrackTitle = AppStringPool.Intern("Unknown track") ?? "Unknown track";
                CurrentTrackDominantColorHex = "#808080";
                CurrentTrackForegroundColorHex = "#FFFFFF";
                return;
            }

            CurrentTrackTitle = track.Title;

            // Get Album
            var album = _libraryService.GetAlbumById(track.AlbumId);

            // Get artists

            CurrentTrackArtists = AppStringPool.Intern(string.Join(", ", _libraryService.GetArtistNamesByIds(track.ArtistIds)))
                      ?? string.Join(", ", _libraryService.GetArtistNamesByIds(track.ArtistIds));

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
            CurrentTrackArtists = string.Empty;
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


