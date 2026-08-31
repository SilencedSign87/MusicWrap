using MusicWrap.UI.Helpers;
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

            if (!CastingHelpers.TryToInt(values[0], out var currentTrackId) || !CastingHelpers.TryToInt(values[1], out var rowTrackId))
            {
                return Visibility.Collapsed;
            }

            var isPlaybackActive = values[2] is bool b && b;
            if (!isPlaybackActive)
            {
                return Visibility.Collapsed;
            }

            var isPlaying = currentTrackId == rowTrackId && currentTrackId > 0;
            var negate = parameter is string s && s.Equals("negative", StringComparison.OrdinalIgnoreCase);

            if (negate)
                return isPlaying ? Visibility.Collapsed : Visibility.Visible;

            return isPlaying ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
