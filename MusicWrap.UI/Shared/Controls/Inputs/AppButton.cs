

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Controls
{
    public class AppButton : Button
    {
        private TextBlock? _iconBlock;
        private TextBlock? _labelBlock;
        private StackPanel? _panel;

        #region Dependency Properties
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(AppButton),
                new PropertyMetadata(null, OnIconChanged));
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(AppButton),
                new PropertyMetadata(null, OnTextChanged));
        public static readonly DependencyProperty IconFontSizeProperty =
            DependencyProperty.Register(nameof(IconFontSize), typeof(double), typeof(AppButton),
                new PropertyMetadata(16.0, OnVisualPropertyChanged));
        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(nameof(TextFontSize), typeof(double), typeof(AppButton),
                new PropertyMetadata(14.0, OnVisualPropertyChanged));
        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(AppButton),
                new PropertyMetadata(4.0, OnVisualPropertyChanged));
        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(System.Windows.Controls.Orientation), typeof(AppButton),
                new PropertyMetadata(System.Windows.Controls.Orientation.Horizontal, OnOrientationChanged));
        public static readonly DependencyProperty IsSquareProperty =
            DependencyProperty.Register(nameof(IsSquare), typeof(bool), typeof(AppButton),
                new PropertyMetadata(false, OnVisualPropertyChanged));
        public string? Icon
        {
            get => (string?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
        public string? Text
        {
            get => (string?)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }
        public double IconFontSize
        {
            get => (double)GetValue(IconFontSizeProperty);
            set => SetValue(IconFontSizeProperty, value);
        }
        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }
        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }
        public System.Windows.Controls.Orientation Orientation
        {
            get => (System.Windows.Controls.Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }
        public bool IsSquare
        {
            get => (bool)GetValue(IsSquareProperty);
            set => SetValue(IsSquareProperty, value);
        }
        #endregion

        public AppButton()
        {
            _panel = new StackPanel { Orientation = Orientation };
            Content = _panel;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshIcon();
            RefreshText();
        }
        #region Static Callbacks
        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((AppButton)d).RefreshIcon();
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((AppButton)d).RefreshText();
        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((AppButton)d).ApplyVisualProperties();
        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var btn = (AppButton)d;
            if (btn._panel is not null)
                btn._panel.Orientation = (System.Windows.Controls.Orientation)e.NewValue;
            btn.RefreshLabelMargin();
        }
        #endregion

        #region Content Builders
        private void RefreshIcon()
        {
            if (_panel is null) return;
            bool hasIcon = !string.IsNullOrEmpty(Icon);
            if (hasIcon && _iconBlock is null)
            {
                _iconBlock = new TextBlock
                {
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _iconBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Icon)) { Source = this });
                _iconBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(IconFontSize)) { Source = this });
                _panel.Children.Insert(0, _iconBlock);
            }
            else if (!hasIcon && _iconBlock is not null)
            {
                _panel.Children.Remove(_iconBlock);
                _iconBlock = null;
            }
            RefreshLabelMargin();
        }
        private void RefreshText()
        {
            if (_panel is null) return;
            bool hasText = !string.IsNullOrEmpty(Text);
            if (hasText && _labelBlock is null)
            {
                _labelBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _labelBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
                _labelBlock.SetBinding(TextBlock.FontSizeProperty, new Binding(nameof(TextFontSize)) { Source = this });
                _panel.Children.Add(_labelBlock);
            }
            else if (!hasText && _labelBlock is not null)
            {
                _panel.Children.Remove(_labelBlock);
                _labelBlock = null;
            }
            RefreshLabelMargin();
        }
        private void ApplyVisualProperties()
        {
            RefreshLabelMargin();
            InvalidateMeasure();
        }
        private void RefreshLabelMargin()
        {
            if (_labelBlock is null) return;
            double gap = _iconBlock is not null ? Spacing : 0;
            if (_panel?.Orientation == System.Windows.Controls.Orientation.Vertical)
                _labelBlock.Margin = new Thickness(0, gap, 0, 0);
            else
                _labelBlock.Margin = new Thickness(gap, 0, 0, 0);
        }
        #endregion

        #region Layout
        protected override Size MeasureOverride(Size constraint)
        {
            var desired = base.MeasureOverride(constraint);
            if (IsSquare)
            {
                double side = Math.Max(desired.Width, desired.Height);
                // No exceder el espacio disponible
                if (!double.IsInfinity(constraint.Width))
                    side = Math.Min(side, constraint.Width);
                if (!double.IsInfinity(constraint.Height))
                    side = Math.Min(side, constraint.Height);
                side = Math.Max(0, side);
                return new Size(side, side);
            }
            return desired;
        }
        #endregion
    }
}
