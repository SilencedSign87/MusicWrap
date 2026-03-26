using MusicWrap.UI.Helpers;
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
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int size = 64;
            if (parameter is string sizeStr && int.TryParse(sizeStr, out int parsedSize) && parsedSize > 0)
            {
                size = parsedSize;
            }

            if (value is not string path || string.IsNullOrWhiteSpace(path))
            {
                return ImageHelper.GetDefaultAlbumImage(size);
            }

            return ImageHelper.LoadThumbnail(path, "album", size);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static void ClearCache()
        {
            ImageHelper.ClearCache();
        }

    }
}

