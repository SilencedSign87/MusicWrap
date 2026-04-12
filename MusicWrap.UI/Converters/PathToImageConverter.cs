using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Helpers;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        private static IImageService _imageService => App.Services.GetRequiredService<IImageService>();
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ParseParameter(parameter, out var size, out var variant, out var hasExplicitVariant);

            if (value is not string fileName || string.IsNullOrWhiteSpace(fileName))
            {
                return _imageService.GetDefaultImage(size, hasExplicitVariant ? variant : ImageVariant.Original);
            }

            if (hasExplicitVariant)
            {
                return _imageService.Load(fileName, variant, size);
            }

            return _imageService.LoadForSize(fileName, size);

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static void ClearCache()
        {
            App.Services.GetRequiredService<IImageService>().ClearCache();
        }

        private static void ParseParameter(object? parameter, out int size, out ImageVariant variant, out bool hasExplicitVariant)
        {
            size = 64;
            variant = ImageVariant.Original;
            hasExplicitVariant = false;

            if (parameter is int intValue && intValue > 0)
            {
                size = intValue;
                return;
            }

            if (parameter is not string text || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var tokens = text.Split(new[] { ':', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in tokens)
            {
                var token = raw.Trim();

                if (int.TryParse(token, out var parsedSize) && parsedSize > 0)
                {
                    size = parsedSize;
                    continue;
                }

                if (token.Equals("small", StringComparison.OrdinalIgnoreCase))
                {
                    variant = ImageVariant.Small;
                    hasExplicitVariant = true;
                    continue;
                }

                if (token.Equals("medium", StringComparison.OrdinalIgnoreCase))
                {
                    variant = ImageVariant.Medium;
                    hasExplicitVariant = true;
                    continue;
                }

                if (token.Equals("large", StringComparison.OrdinalIgnoreCase))
                {
                    variant = ImageVariant.Large;
                    hasExplicitVariant = true;
                    continue;
                }

                if (token.Equals("original", StringComparison.OrdinalIgnoreCase))
                {
                    variant = ImageVariant.Original;
                    hasExplicitVariant = true;
                    continue;
                }

                if (token.Equals("blur", StringComparison.OrdinalIgnoreCase))
                {
                    variant = ImageVariant.Blur;
                    hasExplicitVariant = true;
                }
            }
        }

    }
}



