using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.Lyrics.Viewmodel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Lyrics.View
{
    public partial class LyricsView : UserControl
    {
        private bool _followLyrics = true;
        private readonly LyricsViewModel _viewModel;
        private BlurEffect _textBlurEffect = new() { Radius = 3 };
        public LyricsView()
        {
            InitializeComponent();
            var viewModel = App.Services.GetRequiredService<LyricsViewModel>();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        #region Dependency Properties

        public bool ShowToolbar
        {
            get { return (bool)GetValue(ShowToolbarProperty); }
            set { SetValue(ShowToolbarProperty, value); }
        }

        public static readonly DependencyProperty ShowToolbarProperty =
            DependencyProperty.Register(nameof(ShowToolbar), typeof(bool), typeof(LyricsView), new PropertyMetadata(true));

        public bool AllowScroll
        {
            get { return (bool)GetValue(AllowScrollProperty); }
            set { SetValue(AllowScrollProperty, value); }
        }

        public static readonly DependencyProperty AllowScrollProperty =
            DependencyProperty.Register(nameof(AllowScroll), typeof(bool), typeof(LyricsView), new PropertyMetadata(true));



        public bool AllowSeek
        {
            get { return (bool)GetValue(AllowSeekProperty); }
            set { SetValue(AllowSeekProperty, value); }
        }

        public static readonly DependencyProperty AllowSeekProperty =
            DependencyProperty.Register(nameof(AllowSeek), typeof(bool), typeof(LyricsView), new PropertyMetadata(false));



        public TextAlignment LyricsAligment
        {
            get { return (TextAlignment)GetValue(LyricsAligmentProperty); }
            set { SetValue(LyricsAligmentProperty, value); }
        }

        public static readonly DependencyProperty LyricsAligmentProperty =
            DependencyProperty.Register(nameof(LyricsAligment), typeof(TextAlignment), typeof(LyricsView), new PropertyMetadata(TextAlignment.Center));


        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

            _viewModel.PropertyChanged += OnViewmodelPropertyChanged;

            UpdateSpacers();

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(UpdateLineOpacities));

            Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(CenterActiveLine));
        }


        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -=
                OnViewmodelPropertyChanged;
        }
        private void OnViewmodelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(LyricsViewModel.ActiveIndex):
                    OnActiveIndexChanged();
                    break;
                case nameof(LyricsViewModel.Lyrics):
                    OnLyricsChanged();
                    break;
            }
        }
        private void OnActiveIndexChanged()
        {
            UpdateLineOpacities();

            if (!_followLyrics) return;

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(CenterActiveLine));
        }
        private void OnLyricsChanged()
        {
            _followLyrics = true;
            LyricsScrollViewer.ScrollToVerticalOffset(0);

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    UpdateSpacers();
                    UpdateLineOpacities();
                    CenterActiveLine();
                }));
        }
        private void UpdateSpacers()
        {
            var height =
            LyricsScrollViewer.ViewportHeight / 2.0;

            if (height <= 0)
                return;

            TopSpacer.Height = height;
            BottomSpacer.Height = height;
        }
        private void LyricsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!AllowScroll)
            {
                e.Handled = true;
                return;
            }
            DisableFollow();
        }

        private void LyricsScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            //DisableFollow();
        }

        private void LyricsScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!AllowSeek) return;

            var container = ItemsControl.ContainerFromElement(LyricsItems, (DependencyObject)sender);
            if (container is null) return;

            var index = LyricsItems.ItemContainerGenerator.IndexFromContainer(container);

            if (index >= 0 && _viewModel.CanSeek)
            {
                _followLyrics = true;
                _viewModel.SeekToLineCommand.Execute(index);
            }
        }

        private void LyricsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateSpacers();
        }
        private void DisableFollow()
        {
            if (!_followLyrics) return;
            _followLyrics = false;
        }

        private void HandleResumeFollow(object sender, RoutedEventArgs e)
        {
            _followLyrics = true;

            CenterActiveLine();
        }
        #region Rendering
        private void CenterActiveLine()
        {
            if (!_followLyrics)
                return;

            if (_viewModel == null)
                return;

            var index = _viewModel.ActiveIndex;

            if (index < 0)
                return;

            if (index >= LyricsItems.Items.Count)
                return;


            if (LyricsItems.ItemContainerGenerator
                    .ContainerFromIndex(index) is not FrameworkElement container)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(CenterActiveLine));

                return;
            }

            if (container.ActualHeight <= 0)
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(CenterActiveLine));

                return;
            }

            var transform =
                container.TransformToAncestor(
                    LyricsScrollViewer);

            var position =
                transform.Transform(new Point(0, 0));

            var itemCenter =
                position.Y +
                container.ActualHeight / 2.0;

            var viewportCenter =
                LyricsScrollViewer.ViewportHeight / 2.0;

            var targetOffset =
                LyricsScrollViewer.VerticalOffset +
                itemCenter -
                viewportCenter;

            var maxOffset =
                LyricsScrollViewer.ScrollableHeight;

            targetOffset =
                Math.Clamp(
                    targetOffset,
                    0,
                    maxOffset);

            AnimateScrollTo(targetOffset);
        }

        private void UpdateLineOpacities()
        {
            var activeIdx = _viewModel?.ActiveIndex ?? -1;
            var count = LyricsItems.Items.Count;

            for (int i = 0; i < count; i++)
            {
                if (LyricsItems.ItemContainerGenerator
                    .ContainerFromIndex(i) is FrameworkElement container)
                {
                    container.Opacity = i == activeIdx ? 1.0 : 0.3;
                    container.Effect = i == activeIdx ? null : _textBlurEffect;
                }
            }
        }
        #endregion
        #region Animation
        private void AnimateScrollTo(double targetOffset)
        {
            var animation = new DoubleAnimation
            {
                From = LyricsScrollViewer.VerticalOffset,
                To = targetOffset,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            BeginAnimation(
                AnimatedScrollOffsetProperty,
                animation);
        }
        private double AnimatedScrollOffset
        {
            get => (double)GetValue(AnimatedScrollOffsetProperty);
            set => SetValue(AnimatedScrollOffsetProperty, value);
        }

        private static readonly DependencyProperty AnimatedScrollOffsetProperty =
            DependencyProperty.Register(
                nameof(AnimatedScrollOffset),
                typeof(double),
                typeof(LyricsView),
                new PropertyMetadata(0.0, OnAnimatedScrollOffsetChanged));

        private static void OnAnimatedScrollOffsetChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var view = (LyricsView)d;

            view.LyricsScrollViewer.ScrollToVerticalOffset(
                (double)e.NewValue);
        }
        #endregion
    }
}
