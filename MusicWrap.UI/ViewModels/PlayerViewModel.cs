using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using MusicWrap.UI.Services;
using MusicWrap.Core.Services.Playback;
using ManagedBass;
using MusicWrap.Data.Library.Models;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Helpers;
using MusicWrap.Core.Services.Contracts;
using MusicWrap.UI.Shared.Services;

namespace MusicWrap.UI.ViewModels
{
    public partial class PlayerViewModel : ObservableObject, IDisposable
    {
        private bool _disposed = false;
        private readonly MusicPlayerService _playerService;
        private readonly ILibraryService _libraryService;
        private readonly WindowManagerService _windowManagerService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayPauseIcon))]
        private bool isPlaying = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RepeatModeIcon), nameof(RepeatModeTooltip))]
        private RepeatMode selectedRepeatMode;

        public string RepeatModeIcon => SelectedRepeatMode switch
        {
            RepeatMode.None => "\uebe7",
            RepeatMode.RepeatOne => "\ue8ed",
            _ => "\ue8ee"
        };

        public string RepeatModeTooltip => SelectedRepeatMode switch
        {
            RepeatMode.None => "No repeat",
            RepeatMode.RepeatOne => "Repeat current track",
            _ => "Repeat entire queue"
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShuffleIcon), nameof(ShuffleTooltip))]
        private bool isShuffleEnabled = false;

        public string ShuffleIcon => IsShuffleEnabled ? "\xE8B1" : "\xE73C";

        public string ShuffleTooltip => IsShuffleEnabled ? "Shuffle on" : "Shuffle off";

        [ObservableProperty]
        private string currentTrackTitle = CommonStrings.NoTrackPlaying;
        [ObservableProperty]
        private string currentTrackAlbum = CommonStrings.UnknownAlbum;
        [ObservableProperty]
        private string currentTrackArtists = "";
        [ObservableProperty]
        private string currentTrackImagePath = "";
        [ObservableProperty]
        private string? currentTrackDominantColorHex;
        [ObservableProperty]
        private string? currentTackDominantForegroundColorHex;
        [ObservableProperty]
        private string? currentTrackHighlightColorHex;


        //[ObservableProperty] 
        public string PlayPauseIcon => IsPlaying ? "\ue769" : "\ue768";

        private string ArtworkPath = "";

        private readonly IwindowsImageService _imageService;

        public PlayerViewModel(MusicPlayerService service, ILibraryService libraryService, IwindowsImageService imageService, WindowManagerService windowManagerService)
        {
            _playerService = service;
            _libraryService = libraryService;
            _imageService = imageService;
            _windowManagerService = windowManagerService;


            // Subscribe to player events
            _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
            _playerService.TrackChanged += OnTrackChanged;
            _playerService.ShuffleStateChanged += _playerService_ShuffleStateChanged;

            // Load initial states
            SelectedRepeatMode = _playerService.RepeatMode;

            UpdateCurrentTrackInfo();

            // Initialize state
            IsPlaying = _playerService.IsPlaying;
            IsShuffleEnabled = _playerService.IsShuffleEnabled;
        }

        private void _playerService_ShuffleStateChanged(object? sender, bool enabled)
        {
            Application.Current?.Dispatcher.Invoke(() =>
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
            SelectedRepeatMode = SelectedRepeatMode switch
            {
                RepeatMode.None => RepeatMode.RepeatOne,
                RepeatMode.RepeatOne => RepeatMode.RepeatAll,
                _ => RepeatMode.None
            };
        }

        [RelayCommand]
        private void ToggleShuffle()
        {
            _playerService.ToggleShuffle();
        }

        [RelayCommand]
        private void OpenPropertiesOfCurrentTrack()
        {
            if (_playerService.CurrentTrackId > 0)
            {
                _windowManagerService.LaunchInformationWindow([_playerService.CurrentTrackId]);
            }
        }

        partial void OnSelectedRepeatModeChanged(RepeatMode value)
        {
            if (_playerService.RepeatMode != value)
            {
                _playerService.RepeatMode = value;
            }
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
                CurrentTrackTitle = CommonStrings.NoTrackPlaying;
                return;
            }

            var trackId = _playerService.CurrentTrackId;
            if (trackId == 0)
            {
                CurrentTrackTitle = CommonStrings.UnknownTrack;
                CurrentTrackDominantColorHex = CommonColors.DominantColorFallback;
                CurrentTrackHighlightColorHex = CommonColors.ForegroundOnFallback;
                return;
            }

            var track = _libraryService.GetTrackById(trackId);

            if (track == null)
            {
                CurrentTrackTitle = CommonStrings.UnknownTrack;
                CurrentTrackDominantColorHex = CommonColors.DominantColorFallback;
                CurrentTrackHighlightColorHex = CommonColors.ForegroundOnFallback;
                return;
            }

            CurrentTrackTitle = track.Title;

            // Get Album
            var album = _libraryService.GetAlbumById(track.AlbumId);
            if (album is not null)
            {
                CurrentTrackAlbum = album.Title;
            }

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
                    CurrentTrackHighlightColorHex = coverAsset.HighlightColorHex;
                    CurrentTackDominantForegroundColorHex = coverAsset.DominantForegroundHex;
                }
                else
                {
                    CurrentTrackImagePath = string.Empty;
                    ArtworkPath = string.Empty;
                    CurrentTrackDominantColorHex = CommonColors.DominantColorFallback;
                    CurrentTrackHighlightColorHex = CommonColors.HighlightColorFallback;
                    CurrentTackDominantForegroundColorHex = CommonColors.ForegroundOnFallback;
                }
            }
            else
            {
                CurrentTrackDominantColorHex = CommonColors.DominantColorFallback;
                CurrentTrackHighlightColorHex = CommonColors.HighlightColorFallback;
                CurrentTackDominantForegroundColorHex = CommonColors.ForegroundOnFallback;
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
            CurrentTrackDominantColorHex = CommonColors.DominantColorFallback;
            CurrentTrackHighlightColorHex = CommonColors.HighlightColorFallback;
            CurrentTackDominantForegroundColorHex = CommonColors.ForegroundOnFallback;
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


