using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Controls;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
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
        public partial bool TintForeground { get; set; } = false;
        [ObservableProperty]
        public partial bool BackdropBlur { get; set; } = true;
        [ObservableProperty]
        public partial WaveformPlayerRenderMode WaveformMode { get; set; } = WaveformPlayerRenderMode.Continuous;
        [ObservableProperty]
        public partial bool MirrorWaveformMode { get; set; } = true;
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

            TintForeground = settings.FullScreen.TintForeground;
            WaveformMode = settings.FullScreen.WaveformRenderMode == 0 ? WaveformPlayerRenderMode.None : (settings.FullScreen.WaveformRenderMode == 1 ? WaveformPlayerRenderMode.Discrete : WaveformPlayerRenderMode.Continuous);
            MirrorWaveformMode = settings.FullScreen.WaveformMirrored;
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
        partial void OnTintForegroundChanged(bool value) => SyncSettings();
        partial void OnWaveformModeChanged(WaveformPlayerRenderMode value) => SyncSettings();
        partial void OnMirrorWaveformModeChanged(bool value) => SyncSettings();
        partial void OnPreferredSpectrumTypeChanged(SpectrumType value) => SyncSettings();
        partial void OnPreferredVisualizerChanged(PreferredVisualizer value) => SyncSettings();
        #endregion
        #region Internal
        private void SyncSettings()
        {
            if (!_isInitialized) return;

            _settings.FullScreen.TintForeground = TintForeground;
            _settings.FullScreen.WaveformRenderMode = WaveformMode == WaveformPlayerRenderMode.None ? 0 : (WaveformMode == WaveformPlayerRenderMode.Discrete ? 1 : 2);
            _settings.FullScreen.WaveformMirrored = MirrorWaveformMode;
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
