using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Media;

namespace MusicWrap.UI.Shared.Services
{
    public sealed class SystemMediaTransportControlsController : IDisposable
    {
        private readonly IMusicPlayerService _playerService;
        private readonly ILibraryService _libraryService;
        private readonly IImageService _imageService;
        private readonly ILogger _logger;

        private SystemMediaTransportControls? _smtc;
        private bool _disposed;
        private bool _initialized;

        public SystemMediaTransportControlsController(IMusicPlayerService playerService, ILibraryService libraryService, ILogger<SystemMediaTransportControlsController> logger, IImageService imageService)
        {
            _playerService = playerService;
            _libraryService = libraryService;
            _logger = logger;
            _imageService = imageService;

            TryInitialize();
        }
        public void EnsureInitialized()
        {
            if (!_initialized)
                TryInitialize();
        }
        private void TryInitialize()
        {
            if (_initialized || _disposed) return;

            try
            {
                _smtc = SystemMediaTransportControls.GetForCurrentView();
                _smtc.IsEnabled = true;

                _smtc.IsPlayEnabled = true;
                _smtc.IsPauseEnabled = true;
                _smtc.IsNextEnabled = true;
                _smtc.IsPreviousEnabled = true;
                _smtc.IsStopEnabled = true;

                _smtc.ButtonPressed += OnButtonPressed;
                _playerService.PlaybackStateChanged += OnPlaybackStateChanged;
                _playerService.TrackChanged += OnTrackChanged;

                // initial state
                UpdatePlaybackStatus();
                UpdateTrackMetadata();

                _logger.LogInformation("SMTC initialized. IsEnabled={IsEnabled}, Play={P}, Pause={Pause}, Next={N}, Prev={Prev}", _smtc.IsEnabled, _smtc.IsPlayEnabled, _smtc.IsPauseEnabled, _smtc.IsNextEnabled, _smtc.IsPreviousEnabled);
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80070578))
            {
                _logger.LogDebug("SMTC deferred: no window handle yet. Will retry on window load.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize SMTC");
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_smtc is not null)
            {
                _smtc.ButtonPressed -= OnButtonPressed;
                _smtc.IsEnabled = false;
            }

            _playerService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playerService.TrackChanged -= OnTrackChanged;
        }

        #region internal
        private void OnButtonPressed(SystemMediaTransportControls sender,
       SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            _logger.LogDebug("SMTC button pressed: {Button}", args.Button);

            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _playerService.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _playerService.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _playerService.Next();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _playerService.Previous();
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    _playerService.Stop();
                    break;
            }
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            UpdatePlaybackStatus();
        }
        private void OnTrackChanged(object? sender, string trackPath)
        {
            UpdateTrackMetadata();
        }
        private void UpdatePlaybackStatus()
        {
            _smtc?.PlaybackStatus = _playerService.IsPlaying
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;
        }
        private void UpdateTrackMetadata()
        {
            try
            {
                var updater = _smtc?.DisplayUpdater;
                if (updater is null) return;

                updater.Type = MediaPlaybackType.Music;

                int trackId = _playerService.CurrentTrackId;

                if (trackId > 0)
                {
                    var track = _libraryService.GetTrackById(trackId);
                    if (track != null)
                    {
                        var artistNames = _libraryService.GetArtistNamesForTrack(trackId);
                        var album = _libraryService.GetAlbumById(track.AlbumId);
                        updater.MusicProperties.Title = track.Title ?? "Unknown";
                        updater.MusicProperties.Artist = artistNames;
                        updater.MusicProperties.AlbumTitle = album?.Title ?? track.Title ?? "Unknown Album";
                        updater.MusicProperties.TrackNumber = (uint)(track.TrackNumber);

                        _ = SetThumbnailAsync(updater, track);
                    }
                }
                updater.Update();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to update SMTC metadata");
            }
        }
        private async Task SetThumbnailAsync(SystemMediaTransportControlsDisplayUpdater updater, Track track)
        {
            try
            {
                int coverId = track.CoverId;
                if (coverId == 0)
                {
                    var album = _libraryService.GetAlbumById(track.AlbumId);
                    coverId = album?.CoverId ?? 0;
                }
                
                if (coverId <= 0) return;
                var coverAsset = _libraryService.GetCoverAsset(coverId);

                if (coverAsset is null) return;
                var coverPath = _imageService.ResolvePath(coverAsset.FileName, ImageVariant.Medium);
                
                if (string.IsNullOrEmpty(coverPath) || !File.Exists(coverPath))
                    return;
                var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(coverPath);
                updater.Thumbnail = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromFile(storageFile);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to set SMTC thumbnail");
            }
        }
        #endregion
    }
}
