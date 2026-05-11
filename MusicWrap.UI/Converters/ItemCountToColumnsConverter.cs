using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public class ItemCountToColumnsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length < 2) return 1;

            if (!int.TryParse(values[0]?.ToString() ?? "0", out int itemCount)) return 1;
            if (!double.TryParse(values[1]?.ToString() ?? "0", out double availableWidth)) return 1;

            int minimumItems = 10;
            double minimumWidth = 800.0;

            if (parameter is string paramStr)
            {
                var parts = paramStr.Split(',');
                if (parts.Length >= 1 && int.TryParse(parts[0], out int minItems))
                    minimumItems = minItems;
                if (parts.Length >= 2 && double.TryParse(parts[1], out double minWidth))
                    minimumWidth = minWidth;
            }

            // Return 2 columns if: wide screen AND enough items
            if (availableWidth > minimumWidth && itemCount > minimumItems)
                return 2;

            return 1;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
