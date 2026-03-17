using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && param.Equals("Visibility", StringComparison.OrdinalIgnoreCase))
            {
                if (value is bool boolparam)
                {
                    return boolparam ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
                }
                return System.Windows.Visibility.Visible;
            }

            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
