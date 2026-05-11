using System.Globalization;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public sealed class DiskNumberToGroupHeaderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int diskNumber)
            {
                return diskNumber <= 0 ? "Tracks" : $"Disk {diskNumber}";
            }

            return "Tracks";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}