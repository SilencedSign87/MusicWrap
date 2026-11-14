using System.Globalization;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    internal class TabStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string targerTab)
            {
                return System.Windows.Visibility.Collapsed;
            }
            string currentTab = "";
            if (value is string str)
            {
                currentTab = str;
            }
            else
            {
                currentTab = value?.ToString() ?? "";
            }

            return string.Equals(currentTab, targerTab, StringComparison.OrdinalIgnoreCase) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
