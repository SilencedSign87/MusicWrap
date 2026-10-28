using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Lyrics.View;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Shell.ViewModel;
using MusicWrap.UI.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

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
                Visibility = Visibility.Collapsed
            };

            QueueListPage.Visibility = Visibility.Collapsed;

            _panelHost.Children.Add(LyricsView);
            _panelHost.Children.Add(QueueListPage);
            PanelContainer.Child = _panelHost;

            DataContext = viewmodel;
            //viewmodel.PropertyChanged += OnViewmodelPropertyChanged;

            SizeChanged += (_, _) =>ApplyProgress();
            
        }

        #region Dependency Properties
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

        #endregion

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
                vm.PropertyChanged -= OnViewmodelPropertyChanged;
                vm.PropertyChanged += OnViewmodelPropertyChanged;
            }

            LyricsView.LyricsStateChanged -= LyricsControl_LyricsStateChanged;
            LyricsView.LyricsStateChanged += LyricsControl_LyricsStateChanged;

            ApplyPanelState(animate: false);
            ApplyProgress();
            ScaleUIElements();
        }
        private void RootFullScreenWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is FullscreenWindowViewModel vm)
            {
                vm.PlayerViewModel.PropertyChanged -= OnPlayerViewModelPropertyChanged;
                vm.PropertyChanged -= OnViewmodelPropertyChanged;
            }

            LyricsView.LyricsStateChanged -= LyricsControl_LyricsStateChanged;
        }
        private void OnPlayerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PlayerViewModel.CurrentTrackImagePath)) return;
            if (DataContext is not FullscreenWindowViewModel vm) return;
            if (string.IsNullOrEmpty(vm.PlayerViewModel.CurrentTrackImagePath)) return;
        }

        private void ScaleUIElements()
        {
            var dpi = VisualTreeHelper.GetDpi(this);

            ArtworkBorder.Height = 640 / dpi.DpiScaleY;
            ArtworkBorder.Width = 640 / dpi.DpiScaleX;

            SpectrumVisualizer.Height = 100 / dpi.DpiScaleY;
        }

        private void LyricsControl_LyricsStateChanged(object? sender, LyricsStateChangedEventArgs e) => ApplyPanelState(animate: true);

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumePopup.IsOpen = true;
        }
    }
}
