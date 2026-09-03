using MusicWrap.UI.Features.Lyrics.View;
using MusicWrap.UI.Features.Playback.ViewModels;
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
        public FullScreenWindow(FullscreenWindowViewModel viewmodel)
        {
            InitializeComponent();

            DataContext = viewmodel;
            viewmodel.NowPlayingViewModel.PropertyChanged += OnViewmodelPropertyChanged;
        }

        private void OnViewmodelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NowPlayingViewModel.ShowLyrics))
            {
                UpdateLyricsLayout(animate: true);
            }
        }

        private void UpdateLyricsLayout(bool animate)
        {
            if (DataContext is not FullscreenWindowViewModel vm) return;

            bool userWantsLyrics = vm.NowPlayingViewModel.ShowLyrics;
            bool hasValidLyrics = LyricsControl.HasLyrics;
            bool shouldShow = userWantsLyrics && hasValidLyrics;

            double targetWidth = shouldShow ? (ActualWidth > 0 ? ActualWidth / 2.0 : 500) : 0;
            double targetOpacity = shouldShow ? 1.0 : 0.0;


            if (!animate || ActualWidth <= 0)
            {
                LyricsContainer.BeginAnimation(FrameworkElement.WidthProperty, null);
                LyricsContainer.BeginAnimation(UIElement.OpacityProperty, null);
                LyricsContainer.Width = targetWidth;
                LyricsContainer.Opacity = targetOpacity;
                return;
            }

            var duration = TimeSpan.FromMilliseconds(350);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var widthAnimation = new DoubleAnimation(targetWidth, duration) { EasingFunction = ease };
            var opacityAnimation = new DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(250));

            LyricsContainer.BeginAnimation(FrameworkElement.WidthProperty, widthAnimation);
            LyricsContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnimation);
        }

        private void RootFullScreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLyricsLayout(animate: false);
        }

        private void LyricsControl_LyricsStateChanged(object sender, LyricsStateChangedEventArgs e)
        {
            UpdateLyricsLayout(animate: true);
        }
    }
}
