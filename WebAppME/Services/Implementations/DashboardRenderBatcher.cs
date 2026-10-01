namespace BatteryTestingSystem.Services.Implementations;

/// <summary>
/// Coalesces per-card StateHasChanged calls on the Dashboard into a single batched
/// render dispatch per tick. Without this, every DeviceChannel card independently
/// calls InvokeAsync(StateHasChanged) on its own real-time event - with up to 64
/// channels per device, that's dozens of uncoordinated render dispatches competing
/// for the same Blazor Server circuit's render lock every tick, which is what reads
/// as a UI freeze. Registered Scoped (one instance per circuit) - never Singleton,
/// or cards from different users' sessions would batch together.
/// </summary>
public class DashboardRenderBatcher
{
    private readonly object _lock = new();
    private readonly HashSet<Action> _pending = new();
    private readonly TimeSpan _interval;
    private Func<Func<Task>, Task>? _dispatcher;
    private bool _flushScheduled;

    public DashboardRenderBatcher(TimeSpan? interval = null)
    {
        _interval = interval ?? TimeSpan.FromMilliseconds(150);
    }

    /// <summary>Called once by the dashboard page with its own InvokeAsync, so the
    /// batched flush runs on the circuit's renderer sync context.</summary>
    public void AttachDispatcher(Func<Func<Task>, Task> invokeAsync) => _dispatcher = invokeAsync;

    /// <summary>Cards call this instead of InvokeAsync(StateHasChanged) directly.</summary>
    public void RequestRender(Action stateHasChanged)
    {
        lock (_lock)
        {
            _pending.Add(stateHasChanged);
            if (_flushScheduled) return;
            _flushScheduled = true;
        }

        _ = ScheduleFlushAsync();
    }

    private async Task ScheduleFlushAsync()
    {
        await Task.Delay(_interval);

        Action[] toRun;
        lock (_lock)
        {
            toRun = new Action[_pending.Count];
            _pending.CopyTo(toRun);
            _pending.Clear();
            _flushScheduled = false;
        }

        if (toRun.Length == 0)
            return;

        var dispatcher = _dispatcher;
        if (dispatcher == null)
        {
            // No dashboard attached yet (or already disposed) - fall back to running
            // inline rather than dropping the render request.
            foreach (var render in toRun) render();
            return;
        }

        await dispatcher(() =>
        {
            foreach (var render in toRun) render();
            return Task.CompletedTask;
        });
    }
}
