using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Windows.Forms;
using Application = System.Windows.Application;

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
            var contextMenu = new Forms.ContextMenuStrip
            {
                RenderMode = ToolStripRenderMode.Professional,
                Renderer = new ToolStripProfessionalRenderer(new DarkColorTable()),
                ForeColor = Color.FromArgb(200, 200, 200),
            };

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

    class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);

        public override Color MenuItemSelected => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 60);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 60);
        public override Color MenuBorder => Color.Red;

        public override Color MenuItemPressedGradientBegin => Color.FromArgb(50, 50, 50);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(50, 50, 50);

        public override Color MenuItemBorder => Color.FromArgb(80, 80, 80);

        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 30);

        public override Color SeparatorDark => Color.FromArgb(70, 70, 70);
        public override Color SeparatorLight => Color.FromArgb(70, 70, 70);

        public override Color ToolStripBorder => Color.FromArgb(50, 50, 50);
    }
}




