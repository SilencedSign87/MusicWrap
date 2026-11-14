using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Playback;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Shared.Services;

namespace MusicWrap.UI.Shell.ViewModel
{
    public partial class MainPlayerViewModel : ObservableObject
    {
        private readonly MusicPlayerService _playerService;
        private readonly WindowManagerService _windowManager;
        private readonly MusicWrapSettings _userSettings;

        [ObservableProperty]
        private int selectedTabIndex;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SidebarToggleIcon))]
        [NotifyPropertyChangedFor(nameof(SidebarTooltip))]
        private bool isSidePanelVisible;

        public string SidebarToggleIcon
            => IsSidePanelVisible ? "\xE89F" : "\xE8A0";
        public string SidebarTooltip
            => IsSidePanelVisible ? "Hide Sidebar" : "Show Sidebar";

        private bool _disposed = false;

        public MainPlayerViewModel(MusicPlayerService playerService, WindowManagerService manager, MusicWrapSettings userSettings)
        {
            _playerService = playerService;
            _windowManager = manager;
            _userSettings = userSettings;

            IsSidePanelVisible = _userSettings.IsSidebarOpen;

            SelectedTabIndex = _userSettings.MainWindowTab;
        }

        #region Relay Commands
        [RelayCommand]
        private void PlayPause()
        {
            if (_playerService.IsPlaying)
            {
                _playerService.Pause();
            }
            else
            {
                _playerService.Play();
            }
        }
        [RelayCommand]
        private void Previous()
        {
            _playerService.Previous();
        }
        [RelayCommand]
        private void Next()
        {
            _playerService.Next();
        }
        [RelayCommand]
        private void openSettings()
        {
            _windowManager.LaunchSettingsWindow();

        }
        [RelayCommand]
        private void showMiniplayer()
        {
            _windowManager.SwitchToCompactPlayer();
        }
        [RelayCommand]
        private void showFullScreen()
        {
            _windowManager.SwitchToFullScreenPlayer();
        }
        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidePanelVisible = !IsSidePanelVisible;
            _userSettings.IsSidebarOpen = IsSidePanelVisible;
        }
        #endregion
        #region Partial Functions
        partial void OnSelectedTabIndexChanged(int value) => _userSettings.MainWindowTab = value;
        #endregion
    }
}
