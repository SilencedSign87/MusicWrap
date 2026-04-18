using MusicWrap.UI.Shell.Windows;
using MusicWrap.UI.Shell.Dialogs;
using MusicWrap.UI.Shell.Tray;
using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;

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
        private Icon? _icon;
        private TrayFlyoutWindow? _flyout;

        public void Initialize()
        {
            if (_trayIcon is not null)
            {
                return;
            }

            _trayIcon = (TaskbarIcon)App.Current.Resources["TrayIcon"];
            _trayIcon.DataContext = App.Services.GetRequiredService<TaskbarIconViewModel>();

            _trayIcon.TrayLeftMouseUp += _trayIcon_TrayLeftMouseUp;

        }

        private void _trayIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            ToggleFlyout();
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

        public void Dispose()
        {
            if (_flyout is not null)
            {
                _flyout.Close();
                _flyout = null;
            }

            if (_trayIcon != null)
            {
                _trayIcon.TrayLeftMouseUp -= _trayIcon_TrayLeftMouseUp;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _icon?.Dispose();
            _icon = null;
        }
        #endregion
    }
}




