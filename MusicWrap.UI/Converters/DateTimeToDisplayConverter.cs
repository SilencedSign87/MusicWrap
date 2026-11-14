using System.Globalization;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public class DateTimeToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                if (dt == DateTime.MinValue)
                {
                    return "Never";
                }
                return dt.ToString("g", culture);
            }
            return "Never";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
