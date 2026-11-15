using Microsoft.Extensions.Logging;
using MusicWrap.Data.Infrastructure;
using NetVips;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;

namespace MusicWrap.Core.Services.Images
{
    public class ImageProcessor(ILogger<ImageProcessor> logger)
    {
        private readonly ILogger _logger = logger;
        private static readonly ConcurrentDictionary<string, object> _fileLocks = new();

        public const int SmallCoverSize = 64;
        public const int MediumCoverSize = 180;
        public const int LargeCoverSize = 360;
        public const int BlurCoverSize = 1440;

        private const int ColorSampleSize = 64;

        // blurred background blending factors
        private const float DominantColorFactor = 0.0f;
        private const float ImageColorFactor = 1f - DominantColorFactor;

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

            //using var colorSample = ResizeSquare(image, ColorSampleSize);

            var colors = ExtractColorsKmeans(small);

            using var blur = CreateBlurredBackground(image, colors.DominantColorHex);
            SaveImage(blur, GetPath(MusicWrapDirectories.BlurImageDirectory, fileName));

            return colors;
        }

        public void SaveOriginal(byte[] imageBytes, string fileName)
        {
            var path = Path.Combine(MusicWrapDirectories.CoverDirectory, fileName);
            var lockObj = _fileLocks.GetOrAdd(path, _ => new object());
            lock (lockObj)
            {
                if (!File.Exists(path))
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

                return ExtractColorsKmeans(sample);
            }
            catch
            {
                return ColorExtractionResult.Default;
            }
        }

        public Image CreateBlurredBackground(Image source, string dominantColorHex)
        {
            double scale = Math.Min(
                (double)BlurCoverSize / source.Width,
                (double)BlurCoverSize / source.Height);

            using var resized = source.Resize(scale, kernel: Enums.Kernel.Lanczos3);

            double sigma = resized.Height / 18.0;
            using var blurred = resized.Gaussblur(sigma);

            using var rgb = blurred.Bands == 4
                ? blurred.ExtractBand(0, n: 3)
                : blurred;

            var (baseR, baseG, baseB) = ParseHexColor(dominantColorHex);

            float domFactor = DominantColorFactor;
            float imgFactor = ImageColorFactor;

            double[] a =
            [
                imgFactor,
                imgFactor,
                imgFactor
            ];

            double[] b =
            [
                baseR * domFactor,
                baseG * domFactor,
                baseB * domFactor
            ];

            using var blended = rgb.Linear(a, b);
            return blended.Cast(Enums.BandFormat.Uchar);

            //return ucharBlended;
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
            var lockObj = _fileLocks.GetOrAdd(path, _ => new object());
            lock (lockObj)
            {
                if (File.Exists(path)) return;

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
        }

        public ColorExtractionResult ExtractColorsKmeans(
            Image sample,
            int k = 8,
            int maxIterations = 100,
            int seed = 87)
        {
            var clusters = ImageColorKmeans.Run(
             GetRgbBytes(sample),
             k,
             maxIterations,
             seed);

            return CreateColorExtractionResult(clusters);
        }

        /// <summary>
        /// Only to playground implementation
        /// </summary>
        /// <param name="imageBytes"></param>
        /// <returns></returns>
        public KmeansResult ExecuteKmeans(byte[] imageBytes, int k = 8, int maxIterations = 100, int seed = 87)
        {
            using var source = Image.NewFromBuffer(imageBytes);
            using var image = ResizeSquare(source, ColorSampleSize);

            var clusters = ImageColorKmeans.Run(
                GetRgbBytes(image),
                k,
                maxIterations,
                seed);

            return new KmeansResult
            {
                ScaledImageBytes = image.PngsaveBuffer(),
                Clusters = clusters,
                Result = CreateColorExtractionResult(clusters)
            };
        }

        public class KmeansResult
        {
            public required byte[] ScaledImageBytes { get; set; }
            public required ImageColorKmeans.Cluster[] Clusters { get; set; }
            public required ColorExtractionResult Result { get; set; }
        }

        private static byte[] GetRgbBytes(Image image)
        {
            int pixelCount = image.Width * image.Height;
            int bands = image.Bands;
            var data = image.WriteToMemory<byte>();
            var rgb = new byte[pixelCount * 3];

            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * bands;
                byte r, g, b;
                if (bands == 1)
                {
                    byte gray = data[offset];
                    r = g = b = gray;
                }
                else
                {
                    r = data[offset];
                    g = data[offset + 1];
                    b = data[offset + 2];
                }
                rgb[i * 3] = r;
                rgb[i * 3 + 1] = g;
                rgb[i * 3 + 2] = b;
            }

            return rgb;
        }

        private static ColorExtractionResult CreateColorExtractionResult(ImageColorKmeans.Cluster[] clusters)
        {
            if (clusters.Length == 0)
                return ColorExtractionResult.Default;

            var dominant = clusters
                .OrderByDescending(c => c.Count)
                .First();

            int totalPixels = clusters.Sum(c => c.Count);

            var dominantLuminance = CalculateLuminance(
                (byte)dominant.Center.X,
                (byte)dominant.Center.Y,
                (byte)dominant.Center.Z);
            var contrastedCandidated = clusters
                .Where(c =>
                {
                    if (c.Count < totalPixels * 0.01) return false;
                    if (Vector3.DistanceSquared(c.Center, dominant.Center) < 100f) return false;

                    var luminance = CalculateLuminance(
                        (byte)c.Center.X,
                        (byte)c.Center.Y,
                        (byte)c.Center.Z);

                    var contrast = CalculateContrastRatio(dominantLuminance, luminance);
                    return contrast >= 1.5;
                }).ToArray();

            var cantidates = contrastedCandidated.Length > 0
            ? contrastedCandidated
            : [.. clusters
                .Where(c =>
                    c.Count >= totalPixels * 0.01 &&
                    Vector3.DistanceSquared(c.Center, dominant.Center) >= 100f)];


            var highlight = cantidates
            .OrderByDescending(c =>
            {
                var color = c.Center;

                var hsv = RgbToHsv((byte)color.X, (byte)color.Y, (byte)color.Z);
                var luminance = CalculateLuminance((byte)color.X, (byte)color.Y, (byte)color.Z);
                var contrast = CalculateContrastRatio(dominantLuminance, luminance);

                var frequency = (double)c.Count / totalPixels;

                return contrast
                * (0.5 + hsv.S * 0.5)
                * Math.Pow(frequency, 0.25);
            }).FirstOrDefault();

            if (highlight.Count == 0)
                highlight = dominant;

            return new ColorExtractionResult
            {
                DominantColorHex = ToHex(dominant.Center),
                DominantForegroundHex = ToForeground(dominant.Center),

                HighlightColorHex = ToHex(highlight.Center),
                HighlightForegroundHex = ToForeground(highlight.Center)
            };
        }
        private static string ToHex(System.Numerics.Vector3 color) =>
            RgbToHex((byte)color.X, (byte)color.Y, (byte)color.Z);

        private static string ToForeground(System.Numerics.Vector3 color) =>
            GetContrastColor((byte)color.X, (byte)color.Y, (byte)color.Z);

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
