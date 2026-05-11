using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    public class SelectedAlbumBackgroundConverter : IMultiValueConverter
    {
        private static readonly SolidColorBrush SelectedBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
            {
                return Brushes.Transparent;
            }

            var albumId = values[0];
            var expandedAlbumId = values[1];

            if (albumId == null || expandedAlbumId == null || !albumId.Equals(expandedAlbumId))
            {
                return Brushes.Transparent;
            }

            return SelectedBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
