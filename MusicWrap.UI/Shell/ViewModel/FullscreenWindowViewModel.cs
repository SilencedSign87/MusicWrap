using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace MusicWrap.UI.Shell.ViewModel
{
    public partial class FullscreenWindowViewModel : ObservableObject, IDisposable
    {

        private readonly WindowManagerService _windowManager;
        private readonly MusicWrapSettings _settings;

        public PlayerViewModel PlayerViewModel { get; private set; }
        private bool _isInitialized = false;

        [ObservableProperty]
        public partial bool IsProgressBarVisible { get; set; } = true;
        [ObservableProperty]
        public partial bool BackdropBlur { get; set; } = true;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetPanelCommand))]
        public partial FullscreenPlayerPanel ActivePanel { get; set; } = FullscreenPlayerPanel.None;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetVisualizerCommand))]
        [NotifyPropertyChangedFor(nameof(IsVisualizerVisible))]
        public partial PreferredVisualizer PreferredVisualizer { get; set; } = PreferredVisualizer.BarSpectrum;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SetSpectrumTypeCommand))]
        public partial SpectrumType PreferredSpectrumType { get; set; } = SpectrumType.Centered;

        public bool IsVisualizerVisible => PreferredVisualizer != PreferredVisualizer.None;

        public UIElement? CurrentPanel { get; private set; }

        public FullscreenWindowViewModel(PlayerViewModel vm2, WindowManagerService windowManager, MusicWrapSettings settings)
        {
            PlayerViewModel = vm2;
            _settings = settings;
            _windowManager = windowManager;

            BackdropBlur = settings.FullScreen.BlurEffect;
            ActivePanel = settings.FullScreen.ActivePanel;
            PreferredVisualizer = settings.FullScreen.PreferredVisualizer;
            PreferredSpectrumType = settings.FullScreen.SpectrumType;
            _isInitialized = true;
        }
        #region Relay Commands

        [RelayCommand]
        private void ExitFullScreen()
        {
            _windowManager.SwitchToMainPlayer();
        }
        [RelayCommand]
        private void ExitApp()
        {
            _windowManager.ShellWindow?.Close();
        }
        [RelayCommand(CanExecute = nameof(CanSetVisualizer))]
        private void SetVisualizer(string visualizer)
        {
            if (Enum.TryParse<PreferredVisualizer>(visualizer, out var result))
            {
                PreferredVisualizer = result;
            }
        }
        private bool CanSetVisualizer(string visualizer)
        {
            if (!Enum.TryParse<PreferredVisualizer>(visualizer, out var result))
            {
                return false;
            }
            return PreferredVisualizer != result;
        }
        [RelayCommand(CanExecute = nameof(CanSetSpectrumType))]
        private void SetSpectrumType(string spectrumType)
        {
            if (Enum.TryParse<SpectrumType>(spectrumType, out var result))
            {
                PreferredSpectrumType = result;
            }
        }
        private bool CanSetSpectrumType(string spectrumType)
        {
            if (!Enum.TryParse<SpectrumType>(spectrumType, out var result))
            {
                return false;
            }
            return PreferredSpectrumType != result;
        }
        [RelayCommand(CanExecute = nameof(CanSetPanel))]
        private void SetPanel(string activePanel)
        {
            if (Enum.TryParse<FullscreenPlayerPanel>(activePanel, out var result))
            {
                ActivePanel = result;
            }
        }
        private bool CanSetPanel(string activePanel)
        {
            if (!Enum.TryParse<FullscreenPlayerPanel>(activePanel, out var result))
            {
                return false;
            }
            return ActivePanel != result;
        }
        [RelayCommand]
        private void OpenProperties()
        {
            _windowManager.LaunchInformationWindow([]);
        }
        #endregion
        #region Partials
        partial void OnActivePanelChanged(FullscreenPlayerPanel value) => SyncSettings();
        partial void OnBackdropBlurChanged(bool value) => SyncSettings();
        partial void OnPreferredSpectrumTypeChanged(SpectrumType value) => SyncSettings();
        partial void OnPreferredVisualizerChanged(PreferredVisualizer value) => SyncSettings();
        #endregion
        #region Internal
        private void SyncSettings()
        {
            if(!_isInitialized) return;

            _settings.FullScreen.BlurEffect = BackdropBlur;
            _settings.FullScreen.ActivePanel = ActivePanel;
            _settings.FullScreen.PreferredVisualizer = PreferredVisualizer;
            _settings.FullScreen.SpectrumType = PreferredSpectrumType;
        }
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
        #endregion
    }
}
