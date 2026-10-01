// Utils/LttbDownsampler.cs
namespace BatteryTestingSystem.Utils;

/// <summary>
/// Largest-Triangle-Three-Buckets (LTTB) downsampling algorithm.
///
/// Reduces a data series to <paramref name="threshold"/> points while
/// preserving the visual shape of the original signal.  The algorithm
/// picks one point per bucket such that the area of the triangle formed
/// by the selected point and its neighbours is maximised — this keeps
/// peaks and valleys that matter for a chart while discarding redundant
/// "flat" points.
///
/// Complexity: O(n).  Safe for 10 M+ row datasets.
/// Reference: Sveinn Steinarsson, "Downsampling Time Series for
///   Visual Representation", MSc thesis 2013.
/// </summary>
public static class LttbDownsampler
{
    /// <summary>
    /// Downsample a sequence of (x, y) pairs to at most
    /// <paramref name="threshold"/> points.
    ///
    /// <para>Returns the original list unchanged if
    /// <c>source.Count &lt;= threshold</c>.</para>
    /// </summary>
    /// <typeparam name="T">Any type — the caller provides accessors for x and y.</typeparam>
    /// <param name="source">Input data in chronological order.</param>
    /// <param name="threshold">Maximum number of output points (≥ 2).</param>
    /// <param name="xSelector">Returns the x-value (used for area calculation).</param>
    /// <param name="ySelector">Returns the y-value (used for area calculation).</param>
    public static List<T> Downsample<T>(
        IReadOnlyList<T> source,
        int threshold,
        Func<T, double> xSelector,
        Func<T, double> ySelector)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (threshold < 2) throw new ArgumentOutOfRangeException(nameof(threshold), "threshold must be >= 2");

        int n = source.Count;
        if (n <= threshold) return source.ToList();

        var result = new List<T>(threshold);

        // Always keep the first and last points
        double bucketSize = (double)(n - 2) / (threshold - 2);

        int a = 0;                                  // index of last selected point
        result.Add(source[a]);

        for (int i = 0; i < threshold - 2; i++)
        {
            // Average of next bucket (used as the "future" point for area)
            int nextBucketStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
            int nextBucketEnd   = (int)Math.Floor((i + 2) * bucketSize) + 1;
            nextBucketEnd = Math.Min(nextBucketEnd, n);

            double avgX = 0, avgY = 0;
            int nextLen = nextBucketEnd - nextBucketStart;
            for (int j = nextBucketStart; j < nextBucketEnd; j++)
            {
                avgX += xSelector(source[j]);
                avgY += ySelector(source[j]);
            }
            avgX /= nextLen;
            avgY /= nextLen;

            // Current bucket range
            int bucketStart = (int)Math.Floor(i * bucketSize) + 1;
            int bucketEnd   = (int)Math.Floor((i + 1) * bucketSize) + 1;
            bucketEnd = Math.Min(bucketEnd, n);

            double ax = xSelector(source[a]);
            double ay = ySelector(source[a]);

            double maxArea = -1;
            int maxIndex   = bucketStart;

            for (int j = bucketStart; j < bucketEnd; j++)
            {
                double bx = xSelector(source[j]);
                double by = ySelector(source[j]);

                // Triangle area * 2 (no need to divide by 2 — only relative comparison)
                double area = Math.Abs(
                    (ax - avgX) * (by - ay) -
                    (ax - bx)   * (avgY - ay)
                );

                if (area > maxArea)
                {
                    maxArea  = area;
                    maxIndex = j;
                }
            }

            result.Add(source[maxIndex]);
            a = maxIndex;
        }

        result.Add(source[n - 1]);
        return result;
    }
}
