using MusicWrap.Core.Saving;
using MusicWrap.Core.Threading;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.User.Models;
using NetVips;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace MusicWrap.UI.Shared.Services
{
    public sealed class ThemeService : IStartupInitializer
    {
        private readonly MusicWrapSettings _userSettings;
        private readonly ISaveCoordinator _saveCoordinator;
        private Uri? _appliedThemeUri;

        public ThemeService(MusicWrapSettings userSettings, ISaveCoordinator saveCoordinator)
        {
            _userSettings = userSettings;
            _saveCoordinator = saveCoordinator;
        }

        public void Initialize()
        {
            ApplyTheme(_userSettings.AppThemePreference);
        }
        public void SwitchTheme(ThemePreference newTheme)
        {
            if (newTheme == _userSettings.AppThemePreference)
                return;
            _userSettings.AppThemePreference = newTheme;
            _saveCoordinator.Enqueue(SaveKind.Settings);

            ApplyTheme(newTheme);
        }
        private void ApplyTheme(ThemePreference theme)
        {
            var desire = theme switch
            {
                ThemePreference.Light => new Uri("/Styles/Theme/Light.xaml", UriKind.Relative),
                ThemePreference.Dark => new Uri("/Styles/Theme/Dark.xaml", UriKind.Relative),
                ThemePreference.System => GetSystemThemeUri(),
                _ => new Uri("/Styles/Theme/Dark.xaml", UriKind.Relative)
            };

            if (_appliedThemeUri == desire) return;

            Application.Current.ThemeMode = theme switch
            {
                ThemePreference.Light => ThemeMode.Light,
                ThemePreference.Dark => ThemeMode.Dark,
                ThemePreference.System => ThemeMode.System,
                _ => ThemeMode.Dark
            };

            var oldTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains("/Theme/") == true);

            if (oldTheme != null)
                Application.Current.Resources.MergedDictionaries.Remove(oldTheme);

            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = desire });
            _appliedThemeUri = desire;

        }

        private static Uri GetSystemThemeUri()
        {
            // Detect Windows theme
            var useLight = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", "1")?.ToString() == "1";
            return useLight
                ? new Uri("/Styles/Theme/Light.xaml", UriKind.Relative)
                : new Uri("/Styles/Theme/Dark.xaml", UriKind.Relative);
        }
    }
}
