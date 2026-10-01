using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatteryTestingSystem.Services.Implementations.Workflow;
using BatteryTestingSystem.Services.Interfaces;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Mirrors DashboardView.DoAction's per-device SemaphoreSlim concurrency pattern, extracted into
/// a testable, instantiable class - the dashboard's own copy has zero direct unit tests because
/// it lives inside a Razor file's @code block.
/// </summary>
public class ChannelActionExecutorTests
{
    [Fact]
    public async Task SameDevice_RunsSequentially()
    {
        var probe = new ConcurrencyProbe();
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler>
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(1, 1, 2, probe),
        };

        await executor.ExecuteAsync("start", circuits);

        Assert.Equal(1, probe.PeakPerDevice[1]);
    }

    [Fact]
    public async Task DifferentDevices_RunInParallel()
    {
        var probe = new ConcurrencyProbe();
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler>
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(2, 1, 1, probe),
        };

        await executor.ExecuteAsync("start", circuits);

        Assert.True(probe.PeakPerDevice[1] >= 1);
        Assert.True(probe.PeakPerDevice[2] >= 1);
    }

    [Fact]
    public async Task DifferentDevices_OverlapInWallClockTime()
    {
        var probe = new ConcurrencyProbe();
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler>
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(2, 1, 1, probe),
        };

        var sw = Stopwatch.StartNew();
        await executor.ExecuteAsync("start", circuits);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 90, $"Expected overlap, took {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ReturnsOneResultPerCircuit_WithPhysicalAddress()
    {
        var probe = new ConcurrencyProbe();
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler>
        {
            RecordingChannelCommandHandler.Circuit(1, 2, 3, probe),
            RecordingChannelCommandHandler.Circuit(4, 5, 6, probe),
        };

        var results = await executor.ExecuteAsync("start", circuits);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.DeviceId == 1 && r.SecondaryBoardNumber == 2 && r.ChannelNumber == 3 && r.Success);
        Assert.Contains(results, r => r.DeviceId == 4 && r.SecondaryBoardNumber == 5 && r.ChannelNumber == 6 && r.Success);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("stop")]
    [InlineData("pause")]
    [InlineData("continue")]
    public async Task DispatchesToTheCorrectActionMethod(string action)
    {
        var calls = new List<string>();
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler> { new TrackingHandler(calls) };

        await executor.ExecuteAsync(action, circuits);

        Assert.Equal(new[] { action }, calls);
    }

    [Fact]
    public async Task UnsupportedAction_Throws()
    {
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler> { new TrackingHandler(new List<string>()) };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => executor.ExecuteAsync("transfer", circuits));
    }

    [Fact]
    public async Task InvokesOnStepCompleted_OncePerCircuit()
    {
        var probe = new ConcurrencyProbe();
        var executor = new ChannelActionExecutor();
        var circuits = new List<IChannelCommandHandler>
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(2, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(3, 1, 1, probe),
        };
        var completedCount = 0;

        await executor.ExecuteAsync("start", circuits, onStepCompleted: () => Interlocked.Increment(ref completedCount));

        Assert.Equal(3, completedCount);
    }
}
