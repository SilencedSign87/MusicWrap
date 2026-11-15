using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    public sealed class ConditionalColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
                return Colors.Transparent;

            var tintForeground = values[0] is true;
            var highlightColor = values[1] as string;
            var dominantForegroundColor = values[2] as string;

            var selectedColor = tintForeground
                ? highlightColor
                : dominantForegroundColor;

            return string.IsNullOrWhiteSpace(selectedColor)
                ? Colors.Transparent
                : System.Windows.Media.ColorConverter.ConvertFromString(selectedColor);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
