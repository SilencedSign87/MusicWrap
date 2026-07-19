using MusicWrap.Core.Services.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Mobile.Domain
{
    public interface IMauiImageService : IImageService
    {

    }
    internal class MauiImageService : IMauiImageService
    {
        public void ClearCache(ImageVariant? variant = null)
        {
            throw new NotImplementedException();
        }

        public string? ResolvePath(string? fileName, ImageVariant variant)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MusicWrap", "Covers", variant.ToString());
            var fullPath = Path.Combine(basePath, fileName);
            return File.Exists(fullPath) ? fullPath : null;
        }

        public string? ResolvePathForSize(string? fileName, int requestedSize, bool preferOriginal = false)
        {
            var variant = requestedSize switch
            {
                <= 64 => ImageVariant.Small,
                <= 180 => ImageVariant.Medium,
                <= 360 => ImageVariant.Large,
                _ => ImageVariant.Original
            };
            return ResolvePath(fileName, variant);
        }
    }
}
