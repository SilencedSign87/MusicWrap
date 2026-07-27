using System;
using System.Collections.Generic;
using System.Text;
using TagLib.Mpeg4;

namespace MusicWrap.Core.Services.Images
{
    public static class MedianCutQuantizer
    {
        private const int SigBits = 5;
        private const int RShift = 8 - SigBits;
        private const int HistSize = 1 << (3 * SigBits);
        private const int MaxIterations = 1000;

        public enum PixelFormat
        {
            RGBA,
            BGRA
        }

        public static (byte r, byte g, byte b)[] GetPalette(
        byte[] pixelBuffer,
        int width,
        int height,
        int colorCount,
        int quality = 10,
        byte alphaThreshold = 125,
        bool ignoreWhite = true,
        PixelFormat pixelFormat = PixelFormat.RGBA)
        {
            if (pixelBuffer == null) throw new ArgumentNullException(nameof(pixelBuffer));
            return GetPalette(pixelBuffer.AsSpan(), width, height, colorCount, quality, alphaThreshold, ignoreWhite, pixelFormat);
        }
        public static (byte r, byte g, byte b)[] GetPalette(
        ReadOnlySpan<byte> pixelBuffer,
        int width,
        int height,
        int colorCount,
        int quality = 10,
        byte alphaThreshold = 125,
        bool ignoreWhite = true,
        PixelFormat pixelFormat = PixelFormat.RGBA)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("invalid width or height.");
            if (colorCount < 2) colorCount = 2;
            if (quality < 1) quality = 1;

            int pixelCount = checked(width * height);
            int minBytes = checked(pixelCount * 4);
            if (pixelBuffer.Length < minBytes)
                throw new ArgumentException($"Insufficient pixelBuffer: {pixelBuffer.Length} < {minBytes}");

            var hist = new int[HistSize];

            BuildHistogram(
                pixelBuffer,
                pixelCount,
                quality,
                alphaThreshold,
                ignoreWhite,
                pixelFormat,
                hist,
                out int rmin, out int rmax,
                out int gmin, out int gmax,
                out int bmin, out int bmax);

            if (rmax < rmin || gmax < gmin || bmax < bmin)
                return Array.Empty<(byte r, byte g, byte b)>();

            var initial = new VBox(rmin, rmax, gmin, gmax, bmin, bmax, hist);
            if (initial.Count() == 0)
                return Array.Empty<(byte r, byte g, byte b)>();

            var boxes = new List<VBox>(Math.Max(colorCount * 2, 16)) { initial };

            int targetByPopulation = Math.Max(1, (int)Math.Floor(colorCount * 0.75));
            IterCut(boxes, targetByPopulation, byProduct: false);

            IterCut(boxes, colorCount, byProduct: true);

            boxes.Sort((a, b) => b.Count().CompareTo(a.Count()));

            int outCount = Math.Min(colorCount, boxes.Count);
            var result = new (byte r, byte g, byte b)[outCount];
            for (int i = 0; i < outCount; i++)
                result[i] = boxes[i].Average();

            return result;
        }

        private static void BuildHistogram(
        ReadOnlySpan<byte> pixels,
        int pixelCount,
        int quality,
        byte alphaThreshold,
        bool ignoreWhite,
        PixelFormat pixelFormat,
        int[] hist,
        out int rmin, out int rmax,
        out int gmin, out int gmax,
        out int bmin, out int bmax)
        {
            rmin = gmin = bmin = int.MaxValue;
            rmax = gmax = bmax = int.MinValue;

            bool rgba = pixelFormat == PixelFormat.RGBA;

            for (int i = 0; i < pixelCount; i += quality)
            {
                int o = i * 4;
                byte r, g, b, a;

                if (rgba)
                {
                    r = pixels[o];
                    g = pixels[o + 1];
                    b = pixels[o + 2];
                    a = pixels[o + 3];
                }
                else
                {
                    b = pixels[o];
                    g = pixels[o + 1];
                    r = pixels[o + 2];
                    a = pixels[o + 3];
                }

                if (a < alphaThreshold) continue;
                if (ignoreWhite && r > 250 && g > 250 && b > 250) continue;

                int rq = r >> RShift;
                int gq = g >> RShift;
                int bq = b >> RShift;

                hist[ColorIndex(rq, gq, bq)]++;

                if (rq < rmin) rmin = rq;
                if (rq > rmax) rmax = rq;
                if (gq < gmin) gmin = gq;
                if (gq > gmax) gmax = gq;
                if (bq < bmin) bmin = bq;
                if (bq > bmax) bmax = bq;
            }
        }

        private static void IterCut(List<VBox> boxes, int target, bool byProduct)
        {
            int iter = 0;
            while (boxes.Count < target && iter++ < MaxIterations)
            {
                int index = SelectBoxToSplit(boxes, byProduct);
                if (index < 0) break;

                var box = boxes[index];
                var split = MedianCutApply(box);

                if (!split.HasValue) break;

                // replace the box at index with the first half and add the second half
                boxes[index] = split.Value.b1;
                boxes.Add(split.Value.b2);
            }
        }

        private static int SelectBoxToSplit(List<VBox> boxes, bool byProduct)
        {
            int bestIndex = -1;
            long bestScore = long.MinValue;

            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                int count = b.Count();
                if (count == 0) continue;

                long score = byProduct ? (long)count * b.Volume() : count;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        private static (VBox b1, VBox b2)? MedianCutApply(VBox box)
        {
            int count = box.Count();
            if (count <= 1) return null;

            int rw = box.R2 - box.R1 + 1;
            int gw = box.G2 - box.G1 + 1;
            int bw = box.B2 - box.B1 + 1;

            char channel = 'r';
            int maxw = rw;
            if (gw > maxw) { maxw = gw; channel = 'g'; }
            if (bw > maxw) { channel = 'b'; }

            int d1, d2;
            if (channel == 'r') { d1 = box.R1; d2 = box.R2; }
            else if (channel == 'g') { d1 = box.G1; d2 = box.G2; }
            else { d1 = box.B1; d2 = box.B2; }

            int len = d2 + 1;
            var partial = new int[len];

            // construct the partial sum array
            for (int i = d1; i <= d2; i++)
            {
                int sum = 0;
                if (channel == 'r')
                {
                    for (int g = box.G1; g <= box.G2; g++)
                        for (int b = box.B1; b <= box.B2; b++)
                            sum += box.Hist[ColorIndex(i, g, b)];
                }
                else if (channel == 'g')
                {
                    for (int r = box.R1; r <= box.R2; r++)
                        for (int b = box.B1; b <= box.B2; b++)
                            sum += box.Hist[ColorIndex(r, i, b)];
                }
                else
                {
                    for (int r = box.R1; r <= box.R2; r++)
                        for (int g = box.G1; g <= box.G2; g++)
                            sum += box.Hist[ColorIndex(r, g, i)];
                }

                partial[i] = sum + (i > 0 ? partial[i - 1] : 0);
            }

            int total = partial[d2];
            if (total == 0) return null;

            int cut = -1;
            int half = total >> 1;
            for (int i = d1; i <= d2; i++)
            {
                if (partial[i] >= half)
                {
                    cut = i;
                    break;
                }
            }
            if (cut < 0) return null;

            // adjust empty boxes
            if (cut <= d1) cut = Math.Min(d2 - 1, d1 + 1);
            if (cut >= d2) cut = Math.Max(d1, d2 - 1);
            if (cut < d1 || cut >= d2) return null;

            VBox b1, b2;
            if (channel == 'r')
            {
                b1 = new VBox(box.R1, cut, box.G1, box.G2, box.B1, box.B2, box.Hist);
                b2 = new VBox(cut + 1, box.R2, box.G1, box.G2, box.B1, box.B2, box.Hist);
            }
            else if (channel == 'g')
            {
                b1 = new VBox(box.R1, box.R2, box.G1, cut, box.B1, box.B2, box.Hist);
                b2 = new VBox(box.R1, box.R2, cut + 1, box.G2, box.B1, box.B2, box.Hist);
            }
            else
            {
                b1 = new VBox(box.R1, box.R2, box.G1, box.G2, box.B1, cut, box.Hist);
                b2 = new VBox(box.R1, box.R2, box.G1, box.G2, cut + 1, box.B2, box.Hist);
            }

            return (b1, b2);
        }

        private static int ColorIndex(int r, int g, int b)
            => (r << (2 * SigBits)) | (g << SigBits) | b;

        private static byte ClampByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        private sealed class VBox
        {
            public readonly int[] Hist;
            public int R1, R2, G1, G2, B1, B2;

            private int _count = -1;
            private int _volume = -1;

            public VBox(int r1, int r2, int g1, int g2, int b1, int b2, int[] hist)
            {
                R1 = r1; R2 = r2;
                G1 = g1; G2 = g2;
                B1 = b1; B2 = b2;
                Hist = hist;
            }

            public int Volume()
            {
                if (_volume < 0)
                    _volume = (R2 - R1 + 1) * (G2 - G1 + 1) * (B2 - B1 + 1);
                return _volume;
            }

            public int Count()
            {
                if (_count >= 0) return _count;

                int n = 0;
                for (int r = R1; r <= R2; r++)
                    for (int g = G1; g <= G2; g++)
                        for (int b = B1; b <= B2; b++)
                            n += Hist[ColorIndex(r, g, b)];

                _count = n;
                return _count;
            }

            public (byte r, byte g, byte b) Average()
            {
                int ntot = 0;
                long rsum = 0, gsum = 0, bsum = 0;
                int mult = 1 << RShift; // 8

                for (int r = R1; r <= R2; r++)
                {
                    int rc = (int)((r + 0.5) * mult);
                    for (int g = G1; g <= G2; g++)
                    {
                        int gc = (int)((g + 0.5) * mult);
                        for (int b = B1; b <= B2; b++)
                        {
                            int h = Hist[ColorIndex(r, g, b)];
                            if (h == 0) continue;

                            ntot += h;
                            int bc = (int)((b + 0.5) * mult);

                            rsum += (long)h * rc;
                            gsum += (long)h * gc;
                            bsum += (long)h * bc;
                        }
                    }
                }

                if (ntot > 0)
                {
                    int rr = (int)(rsum / ntot);
                    int gg = (int)(gsum / ntot);
                    int bb = (int)(bsum / ntot);
                    return (ClampByte(rr), ClampByte(gg), ClampByte(bb));
                }

                // fallback
                int fr = ((R1 + R2 + 1) * (1 << (RShift - 1)));
                int fg = ((G1 + G2 + 1) * (1 << (RShift - 1)));
                int fb = ((B1 + B2 + 1) * (1 << (RShift - 1)));
                return (ClampByte(fr), ClampByte(fg), ClampByte(fb));
            }
        }
    }
}
