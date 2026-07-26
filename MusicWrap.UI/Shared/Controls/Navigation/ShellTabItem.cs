using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace MusicWrap.UI.Shared.Controls.Navigation
{
    public class ShellTabItem : TabItem
    {
        private TextBlock? _iconBlock;
        private TextBlock? _labelBlock;
        private StackPanel? _panel;

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ShellTabItem),
                new PropertyMetadata(null, OnIconChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ShellTabItem), 
                new PropertyMetadata(null, OnTextChanged) );

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ShellTabItem),
                new PropertyMetadata(System.Windows.Controls.Orientation.Horizontal, OnOrientationChanged));

        public static readonly DependencyProperty IconSizeProperty =
           DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(ShellTabItem),
               new PropertyMetadata(16.0, OnVisualPropertyChanged));

        public static readonly DependencyProperty TextSizeProperty =
            DependencyProperty.Register(nameof(TextSize), typeof(double), typeof(ShellTabItem),
                new PropertyMetadata(12.0, OnVisualPropertyChanged));

        public static readonly DependencyProperty SpacingProperty =
            DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(ShellTabItem),
                new PropertyMetadata(4.0, OnVisualPropertyChanged));

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

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }
        public double TextSize
        {
            get => (double)GetValue(TextSizeProperty);
            set => SetValue(TextSizeProperty, value);
        }
        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        static ShellTabItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellTabItem),
                new FrameworkPropertyMetadata(typeof(ShellTabItem)));
        }

        public ShellTabItem()
        {
            _panel = new StackPanel {Orientation = Orientation.Horizontal };
            Header = _panel;
            Loaded += ShellTabItem_Loaded;
        }

        private void ShellTabItem_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshIcon();
            RefreshText();
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShellTabItem item)
            {
                item.RefreshIcon();
            }
        }
        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShellTabItem item)
            {
                item.RefreshText();
            }
        }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShellTabItem item)
                item.UpdateOrientation();
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShellTabItem item) item.ApplyVisualProperties();
        }

        private void ApplyVisualProperties()
        {
            if (_iconBlock is not null)
                _iconBlock.FontSize = IconSize;
            if (_labelBlock is not null)
                _labelBlock.FontSize = TextSize;

            RefreshLabelMargin();
        }

        private void UpdateOrientation()
        {
            if (_panel is null) return;
            _panel.Orientation = Orientation;
            ApplyChildAlignments();
            RefreshLabelMargin();
        }

        private void RefreshIcon()
        {
            if (_panel is null) return;
            
            bool hasIcon = !string.IsNullOrEmpty(Icon);

            if (hasIcon && _iconBlock is null) {
                _iconBlock = new TextBlock
                {
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = IconSize,
                };
                _iconBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Icon)) { Source = this });
                _panel.Children.Insert(0, _iconBlock);
            }else if (!hasIcon && _iconBlock is not null)
            {
                _panel.Children.Remove(_iconBlock);
                _iconBlock = null;
            }

            ApplyChildAlignments();
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
                    FontSize = TextSize,
                    FontWeight = FontWeights.SemiBold,
                };
                _labelBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
                _panel.Children.Add(_labelBlock);
            }
            else if (!hasText && _labelBlock is not null)
            {
                _panel.Children.Remove(_labelBlock);
                _labelBlock = null;
            }

            ApplyChildAlignments();
            RefreshLabelMargin();
        }

        private void ApplyChildAlignments()
        {
            foreach (var child in new[] { _iconBlock, _labelBlock })
            {
                if (child is null) continue;
                if (Orientation == System.Windows.Controls.Orientation.Horizontal)
                {
                    child.VerticalAlignment = VerticalAlignment.Center;
                    child.HorizontalAlignment = HorizontalAlignment.Center;
                }
                else
                {
                    child.HorizontalAlignment = HorizontalAlignment.Center;
                    child.VerticalAlignment = VerticalAlignment.Center;
                }
            }
        }

        private void RefreshLabelMargin()
        {
            if (_labelBlock is null) return;
            _labelBlock.Margin = _iconBlock is not null
                ? Orientation == System.Windows.Controls.Orientation.Horizontal
                    ? new Thickness(Spacing, 0, 0, 0)
                    : new Thickness(0, Spacing, 0, 0)
                : new Thickness(0);
        }

        internal void SetCompactMode(bool compact)
        {
            if (_labelBlock is not null)
            {
                _labelBlock.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}
