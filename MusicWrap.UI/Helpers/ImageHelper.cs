using MusicWrap.Data.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Helpers
{
    public static class ImageHelper
    {
        private const int SmallImageThreshold = 80;
        private const int SmallCacheLimit = 60;
        private const int LargeCacheLimit = 10;

        private static readonly object _cacheLock = new();

        private static readonly Dictionary<string, CacheEntry> _smallCache = new();
        private static readonly LinkedList<string> _smallLru = new();

        private static readonly Dictionary<string, CacheEntry> _largeCache = new();
        private static readonly LinkedList<string> _largeLru = new();

        private static readonly Dictionary<string, BitmapImage?> _defaultImages = new();

        public static readonly string BaseCoverPath = MusicWrapDirectories.CoverDirectory;

        public static BitmapImage? GetDefaultAlbumImage(int size = 64)
        {
            return GetDefaultImage("album", size);
        }

        public static BitmapImage? LoadThumbnail(string? imagePath, string type = "album", int size = 64)
        {
            if (size <= 0) size = 64;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return GetDefaultImage(type, size);
            }

            string normalizedPath = NormalizePath(imagePath);
            string cacheKey = BuildKey(normalizedPath, type, size);

            if (TryGetFromCache(cacheKey, size, out var cached))
            {
                return cached;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = size;
                bitmap.UriSource = new Uri(normalizedPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                AddToCache(cacheKey, bitmap, size);
                return bitmap;
            }
            catch
            {
                RemoveFromCache(cacheKey, size);
                return GetDefaultImage(type, size);
            }
        }

        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _smallCache.Clear();
                _smallLru.Clear();
                _largeCache.Clear();
                _largeLru.Clear();
                _defaultImages.Clear();
            }
        }

        private static BitmapImage? GetDefaultImage(string type, int size)
        {
            if (size <= 0) size = 64;
            string key = $"default|{type}|{size}";

            lock (_cacheLock)
            {
                if (_defaultImages.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = size;

                    // Puedes enrutar por tipo más adelante (artist, playlist, etc.)
                    bitmap.UriSource = new Uri("pack://application:,,,/Resources/DefaultTrack.png", UriKind.Absolute);

                    bitmap.EndInit();
                    bitmap.Freeze();

                    _defaultImages[key] = bitmap;
                    return bitmap;
                }
                catch
                {
                    _defaultImages[key] = null;
                    return null;
                }
            }
        }

        private static string BuildKey(string path, string type, int size)
        {
            return $"{path}|{type}|{size}";
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).ToLowerInvariant();
        }

        private static bool TryGetFromCache(string key, int size, out BitmapImage? image)
        {
            lock (_cacheLock)
            {
                bool isSmall = size <= SmallImageThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;

                if (cache.TryGetValue(key, out var entry) && entry.Image != null)
                {
                    lru.Remove(entry.Node);
                    lru.AddLast(entry.Node);
                    image = entry.Image;
                    return true;
                }

                image = null;
                return false;
            }
        }

        private static void AddToCache(string key, BitmapImage image, int size)
        {
            lock (_cacheLock)
            {
                bool isSmall = size <= SmallImageThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;
                int cacheLimit = isSmall ? SmallCacheLimit : LargeCacheLimit;

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
                    if (oldest == null) break;

                    lru.RemoveFirst();
                    cache.Remove(oldest.Value);
                }
            }
        }

        private static void RemoveFromCache(string key, int size)
        {
            lock (_cacheLock)
            {
                bool isSmall = size <= SmallImageThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;

                if (cache.TryGetValue(key, out var entry))
                {
                    lru.Remove(entry.Node);
                    cache.Remove(key);
                }
            }
        }

        private sealed class CacheEntry
        {
            public required BitmapImage Image { get; init; }
            public required LinkedListNode<string> Node { get; init; }
        }

    }
}
