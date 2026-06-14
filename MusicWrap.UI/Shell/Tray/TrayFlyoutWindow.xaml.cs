using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Data.User.Models;
using MusicWrap.UI.Shared.Services;
using MusicWrap.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MusicWrap.UI.Shell.Tray
{
    /// <summary>
    /// Lógica de interacción para TrayFlyoutWindow.xaml
    /// </summary>
    public partial class TrayFlyoutWindow : Window
    {
        private readonly PlayerViewModel _viewmodel;
        private readonly WindowManager _windowManager;
        private readonly UserSettings _userSettings;

        private bool _isAnimatingClose;
        private double _homeTop;
        private const int FlyoutMargin = 8;
        private bool IsTopPosition => _userSettings.TrayPopupPosition is TrayPopupPosition.TopLeft or TrayPopupPosition.TopCenter or TrayPopupPosition.TopRight;
        private double SlideOffset => IsTopPosition ? -15 : 15;

        public TrayFlyoutWindow(PlayerViewModel vm, WindowManager windowManager, UserSettings userSettings)
        {
            InitializeComponent();
            _viewmodel = vm;
            _windowManager = windowManager;
            _userSettings = userSettings;
            DataContext = _viewmodel;
        }

        public void ShowFlyout()
        {
            _isAnimatingClose = false;

            RootPanel.BeginAnimation(UIElement.OpacityProperty, null);
            RootTranslate.BeginAnimation(TranslateTransform.YProperty, null);

            Top = _homeTop;
            RootPanel.Opacity = 0;
            RootTranslate.Y = SlideOffset;

            if (!IsVisible)
                Show();
            

            Topmost = true;
            Topmost = false;
            Topmost = true;

            Activate();

            var duration = TimeSpan.FromMilliseconds(200);

            var slide = new DoubleAnimation
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop,
                Duration = duration
            };

            slide.From = SlideOffset;
            slide.To = 0;
            slide.Completed += (_, _) => RootTranslate.Y = 0;

            RootTranslate.BeginAnimation(TranslateTransform.YProperty, slide);

            var fade = new DoubleAnimation(0, 1, duration)
            {
                FillBehavior = FillBehavior.Stop
            };

            fade.Completed += (_, _) => RootPanel.Opacity = 1;

            RootPanel.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        public void AnimateClose()
        {
            if (_isAnimatingClose) return;

            _isAnimatingClose = true;

            RootPanel.BeginAnimation(UIElement.OpacityProperty, null);
            RootTranslate.BeginAnimation(TranslateTransform.YProperty, null);

            var duration = TimeSpan.FromMilliseconds(160);

            var slide = new DoubleAnimation
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior = FillBehavior.Stop,
                Duration = duration
            };

            slide.From = 0;
            slide.To = SlideOffset;
            slide.Completed += (_, _) => RootTranslate.Y = SlideOffset;

            RootTranslate.BeginAnimation(TranslateTransform.YProperty, slide);

            var fade = new DoubleAnimation(1, 0, duration)
            {
                FillBehavior = FillBehavior.HoldEnd
            };

            fade.Completed += (_, _) =>
            {
                _isAnimatingClose = false;

                RootPanel.Opacity = 1;
                RootTranslate.Y = 0;
                Top = _homeTop;
                Close();
            };

            RootPanel.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        private void OpenMainWindow(object sender, RoutedEventArgs e)
        {
            Close();
            _windowManager.ShowOrRestoreCurrentWindow();
        }
        private void ExitApp(object sender, RoutedEventArgs e)
        {
            AnimateClose();
        }
        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            AnimateClose();
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int disable = 1;
            DwmSetWindowAttribute(hwnd, 3, ref disable, sizeof(int));

            var area = SystemParameters.WorkArea;
            var pos = _userSettings.TrayPopupPosition;

            // Horizontal
            Left = pos switch
            {
                TrayPopupPosition.TopLeft or TrayPopupPosition.BottomLeft => area.Left + FlyoutMargin,
                TrayPopupPosition.TopCenter or TrayPopupPosition.BottomCenter => area.Left + (area.Width - Width) / 2,
                TrayPopupPosition.TopRight or TrayPopupPosition.BottomRight => area.Right - Width - FlyoutMargin,
                _ => area.Right - Width - FlyoutMargin
            };
            // Vertical
            Top = pos switch
            {
                TrayPopupPosition.TopLeft or TrayPopupPosition.TopCenter or TrayPopupPosition.TopRight => area.Top + FlyoutMargin,
                TrayPopupPosition.BottomLeft or TrayPopupPosition.BottomCenter or TrayPopupPosition.BottomRight => area.Bottom - Height - FlyoutMargin,
                _ => area.Bottom - Height - FlyoutMargin
            };

            _homeTop = Top;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    }
}

