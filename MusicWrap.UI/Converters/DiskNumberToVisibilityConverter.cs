using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public class DiskNumberToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int diskNumber)
            {
                // Show disk header only if disk number is greater than 0 (multi-disk albums)
                return diskNumber > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
