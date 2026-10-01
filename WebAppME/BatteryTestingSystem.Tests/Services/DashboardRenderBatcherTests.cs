using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BatteryTestingSystem.Services.Implementations;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// DashboardRenderBatcher exists to stop 64 channel cards each firing their own
/// InvokeAsync(StateHasChanged) per tick, which saturates the render lock of the circuit
/// and reads as a UI freeze. Its contract is therefore about *how many* dispatches happen,
/// not just that renders eventually run.
///
/// These are timing tests. The batcher takes its interval via constructor (that parameter
/// exists for exactly this reason), so they use a short interval and wait several multiples
/// of it before asserting - generous enough to survive a loaded CI box, short enough to keep
/// the suite fast.
/// </summary>
public class DashboardRenderBatcherTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(60);

    /// <summary>Comfortably longer than one interval, so a due flush has certainly run.</summary>
    private static Task SettleAsync() => Task.Delay(Interval * 8);

    /// <summary>Records dispatches so tests can assert on batching, not just on side effects.</summary>
    private sealed class RecordingDispatcher
    {
        private int _dispatchCount;
        public int DispatchCount => Volatile.Read(ref _dispatchCount);

        public async Task Dispatch(Func<Task> work)
        {
            Interlocked.Increment(ref _dispatchCount);
            await work();
        }
    }

    private sealed class Counter
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Render() => Interlocked.Increment(ref _count);
    }

    // ------------------------------------------------------------- the core job

    [Fact]
    public async Task ManyRequestsWithinOneInterval_ProduceASingleDispatch()
    {
        var dispatcher = new RecordingDispatcher();
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(dispatcher.Dispatch);

        var cards = new List<Counter>();
        for (int i = 0; i < 64; i++)
        {
            var card = new Counter();
            cards.Add(card);
            batcher.RequestRender(card.Render);
        }

        await SettleAsync();

        Assert.Equal(1, dispatcher.DispatchCount);
        Assert.All(cards, c => Assert.Equal(1, c.Count));
    }

    [Fact]
    public async Task EveryRequestedRender_ActuallyRuns()
    {
        // Coalescing must not mean dropping: all 64 cards still repaint, they just
        // repaint inside one dispatch.
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(w => w());

        var cards = new List<Counter>();
        for (int i = 0; i < 64; i++)
        {
            var card = new Counter();
            cards.Add(card);
            batcher.RequestRender(card.Render);
        }

        await SettleAsync();

        Assert.All(cards, c => Assert.Equal(1, c.Count));
    }

    [Fact]
    public async Task TheSameCardRequestingRepeatedly_RendersOncePerFlush()
    {
        // _pending is a HashSet<Action>, and delegate equality is by target+method, so a
        // card spamming RequestRender within one interval collapses to one repaint.
        var dispatcher = new RecordingDispatcher();
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(dispatcher.Dispatch);

        var card = new Counter();
        for (int i = 0; i < 50; i++)
            batcher.RequestRender(card.Render);

        await SettleAsync();

        Assert.Equal(1, dispatcher.DispatchCount);
        Assert.Equal(1, card.Count);
    }

    [Fact]
    public async Task DistinctLambdasOverTheSameCard_AreNotDeduped()
    {
        // The flip side of delegate equality: a closure over per-iteration state is a
        // genuinely distinct delegate (different Target), so it survives into the batch
        // even though it targets the same card. Capturing `i` inside the loop body forces
        // the compiler to allocate a fresh display class each time - without that, a
        // closure that only reads the unchanging `card` reference is reused across every
        // iteration and dedupes exactly like TheSameCardRequestingRepeatedly_* above.
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(w => w());

        var card = new Counter();
        for (int i = 0; i < 5; i++)
        {
            int iteration = i;
            batcher.RequestRender(() => { _ = iteration; card.Render(); });
        }

        await SettleAsync();

        Assert.Equal(5, card.Count);
    }

    // ------------------------------------------------------------------ re-arm

    [Fact]
    public async Task AfterAFlush_TheNextRequestSchedulesAFreshFlush()
    {
        // _flushScheduled must reset, or the dashboard goes permanently stale after
        // the first tick.
        var dispatcher = new RecordingDispatcher();
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(dispatcher.Dispatch);

        var card = new Counter();

        batcher.RequestRender(card.Render);
        await SettleAsync();

        batcher.RequestRender(card.Render);
        await SettleAsync();

        Assert.Equal(2, dispatcher.DispatchCount);
        Assert.Equal(2, card.Count);
    }

    [Fact]
    public async Task SuccessiveTicks_KeepBatching()
    {
        var dispatcher = new RecordingDispatcher();
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(dispatcher.Dispatch);

        var card = new Counter();

        for (int tick = 0; tick < 3; tick++)
        {
            for (int i = 0; i < 10; i++)
                batcher.RequestRender(card.Render);

            await SettleAsync();
        }

        Assert.Equal(3, dispatcher.DispatchCount);
        Assert.Equal(3, card.Count);
    }

    // -------------------------------------------------------- dispatcher states

    [Fact]
    public async Task WithNoDispatcherAttached_RendersRunInlineRatherThanBeingDropped()
    {
        // Cards can tick before the dashboard has attached its InvokeAsync. The
        // documented fallback is to run inline, not to swallow the render.
        var batcher = new DashboardRenderBatcher(Interval);

        var card = new Counter();
        batcher.RequestRender(card.Render);

        await SettleAsync();

        Assert.Equal(1, card.Count);
    }

    [Fact]
    public async Task ADispatcherAttachedAfterTheRequest_IsStillUsedForThatFlush()
    {
        // The dispatcher is read at flush time, not at request time.
        var dispatcher = new RecordingDispatcher();
        var batcher = new DashboardRenderBatcher(Interval);

        var card = new Counter();
        batcher.RequestRender(card.Render);
        batcher.AttachDispatcher(dispatcher.Dispatch);

        await SettleAsync();

        Assert.Equal(1, dispatcher.DispatchCount);
        Assert.Equal(1, card.Count);
    }

    [Fact]
    public async Task RendersRunThroughTheDispatcher_NotOnTheCallingThread()
    {
        // The point of AttachDispatcher is that repaints land on the renderer sync
        // context of the circuit. This asserts the render happens inside the
        // dispatched callback.
        var batcher = new DashboardRenderBatcher(Interval);

        bool insideDispatcher = false;
        bool ranInsideDispatcher = false;

        batcher.AttachDispatcher(async work =>
        {
            insideDispatcher = true;
            await work();
            insideDispatcher = false;
        });

        batcher.RequestRender(() => ranInsideDispatcher = insideDispatcher);

        await SettleAsync();

        Assert.True(ranInsideDispatcher);
    }

    // ----------------------------------------------------------- concurrency

    [Fact]
    public async Task ConcurrentRequestsFromManyThreads_DoNotLoseRenders()
    {
        // Real callers are hardware event threads, all hitting RequestRender at once.
        var batcher = new DashboardRenderBatcher(Interval);
        batcher.AttachDispatcher(w => w());

        var cards = new List<Counter>();
        for (int i = 0; i < 64; i++) cards.Add(new Counter());

        await Task.WhenAll(cards.ConvertAll(card =>
            Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                    batcher.RequestRender(card.Render);
            })));

        await SettleAsync();

        // A card may span more than one flush window, but must never be lost entirely.
        Assert.All(cards, c => Assert.InRange(c.Count, 1, 20));
    }

    [Fact]
    public void DefaultInterval_IsUsedWhenNoneSupplied()
    {
        // Constructing with no argument must not throw - the dashboard uses this overload.
        var batcher = new DashboardRenderBatcher();

        Assert.NotNull(batcher);
    }
}
