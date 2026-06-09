using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Shell.Dialogs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace MusicWrap.UI.Shared.Services
{
    public class WindowManager
    {
        private readonly IServiceProvider _serviceProvider;

        public WindowManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void LaunchSettingsWindow()
        {
            var currentWindow = App.CurrentWindow;
            if (currentWindow is null) return;

            var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            
            WindowHelper.LauchFromParent(currentWindow, settingsWindow, false);
        }

        public void LaunchIndexingWindow()
        {
            var currentWindow = App.CurrentWindow;
            if (currentWindow is null) return;

            var IndexingWindow = _serviceProvider.GetRequiredService<IndexingWindow>();

            WindowHelper.LauchFromParent(currentWindow, IndexingWindow, false);

        }

        public void SwitchToMiniplayer()
        {
            App.ShowCompactPlayer();
        }
        public void SwitchToMainPlayer()
        {
            App.ShowMainPlayer();
        }
    }
}
