using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace MusicWrap.UI.Services
{
    public interface ITrayService
    {
        void Initialize();
        void ShowFlyout();
        void HideFlyout();
        void ToggleFlyout();
    }
    class TrayService : ITrayService, IDisposable
    {
        private Forms.NotifyIcon? _trayIcon;
        private Icon? _icon;
        private TrayFlyoutWindow? _flyout;

        public void Initialize()
        {
            if (_trayIcon is not null)
            {
                return;
            }

            using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/icon.ico"))!.Stream;
            _icon = new Icon(iconStream);

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = _icon,
                Text = "Music Wrap",
                Visible = true,
                ContextMenuStrip = CreateContextMenu(),
            };

            _trayIcon.MouseClick += OnTrayMouseClick;
        }

        private void OnTrayMouseClick(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                Application.Current.Dispatcher.Invoke(ToggleFlyout);
            }
        }

        public void ShowFlyout()
        {
            if (_flyout == null || !_flyout.IsLoaded)
                _flyout = new TrayFlyoutWindow();

            _flyout.ShowFlyout();
        }
        public void HideFlyout()
        {
            _flyout?.AnimateClose();
        }
        public void ToggleFlyout()
        {
            if (_flyout == null || !_flyout.IsVisible)
                ShowFlyout();
            else
                HideFlyout();
        }

        #region Internal 
        private static Forms.ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (_, _) => App.ShowOrRestoreCurrentWindow());
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (_, _) => App.RequestShutdown());

            return contextMenu;
        }

        public void Dispose()
        {
            if (_flyout is not null)
            {
                _flyout.Close();
                _flyout = null;
            }

            if (_trayIcon != null)
            {
                _trayIcon.MouseClick -= OnTrayMouseClick;
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _icon?.Dispose();
            _icon = null;
        }
        #endregion
    }
}




