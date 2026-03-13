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
        private const int SmallImageThreshold = 80;
        private const int SmallCacheLimit = 200;
        private const int LargeCacheLimit = 50;

        private static readonly object _cacheLock = new();

        private static readonly Dictionary<string, CacheEntry> _smallCache = new();
        private static readonly LinkedList<string> _smallLru = new();

        private static readonly Dictionary<string, CacheEntry> _largeCache = new();
        private static readonly LinkedList<string> _largeLru = new();


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
                return _defaultImage!;
            }
        }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return DefaultImage;


            // Parse size from parameter
            int size = 64;
            if (parameter is string sizeStr && int.TryParse(sizeStr, out int parsedSize))
                size = parsedSize;


            // Create cache key with both path and size
            string cacheKey = $"{path}|{size}";

            // Check cache with size-specific key
            if (TryGetFromCache(cacheKey, size, out var cached))
            {
                return cached!;
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
                AddToCache(cacheKey, bitmap, size);

                return bitmap;
            }
            catch
            {
                RemoveFromCache(cacheKey, size);
                return DefaultImage;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static bool TryGetFromCache(string key, int size, out BitmapImage? image)
        {
            lock (_cacheLock)
            {
                var isSmall = size <= SmallImageThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;

                if (cache.TryGetValue(key, out var entry) && entry.Image != null)
                {
                    // Move accessed item to the end of the LRU list
                    lru.Remove(entry.Node);
                    lru.AddLast(entry.Node);
                    image = entry.Image;
                    return true;
                }

                image = null!;
                return false;
            }
        }
        private static void AddToCache(string key, BitmapImage image, int size)
        {
            lock (_cacheLock)
            {
                var isSmall = size <= SmallImageThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;
                var cacheLimit = isSmall ? SmallCacheLimit : LargeCacheLimit;

                if (cache.TryGetValue(key, out var existing))
                {
                    lru.Remove(existing.Node);
                    cache.Remove(key);
                }

                var node = new LinkedListNode<string>(key);
                lru.AddLast(node);
                cache[key] = new CacheEntry { Image = image, Node = node };

                while (cache.Count > cacheLimit)
                {
                    var oldest = lru.First;
                    if (oldest == null)
                        break;

                    lru.RemoveFirst();
                    cache.Remove(oldest.Value);
                }
            }
        }
        private static void RemoveFromCache(string key, int size)
        {
            lock (_cacheLock)
            {
                var isSmall = size <= SmallImageThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;

                if (cache.TryGetValue(key, out var entry))
                {
                    lru.Remove(entry.Node);
                    cache.Remove(key);
                }
            }
        }

        public static void ClearCache()
        {
            lock (_cacheLock) {
                _smallCache.Clear();
                _smallLru.Clear();
                _largeCache.Clear();
                _largeLru.Clear();
            }
        }

        private sealed class CacheEntry
        {
            public required BitmapImage Image { get; init; }
            public required LinkedListNode<string> Node { get; init; }
        }
    }
}

