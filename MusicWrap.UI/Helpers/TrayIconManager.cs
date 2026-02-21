using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using MusicWrap.UI.Pages.TrayIcon;

namespace MusicWrap.UI.Helpers
{
    public static class TrayIconManager
    {
        private static TaskbarIcon _trayIcon = null!;

        public static TaskbarIcon GetTrayIcon()
        {
            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/icon.ico"))!.Stream;

            _trayIcon ??= new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                ToolTipText = "Music Wrap",
                ContextMenu = CreateContextMenu(),
            };
            return _trayIcon;
        }

        private static ContextMenu CreateContextMenu()
        {

            var contextMenu = new ContextMenu();

            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Open" });
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Play" });
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Pause" });
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Stop" });
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Next track" });
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Previous track" });
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(new System.Windows.Controls.MenuItem { Header = "Exit music wrap" });

            return contextMenu;
        }

        public static void DisposeTrayIcon()
        {
            _trayIcon?.Dispose();
        }
    }
}
