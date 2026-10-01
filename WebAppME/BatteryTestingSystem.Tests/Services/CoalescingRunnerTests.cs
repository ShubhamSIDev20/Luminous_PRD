using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BatteryTestingSystem.Services.Implementations;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class CoalescingRunnerTests
{
    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        while (value > (seen = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, seen) == seen)
                return;
        }
    }

    /// <summary>
    /// The whole point of the class: a burst of requests from hardware threads must never
    /// put two runs of the work delegate in flight at once. That is what was corrupting the
    /// circuit-scoped DbContext when 64 channels registered at the same time.
    /// </summary>
    [Fact]
    public async Task RunAsync_ConcurrentRequests_NeverRunsWorkConcurrently()
    {
        var runner = new CoalescingRunner();
        int concurrent = 0;
        int maxConcurrent = 0;

        async Task Work()
        {
            InterlockedMax(ref maxConcurrent, Interlocked.Increment(ref concurrent));
            await Task.Delay(5);
            Interlocked.Decrement(ref concurrent);
        }

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => runner.RunAsync(Work)))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    /// <summary>
    /// Requests that arrive mid-run must not be lost, but must also not queue up one-for-one.
    /// Any number of them collapse into exactly one follow-up run.
    /// </summary>
    [Fact]
    public async Task RunAsync_RequestsDuringRun_CollapseToExactlyOneFollowUpRun()
    {
        var runner = new CoalescingRunner();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        int runs = 0;

        async Task Work()
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                started.SetResult();
                await release.Task;
            }
        }

        var first = runner.RunAsync(Work);
        await started.Task;

        // Three more requests while the first run is still in flight.
        await runner.RunAsync(Work);
        await runner.RunAsync(Work);
        await runner.RunAsync(Work);

        release.SetResult();
        await first;

        Assert.Equal(2, runs);
    }

    /// <summary>
    /// A failed reload must not wedge the runner forever - the next hardware event has to be
    /// able to run. Losing the in-flight pending flag on failure is acceptable: the next event
    /// sets it again.
    /// </summary>
    [Fact]
    public async Task RunAsync_WorkThrows_RunnerStillAcceptsLaterWork()
    {
        var runner = new CoalescingRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(() => throw new InvalidOperationException("boom")));

        int runs = 0;
        await runner.RunAsync(() => { runs++; return Task.CompletedTask; });

        Assert.Equal(1, runs);
    }

    /// <summary>
    /// Sequential (non-overlapping) requests must each run - coalescing only applies while a
    /// run is actually in flight.
    /// </summary>
    [Fact]
    public async Task RunAsync_SequentialRequests_EachRun()
    {
        var runner = new CoalescingRunner();
        int runs = 0;

        for (int i = 0; i < 3; i++)
            await runner.RunAsync(() => { runs++; return Task.CompletedTask; });

        Assert.Equal(3, runs);
    }

    /// <summary>
    /// Request() is the fire-and-forget entry point used from event handlers. A throwing work
    /// delegate must not surface as an unobserved task exception that can tear down the process.
    /// </summary>
    [Fact]
    public async Task Request_WorkThrows_ReportsToOnErrorAndDoesNotThrow()
    {
        var runner = new CoalescingRunner();
        var captured = new TaskCompletionSource<Exception>();

        runner.Request(
            () => throw new InvalidOperationException("boom"),
            ex => captured.TrySetResult(ex));

        var completed = await Task.WhenAny(captured.Task, Task.Delay(2000));

        Assert.Same(captured.Task, completed);
        Assert.IsType<InvalidOperationException>(await captured.Task);
    }
}
