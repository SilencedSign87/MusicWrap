using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Shell.Tray;
using MusicWrap.UI.ViewModels;
using System.Drawing;
using System.Windows;

namespace MusicWrap.UI.Services
{
    public interface ITrayService
    {
        void Initialize();
        void SetEnabled(bool enabled);
        void ShowFlyout();
        void HideFlyout();
        void ToggleFlyout();
    }
    class TrayService : ITrayService, IDisposable
    {
        private TaskbarIcon? _trayIcon;
        private Icon? _icon;
        private TrayFlyoutWindow? _flyout;
        private bool _isSubscribed;

        public void Initialize()
        {
            SetEnabled(true);
        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                if (_trayIcon is not null)
                {
                    _trayIcon.TrayLeftMouseUp -= _trayIcon_TrayLeftMouseUp;
                    _trayIcon.DataContext = null;
                    _trayIcon.Visibility = Visibility.Collapsed;
                    _isSubscribed = false;
                }

                return;
            }

            _trayIcon ??= (TaskbarIcon)App.Current.Resources["TrayIcon"];
            _trayIcon.DataContext = App.Services.GetRequiredService<TaskbarIconViewModel>();
            _trayIcon.Visibility = Visibility.Visible;

            if (!_isSubscribed)
            {
                _trayIcon.TrayLeftMouseUp += _trayIcon_TrayLeftMouseUp;
                _isSubscribed = true;
            }

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
                if (_isSubscribed)
                {
                    _trayIcon.TrayLeftMouseUp -= _trayIcon_TrayLeftMouseUp;
                    _isSubscribed = false;
                }

                _trayIcon.Visibility = Visibility.Collapsed;
                _trayIcon.DataContext = null;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _icon?.Dispose();
            _icon = null;
        }
        #endregion
    }
}




