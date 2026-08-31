using MusicWrap.UI.Controls.Models;
using MusicWrap.UI.Helpers;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicWrap.UI.Converters
{
    public sealed class TrackPlayingSlotVisibilityConverter : IMultiValueConverter
    {
        // values[0]=CurrentTrackId, [1]=Id, [2]=IsPlaybackActive, [3]=IndexMode
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3) return Visibility.Collapsed;
            if (!CastingHelpers.TryToInt(values[0], out var cur) || !CastingHelpers.TryToInt(values[1], out var row)) return Visibility.Collapsed;
            bool active = values[2] is bool b && b;

            if (!active) return IsNegative(parameter) ? Visibility.Visible : Visibility.Collapsed;

            bool isPlaying = cur == row && cur > 0;
            bool gate = true;
            if (values.Length >= 4 && values[3] is TrackIndexDisplayMode mode)
            {
                string param = parameter as string ?? "";
                if (param.Contains("Index", StringComparison.OrdinalIgnoreCase))
                    gate = mode != TrackIndexDisplayMode.None;
                else if (param.Contains("Duration", StringComparison.OrdinalIgnoreCase))
                    gate = mode == TrackIndexDisplayMode.None;
            }
            bool shouldShow = isPlaying && gate;
            bool neg = IsNegative(parameter);
            return neg ? (shouldShow ? Visibility.Collapsed : Visibility.Visible)
                       : (shouldShow ? Visibility.Visible : Visibility.Collapsed);

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        static bool IsNegative(object p) => p is string s && s.Contains("negative", StringComparison.OrdinalIgnoreCase);
    }
}
