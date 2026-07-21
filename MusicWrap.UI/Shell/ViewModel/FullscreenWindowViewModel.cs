using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Shell.ViewModel
{
    public partial class FullscreenWindowViewModel : ObservableObject, IDisposable
    {

        private readonly WindowManager _windowManager;

        public NowPlayingViewModel NowPlayingViewModel { get; private set; }
        public PlayerViewModel PlayerViewModel { get; private set; }

        public FullscreenWindowViewModel(NowPlayingViewModel vm1, PlayerViewModel vm2, WindowManager windowManager)
        {
            NowPlayingViewModel = vm1;
            PlayerViewModel = vm2;
            _windowManager = windowManager;
        }

        [RelayCommand]
        private void ExitFullScreen()
        {
            _windowManager.SwitchToMainPlayer();
        }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
