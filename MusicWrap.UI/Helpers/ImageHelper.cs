using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Helpers
{
    public static class ImageHelper
    {
        private static BitmapImage? _defaultAlbumImage = null;
        public static BitmapImage DefaultAlbumImage
        {
            get
            {
                if (_defaultAlbumImage == null)
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 64;
                        bitmap.UriSource = new Uri("pack://application:,,,/Resources/DefaultTrack.png", UriKind.Absolute);
                        bitmap.EndInit();
                        bitmap.Freeze();
                        _defaultAlbumImage = bitmap;
                    }
                    catch
                    {
                        _defaultAlbumImage = null;
                    }
                }
                return _defaultAlbumImage!;
            }
        }

        public static BitmapImage? LoadThumbnail(string? imagePath, string type = "album", int size = 64)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return type == "album" ? DefaultAlbumImage : DefaultAlbumImage; // TODO: Add default artist image
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
                return type == "album" ? DefaultAlbumImage : DefaultAlbumImage;
            }
        }

    }
}
