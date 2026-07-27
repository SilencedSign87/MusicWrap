using MusicWrap.Data.Infrastructure;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicWrap.Core.Services.Images
{

    public class ImageProcessor
    {
        public const int SmallCoverSize = 64;
        public const int MediumCoverSize = 180;
        public const int LargeCoverSize = 360;
        public const int BlurCoverSize = 1080;

        private const int ColorSampleSize = 64;
        private const int QuantizeFactor = 32;
        private const double MinColorFrequency = 0.008; // 0.8%

        public ColorExtractionResult ProcessPipeline(byte[] imageBytes, string fileName)
        {
            EnsureDirectories();

            SaveOriginal(imageBytes, fileName);

            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null) return ColorExtractionResult.Default;

            using var large = ResizeToCover(bitmap, LargeCoverSize);
            SaveBitmap(large, GetPath(MusicWrapDirectories.LargeImageDirectory, fileName));

            using var medium = ResizeToCover(large, MediumCoverSize);
            SaveBitmap(medium, GetPath(MusicWrapDirectories.MediumImageDirectory, fileName));

            using var small = ResizeToCover(medium, SmallCoverSize);
            SaveBitmap(small, GetPath(MusicWrapDirectories.SmallImageDirectory, fileName));

            var colors = ExtractColorPalette(medium);

            using var blur = CreateBlurredBackground(bitmap, colors.DominantColorHex);
            SaveBitmap(blur, GetPath(MusicWrapDirectories.BlurImageDirectory, fileName));

            return colors;
        }

        public void SaveOriginal(byte[] imageBytes, string fileName)
        {
            var path = Path.Combine(MusicWrapDirectories.CoverDirectory, fileName);
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, imageBytes);
            }
        }
        public void SaveResizedVariant(byte[] imageBytes, string fileName, int maxSize)
        {
            var dir = GetDirectoryForSize(maxSize);
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path)) return;
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null) return;
            using var resized = Resize(bitmap, maxSize);
            SaveBitmap(resized, path);
        }
        public void SaveBlurredVariant(byte[] imageBytes, string fileName, string dominantColorHex)
        {
            var path = Path.Combine(MusicWrapDirectories.BlurImageDirectory, fileName);
            if (File.Exists(path)) return;
            using var bitmap = SKBitmap.Decode(imageBytes);
            if (bitmap is null) return;
            using var blurred = CreateBlurredBackground(bitmap, dominantColorHex);
            SaveBitmap(blurred, path, 99);
        }

        public ColorExtractionResult ExtractColors(byte[] imageBytes)
        {
            try
            {
                using var bitmap = SKBitmap.Decode(imageBytes);

                if (bitmap is null) return ColorExtractionResult.Default;

                using var sample = Resize(bitmap, ColorSampleSize);

                return ExtractColorPalette(sample);
            }
            catch
            {
                return ColorExtractionResult.Default;
            }
        }

        public SKBitmap CreateBlurredBackground(SKBitmap source, string dominantColorHex)
        {
            using var resized = Resize(source, BlurCoverSize);

            var blurred = new SKBitmap(resized.Width, resized.Height);

            using (var filter = SKImageFilter.CreateBlur(blurred.Height / 18f, blurred.Height / 18f))
            using (var paint = new SKPaint { ImageFilter = filter })
            using (var canvas = new SKCanvas(blurred))
            {
                canvas.DrawBitmap(resized, 0, 0, SKSamplingOptions.Default, paint);
            }

            var (baseR, baseG, baseB) = ParseHexColor(dominantColorHex);

            BlendWithColor(blurred, baseR, baseG, baseB, dominantFactor: 0.85f);

            AddNoiseGrain(blurred, intensity: 0.02f, grainSize: 3);

            return blurred;
        }

        public static SKBitmap Resize(SKBitmap source, int maxSize)
        {
            float scale = Math.Min((float)maxSize / source.Width, (float)maxSize / source.Height);
            int newW = Math.Max(1, (int)(source.Width * scale));
            int newH = Math.Max(1, (int)(source.Height * scale));
            var info = new SKImageInfo(newW, newH);

            return source.Resize(info, SKSamplingOptions.Default);
        }
        public static SKBitmap ResizeToCover(SKBitmap source, int targetSize)
        {
            float scale = Math.Max((float)targetSize / source.Width, (float)targetSize / source.Height);
            int newW = Math.Max(1, (int)(source.Width * scale));
            int newH = Math.Max(1, (int)(source.Height * scale));
            return source.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        public static SKBitmap ResizeToFit(SKBitmap source, int targetSize)
        {
            float scale = Math.Min((float)targetSize / source.Width, (float)targetSize / source.Height);
            int newW = Math.Max(1, (int)(source.Width * scale));
            int newH = Math.Max(1, (int)(source.Height * scale));
            return source.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        public static (float H, float S, float V) RgbToHsv(byte r, byte g, byte b)
        {
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;
            float max = Math.Max(rf, Math.Max(gf, bf));
            float min = Math.Min(rf, Math.Min(gf, bf));
            float delta = max - min;
            float h = 0f;
            if (delta > 0.001f)
            {
                if (max == rf) h = 60f * (((gf - bf) / delta) % 6f);
                else if (max == gf) h = 60f * (((bf - rf) / delta) + 2f);
                else h = 60f * (((rf - gf) / delta) + 4f);
            }
            if (h < 0) h += 360f;
            float s = max > 0.001f ? delta / max : 0f;
            return (h, s, max);
        }
        public static string RgbToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
        public static (byte R, byte G, byte B) ParseHexColor(string hex)
        {
            if (hex is { Length: >= 7 } && hex[0] == '#')
            {
                try
                {
                    return (
                        Convert.ToByte(hex[1..3], 16),
                        Convert.ToByte(hex[3..5], 16),
                        Convert.ToByte(hex[5..7], 16)
                    );
                }
                catch { }
            }
            return (0x40, 0x40, 0x40);
        }
        public static string GetContrastColor(byte r, byte g, byte b)
        {
            return CalculateLuminance(r, g, b) > 0.179 ? "#000000" : "#FFFFFF";
        }

        #region Internal
        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(MusicWrapDirectories.CoverDirectory);
            Directory.CreateDirectory(MusicWrapDirectories.SmallImageDirectory);
            Directory.CreateDirectory(MusicWrapDirectories.MediumImageDirectory);
            Directory.CreateDirectory(MusicWrapDirectories.LargeImageDirectory);
            Directory.CreateDirectory(MusicWrapDirectories.BlurImageDirectory);
        }
        private static string GetPath(string directory, string fileName) => Path.Combine(directory, fileName);
        private static string GetDirectoryForSize(int maxSize) => maxSize switch
        {
            <= SmallCoverSize => MusicWrapDirectories.SmallImageDirectory,
            <= MediumCoverSize => MusicWrapDirectories.MediumImageDirectory,
            <= LargeCoverSize => MusicWrapDirectories.LargeImageDirectory,
            _ => MusicWrapDirectories.CoverDirectory
        };

        private static void SaveBitmap(SKBitmap bitmap, string path, int quality = 90)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var format = ext switch
            {
                ".png" => SKEncodedImageFormat.Png,
                ".webp" => SKEncodedImageFormat.Webp,
                _ => SKEncodedImageFormat.Jpeg
            };
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, format == SKEncodedImageFormat.Png ? 100 : quality);
            File.WriteAllBytes(path, data.ToArray());
        }
        private static ColorExtractionResult ExtractColorPalette(SKBitmap sample)
        {
            var buffer = GetPixelData(sample);

            var pixelFormat = sample.ColorType switch
            {
                SKColorType.Bgra8888 => MedianCutQuantizer.PixelFormat.BGRA,
                SKColorType.Rgba8888 => MedianCutQuantizer.PixelFormat.RGBA,
                _ => MedianCutQuantizer.PixelFormat.BGRA 
            };

            var palette = MedianCutQuantizer.GetPalette(buffer, 
                width: sample.Width, 
                height: sample.Height, 
                quality: 4, 
                colorCount: 10,
                pixelFormat: pixelFormat);

            if (palette == null || palette.Length == 0)
                return ColorExtractionResult.Default;

            var (domR, domG, domB) = palette[0];

            string dominantHex = RgbToHex(domR, domG, domB);
            string dominantFg = GetContrastColor(domR, domG, domB);
            var domHsv = RgbToHsv(domR, domG, domB);

            
            // highlight
            (byte R, byte G, byte B) bestHighlight = (domR, domG, domB);
            bool foundDistinctHue = false;
            for (int idx = 1; idx < palette.Length; idx++)
            {
                var hsv = RgbToHsv(palette[idx].r, palette[idx].g, palette[idx].b);
                float hueDiff = Math.Abs(hsv.H - domHsv.H);
                hueDiff = Math.Min(hueDiff, 360f - hueDiff);

                if (hueDiff > 25f && hsv.S > 0.15f)
                {
                    bestHighlight = palette[idx];
                    foundDistinctHue = true;
                    break;
                }
            }

            // fallback
            if (!foundDistinctHue && palette.Length > 1)
            {
                bestHighlight = palette[1];
            }

            bestHighlight = EnsureUIContrast(bestHighlight, (domR, domG, domB), dominantFg);

            string highlightHex = RgbToHex(bestHighlight.R, bestHighlight.G, bestHighlight.B);
            string highlightFg = GetContrastColor(bestHighlight.R, bestHighlight.G, bestHighlight.B);

            return new ColorExtractionResult
            {
                DominantColorHex = dominantHex,
                DominantForegroundHex = dominantFg,
                HighlightColorHex = highlightHex,
                HighlightForegroundHex = highlightFg
            };

        }

        private static byte[] GetPixelData(SKBitmap bitmap)
        {
            IntPtr ptr = bitmap.GetPixels();
            if (ptr == IntPtr.Zero)
                return [];

            int length = bitmap.Height * bitmap.RowBytes;
            var buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, length);
            return buffer;
        }
        private static void SetPixelData(SKBitmap bitmap, byte[] buffer)
        {
            IntPtr ptr = bitmap.GetPixels();
            if (ptr == IntPtr.Zero) return;

            Marshal.Copy(buffer, 0, ptr, buffer.Length);
        }
        private static int Quantize(byte r, byte g, byte b)
        => (r / QuantizeFactor) << 16 | (g / QuantizeFactor) << 8 | (b / QuantizeFactor);
        private static (byte R, byte G, byte B) BoostSaturation((byte R, byte G, byte B) color, float minSat = 0.14f)
        {
            var hsv = RgbToHsv(color.R, color.G, color.B);
            if (hsv.S < 0.06f) return color;
            if (hsv.V > 0.92f && hsv.S < 0.18f) return color;
            float s = hsv.S;
            if (s < minSat) s += (minSat - s) * 0.35f;
            return HsvToRgb(hsv.H, s, Math.Min(1f, hsv.V * 1.01f));
        }
        private static (byte R, byte G, byte B) HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            float rf, gf, bf;
            if (h < 60f) { rf = c; gf = x; bf = 0f; }
            else if (h < 120f) { rf = x; gf = c; bf = 0f; }
            else if (h < 180f) { rf = 0f; gf = c; bf = x; }
            else if (h < 240f) { rf = 0f; gf = x; bf = c; }
            else if (h < 300f) { rf = x; gf = 0f; bf = c; }
            else { rf = c; gf = 0f; bf = x; }
            return ((byte)((rf + m) * 255), (byte)((gf + m) * 255), (byte)((bf + m) * 255));
        }
        private static void BlendWithColor(SKBitmap bitmap, byte baseR, byte baseG, byte baseB, float dominantFactor)
        {
            var buffer = GetPixelData(bitmap);
            float imgFactor = 1f - dominantFactor;
            int totalPixels = bitmap.Width * bitmap.Height;
            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                // SkiaSharp: BGRA
                buffer[offset] = (byte)(baseB * dominantFactor + buffer[offset] * imgFactor);
                buffer[offset + 1] = (byte)(baseG * dominantFactor + buffer[offset + 1] * imgFactor);
                buffer[offset + 2] = (byte)(baseR * dominantFactor + buffer[offset + 2] * imgFactor);
                buffer[offset + 3] = 255;
            }
            SetPixelData(bitmap, buffer);
        }
        private static void AddNoiseGrain(SKBitmap bitmap, float intensity, int grainSize)
        {
            var buffer = GetPixelData(bitmap);
            var random = new Random(42);
            int totalPixels = bitmap.Width * bitmap.Height;
            for (int i = 0; i < totalPixels; i++)
            {
                if (random.NextSingle() <= intensity)
                {
                    int offset = i * 4;
                    int noise = random.Next(-grainSize, grainSize + 1);
                    buffer[offset] = (byte)Math.Clamp(buffer[offset] + noise, 0, 255);
                    buffer[offset + 1] = (byte)Math.Clamp(buffer[offset + 1] + noise, 0, 255);
                    buffer[offset + 2] = (byte)Math.Clamp(buffer[offset + 2] + noise, 0, 255);
                }
            }
            SetPixelData(bitmap, buffer);
        }
        private static double CalculateLuminance(byte r, byte g, byte b)
        {
            double[] rgb = [r / 255.0, g / 255.0, b / 255.0];
            for (int i = 0; i < rgb.Length; i++)
            {
                rgb[i] = rgb[i] <= 0.03928 ? rgb[i] / 12.92 : Math.Pow((rgb[i] + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
        }
        public static double CalculateContrastRatio(double lum1, double lum2)
        {
            double lighter = Math.Max(lum1, lum2);
            double darker = Math.Min(lum1, lum2);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static (byte R, byte G, byte B) EnsureUIContrast((byte R, byte G, byte B) highlight, (byte R, byte G, byte B) dominant, string dominantFg)
        {
            double domLum = CalculateLuminance(dominant.R, dominant.G, dominant.B);
            double targetContrast = 4.5; // WCAG AA standard for normal text

            var hsv = RgbToHsv(highlight.R, highlight.G, highlight.B);
            bool pushTowardsWhite = dominantFg == "#FFFFFF";

            for (int i = 0; i < 20; i++)
            {
                var (r, g, b) = HsvToRgb(hsv.H, hsv.S, hsv.V);
                double currentLum = CalculateLuminance(r, g, b);
                double contrast = CalculateContrastRatio(domLum, currentLum);

                if (contrast >= targetContrast)
                    return (r, g, b);

                if (pushTowardsWhite)
                {
                    if (hsv.V < 1.0f) hsv.V = Math.Min(1.0f, hsv.V + 0.05f);
                    else hsv.S = Math.Max(0.0f, hsv.S - 0.05f);
                }
                else
                {
                    hsv.V = Math.Max(0.0f, hsv.V - 0.05f);
                }
            }

            return HsvToRgb(hsv.H, hsv.S, hsv.V); // best effort
        }

        #endregion
    }

    #region Models
    public sealed class ColorExtractionResult
    {
        public static readonly ColorExtractionResult Default = new()
        {
            DominantColorHex = "#404040",
            DominantForegroundHex = "#FFFFFF",
            HighlightColorHex = "#404040",
            HighlightForegroundHex = "#FFFFFF"
        };
        public required string DominantColorHex { get; init; }
        public required string DominantForegroundHex { get; init; }
        public required string HighlightColorHex { get; init; }
        public required string HighlightForegroundHex { get; init; }
    }

    #endregion
}
