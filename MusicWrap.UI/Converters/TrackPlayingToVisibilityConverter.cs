using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public sealed class TrackPlayingToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is null || values.Length < 3)
            {
                return Visibility.Collapsed;
            }

            if (!TryToInt(values[0], out var currentTrackId) || !TryToInt(values[1], out var rowTrackId))
            {
                return Visibility.Collapsed;
            }

            var isPlaybackActive = values[2] is bool b && b;
            if (!isPlaybackActive)
            {
                return Visibility.Collapsed;
            }

            return currentTrackId == rowTrackId && currentTrackId > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static bool TryToInt(object value, out int result)
        {
            if (value is int i)
            {
                result = i;
                return true;
            }

            if (value is string s && int.TryParse(s, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = 0;
            return false;
        }
    }
}
