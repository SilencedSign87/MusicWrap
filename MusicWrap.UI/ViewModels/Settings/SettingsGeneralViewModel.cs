using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Services;
using System;
using System.Collections.Generic;
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

        public SettingsGeneralViewModel(UserSettings settings, ISaveCoordinator saveCoordinator)
        {
            _settings = settings;
            _saveCoordinator = saveCoordinator;
            LoadFromSettings();
        }

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

        #region Internal
        private void LoadFromSettings() {
            ResumePlayback = _settings.StartupBehavior == StartupBehavior.ResumePlayback;
            RestoreQueueAndIndexOnly = _settings.StartupBehavior == StartupBehavior.RestoreQueueAndIndexOnly;
            RestoreQueueOnly = _settings.StartupBehavior == StartupBehavior.RestoreQueueOnly;
            StartClean = _settings.StartupBehavior == StartupBehavior.StartClean;
        }
        #endregion
    }
}
