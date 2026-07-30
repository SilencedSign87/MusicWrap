using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public class NullToDoNothingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
       => value; // pass value
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value ?? Binding.DoNothing; // not update source if value is null
    }
}
