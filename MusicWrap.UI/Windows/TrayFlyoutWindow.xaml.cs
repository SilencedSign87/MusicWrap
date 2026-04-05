using Microsoft.Extensions.DependencyInjection;
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

namespace MusicWrap.UI.Windows
{
    /// <summary>
    /// Lógica de interacción para TrayFlyoutWindow.xaml
    /// </summary>
    public partial class TrayFlyoutWindow : Window
    {
        private PlayerViewModel _viewmodel;
        private bool _isAnimatingClose;
        private double _homeTop;
        private const int FlyoutMargin = 8;
        public TrayFlyoutWindow()
        {
            InitializeComponent();
            _viewmodel = App.Services.GetRequiredService<PlayerViewModel>();
            DataContext = _viewmodel;
        }

        public void ShowFlyout()
        {
            _isAnimatingClose = false;

            RootPanel.BeginAnimation(UIElement.OpacityProperty, null);
            RootTranslate.BeginAnimation(TranslateTransform.YProperty, null);

            Top = _homeTop;
            RootPanel.Opacity = 0;
            RootTranslate.Y = 15;

            if (!IsVisible)
            {
                Show();
            }

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

            slide.From = 15;
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
            slide.To = 15;
            slide.Completed += (_, _) => RootTranslate.Y = 15;

            RootTranslate.BeginAnimation(TranslateTransform.YProperty, slide);

            var fade = new DoubleAnimation(1, 0, duration)
            {
                FillBehavior = FillBehavior.HoldEnd
            };

            fade.Completed += (_, _) =>
            {
                _isAnimatingClose = false;

                Hide();
                RootPanel.Opacity = 1;
                RootTranslate.Y = 0;
                Top = _homeTop;
            };

            RootPanel.BeginAnimation(UIElement.OpacityProperty, fade);
        }
        private void OpenMainWindow(object sender, RoutedEventArgs e)
        {
            Hide();
            App.ShowOrRestoreCurrentWindow();
        }
        private void ExitApp(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
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

            Left = area.Right - Width - FlyoutMargin;
            Top = area.Bottom - Height - FlyoutMargin;

            _homeTop = Top;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    }
}
