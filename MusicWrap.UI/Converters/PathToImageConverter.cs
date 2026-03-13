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
        private static readonly Dictionary<string, WeakReference<BitmapImage>> _cache = [];

        private static BitmapImage? _defaultImage;

        private static BitmapImage DefaultImage
        {
            get
            {
                if (_defaultImage == null)
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri("pack://application:,,,/Resources/DefaultTrack.png", UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        _defaultImage = bitmap;
                    }
                    catch
                    {
                        _defaultImage = null;
                    }
                }
                return _defaultImage;
            }
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value is not string path || string.IsNullOrWhiteSpace(path))
            {
                return DefaultImage;
            }

            // Parse size from parameter
            int size = 64; // default thumbnail size
            if (parameter is string sizeStr && int.TryParse(sizeStr, out int parsedSize))
            {
                size = parsedSize;
            }

            // Create cache key with both path and size
            string cacheKey = $"{path}|{size}";

            // Check cache with size-specific key
            if (_cache.TryGetValue(cacheKey, out var weakRef) && weakRef.TryGetTarget(out var cachedImage))
            {
                return cachedImage;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = size;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                // Cache with size-specific key
                _cache[cacheKey] = new WeakReference<BitmapImage>(bitmap);

                return bitmap;
            }
            catch
            {
                _cache.Remove(cacheKey);
                return DefaultImage;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}

