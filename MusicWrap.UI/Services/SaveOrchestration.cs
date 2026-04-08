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

            _player.DeviceIndexChanged += OnDeviceIndexChanged;
            _player.SampleRateChanged += OnSampleRateChanged;
            _player.OutputModeChanged += OnOutputModeChanged;
        }

        public PlaybackQueueSnapshot BuildPlaybackSnapshot()
        {
            return _player.BuildPlaybackSnapshot();
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
