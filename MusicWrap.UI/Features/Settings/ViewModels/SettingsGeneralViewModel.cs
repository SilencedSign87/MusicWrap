using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Activity.Models;
using MusicWrap.UI.Features.Activity.Services;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Shared.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Policy;
using System.Text;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public partial class SettingsGeneralViewModel : ObservableObject
    {
        private readonly UserSettings _settings;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILibraryIntegrityService _integrityService;
        private readonly ActivityService _activityService;
        private readonly WindowManager _windowManager;

        [ObservableProperty] private bool _restoreEverything;
        [ObservableProperty] private bool _restoreCurrentTrackAndPosition;
        [ObservableProperty] private bool _restoreQueueAndIndexOnly;
        [ObservableProperty] private bool _restoreQueueOnly;
        [ObservableProperty] private bool _startClean;

        [ObservableProperty] private bool _minimizeToTray;
        [ObservableProperty] private bool _exitAppOnClose;

        [ObservableProperty] private bool _useCustomFfmpegPath;
        [ObservableProperty] private string _customFfmpegPath = string.Empty;
        [ObservableProperty] private TrayPopupPosition _trayPopupPosition;


        public string WallpaperPath { get; } = string.Empty;
        private bool _updatingCloseBehavior;
        public List<TrayPopupPosition> TrayPopupPositions { get; } = Enum.GetValues<TrayPopupPosition>().ToList();

        public SettingsGeneralViewModel(
            UserSettings settings,
            ISaveCoordinator saveCoordinator,
            ILibraryIntegrityService integrityService,
            ActivityService activityService,
            WindowManager windowManager)
        {
            _settings = settings;
            _saveCoordinator = saveCoordinator;
            _integrityService = integrityService;
            _activityService = activityService;
            _windowManager = windowManager;
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
            RestoreQueueAndIndexOnly = _settings.StartupBehavior == StartupBehavior.RestoreQueueAndIndexOnly;
            RestoreQueueOnly = _settings.StartupBehavior == StartupBehavior.RestoreQueueOnly;
            StartClean = _settings.StartupBehavior == StartupBehavior.StartClean;
            RestoreEverything = _settings.StartupBehavior == StartupBehavior.RestorePlayback;
            RestoreCurrentTrackAndPosition = _settings.StartupBehavior == StartupBehavior.RestorePosition;
            MinimizeToTray = _settings.KeepAppInTray;
            ExitAppOnClose = !_settings.KeepAppInTray;
            UseCustomFfmpegPath = _settings.FFMpegSettings.UseCustomFfmpegPath;
            CustomFfmpegPath = _settings.FFMpegSettings.CustomFfmpegPath ?? string.Empty;
            TrayPopupPosition = _settings.TrayPopupPosition;
        }
        #endregion

        #region Partials
        partial void OnRestoreEverythingChanged(bool value)
        {
            if (!value) return;
            _settings.StartupBehavior = StartupBehavior.RestorePlayback;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnRestoreCurrentTrackAndPositionChanged(bool value)
        {
            if (!value) return;
            _settings.StartupBehavior = StartupBehavior.RestorePosition;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnRestoreQueueAndIndexOnlyChanged(bool value)
        {
            if (!value) return;
            _settings.StartupBehavior = StartupBehavior.RestoreQueueAndIndexOnly;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }

        partial void OnRestoreQueueOnlyChanged(bool value)
        {
            if (!value) return;
            _settings.StartupBehavior = StartupBehavior.RestoreQueueOnly;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }

        partial void OnStartCleanChanged(bool value)
        {
            if (!value) return;
            _settings.StartupBehavior = StartupBehavior.StartClean;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }

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
            _settings.FFMpegSettings.UseCustomFfmpegPath = value;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnCustomFfmpegPathChanged(string value)
        {
            _settings.FFMpegSettings.CustomFfmpegPath = value?.Trim() ?? string.Empty;
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
        #endregion
    }
}

