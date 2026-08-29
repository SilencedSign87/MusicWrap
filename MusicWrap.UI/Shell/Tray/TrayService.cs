using MusicWrap.UI.Shell.Tray;
using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.ViewModels;
using MusicWrap.Core.Threading;
using MusicWrap.Data.User.Models;

namespace MusicWrap.UI.Services
{
    public class TrayService : IStartupInitializer, IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private TaskbarIcon? _trayIcon;
        private TrayFlyoutWindow? _flyout;
        private bool _isSubscribed;

        public TrayService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Initialize()
        {
            _trayIcon ??= (TaskbarIcon)App.Current.Resources["TrayIcon"];
            _trayIcon.DataContext = _serviceProvider.GetRequiredService<TaskbarIconViewModel>();
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
            if (_flyout is null || !_flyout.IsLoaded)
                _flyout = _serviceProvider.GetRequiredService<TrayFlyoutWindow>();

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

            if (_trayIcon is not null)
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
        }
        #endregion
    }
}




