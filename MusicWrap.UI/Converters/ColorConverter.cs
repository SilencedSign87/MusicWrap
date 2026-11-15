using Acornima;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicWrap.UI.Converters
{
    class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string hex || System.Windows.Media.ColorConverter.ConvertFromString(hex) is not Color color)
            {
                return Colors.Transparent;
            }

            ParseParameter(parameter, out var alpha, out var inverse);

            color = Color.FromArgb(
                (byte)(alpha * 255),
                color.R,
                color.G,
                color.B);


            if (inverse)
            {
                color = Color.FromRgb(
                    (byte)(255 - color.R),
                    (byte)(255 - color.G),
                    (byte)(255 - color.B)
                );
            }
            return color;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static void ParseParameter(object? parameter, out float alpha, out bool inverse)
        {
            inverse = false;
            alpha = 1.0f;

            if (parameter is float fvalue && fvalue > 0)
            {
                alpha = Math.Clamp(fvalue, 0f, 1f);
                return;
            }

            if (parameter is not string text || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var tokens = text.Split([';', ':', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in tokens)
            {
                var token = raw.Trim();

                if (float.TryParse(token, out float alphaValue))
                {
                    alpha = Math.Clamp(alphaValue, 0f, 1f);
                    continue;
                }
                if (token.Equals("inverse", StringComparison.OrdinalIgnoreCase))
                {
                    inverse = true;
                    continue;
                }
            }

        }
    }
}
