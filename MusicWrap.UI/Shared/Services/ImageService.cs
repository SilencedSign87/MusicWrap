using MusicWrap.Data.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Services
{
    public enum ImageVariant
    {
        Small,
        Medium,
        Large,
        Original,
        Blur
    }
    public interface IImageService
    {
        string? ResolvePath(string? fileName, ImageVariant variant);
        string? ResolvePathForSize(string? fileName, int requestedSize, bool preferOriginal = false);
        BitmapImage? Load(string? fileName, ImageVariant variant, int decodeSize = 0);
        Task<BitmapImage?> LoadAsync(string? fileName, ImageVariant variant, int decodeSize = 0, CancellationToken ct = default);
        BitmapImage? LoadForSize(string? fileName, int requestedSize, bool preferOriginal = false);
        Task<BitmapImage?> LoadForSizeAsync(string? fileName, int requestedSize, bool preferOriginal = false, CancellationToken ct = default);
        BitmapImage? GetDefaultImage(int size = 64, ImageVariant variant = ImageVariant.Original);
        void ClearCache();
    }
    public class ImageService : IImageService
    {
        private const int SmallThreshold = 64;
        private const int MediumThreshold = 180;
        private const int LargeThreshold = 340;

        private const int SmallCacheLimit = 80;
        private const int MediumCacheLimit = 40;
        private const int LargeCacheLimit = 10;

        private static readonly object _cacheLock = new();
        private static readonly Dictionary<string, CacheEntry> _smallCache = new();
        private static readonly LinkedList<string> _smallLru = new();

        private static readonly Dictionary<string, CacheEntry> _largeCache = new();
        private static readonly LinkedList<string> _largeLru = new();

        private static readonly Dictionary<string, BitmapImage?> _defaultImages = new();

        public void ClearCache()
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

        public BitmapImage? Load(string? fileName, ImageVariant variant, int decodeSize = 0)
        {
            int size = decodeSize > 0 ? decodeSize : 64;
            var path = ResolvePath(fileName, variant);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return GetDefaultImage(size, variant);
            }
            string normalizedPath = NormalizePath(path);
            string cacheKey = BuildCacheKey(normalizedPath, size);

            if (TryGetFromCache(cacheKey, size, out var cached))
            {
                return cached;
            }

            try
            {
                var bitmap = DecodeBitmap(normalizedPath, size);
                AddToCache(cacheKey, bitmap, size);
                return bitmap;
            }
            catch
            {
                RemoveFromCache(cacheKey, size);
                return GetDefaultImage(size, variant);
            }
        }

        public async Task<BitmapImage?> LoadAsync(string? fileName, ImageVariant variant, int decodeSize = 0, CancellationToken ct = default)
        {
            int size = decodeSize > 0 ? decodeSize : 64;
            var path = ResolvePath(fileName, variant);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return GetDefaultImage(size, variant);
            }

            string normalizedPath = NormalizePath(path);
            string cacheKey = BuildCacheKey(normalizedPath, size);

            if (TryGetFromCache(cacheKey, size, out var cached))
            {
                return cached;
            }

            try
            {
                var bitmap = await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    return DecodeBitmap(normalizedPath, size);
                }, ct).ConfigureAwait(false);

                AddToCache(cacheKey, bitmap, size);
                return bitmap;
            }
            catch
            {
                RemoveFromCache(cacheKey, size);
                return GetDefaultImage(size, variant);
            }
        }

        public BitmapImage? LoadForSize(string? fileName, int requestedSize, bool preferOriginal = false)
        {
            int size = requestedSize > 0 ? requestedSize : 64;
            var path = ResolvePathForSize(fileName, size, preferOriginal);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return GetDefaultImage(size, ImageVariant.Original);
            }

            string normalizedPath = NormalizePath(path);
            string cacheKey = BuildCacheKey(normalizedPath, size);

            if (TryGetFromCache(cacheKey, size, out var cached))
            {
                return cached;
            }

            try
            {
                var bitmap = DecodeBitmap(normalizedPath, size);
                AddToCache(cacheKey, bitmap, size);
                return bitmap;
            }
            catch
            {
                RemoveFromCache(cacheKey, size);
                return GetDefaultImage(size, ImageVariant.Original);
            }
        }

        public async Task<BitmapImage?> LoadForSizeAsync(string? fileName, int requestedSize, bool preferOriginal = false, CancellationToken ct = default)
        {
            int size = requestedSize > 0 ? requestedSize : 64;
            var path = ResolvePathForSize(fileName, size, preferOriginal);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return GetDefaultImage(size , ImageVariant.Original);
            }

            string normalizedPath = NormalizePath(path);
            string cacheKey = BuildCacheKey(normalizedPath, size);

            if (TryGetFromCache(cacheKey, size, out var cached))
            {
                return cached;
            }

            try
            {
                var bitmap = await Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    return DecodeBitmap(normalizedPath, size);
                }, ct).ConfigureAwait(false);

                AddToCache(cacheKey, bitmap, size);
                return bitmap;
            }
            catch
            {
                RemoveFromCache(cacheKey, size);
                return GetDefaultImage(size, ImageVariant.Original);
            }
        }

        public string? ResolvePath(string? fileName, ImageVariant variant)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (Path.IsPathRooted(fileName) && variant == ImageVariant.Original)
                return fileName;

            var cleanFileName = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(cleanFileName))
                return null;

            string primaryPath = variant switch
            {
                ImageVariant.Small => Path.Combine(MusicWrapDirectories.SmallImageDirectory, cleanFileName),
                ImageVariant.Medium => Path.Combine(MusicWrapDirectories.MediumImageDirectory, cleanFileName),
                ImageVariant.Large => Path.Combine(MusicWrapDirectories.LargeImageDirectory, cleanFileName),
                ImageVariant.Blur => Path.Combine(MusicWrapDirectories.BlurImageDirectory, cleanFileName),
                _ => Path.Combine(MusicWrapDirectories.CoverDirectory, cleanFileName),
            };

            if (File.Exists(primaryPath))
                return primaryPath;

            string fallbackOriginal = Path.Combine(MusicWrapDirectories.CoverDirectory, cleanFileName);
            if (File.Exists(fallbackOriginal)) return fallbackOriginal;

            string fallbackSmall = Path.Combine(MusicWrapDirectories.SmallImageDirectory, cleanFileName);
            if (File.Exists(fallbackSmall)) return fallbackSmall;

            string fallbackMedium = Path.Combine(MusicWrapDirectories.MediumImageDirectory, cleanFileName);
            if (File.Exists(fallbackMedium)) return fallbackMedium;

            string fallbackLarge = Path.Combine(MusicWrapDirectories.LargeImageDirectory, cleanFileName);
            if (File.Exists(fallbackLarge)) return fallbackLarge;

            string fallbackBlur = Path.Combine(MusicWrapDirectories.BlurImageDirectory, cleanFileName);
            if (File.Exists(fallbackBlur)) return fallbackBlur;

            return fallbackOriginal;
        }

        public string? ResolvePathForSize(string? fileName, int requestedSize, bool preferOriginal = false)
        {
            if (preferOriginal)
            {
                return ResolvePath(fileName, ImageVariant.Original);
            }

            var variant = ResolveVariantBySize(requestedSize);
            return ResolvePath(fileName, variant);
        }
        public BitmapImage? GetDefaultImage(int size = 64, ImageVariant variant = ImageVariant.Original)
        {
            if (size <= 0) size = 64;

            string key = $"default|{variant}|{size}";
            string uri = variant == ImageVariant.Blur
                ? "pack://application:,,,/Resources/BlurDefault.jpg"
                : "pack://application:,,,/Resources/DefaultTrack.png";

            lock (_cacheLock)
            {
                if (_defaultImages.TryGetValue(key, out var cached))
                    return cached;

                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = size;
                    bmp.UriSource = new Uri(uri, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();

                    _defaultImages[key] = bmp;
                    return bmp;
                }
                catch
                {
                    _defaultImages[key] = null;
                    return null;
                }
            }
        }
        private static ImageVariant ResolveVariantBySize(int size)
        {
            if (size <= SmallThreshold) return ImageVariant.Small;
            if (size <= MediumThreshold) return ImageVariant.Medium;
            if (size <= LargeThreshold) return ImageVariant.Large;
            return ImageVariant.Original;
        }
        private static BitmapImage DecodeBitmap(string absolutePath, int decodeSize)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodeSize;
            bitmap.UriSource = new Uri(absolutePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).ToLowerInvariant();
        }
        private static string BuildCacheKey(string path, int decodeSize)
        {
            return $"{path}|{decodeSize}";
        }

        private static bool TryGetFromCache(string key, int size, out BitmapImage? image)
        {
            lock (_cacheLock)
            {
                bool isSmall = size <= SmallThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;

                if (cache.TryGetValue(key, out var entry) && entry.BitmapImage != null)
                {
                    lru.Remove(entry.LruNode);
                    lru.AddLast(entry.LruNode);
                    image = entry.BitmapImage;
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
                bool isSmall = size <= SmallThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;
                int cacheLimit = isSmall ? SmallCacheLimit : LargeCacheLimit;

                if (cache.TryGetValue(key, out var existing))
                {
                    lru.Remove(existing.LruNode);
                    cache.Remove(key);
                }

                var node = new LinkedListNode<string>(key);
                lru.AddLast(node);
                cache[key] = new CacheEntry { BitmapImage = image, LruNode = node };

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
                bool isSmall = size <= SmallThreshold;
                var cache = isSmall ? _smallCache : _largeCache;
                var lru = isSmall ? _smallLru : _largeLru;

                if (cache.TryGetValue(key, out var entry))
                {
                    lru.Remove(entry.LruNode);
                    cache.Remove(key);
                }
            }
        }
        private sealed class CacheEntry
        {
            public required BitmapImage BitmapImage { get; init; }
            public required LinkedListNode<string> LruNode { get; init; }
        }
    }
}


