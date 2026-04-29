using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace MusicWrap.UI.Helpers
{
    public class ImageExtension : MarkupExtension
    {
        public string? Source { get; set; }
        public double Width { get; set; } = 16;
        public double Height { get; set; } = 16;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Source))
                return DependencyProperty.UnsetValue;

            var uri = new Uri(Source, UriKind.RelativeOrAbsolute);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            return new Image
            {
                Source = bitmap,
                Width = Width,
                Height = Height,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }
    }
}
