using MusicWrap.Data.Infrastructure;
using NetVips;
using System.Runtime.InteropServices;

namespace MusicWrap.Core.Services.Images
{

    public class ImageProcessor
    {
        public const int SmallCoverSize = 64;
        public const int MediumCoverSize = 180;
        public const int LargeCoverSize = 360;
        public const int BlurCoverSize = 1080;

        private const int ColorSampleSize = 64;

        public ColorExtractionResult ProcessPipeline(byte[] imageBytes, string fileName)
        {
            EnsureDirectories();

            SaveOriginal(imageBytes, fileName);

            using var image = SafeDecode(imageBytes);
            if (image is null) return ColorExtractionResult.Default;

            using var large = ResizeToCover(image, LargeCoverSize);
            SaveImage(large, GetPath(MusicWrapDirectories.LargeImageDirectory, fileName));

            using var medium = ResizeToCover(large, MediumCoverSize);
            SaveImage(medium, GetPath(MusicWrapDirectories.MediumImageDirectory, fileName));

            using var small = ResizeToCover(medium, SmallCoverSize);
            SaveImage(small, GetPath(MusicWrapDirectories.SmallImageDirectory, fileName));

            var colors = ExtractColorPalette(medium);

            using var blur = CreateBlurredBackground(image, colors.DominantColorHex);
            SaveImage(blur, GetPath(MusicWrapDirectories.BlurImageDirectory, fileName));

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
            using var image = SafeDecode(imageBytes);
            if (image is null) return;
            using var resized = ResizeToCover(image, maxSize);
            SaveImage(resized, path);
        }
        public void SaveBlurredVariant(byte[] imageBytes, string fileName, string dominantColorHex)
        {
            var path = Path.Combine(MusicWrapDirectories.BlurImageDirectory, fileName);
            if (File.Exists(path)) return;
            using var image = SafeDecode(imageBytes);
            if (image is null) return;
            using var blurred = CreateBlurredBackground(image, dominantColorHex);
            SaveImage(blurred, path, 99);
        }

        public ColorExtractionResult ExtractColors(byte[] imageBytes)
        {
            try
            {
                using var image = Image.NewFromBuffer(imageBytes);
                using var sample = ResizeSquare(image, ColorSampleSize);

                return ExtractColorPalette(sample);
            }
            catch
            {
                return ColorExtractionResult.Default;
            }
        }

        public Image CreateBlurredBackground(Image source, string dominantColorHex)
        {
            double scale = Math.Min((double)BlurCoverSize / source.Width,
                                    (double)BlurCoverSize / source.Height);

            using var resized = source.Resize(scale, kernel: Enums.Kernel.Lanczos3);

            double sigma = resized.Height / 18.0;
            using var blurred = resized.Gaussblur(sigma);
            var (baseR, baseG, baseB) = ParseHexColor(dominantColorHex);
            // Blend: output = blurred * (1 - domFactor) + color * domFactor
            float domFactor = 0.85f;
            float imgFactor = 1f - domFactor;
            using var blended = blurred.Linear(
                new double[] { imgFactor },
                new double[] { baseR * domFactor, baseG * domFactor, baseB * domFactor }
            );

            using var ucharBlended = blended.Cast(Enums.BandFormat.Uchar);
            return AddNoiseGrain(ucharBlended, grainSize: 3, intensity: 0.02f);
        }

        public static Image ResizeSquare(Image source, int maxSize)
        {
            double scale = Math.Min((double)maxSize / source.Width,
                                     (double)maxSize / source.Height);
            return source.Resize(scale, kernel: Enums.Kernel.Lanczos3);
        }
        public static Image ResizeToCover(Image source, int targetSize)
        {
            double scale = Math.Max((double)targetSize / source.Width,
                                    (double)targetSize / source.Height);
            return source.Resize(scale, kernel: Enums.Kernel.Lanczos3);
        }
        public static Image ResizeToFit(Image source, int targetSize)
        {
            double scale = Math.Min((double)targetSize / source.Width,
                                     (double)targetSize / source.Height);
            return source.Resize(scale, kernel: Enums.Kernel.Lanczos3);
        }
        #region Color utilities
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
        public static double CalculateLuminance(byte r, byte g, byte b)
        {
            double[] rgb = [r / 255.0, g / 255.0, b / 255.0];
            for (int i = 0; i < rgb.Length; i++)
                rgb[i] = rgb[i] <= 0.03928
                    ? rgb[i] / 12.92
                    : Math.Pow((rgb[i] + 0.055) / 1.055, 2.4);
            return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2];
        }
        public static double CalculateContrastRatio(double lum1, double lum2)
        {
            double lighter = Math.Max(lum1, lum2);
            double darker = Math.Min(lum1, lum2);
            return (lighter + 0.05) / (darker + 0.05);
        }
        #endregion

        #region Internal
        private static Image? SafeDecode(byte[] image)
        {
            try { return Image.NewFromBuffer(image); }
            catch { return null; }
        }
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

        private static void SaveImage(Image image, string path, int quality = 90)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            switch (ext)
            {
                case ".png":
                    image.Pngsave(path);
                    break;
                case ".webp":
                    image.Webpsave(path, quality);
                    break;
                default:
                    image.Jpegsave(path, quality);
                    break;
            }
        }
        private static ColorExtractionResult ExtractColorPalette(Image sample)
        {
            var data = sample.WriteToMemory<byte>();
            int pixelCount = sample.Width * sample.Height;
            int bands = sample.Bands;
            int bytesPerPixel = bands;
            const int bucketSize = 8;
            var freq = new Dictionary<int, int>();
            var sums = new Dictionary<int, (long R, long G, long B)>();
            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * bytesPerPixel;
                byte r = data[offset];
                byte g = data[offset + 1];
                byte b = data[offset + 2];
                // alpha
                int key = (r / bucketSize) << 16 | (g / bucketSize) << 8 | (b / bucketSize);
                freq.TryGetValue(key, out int count);
                freq[key] = count + 1;
                if (!sums.TryGetValue(key, out var s))
                    sums[key] = (r, g, b);
                else
                    sums[key] = (s.R + r, s.G + g, s.B + b);
            }
            if (freq.Count == 0)
                return ColorExtractionResult.Default;
            var sorted = freq
                .OrderByDescending(kv => kv.Value)
                .Select(kv =>
                {
                    var s = sums[kv.Key];
                    return (
                        R: (byte)(s.R / kv.Value),
                        G: (byte)(s.G / kv.Value),
                        B: (byte)(s.B / kv.Value),
                        Freq: (double)kv.Value / pixelCount
                    );
                })
                .Where(c => c.Freq >= 0.002)
                .ToList();
            if (sorted.Count == 0)
                return ColorExtractionResult.Default;
            
            // dominant
            var dom = sorted[0];
            var domHsv = RgbToHsv(dom.R, dom.G, dom.B);
            double domLum = CalculateLuminance(dom.R, dom.G, dom.B);

            // Highlight
            (byte R, byte G, byte B) bestHighlight = (dom.R, dom.G, dom.B);
            double bestScore = -1;
            int searchLimit = Math.Min(15, sorted.Count);
            for (int idx = 1; idx < searchLimit; idx++)
            {
                var c = sorted[idx];
                var hsv = RgbToHsv(c.R, c.G, c.B);
                float hueDiff = Math.Abs(hsv.H - domHsv.H);
                hueDiff = Math.Min(hueDiff, 360f - hueDiff);
                if (hueDiff < 15f || hsv.S < 0.08f) continue;
                double score = c.Freq * 100.0
                             + Math.Min(hueDiff, 180f) * 0.3
                             + hsv.S * 20.0;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHighlight = (c.R, c.G, c.B);
                }
            }

            // Fallback
            if (bestScore < 0 && sorted.Count > 1)
                bestHighlight = (sorted[1].R, sorted[1].G, sorted[1].B);

            // Adjust brightness for contrast
            bestHighlight = EnsureMinimalContrast(bestHighlight, (dom.R, dom.G, dom.B));
            return new ColorExtractionResult
            {
                DominantColorHex = RgbToHex(dom.R, dom.G, dom.B),
                DominantForegroundHex = GetContrastColor(dom.R, dom.G, dom.B),
                HighlightColorHex = RgbToHex(bestHighlight.R, bestHighlight.G, bestHighlight.B),
                HighlightForegroundHex = GetContrastColor(bestHighlight.R, bestHighlight.G, bestHighlight.B)
            };
        }

        private static (byte R, byte G, byte B) EnsureMinimalContrast(
            (byte R, byte G, byte B) highlight,
            (byte R, byte G, byte B) dominant)
        {
            double domLum = CalculateLuminance(dominant.R, dominant.G, dominant.B);
            double hlLum = CalculateLuminance(highlight.R, highlight.G, highlight.B);
            double contrast = CalculateContrastRatio(domLum, hlLum);
            if (contrast >= 3.0 || highlight == dominant)
                return highlight;
            var hsv = RgbToHsv(highlight.R, highlight.G, highlight.B);
            bool needLighter = domLum < 0.5;
            for (int i = 0; i < 15; i++)
            {
                hsv.V = needLighter
                    ? Math.Min(1f, hsv.V + 0.06f)
                    : Math.Max(0f, hsv.V - 0.06f);
                var (r, g, b) = HsvToRgb(hsv.H, hsv.S, hsv.V);
                hlLum = CalculateLuminance(r, g, b);
                contrast = CalculateContrastRatio(domLum, hlLum);
                if (contrast >= 3.0)
                    return (r, g, b);
            }
            return highlight;
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
        private static Image AddNoiseGrain(Image image, double grainSize, double intensity)
        {
            var data = image.WriteToMemory<byte>();
            int pixelCount = image.Width * image.Height;
            int bands = image.Bands;
            var random = new Random(42);
            for (int i = 0; i < pixelCount; i++)
            {
                if (random.NextDouble() <= intensity)
                {
                    int offset = i * bands;
                    int noise = random.Next(-(int)grainSize, (int)grainSize + 1);
                    for (int b = 0; b < bands; b++)
                        data[offset + b] = (byte)Math.Clamp(data[offset + b] + noise, 0, 255);
                }
            }
            return Image.NewFromMemory(data, image.Width, image.Height, bands, Enums.BandFormat.Uchar);
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
