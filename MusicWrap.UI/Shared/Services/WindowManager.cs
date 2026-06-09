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
        private NewPlaylistWindow? newPlaylistWindow = null;

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

        public void LaunchNewPlaylistWindow(IEnumerable<int>? tracksId = null)
        {
            var currentWindow = App.CurrentWindow;
            if (currentWindow is null) return;

            if (newPlaylistWindow is null)
            {
                newPlaylistWindow = _serviceProvider.GetRequiredService<NewPlaylistWindow>();

                newPlaylistWindow.Initialize(tracksId);

                WindowHelper.LauchFromParent(currentWindow, newPlaylistWindow, false);

                newPlaylistWindow.Closed += NewPlaylistWindow_Closed;
            }else
            {
                newPlaylistWindow.AddTracks(tracksId ?? []);
            }

            newPlaylistWindow.Activate();
        }

        private void NewPlaylistWindow_Closed(object? sender, EventArgs e)
        {
            newPlaylistWindow?.Closed -= NewPlaylistWindow_Closed;
            newPlaylistWindow = null;
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
