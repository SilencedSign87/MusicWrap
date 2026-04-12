using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MusicWrap.UI.ViewModels.Settings
{
    public partial class SettingsYoutubeViewModel : ObservableObject
    {
        [ObservableProperty] private bool _useCustomFfmpegPath;
        [ObservableProperty] private string _customFfmpegPath = string.Empty; 
        [ObservableProperty] private List<string> _supportedFormats = Enum.GetNames(typeof(SuportedFFMpegAudioFormat)).ToList();
        [ObservableProperty] private string _selectedFormat;

        private readonly UserSettings _settings;
        private readonly ISaveCoordinator _saveCoordinator;
        public SettingsYoutubeViewModel(UserSettings settings, ISaveCoordinator saveCoordinator)
        {
            _settings = settings;
            _saveCoordinator = saveCoordinator;
            _selectedFormat = settings.YoutubeSettings.PreferredAudioFormatForYoutube.ToString();

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
        #region Partials
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
        partial void OnSelectedFormatChanged(string value)
        {
            _settings.YoutubeSettings.PreferredAudioFormatForYoutube = Enum.TryParse<SuportedFFMpegAudioFormat>(value, out var format) ? format : SuportedFFMpegAudioFormat.mp3;
            _saveCoordinator.Enqueue(SaveKind.Settings);
        }
        #endregion

        #region Internal
        private void LoadFromSettings()
        {
            UseCustomFfmpegPath = _settings.FFMpegSettings.UseCustomFfmpegPath;
            CustomFfmpegPath = _settings.FFMpegSettings.CustomFfmpegPath ?? string.Empty;
        }
        #endregion

    }
}
