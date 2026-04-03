using CommunityToolkit.Mvvm.Input;
using Hardcodet.Wpf.TaskbarNotification;
using MusicWrap.UI.Windows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
        private TaskbarIcon? _trayIcon;
        private TrayFlyoutWindow? _flyout;

        public void Initialize()
        {
            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/icon.ico"))!.Stream;

            _trayIcon ??= new TaskbarIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                ToolTipText = "Music Wrap",
                ContextMenu = CreateContextMenu(),
            };

            _trayIcon.TrayMouseDoubleClick += (s, e) => App.ShowOrRestoreCurrentWindow();
            _trayIcon.LeftClickCommand = new RelayCommand(() => ToggleFlyout());
        }
        public void ShowFlyout()
        {
            if (_flyout == null || !_flyout.IsLoaded)
                _flyout = new TrayFlyoutWindow();

            var area = SystemParameters.WorkArea;

            _flyout.Left = area.Right - _flyout.Width - 10;
            _flyout.Top = area.Bottom - _flyout.Height - 10;

            _flyout.Show();
            _flyout.Activate();
        }
        public void HideFlyout()
        {
            _flyout?.Hide();
        }
        public void ToggleFlyout()
        {
            if (_flyout == null || !_flyout.IsVisible)
                ShowFlyout();
            else
                HideFlyout();
        }

        #region Internal 
        private static ContextMenu CreateContextMenu()
        {

            var contextMenu = new ContextMenu();

            contextMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "Open",
                Command = new RelayCommand(() => App.ShowOrRestoreCurrentWindow())
            });
            contextMenu.Items.Add(new System.Windows.Controls.Separator());
            contextMenu.Items.Add(new MenuItem { Header = "Exit", Command = new RelayCommand(() => App.RequestShutdown()) });

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
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
        #endregion
    }
}
