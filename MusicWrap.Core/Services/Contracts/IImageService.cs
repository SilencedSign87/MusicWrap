using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Contracts
{
    public enum ImageVariant { Small, Medium, Large, Original, Blur }
    public interface IImageService
    {
        string? ResolvePath(string? fileName, ImageVariant variant);
        string? ResolvePathForSize(string? fileName, int requestedSize, bool preferOriginal = false);
        void ClearCache(ImageVariant? variant = null);
    }
}
