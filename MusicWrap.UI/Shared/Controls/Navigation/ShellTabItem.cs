using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

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

        private void RefreshIcon()
        {
            if (_panel is null) return;
            
            bool hasIcon = !string.IsNullOrEmpty(Icon);

            if (hasIcon && _iconBlock is null) {
                _iconBlock = new TextBlock
                {
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _iconBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Icon)) { Source = this });
                _panel.Children.Insert(0, _iconBlock);
            }else if (!hasIcon && _iconBlock is not null)
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
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _labelBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });
                _panel.Children.Add(_labelBlock);
            }
            else if (!hasText && _labelBlock is not null)
            {
                _panel.Children.Remove(_labelBlock);
                _labelBlock = null;
            }
            RefreshLabelMargin();
        }
        private void RefreshLabelMargin()
        {
            if (_labelBlock is null) return;
            _labelBlock.Margin = _iconBlock is not null ? new Thickness(4, 0, 0, 0) : new Thickness(0);
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
