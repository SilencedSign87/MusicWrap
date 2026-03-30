using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Policy;
using System.Text;

namespace MusicWrap.UI.ViewModels.Settings
{
    public partial class SettingsGeneralViewModel : ObservableObject
    {
        private readonly UserSettings _settings;
        private readonly ISaveCoordinator _saveCoordinator;

        [ObservableProperty] private bool _resumePlayback;
        [ObservableProperty] private bool _restoreQueueAndIndexOnly;
        [ObservableProperty] private bool _restoreQueueOnly;
        [ObservableProperty] private bool _startClean;
        [ObservableProperty] private bool _useCustomFfmpegPath;
        [ObservableProperty] private string _customFfmpegPath = string.Empty;

        public SettingsGeneralViewModel(UserSettings settings, ISaveCoordinator saveCoordinator)
        {
            _settings = settings;
            _saveCoordinator = saveCoordinator;
            LoadFromSettings();
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
        private void LoadFromSettings() {
            ResumePlayback = _settings.StartupBehavior == StartupBehavior.ResumePlayback;
            RestoreQueueAndIndexOnly = _settings.StartupBehavior == StartupBehavior.RestoreQueueAndIndexOnly;
            RestoreQueueOnly = _settings.StartupBehavior == StartupBehavior.RestoreQueueOnly;
            StartClean = _settings.StartupBehavior == StartupBehavior.StartClean;
            UseCustomFfmpegPath = _settings.UseCustomFfmpegPath;
            CustomFfmpegPath = _settings.CustomFfmpegPath ?? string.Empty;
        }
        #endregion

        #region Partials
        partial void OnResumePlaybackChanged(bool value)
        {
            if (!value) return;
            _settings.StartupBehavior = StartupBehavior.ResumePlayback;
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
        partial void OnUseCustomFfmpegPathChanged(bool value)
        {
            _settings.UseCustomFfmpegPath = value;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        partial void OnCustomFfmpegPathChanged(string value)
        {
            _settings.CustomFfmpegPath = value?.Trim() ?? string.Empty;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        #endregion
    }
}
