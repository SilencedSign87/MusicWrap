using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace MusicWrap.UI.Shared.Controls.Navigation
{
    [TemplatePart(Name = "HeaderPanel", Type = typeof(StackPanel))]
    public class ShellTabControl : TabControl
    {
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
        private bool IsCompactModeEnabled =>
            CompactWidthThreshold > 0 && !double.IsNaN(CompactWidthThreshold) &&
            !double.IsInfinity(CompactWidthThreshold);
        static ShellTabControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellTabControl),
                new FrameworkPropertyMetadata(typeof(ShellTabControl)));
        }
        public ShellTabControl()
        {
            Loaded += ShellTabControl_Loaded;
            Unloaded += ShellTabControl_Unloaded;
        }
        private void ShellTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindowSubscription();
            UpdateCompactMode();
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

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateCompactMode();
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCompactMode();
        }
        private void UpdateCompactMode()
        {
            if (!IsCompactModeEnabled)
            {
                if (IsCompact)
                {
                    IsCompact = false;
                    foreach (var item in Items)
                    {
                        if (ItemContainerGenerator.ContainerFromItem(item) is ShellTabItem tabItem)
                            tabItem.SetCompactMode(false);
                    }
                }
                return;
            }
            double availableWidth = _parentWindow?.ActualWidth ?? ActualWidth;
            bool compact = availableWidth > 0 && availableWidth < CompactWidthThreshold;
            if (compact != IsCompact)
            {
                IsCompact = compact;
                foreach (var item in Items)
                {
                    if (ItemContainerGenerator.ContainerFromItem(item) is ShellTabItem tabItem)
                        tabItem.SetCompactMode(compact);
                }
            }
        }
        private void UpdateWindowSubscription()
        {
            var window = Window.GetWindow(this);
            if (window is null)
            {
                _parentWindow = null;
                _windowSuscribed = false;
                return;
            }
            bool shouldSubscribe = IsCompactModeEnabled;
            if (shouldSubscribe && !_windowSuscribed)
            {
                window.SizeChanged += OnParentWindowSizeChanged;
                _windowSuscribed = true;
                _parentWindow = window;
            }
            else if (!shouldSubscribe && _windowSuscribed)
            {
                window.SizeChanged -= OnParentWindowSizeChanged;
                _windowSuscribed = false;
                _parentWindow = null;
            }
        }
        private static void OnThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ShellTabControl)d;
            control.UpdateWindowSubscription();
            control.UpdateCompactMode();
        }
    }
}
