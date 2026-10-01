using System;
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

/// <summary>
/// LTTB decides which points a chart shows. Its failure mode is not an exception - it is a
/// chart that looks plausible while having quietly dropped the spike the operator needed to
/// see. The shape-preservation tests below are the ones that matter; the count and boundary
/// tests just stop the arithmetic drifting.
/// </summary>
public class LttbDownsamplerTests
{
    private record Point(double X, double Y);

    private static List<Point> Series(int count, Func<int, double> y) =>
        Enumerable.Range(0, count).Select(i => new Point(i, y(i))).ToList();

    private static List<Point> Run(IReadOnlyList<Point> src, int threshold) =>
        LttbDownsampler.Downsample(src, threshold, p => p.X, p => p.Y);

    // ------------------------------------------------------------- pass-through

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(50)]
    public void AtOrBelowThreshold_ReturnsEverythingUnchanged(int count)
    {
        var src = Series(count, i => i * 1.5);

        var result = Run(src, 50);

        Assert.Equal(src, result);
    }

    [Fact]
    public void AtOrBelowThreshold_ReturnsACopy_NotTheSameInstance()
    {
        // Callers mutate/sort chart buffers; handing back the caller's own list would
        // alias it. The implementation calls ToList() - this pins that.
        var src = Series(5, i => i);

        var result = Run(src, 50);

        Assert.NotSame(src, result);
    }

    // ------------------------------------------------------------------ counts

    [Theory]
    [InlineData(1000, 100)]
    [InlineData(1000, 3)]
    [InlineData(1000, 2)]
    [InlineData(10_000, 500)]
    public void AboveThreshold_ReturnsExactlyThresholdPoints(int count, int threshold)
    {
        var result = Run(Series(count, i => Math.Sin(i / 10.0)), threshold);

        Assert.Equal(threshold, result.Count);
    }

    [Fact]
    public void OutputIsAlwaysAStrictSubsetOfTheInput_InOriginalOrder()
    {
        var src = Series(500, i => Math.Sin(i / 7.0) * 100);

        var result = Run(src, 50);

        Assert.All(result, p => Assert.Contains(p, src));
        Assert.Equal(result.Select(p => p.X).OrderBy(x => x), result.Select(p => p.X));
        Assert.Equal(result.Select(p => p.X).Distinct().Count(), result.Count);
    }

    // ----------------------------------------------------------- shape fidelity

    [Fact]
    public void FirstAndLastPoints_AreAlwaysKept()
    {
        // Chart axes are derived from the endpoints; losing them shifts the whole plot.
        var src = Series(1000, i => Math.Sin(i / 10.0));

        var result = Run(src, 25);

        Assert.Equal(src[0], result[0]);
        Assert.Equal(src[^1], result[^1]);
    }

    [Fact]
    public void ASingleSpikeInFlatData_SurvivesDownsampling()
    {
        // The whole reason LTTB is used instead of "take every Nth point": a naive
        // stride would step straight over this spike.
        var src = Series(1000, i => i == 500 ? 9999.0 : 1.0);

        var result = Run(src, 50);

        Assert.Contains(result, p => p.Y == 9999.0);
    }

    [Fact]
    public void BothExtremes_SurviveDownsampling()
    {
        var src = Series(2000, i => i == 700 ? 5000.0 : i == 1300 ? -5000.0 : 0.0);

        var result = Run(src, 100);

        Assert.Contains(result, p => p.Y == 5000.0);
        Assert.Contains(result, p => p.Y == -5000.0);
    }

    [Fact]
    public void NaiveStride_WouldMissTheSpike_ProvingTheAlgorithmIsDoingWork()
    {
        // Guards against someone "simplifying" Downsample into a stride: this asserts the
        // spike sits where a stride of the same output size would skip it.
        var src = Series(1000, i => i == 501 ? 9999.0 : 1.0);
        const int threshold = 50;

        var stride = Enumerable.Range(0, threshold).Select(i => src[i * (src.Count / threshold)]);
        Assert.DoesNotContain(stride, p => p.Y == 9999.0);

        Assert.Contains(Run(src, threshold), p => p.Y == 9999.0);
    }

    // ----------------------------------------------------------- bucket edges

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(10, 9)]
    [InlineData(101, 100)]
    public void ThresholdJustBelowCount_DoesNotProduceNaN(int count, int threshold)
    {
        // The tightest bucket geometry: this is where the "next bucket" can collapse and
        // the average would divide by zero, yielding NaN and silently degrading selection
        // to "first point of every bucket". Reasoning says the divisor stays >= 1; this
        // asserts it rather than trusting the reasoning.
        var src = Series(count, i => Math.Sin(i));

        var result = Run(src, threshold);

        Assert.Equal(threshold, result.Count);
        Assert.All(result, p => Assert.False(double.IsNaN(p.Y) || double.IsNaN(p.X)));
    }

    [Fact]
    public void ThresholdOfTwo_ReturnsOnlyTheEndpoints()
    {
        var src = Series(1000, i => Math.Sin(i / 10.0));

        var result = Run(src, 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(src[0], result[0]);
        Assert.Equal(src[^1], result[1]);
    }

    // ---------------------------------------------------------------- contract

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThresholdBelowTwo_Throws(int threshold)
    {
        var src = Series(100, i => i);

        Assert.Throws<ArgumentOutOfRangeException>(() => Run(src, threshold));
    }

    [Fact]
    public void NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => LttbDownsampler.Downsample<Point>(null!, 10, p => p.X, p => p.Y));
    }

    [Fact]
    public void ThresholdBelowTwo_ThrowsEvenWhenSourceIsSmallEnoughToPassThrough()
    {
        // Validation must happen before the n <= threshold shortcut, or the contract
        // depends on the data.
        Assert.Throws<ArgumentOutOfRangeException>(() => Run(Series(1, i => i), 1));
    }

    [Fact]
    public void IsDeterministic_SameInputGivesSameOutput()
    {
        var src = Series(3000, i => Math.Sin(i / 13.0) + Math.Cos(i / 29.0));

        Assert.Equal(Run(src, 200), Run(src, 200));
    }

    [Fact]
    public void HandlesLargeSeries_WithinLinearTime()
    {
        // Documented as O(n) and "safe for 10M+ rows"; 1M keeps CI quick while still
        // catching an accidental O(n^2) rewrite.
        var src = Series(1_000_000, i => Math.Sin(i / 1000.0));

        var result = Run(src, 1000);

        Assert.Equal(1000, result.Count);
    }
}
