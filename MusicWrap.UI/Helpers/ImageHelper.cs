using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Helpers
{
    public static class ImageHelper
    {
        private static readonly Dictionary<int, BitmapImage?> _defaultAlbumImages = new();
        public static readonly string BaseCoverPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MusicWrap",
                "covers"
                );
        public static BitmapImage? GetDefaultAlbumImage(int size = 64)
        {
            if (!_defaultAlbumImages.TryGetValue(size, out var image))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = size;
                    bitmap.UriSource = new Uri("pack://application:,,,/Resources/DefaultTrack.png", UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _defaultAlbumImages[size] = bitmap;
                    image = bitmap;
                }
                catch
                {
                    _defaultAlbumImages[size] = null;
                    image = null;
                }
            }
            return image;
        }

        public static BitmapImage? LoadThumbnail(string? imagePath, string type = "album", int size = 64)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return GetDefaultAlbumImage(size); // TODO: Add default artist image
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = size;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return GetDefaultAlbumImage(size); // TODO: Add default artist image
            }
        }

    }
}
