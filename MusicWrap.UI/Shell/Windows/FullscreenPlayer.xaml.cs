using MusicWrap.Data.User.Models;
using MusicWrap.UI.Features.Lyrics.View;
using MusicWrap.UI.Features.Playback.ViewModels;
using MusicWrap.UI.Features.Playback.Views;
using MusicWrap.UI.Shell.ViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace MusicWrap.UI.Shell.Windows
{
    /// <summary>
    /// Lógica de interacción para FullScreenWindow.xaml
    /// </summary>
    public partial class FullScreenWindow : UserControl
    {
        private LyricsView LyricsView;
        private QueueListPage QueueListPage;
        public FullScreenWindow(FullscreenWindowViewModel viewmodel, QueueListPage queueListPage)
        {
            InitializeComponent();

            QueueListPage = queueListPage;
            LyricsView = new LyricsView
            {
                FontSize = 36,
                AllowScroll = false,
                AllowSeek = false,
                ShowToolbar = false,
                LyricsAligment = TextAlignment.Left,
            };
            LyricsView.LyricsStateChanged += LyricsControl_LyricsStateChanged;


            DataContext = viewmodel;
            viewmodel.PropertyChanged += OnViewmodelPropertyChanged;
        }

        private void OnViewmodelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FullscreenWindowViewModel.ActivePanel))
            {
                UpdateLayout(animate: true);
            }
        }

        private void UpdateLayout(bool animate)
        {
            if (DataContext is not FullscreenWindowViewModel vm) return;

            bool shouldShow;

            switch (vm.ActivePanel) {
                case FullscreenPlayerPanel.Queue:
                    shouldShow = true;
                    break;
                case FullscreenPlayerPanel.Lyrics:
                    bool lyricsAvailable = LyricsView.HasLyrics;
                    shouldShow = lyricsAvailable;
                    break;
                default:
                    shouldShow = false;
                    break;
            }
            
            double targetWidth = shouldShow ? (ActualWidth > 0 ? (ActualWidth / 2.0) : 500) : 0;
            double targetOpacity = shouldShow ? 1.0 : 0.0;

            PanelContainer.Child = vm.ActivePanel switch
            {
                FullscreenPlayerPanel.Queue => QueueListPage,
                FullscreenPlayerPanel.Lyrics => LyricsView,
                _ => null
            };

            if (!animate || ActualWidth <= 0)
            {
                PanelContainer.BeginAnimation(FrameworkElement.WidthProperty, null);
                PanelContainer.BeginAnimation(UIElement.OpacityProperty, null);
                PanelContainer.Width = targetWidth;
                PanelContainer.Opacity = targetOpacity;
                return;
            }

            var duration = TimeSpan.FromMilliseconds(250);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var widthAnimation = new DoubleAnimation(targetWidth, duration) { EasingFunction = ease };
            var opacityAnimation = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(200));

            PanelContainer.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
            PanelContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        private void RootFullScreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLayout(animate: false);
        }

        private void LyricsControl_LyricsStateChanged(object? sender, LyricsStateChangedEventArgs e)
        {
            UpdateLayout(animate: true);
        }
    }
}
