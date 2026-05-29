using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Playback;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Shared.Controls.ViewModel
{
    internal partial class VolumeControlViewModel : ObservableObject, IDisposable
    {
        private readonly IMusicPlayerService _musicPlayerService;
        private bool _disposed;
        private float _previousVolume = 1.0f;

        [ObservableProperty] private float volume;
        [ObservableProperty] private string muteButtonIcon = "\xE767";
        [ObservableProperty] private bool isMuted = false;

        public VolumeControlViewModel(IMusicPlayerService musicPlayerService)
        {
            _musicPlayerService = musicPlayerService;
            Volume = _musicPlayerService.Volume;
            _previousVolume = Volume > 0 ? Volume : 1.0f;

            UpdateVolumeIcon(Volume);

            _musicPlayerService.VolumeChanged += OnPlayerVolumeChanged;
        }

        private void OnPlayerVolumeChanged(object? sender, float newVolume)
        {
            if (Math.Abs(Volume - newVolume) > 0.0001f && !_disposed)
            {
                Volume = newVolume;
            }
        }

        partial void OnVolumeChanged(float value)
        {
            if (IsMuted && value > 0)
                IsMuted = false;

            if (!IsMuted)
                UpdateVolumeIcon(value);

            if (Math.Abs(_musicPlayerService.Volume - value) > 0.0001f)
                _musicPlayerService.SetVolume(value);
        }

        [RelayCommand]
        private void ToggleMute()
        {
            if (IsMuted)
            {
                Volume = _previousVolume;
                IsMuted = false;
                UpdateVolumeIcon(Volume);
            }
            else
            {
                _previousVolume = Volume;
                Volume = 0;
                IsMuted = true;
                MuteButtonIcon = "\xE74F"; // Muted icon

            }
        }

        private void UpdateVolumeIcon(float v)
        {
            MuteButtonIcon = v switch
            {
                0 => "\xE992",
                < 0.35f => "\xE993",
                < 0.75f => "\xE994",
                _ => "\xE767"
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _musicPlayerService.VolumeChanged -= OnPlayerVolumeChanged;
        }
    }
}
