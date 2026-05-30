using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public class ItemCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            {
                if (value is null)
                    return "Visible";
                if (value is int val)
                {
                    return val > 0 ? "Collapsed" : "Visible";
                }
                return "Visible";
            }else
            {
                if (value is null)
                    return "Collapsed";
                if (value is int val)
                {
                    return val > 0 ? "Visible" : "Collapsed";
                }
                return "Collapsed";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
