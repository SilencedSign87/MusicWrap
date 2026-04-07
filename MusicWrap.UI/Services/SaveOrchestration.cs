using MusicWrap.Core;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Player.Models;
using MusicWrap.Data.User.Models;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Windows;

namespace MusicWrap.UI.Services
{
    public interface ISaveOrchestration
    {
    }

    public sealed class SaveOrchestration : ISaveOrchestration, ISaveStateProvider, IDisposable
    {
        private readonly IMusicPlayerService _player;
        private readonly IServiceProvider _services;
        private ISaveCoordinator? _saveCoordinator;

        public SaveOrchestration(IMusicPlayerService player, IServiceProvider services)
        {
            _player = player;
            _services = services;

            _player.QueueChanged += OnQueueChanged;
            _player.PlaybackStateChanged += OnPlaybackStateChanged;
            _player.TrackChanged += OnTrackChanged;
            _player.DeviceIndexChanged += OnDeviceIndexChanged;
            _player.SampleRateChanged += OnSampleRateChanged;
            _player.OutputModeChanged += OnOutputModeChanged;
        }

        public PlaybackQueueSnapshot BuildPlaybackSnapshot()
        {
            return new PlaybackQueueSnapshot
            {
                TrackIds = _player.GetQueue(),
                CurrentIndex = _player.CurrentQueueIndex,
                PositionInSeconds = _player.CurrentPosition,
                RepeatMode = (int)_player.RepeatMode,
                ContinueMode = (int)_player.ContinueMode,
                PlaybackState = _player.IsPlaying ? 1 : (_player.IsPaused ? 2 : 0)
            };
        }

        public float GetCurrentVolume()
        {
            return _player.Volume;
        }

        public LastWindowMode GetCurrentWindowMode()
        {
            return App.CurrentWindow is CompactPlayer
                ? LastWindowMode.CompactPlayer
                : LastWindowMode.MainPlayer;
        }

        public void Dispose()
        {
            _player.QueueChanged -= OnQueueChanged;
            _player.PlaybackStateChanged -= OnPlaybackStateChanged;
            _player.TrackChanged -= OnTrackChanged;
            _player.DeviceIndexChanged -= OnDeviceIndexChanged;
            _player.SampleRateChanged -= OnSampleRateChanged;
            _player.OutputModeChanged -= OnOutputModeChanged;
        }

        private void OnOutputModeChanged(object? sender, OutputMode e)
        {
            Enqueue(SaveKind.Settings);
        }

        private void OnSampleRateChanged(object? sender, SampleRateChangedEventArgs e)
        {
            Enqueue(SaveKind.Settings);
        }

        private void OnDeviceIndexChanged(object? sender, int e)
        {
            Enqueue(SaveKind.Settings);
        }

        private void OnTrackChanged(object? sender, string e)
        {
            Enqueue(SaveKind.Playback);
        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState e)
        {
            Enqueue(SaveKind.Playback);
        }

        private void OnQueueChanged(object? sender, int[] e)
        {
            Enqueue(SaveKind.Playback);
        }

        private void Enqueue(SaveKind kind)
        {
            _saveCoordinator ??= _services.GetRequiredService<ISaveCoordinator>();
            _saveCoordinator.Enqueue(kind);
        }
    }

    public sealed class LibraryCacheStoreAdapter : ILibraryCacheStore
    {
        private readonly ILibraryCacheService _libraryCacheService;

        public LibraryCacheStoreAdapter(ILibraryCacheService libraryCacheService)
        {
            _libraryCacheService = libraryCacheService;
        }

        public void Save()
        {
            _libraryCacheService.SaveToDisk();
        }
    }
}
