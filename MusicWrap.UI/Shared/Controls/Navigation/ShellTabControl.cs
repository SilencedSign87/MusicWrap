using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace MusicWrap.UI.Shared.Controls.Navigation
{
    [TemplatePart(Name = "HeaderPanel", Type = typeof(StackPanel))]
    [TemplatePart(Name = "SelectionIndicator", Type = typeof(FrameworkElement))]
    public class ShellTabControl : TabControl
    {
        private FrameworkElement? _indicator;
        private TranslateTransform? _indicatorTransform;
        private Window? _parentWindow;
        private bool _windowSuscribed;

        public static readonly DependencyProperty CompactWidthThresholdProperty =
            DependencyProperty.Register(nameof(CompactWidthThreshold), typeof(double), typeof(ShellTabControl),
                new PropertyMetadata(450.0, OnThresholdChanged));

        public double CompactWidthThreshold
        {
            get => (double)GetValue(CompactWidthThresholdProperty);
            set => SetValue(CompactWidthThresholdProperty, value);
        }

        private static readonly DependencyPropertyKey IsCompactPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsCompact), typeof(bool), typeof(ShellTabControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsCompactProperty = IsCompactPropertyKey.DependencyProperty;

        public bool IsCompact
        {
            get => (bool)GetValue(IsCompactProperty);
            private set => SetValue(IsCompactPropertyKey, value);
        }

        static ShellTabControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellTabControl),
                new FrameworkPropertyMetadata(typeof(ShellTabControl)));
        }

        public ShellTabControl()
        {
            Loaded += ShellTabControl_Loaded;
            Unloaded += ShellTabControl_Unloaded;
            SelectionChanged += ShellTabControl_SelectionChanged;
        }


        private void ShellTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            _parentWindow = Window.GetWindow(this);
            if (_parentWindow is not null && !_windowSuscribed)
            {
                _parentWindow.SizeChanged += OnParentWindowSizeChanged;
                _windowSuscribed = true;
            }

            UpdateIndicatorPosition(animate: false);
            UpdateCompactMode();
        }
        private void ShellTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateIndicatorPosition(animate: true);
        }
        private void OnParentWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateCompactMode();
        }
        private void ShellTabControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_parentWindow is not null && _windowSuscribed)
            {
                _parentWindow.SizeChanged -= OnParentWindowSizeChanged;
                _windowSuscribed = false;
            }
            _parentWindow = null;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _indicator = GetTemplateChild("SelectionIndicator") as FrameworkElement;
            if (_indicator != null)
            {
                _indicatorTransform = new TranslateTransform();
                _indicator.RenderTransform = _indicatorTransform;
                _indicator.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            Loaded += OnLoaded;
            SelectionChanged += OnSelectionChanged;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateIndicatorPosition(animate: false);
            UpdateCompactMode();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateIndicatorPosition(animate: true);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCompactMode();
            UpdateIndicatorPosition(animate: false);
        }

        private void UpdateCompactMode()
        {
            double availablewidth = _parentWindow?.ActualWidth ?? ActualWidth;
            bool compact = availablewidth > 0 && availablewidth < CompactWidthThreshold;
            if (compact != IsCompact)
            {
                IsCompact = compact;
                foreach (var item in Items)
                {
                    if (ItemContainerGenerator.ContainerFromItem(item) is ShellTabItem tabItem)
                    {
                        tabItem.SetCompactMode(compact);
                    }
                }
                UpdateIndicatorPosition(animate: false);
            }
        }

        private void UpdateIndicatorPosition(bool animate)
        {
            if (_indicator == null || _indicatorTransform == null)
                return;

            var container = ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as TabItem;
            if (container?.IsLoaded != true)
                return;

            var headerPanel = GetTemplateChild("HeaderPanel") as UIElement;
            if (headerPanel == null)
                return;

            Point pos;
            try
            {
                pos = container.TranslatePoint(new Point(0, 0), headerPanel);
            }
            catch
            {
                return;
            }

            double targetX = pos.X + (container.ActualWidth - _indicator.Width) / 2;

            if (animate)
            {
                var anim = new DoubleAnimation
                {
                    To = targetX,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                _indicatorTransform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            else
            {
                _indicatorTransform.X = targetX;
            }
        }

        private static void OnThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ShellTabControl)d).UpdateCompactMode();
        }
    }
}
