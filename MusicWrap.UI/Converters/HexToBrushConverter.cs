using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    public class HexToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush _defaultBackgroundBrush;
        private static readonly SolidColorBrush _defaultForegroundBrush;

        static HexToBrushConverter()
        {
            _defaultBackgroundBrush = new(Color.FromRgb(26, 26, 26));
            _defaultBackgroundBrush.Freeze();
            _defaultForegroundBrush = new(Color.FromRgb(255, 255, 255));
            _defaultForegroundBrush.Freeze();

        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isForeground = parameter is string param &&
                        (param.Equals("foreground", StringComparison.OrdinalIgnoreCase) ||
                         param.Equals("fg", StringComparison.OrdinalIgnoreCase));

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
                    return isForeground ? _defaultForegroundBrush : _defaultBackgroundBrush;
                }
            }

            return isForeground ? _defaultForegroundBrush : _defaultBackgroundBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
