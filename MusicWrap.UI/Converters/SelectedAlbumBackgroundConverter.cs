using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    public class SelectedAlbumBackgroundConverter : IMultiValueConverter
    {
        private const byte SelectedAlpha = 0x33; // ~20% opacity

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
            {
                return Brushes.Transparent;
            }

            var albumId = values[0];
            var expandedAlbumId = values[1];
            var dominantColorHex = values[2] as string;

            if (albumId == null || expandedAlbumId == null || !albumId.Equals(expandedAlbumId))
            {
                return Brushes.Transparent;
            }

            if (string.IsNullOrWhiteSpace(dominantColorHex))
            {
                return Brushes.Transparent;
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(dominantColorHex);
                color.A = SelectedAlpha;
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
