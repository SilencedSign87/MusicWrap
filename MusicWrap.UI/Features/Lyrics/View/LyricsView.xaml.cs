using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Services.Lyrics;
using MusicWrap.UI.Features.Lyrics.Viewmodel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace MusicWrap.UI.Features.Lyrics.View
{
    public partial class LyricsView : UserControl
    {
        private const float VerticalLyricsMarginPercent = 0.2f;
        private bool _followLyrics = true;
        private readonly LyricsViewModel _viewModel;
        private readonly BlurEffect _textBlurEffect = new() { Radius = 4 };

        public bool HasLyrics => _viewModel?.HasLyrics ?? false;
        public bool HasSyncedLyrics => _viewModel?.HasSyncedLyrics ?? false;
        public event EventHandler<LyricsStateChangedEventArgs>? LyricsStateChanged;

        private bool _hasBeenShown;

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



        public bool BlurLyrics
        {
            get { return (bool)GetValue(BlurLyricsProperty); }
            set { SetValue(BlurLyricsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for BlurLyrics.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty BlurLyricsProperty =
            DependencyProperty.Register(nameof(BlurLyrics), typeof(bool), typeof(LyricsView), new PropertyMetadata(false));



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



        public Thickness LyricsPadding
        {
            get { return (Thickness)GetValue(LyricsPaddingProperty); }
            set { SetValue(LyricsPaddingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LyricsPadding.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LyricsPaddingProperty =
            DependencyProperty.Register(nameof(LyricsPadding), typeof(Thickness), typeof(LyricsView), new PropertyMetadata(new Thickness(24, 48, 24, 48)));



        public LyricsVerticalAlignment LyricsVerticalAlignment
        {
            get { return (LyricsVerticalAlignment)GetValue(LyricsVerticalAlignmentProperty); }
            set { SetValue(LyricsVerticalAlignmentProperty, value); }
        }

        public static readonly DependencyProperty LyricsVerticalAlignmentProperty =
            DependencyProperty.Register(nameof(LyricsVerticalAlignment), typeof(LyricsVerticalAlignment), typeof(LyricsView), new PropertyMetadata(LyricsVerticalAlignment.Center, OnLyricsAlignmentPropertyChanged));

        private static void OnLyricsAlignmentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LyricsView lyricsView)
            {
                lyricsView.AlignLyrics();
            }
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.PropertyChanged += OnViewmodelPropertyChanged;
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
                    RaiseLyricsStateChanged();
                    break;
                case nameof(LyricsViewModel.HasLyrics):
                case nameof(LyricsViewModel.HasSyncedLyrics):
                    RaiseLyricsStateChanged();
                    break;
                case nameof(LyricsViewModel.AvailableLyrics):
                    RefreshVariantsCombobox();
                    break;
            }
        }

        private void RefreshVariantsCombobox()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                var template = LyricsVariantCombobox.Template;
                LyricsVariantCombobox.Template = null;
                LyricsVariantCombobox.ApplyTemplate();
                LyricsVariantCombobox.Template = template;
                LyricsVariantCombobox.Items.Refresh();
            }));
        }

        private void RaiseLyricsStateChanged()
        {
            LyricsStateChanged?.Invoke(this, new LyricsStateChangedEventArgs(HasLyrics, HasSyncedLyrics, _viewModel?.Lyrics));
        }
        private void OnActiveIndexChanged()
        {
            UpdateLineOpacities();

            if (!_followLyrics) return;

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => AlignLyrics()));
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
                    AlignLyrics();
                }));
        }
        private void UpdateSpacers()
        {

            var height = LyricsVerticalAlignment == LyricsVerticalAlignment.Center ? LyricsScrollViewer.ViewportHeight / 2.0 : LyricsScrollViewer.ViewportHeight * VerticalLyricsMarginPercent;

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
            UpdateLineOpacities();
        }

        private void HandleResumeFollow(object sender, RoutedEventArgs e)
        {
            _followLyrics = true;

            AlignLyrics();
            UpdateLineOpacities();
        }
        #region Rendering
        private void AlignLyrics(bool animate = true)
        {
            switch (LyricsVerticalAlignment)
            {
                case LyricsVerticalAlignment.Top:
                    TopActiveLine(animate);
                    break;
                case LyricsVerticalAlignment.Center:
                    CenterActiveLine(animate);
                    break;
            }
        }
        private void TopActiveLine(bool animate = true)
        {
            if (!_followLyrics)
                return;
            if (_viewModel == null)
                return;
            if (!IsVisible || !LyricsScrollViewer.IsVisible) return;
            if (LyricsScrollViewer.ViewportHeight <= 0)
                return;
            var index = _viewModel.ActiveIndex;
            if (index < 0 || index >= LyricsItems.Items.Count)
                return;
            if (LyricsItems.ItemContainerGenerator
                    .ContainerFromIndex(index) is not FrameworkElement container)
            {
                return;
            }
            if (container.ActualHeight <= 0)
            {
                return;
            }
            var transform =
                container.TransformToAncestor(
                    LyricsScrollViewer);

            var position =
                transform.Transform(new Point(0, 0));

            var margin = LyricsScrollViewer.ViewportHeight * VerticalLyricsMarginPercent;

            var targetOffset =
                LyricsScrollViewer.VerticalOffset +
                position.Y - margin;

            var maxOffset =
                LyricsScrollViewer.ScrollableHeight;

            targetOffset =
                Math.Clamp(
                    targetOffset,
                    0,
                    maxOffset);

            AnimateScrollTo(targetOffset, animate);
        }
        private void CenterActiveLine(bool animate = true)
        {
            if (!_followLyrics)
                return;

            if (_viewModel == null)
                return;

            if (!IsVisible || !LyricsScrollViewer.IsVisible) return;

            if (LyricsScrollViewer.ViewportHeight <= 0)
                return;

            var index = _viewModel.ActiveIndex;

            if (index < 0 || index >= LyricsItems.Items.Count)
                return;

            if (LyricsItems.ItemContainerGenerator
                    .ContainerFromIndex(index) is not FrameworkElement container)
            {
                return;
            }

            if (container.ActualHeight <= 0)
            {

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

            AnimateScrollTo(targetOffset, animate);
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
                    if (BlurLyrics)
                        container.Effect = _followLyrics ? (i == activeIdx ? null : _textBlurEffect) : null;
                }
            }
        }
        #endregion
        #region Animation
        private void AnimateScrollTo(double targetOffset, bool animate = true)
        {
            if (!animate || !IsVisible)
            {
                LyricsScrollViewer.ScrollToVerticalOffset(targetOffset);
                return;
            }
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

        private void LyricsViewRoot_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!IsVisible) return;

            var animate = _hasBeenShown;
            _hasBeenShown = true;

            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    UpdateSpacers();
                    UpdateLineOpacities();
                    AlignLyrics(animate);
                }));

        }
    }
    public enum LyricsVerticalAlignment
    {
        Top,
        Center
    }
    public class LyricsStateChangedEventArgs : EventArgs
    {
        public bool HasLyrics { get; }
        public bool HasSyncedLyrics { get; }
        public ParsedLyrics? Lyrics { get; }

        public LyricsStateChangedEventArgs(bool hasLyrics, bool hasSyncedLyrics, ParsedLyrics? lyrics)
        {
            HasLyrics = hasLyrics;
            HasSyncedLyrics = hasSyncedLyrics;
            Lyrics = lyrics;
        }
    }
}
