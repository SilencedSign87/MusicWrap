using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Lyrics.View;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Shell.ViewModel;
using MusicWrap.UI.ViewModels;
using System.ComponentModel;
using System.Runtime.Intrinsics.Arm;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para FullScreenWindow.xaml
    /// </summary>
    public partial class FullScreenWindow : UserControl
    {
        private readonly TimeSpan PanelAnimationDuration = TimeSpan.FromMilliseconds(300);
        private readonly LyricsView LyricsView;
        private readonly QueueListPage QueueListPage;
        private readonly Grid _panelHost = new();
        private readonly TranslateTransform _panelSlide = new();
        private UIElement? _openPanelChild;

        private double backdropRPM = 1.2;


        public FullScreenWindow(FullscreenWindowViewModel viewmodel, QueueListPage queueListPage)
        {
            InitializeComponent();

            PanelContainer.RenderTransform = _panelSlide;

            QueueListPage = queueListPage;
            QueueListPage.Margin = new Thickness(0, 52, 0, 36);
            LyricsView = new LyricsView
            {
                FontSize = 36,
                AllowScroll = false,
                AllowSeek = false,
                ShowToolbar = false,
                LyricsAligment = TextAlignment.Left,
            };
            LyricsView.LyricsStateChanged += LyricsControl_LyricsStateChanged;

            LyricsView.Visibility = Visibility.Collapsed;
            QueueListPage.Visibility = Visibility.Collapsed;

            _panelHost.Children.Add(LyricsView);
            _panelHost.Children.Add(QueueListPage);
            PanelContainer.Child = _panelHost;

            DataContext = viewmodel;
            viewmodel.PropertyChanged += OnViewmodelPropertyChanged;
            SizeChanged += (_, _) => ApplyProgress();
        }

        public double PanelProgress
        {
            get { return (double)GetValue(PanelProgressProperty); }
            set { SetValue(PanelProgressProperty, value); }
        }
        public static readonly DependencyProperty PanelProgressProperty =
            DependencyProperty.Register(nameof(PanelProgress), typeof(double), typeof(FullScreenWindow), new PropertyMetadata(0d, OnPanelProgressChanged));
        private static void OnPanelProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FullScreenWindow)d).ApplyProgress();


        public double PanelShare
        {
            get { return (double)GetValue(PanelShareProperty); }
            set { SetValue(PanelShareProperty, value); }
        }
        public static readonly DependencyProperty PanelShareProperty =
            DependencyProperty.Register(nameof(PanelShare), typeof(double), typeof(FullScreenWindow), new PropertyMetadata(0.5d, OnLayoutChanged));
        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((FullScreenWindow)d).ApplyProgress();

        private void OnViewmodelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FullscreenWindowViewModel.ActivePanel))
            {
                ApplyPanelState(animate: true);
            }
        }

        private void ApplyProgress()
        {
            double w = ActualWidth;
            if (w <= 0) return;

            double panelWidth = w * PanelShare;
            double t = PanelProgress;

            PanelContainer.Width = panelWidth;
            _panelSlide.X = panelWidth * (1 - t);
            ArtworkHost.Width = w - panelWidth * t;
        }

        private void AnimateValue(DependencyProperty dp, double target, bool closing, EventHandler? completed = null)
        {
            var anim = new DoubleAnimation
            {
                From = (double)GetValue(dp),
                To = target,
                Duration = PanelAnimationDuration,
                EasingFunction = new QuadraticEase
                {
                    EasingMode = closing ? EasingMode.EaseIn : EasingMode.EaseOut
                },
                FillBehavior = FillBehavior.HoldEnd
            };
            if (completed is not null) anim.Completed += completed;
            BeginAnimation(dp, anim);
        }

        private void SnapValue(DependencyProperty dp, double value)
        {
            BeginAnimation(dp, null);
            SetValue(dp, value);
        }

        private void ApplyPanelState(bool animate)
        {
            if (DataContext is not FullscreenWindowViewModel vm) return;

            bool shouldShow = ShouldShowPanel(vm);
            bool isOpen = _openPanelChild is not null;
            double targetShare = ShareFor(vm);

            if (shouldShow)
            {
                UIElement target = ResolvePanelChild(vm);
                if (isOpen)
                {
                    bool switchChild = !ReferenceEquals(_openPanelChild, target);
                    if (switchChild)
                    {
                        ShowPanelChild(target);
                        _openPanelChild = target;
                    }
                    if (Math.Abs(PanelShare - targetShare) > 0.0001)
                    {
                        if (animate) AnimateValue(PanelShareProperty, targetShare, closing: false);
                        else SnapValue(PanelShareProperty, targetShare);
                    }
                    return;
                }

                ShowPanelChild(target);
                _openPanelChild = target;

                SnapValue(PanelShareProperty, targetShare);

                if (animate) AnimateValue(PanelProgressProperty, 1d, closing: false);
                else SnapValue(PanelProgressProperty, 1d);
            }
            else
            {
                if (!isOpen)
                {
                    if (!animate) SnapValue(PanelProgressProperty, 0d);
                    return;
                }

                _openPanelChild = null;

                if (animate)
                {
                    AnimateValue(PanelProgressProperty, 0d, closing: true, completed: (_, _) =>
                    {
                        if (_openPanelChild is not null) return;
                        if (DataContext is FullscreenWindowViewModel v && ShouldShowPanel(v)) return;

                        SnapValue(PanelProgressProperty, 0d);
                        HidePanelChildren();
                    });
                }
                else
                {
                    SnapValue(PanelProgressProperty, 0d);
                    HidePanelChildren();
                }

            }

        }

        private void HidePanelChildren()
        {
            LyricsView.Visibility = Visibility.Collapsed;
            QueueListPage.Visibility = Visibility.Collapsed;
        }

        private void ShowPanelChild(UIElement target)
        {
            LyricsView.Visibility = ReferenceEquals(target, LyricsView) ? Visibility.Visible : Visibility.Collapsed;
            QueueListPage.Visibility = ReferenceEquals(target, QueueListPage) ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool ShouldShowPanel(FullscreenWindowViewModel vm) => vm.ActivePanel switch
        {
            FullscreenPlayerPanel.Lyrics => LyricsView.HasLyrics,
            FullscreenPlayerPanel.Queue => true,
            _ => false
        };

        private UIElement ResolvePanelChild(FullscreenWindowViewModel vm) => vm.ActivePanel switch
        {
            FullscreenPlayerPanel.Queue => QueueListPage,
            _ => LyricsView,
        };


        private static double ShareFor(FullscreenWindowViewModel vm) => vm.ActivePanel switch
        {
            FullscreenPlayerPanel.Queue => 0.3,
            FullscreenPlayerPanel.Lyrics => 0.5,
            _ => 0.5,
        };

        private void RootFullScreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is FullscreenWindowViewModel vm)
            {
                vm.PlayerViewModel.PropertyChanged -= OnPlayerViewModelPropertyChanged;
                vm.PlayerViewModel.PropertyChanged += OnPlayerViewModelPropertyChanged;
            }

            ApplyPanelState(animate: false);
            ApplyProgress();
            RandomizeBackdrop();
        }
        private void RootFullScreenWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is FullscreenWindowViewModel vm)
            {
                vm.PlayerViewModel.PropertyChanged -= OnPlayerViewModelPropertyChanged;
            }

            BackdropRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        }
        private void OnPlayerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PlayerViewModel.CurrentTrackImagePath)) return;
            if (DataContext is not FullscreenWindowViewModel vm) return;
            if (string.IsNullOrEmpty(vm.PlayerViewModel.CurrentTrackImagePath)) return;

            RandomizeBackdrop();
        }

        private void RandomizeBackdrop()
        {
            double viewW = BackdropRoot.ActualWidth;
            double viewH = BackdropRoot.ActualHeight;
            double imgW = BackdropImage.ActualWidth;
            double imgH = BackdropImage.ActualHeight;
            if (viewW <= 0 || viewH <= 0 || imgW <= 0 || imgH <= 0) return;

            // Visible zone
            double visibleLeft = -BackdropImage.Margin.Left;
            double visibleTop = -BackdropImage.Margin.Top;

            // random image UV
            double u = 0.35 + Random.Shared.NextDouble() * 0.30; // 0.35..0.65
            double v = 0.35 + Random.Shared.NextDouble() * 0.30;
            var pImg = new Point(visibleLeft + u * viewW, visibleTop + v * viewH);

            // random window UV
            double su = 0.25 + Random.Shared.NextDouble() * 0.50; // 0.25..0.75
            double sv = 0.25 + Random.Shared.NextDouble() * 0.50;
            var pScr = new Point(su * viewW, sv * viewH);

            // Random angle
            double currentAngle = BackdropRotate.Angle;
            double direction = Math.Sign(backdropRPM);

            // Minimun Zoom
            double minZoom = MinZoomForFullRotation(pImg, pScr, 128, imgW, imgH, viewW, viewH);

            double zoom = Math.Max(minZoom, 1.3 + Random.Shared.NextDouble() * 0.5);

            BackdropScale.CenterX = pImg.X;
            BackdropScale.CenterY = pImg.Y;
            BackdropScale.ScaleX = zoom;
            BackdropScale.ScaleY = zoom;

            BackdropRotate.CenterX = pImg.X;
            BackdropRotate.CenterY = pImg.Y;

            BackdropTranslate.X = pScr.X - pImg.X - BackdropImage.Margin.Left;
            BackdropTranslate.Y = pScr.Y - pImg.Y - BackdropImage.Margin.Top;

            var duration = TimeSpan.FromSeconds(30 + Random.Shared.NextDouble() * 30);
            var animation = new DoubleAnimation
            {
                From = currentAngle,
                To = currentAngle + 360.0 * direction,
                Duration = TimeSpan.FromSeconds(60.0/Math.Abs(backdropRPM)),
                RepeatBehavior = RepeatBehavior.Forever,
            };

            BackdropRotate.BeginAnimation(
                RotateTransform.AngleProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static double MinZoomForFullRotation(
            Point pImg, Point pScr, double blurFade,
            double imgW, double imgH, double viewW, double viewH)
        {
            double maxCornerDistance = 0;
            var corners = new[]
            {
        new Point(0, 0),
        new Point(viewW, 0),
        new Point(0, viewH),
        new Point(viewW, viewH)
    };

            foreach (var corner in corners)
            {
                double dx = corner.X - pScr.X;
                double dy = corner.Y - pScr.Y;
                maxCornerDistance = Math.Max(maxCornerDistance, Math.Sqrt(dx * dx + dy * dy));
            }

            double edgeDistance = Math.Min(
                Math.Min(pImg.X, imgW - pImg.X),
                Math.Min(pImg.Y, imgH - pImg.Y)) - blurFade;

            if (edgeDistance <= 0) return double.PositiveInfinity;

            return maxCornerDistance / edgeDistance;
        }

        private void LyricsControl_LyricsStateChanged(object? sender, LyricsStateChangedEventArgs e) => ApplyPanelState(animate: true);

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }
    }
}
