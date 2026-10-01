namespace BatteryTestingSystem.Services.Implementations
{
    /// <summary>
    /// Runs an async action, never concurrently, collapsing a burst of requests into a single
    /// follow-up run.
    /// <para>
    /// This exists because <see cref="ChannelManager.HardwareManagerChanged"/> is raised from
    /// hardware threads, while its Blazor subscribers reload data through circuit-<b>scoped</b>
    /// services - and a Blazor circuit's scoped services share one <c>AppDbContext</c>. A burst
    /// of 64 channel registrations used to start 64 overlapping queries on that single context,
    /// which EF Core rejects with "A second operation was started on this context instance
    /// before a previous operation completed."
    /// </para>
    /// <para>
    /// Semantics: while a run is in flight, further requests set a pending flag instead of
    /// starting a second run. Any number of them collapse into exactly one follow-up run, so no
    /// change is ever missed and no work piles up. If the work delegate throws, the exception
    /// propagates to the caller of <see cref="RunAsync"/> (or goes to <c>onError</c> for
    /// <see cref="Request"/>), the runner resets, and any pending flag is dropped - the next
    /// event will set it again.
    /// </para>
    /// </summary>
    public sealed class CoalescingRunner
    {
        private readonly object _lock = new();
        private bool _running;
        private bool _pending;

        /// <summary>
        /// Fire-and-forget entry point for event handlers, so they never need to be
        /// <c>async void</c>. Exceptions are routed to <paramref name="onError"/> rather than
        /// becoming unobserved task exceptions.
        /// </summary>
        public void Request(Func<Task> work, Action<Exception>? onError = null)
        {
            _ = RunGuardedAsync(work, onError);
        }

        private async Task RunGuardedAsync(Func<Task> work, Action<Exception>? onError)
        {
            try
            {
                await RunAsync(work).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Requests a run. Returns once this caller's obligation is discharged: either it ran
        /// the work itself, or a run already in flight has been told to repeat.
        /// </summary>
        public async Task RunAsync(Func<Task> work)
        {
            ArgumentNullException.ThrowIfNull(work);

            lock (_lock)
            {
                // Someone else owns the run loop. Flag it and leave - they will pick this up.
                if (_running)
                {
                    _pending = true;
                    return;
                }

                _running = true;
            }

            try
            {
                while (true)
                {
                    await work().ConfigureAwait(false);

                    // Re-check under the lock: a request arriving between the end of work() and
                    // clearing _running would otherwise be silently dropped.
                    lock (_lock)
                    {
                        if (!_pending)
                        {
                            _running = false;
                            return;
                        }

                        _pending = false;
                    }
                }
            }
            catch
            {
                lock (_lock)
                {
                    _running = false;
                    _pending = false;
                }

                throw;
            }
        }
    }
}
