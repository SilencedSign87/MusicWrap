using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Activity;
using MusicWrap.Core.Services.Library;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Shared.Services;
using System.Diagnostics;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public partial class SettingsGeneralViewModel : ObservableObject
    {
        private readonly MusicWrapSettings _settings;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ThemeService _themeService;

        [ObservableProperty] private StartupBehavior _startupBehavior;

        [ObservableProperty] private bool _minimizeToTray;
        [ObservableProperty] private bool _exitAppOnClose;

        [ObservableProperty] private bool _useCustomFfmpegPath;
        [ObservableProperty] private string _customFfmpegPath = string.Empty;
        [ObservableProperty] private TrayPopupPosition _trayPopupPosition;

        [ObservableProperty] private ThemePreference _selectedTheme;


        public string WallpaperPath { get; } = string.Empty;
        private bool _updatingCloseBehavior;
        public List<TrayPopupPosition> TrayPopupPositions { get; } = Enum.GetValues<TrayPopupPosition>().ToList();

        public SettingsGeneralViewModel(
            MusicWrapSettings settings,
            ISaveCoordinator saveCoordinator,
            ILibraryIntegrityService integrityService,
            ActivityService activityService,
            ThemeService themeService)
        {
            _settings = settings;
            _saveCoordinator = saveCoordinator;
            _themeService = themeService;
            LoadFromSettings();
            WallpaperPath = WallpaperHelper.GetWallpaperPath() ?? "";
        }

        #region Commands
        [RelayCommand]
        private void BrowseForFfmpegPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select ffmpeg executable",
                Filter = "FFmpeg executable|ffmpeg.exe|Executable files|*.exe|All files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog() == true)
            {
                CustomFfmpegPath = dialog.FileName;
                UseCustomFfmpegPath = true;
            }
        }
        [RelayCommand]
        private void OpenffmpegDownloadPage()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ffmpeg.org/download.html",
                UseShellExecute = true
            });
        }
        #endregion


        #region Internal
        private void LoadFromSettings()
        {
            MinimizeToTray = _settings.KeepAppInTray;
            ExitAppOnClose = !_settings.KeepAppInTray;
            UseCustomFfmpegPath = _settings.FFMpeg.UseCustomFfmpegPath;
            CustomFfmpegPath = _settings.FFMpeg.CustomFfmpegPath ?? string.Empty;
            TrayPopupPosition = _settings.TrayPopupPosition;
            SelectedTheme = _settings.AppThemePreference;
            StartupBehavior = _settings.StartupBehavior;
        }
        #endregion

        #region Partials
        partial void OnMinimizeToTrayChanged(bool value)
        {
            if (_updatingCloseBehavior || !value) return;
            SetCloseBehavior(true);
        }

        partial void OnExitAppOnCloseChanged(bool value)
        {
            if (_updatingCloseBehavior || !value) return;
            SetCloseBehavior(false);
        }

        partial void OnUseCustomFfmpegPathChanged(bool value)
        {
            _settings.FFMpeg.UseCustomFfmpegPath = value;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnCustomFfmpegPathChanged(string value)
        {
            _settings.FFMpeg.CustomFfmpegPath = value?.Trim() ?? string.Empty;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }

        private void SetCloseBehavior(bool keepInTray)
        {
            _updatingCloseBehavior = true;
            try
            {
                MinimizeToTray = keepInTray;
                ExitAppOnClose = !keepInTray;
            }
            finally
            {
                _updatingCloseBehavior = false;
            }

            _settings.KeepAppInTray = keepInTray;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnTrayPopupPositionChanged(TrayPopupPosition value)
        {
            _settings.TrayPopupPosition = value;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnSelectedThemeChanged(ThemePreference value)
        {
            _themeService.SwitchTheme(value);
        }
        partial void OnStartupBehaviorChanged(StartupBehavior value)
        {
            _settings.StartupBehavior = value;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        #endregion
    }
}

