using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Shared.Controls.Navigation
{
    public class ShellTabItem : TabItem
    {
        private TextBlock? _labelText;

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ShellTabItem));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ShellTabItem));

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
            // Build default header: Icon + Text
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var iconBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Icon)) { Source = this });

            _labelText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _labelText.SetBinding(TextBlock.TextProperty, new Binding(nameof(Text)) { Source = this });

            panel.Children.Add(iconBlock);
            panel.Children.Add(_labelText);
            Header = panel;
        }

        internal void SetCompactMode(bool compact)
        {
            if (_labelText != null)
            {
                _labelText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}
