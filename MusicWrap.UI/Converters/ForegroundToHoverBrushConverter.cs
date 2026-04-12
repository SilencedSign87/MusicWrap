using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    public sealed class ForegroundToHoverBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not SolidColorBrush foreground)
            {
                return new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            }

            var color = foreground.Color;
            return new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}