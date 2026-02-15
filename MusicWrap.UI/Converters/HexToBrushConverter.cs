using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor && !string.IsNullOrWhiteSpace(hexColor))
            {
                try
                {
                    if (!hexColor.StartsWith("#"))
                        hexColor = "#" + hexColor;

                    var color = (Color)ColorConverter.ConvertFromString(hexColor);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return new SolidColorBrush(Color.FromRgb(26, 26, 26));
                }
            }

            return new SolidColorBrush(Color.FromRgb(26, 26, 26));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
