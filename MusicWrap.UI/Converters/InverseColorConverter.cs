using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    class InverseColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string hex || ColorConverter.ConvertFromString(hex) is not Color color)
            {
                return Colors.Transparent;
            }

            if (double.TryParse(parameter?.ToString(), out double alpha))
            {
                return Color.FromArgb(
                    (byte)(alpha * 255),
                    (byte)(255 - color.R),
                    (byte)(255 - color.G),
                    (byte)(255 - color.B));
            }

            return Color.FromArgb(
                color.A,
                (byte)(255 - color.R),
                (byte)(255 - color.G),
                (byte)(255 - color.B));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
