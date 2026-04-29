using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Playback;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace MusicWrap.UI.ViewModels
{
    public partial class TaskbarIconViewModel : ObservableObject, IDisposable
    {
        private bool _disposed = false;
        private const string pauseIcon = "\xE769";
        private const string playIcon = "\xE768";
        [ObservableProperty] private string playPauseStatus = "Play";
        [ObservableProperty] private string playPauseIcon = playIcon;
        private float previousVolume = 1.0f;

        private readonly IMusicPlayerService _playerService;
        private readonly ITrayService _trayService;
        public TaskbarIconViewModel(IMusicPlayerService musicPlayerService, ITrayService trayService)
        {
            _playerService = musicPlayerService;
            _trayService = trayService;

            LoadInitialState();

            _playerService.PlaybackStateChanged += _playerService_PlaybackStateChanged;
        }
        [RelayCommand]
        private void OpenMainWindow()
        {
            App.ShowOrRestoreCurrentWindow();
        }
        [RelayCommand]
        private void ExitApp()
        {
            App.Current.Shutdown();
        }
        [RelayCommand]
        private void TogglePlayPause()
        {
            if (PlayPauseStatus == "Play")
            {
                _playerService.Play();
            }
            else
            {
                _playerService.Pause();
            }
        }
        [RelayCommand]
        private void Stop()
        {
            _playerService.Stop();
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
        private void Shuffle()
        {
            var tracks = _playerService.GetQueue().Shuffle();
            _playerService.SetQueue(tracks, true);
        }
        [RelayCommand]
        private void Info()
        {
            _trayService.ShowFlyout();
        }

        private void LoadInitialState()
        {
            if (_playerService.IsPlaying)
            {
                SetUiPlayingState(PlaybackState.Playing);
            }
            else
            {
                SetUiPlayingState(PlaybackState.Paused);
            }
            previousVolume = _playerService.Volume;
        }
        private void _playerService_PlaybackStateChanged(object? sender, PlaybackState e)
        {
            SetUiPlayingState(e);
        }
        private void SetUiPlayingState(PlaybackState state)
        {
            switch (state)
            {
                case PlaybackState.Playing:
                    PlayPauseStatus = "Pause";
                    PlayPauseIcon = pauseIcon;
                    break;
                case PlaybackState.Paused:
                case PlaybackState.Stopped:
                    PlayPauseStatus = "Play";
                    PlayPauseIcon = playIcon;
                    break;
            }

        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _playerService.PlaybackStateChanged -= _playerService_PlaybackStateChanged;
            }
        }

    }
}
