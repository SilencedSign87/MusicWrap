using System.Numerics;

namespace MusicWrap.Core.Services.Images;

public static class ImageColorKmeans
{
    public readonly record struct Cluster(Vector3 Center, int Count);

    public static Cluster[] Run(ReadOnlySpan<byte> rgb, int k = 8, int maxIterations = 100, int seed = 87)
    {
        int n = rgb.Length / 3;

        var points = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            points[i] = new Vector3(rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]);
        }

        k = Math.Min(k, n);
        var rng = new Random(seed);
        var centers = InitPlusPlus(points, k, rng);
        var assign = new int[n];

        Array.Fill(assign, -1);
        var counts = new int[k];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            bool changed = false;

            // asign
            for (int i = 0; i < n; i++)
            {
                int best = 0;
                float bestDist = float.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    float dist = Vector3.DistanceSquared(points[i], centers[c]);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = c;
                    }
                }
                if (assign[i] != best)
                {
                    assign[i] = best;
                    changed = true;
                }
            }
            if (!changed)
                break;

            // update

            var sums = new Vector3[k];
            Array.Clear(counts);
            for (int i = 0; i < n; i++)
            {
                sums[assign[i]] += points[i];
                counts[assign[i]]++;
            }
            for (int c = 0; c < k; c++)
            {
                if (counts[c] > 0)
                {
                    centers[c] = sums[c] / counts[c];
                }
            }
        }

        Array.Clear(counts);
        for (int i = 0; i < n; i++)
        {
            counts[assign[i]]++;
        }

        return [.. Enumerable.Range(0, k)
            .Where(c => counts[c] > 0)
            .Select(c => new Cluster(centers[c], counts[c]))
            .OrderByDescending(c => c.Count)];
    }

    private static Vector3[] InitPlusPlus(Vector3[] pts, int k, Random rng)
    {
        var centers = new Vector3[k];
        centers[0] = pts[rng.Next(pts.Length)];
        var minDist = new float[pts.Length];

        Array.Fill(minDist, float.MaxValue);

        for (int c = 1; c < k; c++)
        {
            double total = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                minDist[i] = Math.Min(minDist[i], Vector3.DistanceSquared(pts[i], centers[c - 1]));
                total += minDist[i];
            }
            double target = rng.NextDouble() * total, acc = 0;
            int chosen = pts.Length - 1;
            for (int i = 0; i < pts.Length; i++)
            {
                acc += minDist[i];
                if (acc >= target) { chosen = i; break; }
            }
            centers[c] = pts[chosen];
        }
        return centers;
    }
}
