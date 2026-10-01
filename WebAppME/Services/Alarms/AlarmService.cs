using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace BatteryTestingSystem.Services.Alarms
{
    /// <summary>
    /// Owns the alarm lifecycle. Singleton: producers are singletons, so this cannot hold a
    /// scoped AppDbContext - it resolves IAlarmRepository per operation instead.
    ///
    /// RaiseAsync order is fixed and load-bearing:
    ///   collapse-or-insert -> persist -> policy -> fan out.
    /// Persisting before fan-out is what guarantees an alarm raised while no browser is
    /// connected is still there when an operator next logs in.
    /// </summary>
    public class AlarmService : IAlarmService, IDisposable
    {
        private readonly Func<IAlarmRepository> _repoFactory;
        private readonly IServiceScope? _ownedScope;
        private readonly AlarmOptions _options;
        private readonly Serilog.ILogger _log = Log.ForContext<AlarmService>();
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Alarm rows with an unwritten OccurrenceCount / LastSeenUtc bump.</summary>
        private readonly Dictionary<string, AlarmLog> _pending = new();
        private readonly Dictionary<string, DateTime> _lastFlushed = new();

        private readonly Timer? _flushTimer;

        public AlarmPolicy Policy { get; }

        /// <summary>Overridable clock. Tests set this; production leaves the default.</summary>
        public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        public event Action<AlarmChanged>? OnChanged;

        /// <summary>Test constructor: caller supplies the repository directly.</summary>
        public AlarmService(Func<IAlarmRepository> repoFactory, AlarmOptions options, AlarmPolicy policy)
        {
            _repoFactory = repoFactory;
            _options = options;
            Policy = policy;
        }

        /// <summary>DI constructor.</summary>
        public AlarmService(IServiceScopeFactory scopeFactory, IOptions<AlarmOptions> options, AlarmPolicy policy)
        {
            _options = options.Value;
            Policy = policy;
            _repoFactory = () =>
            {
                var scope = scopeFactory.CreateScope();
                return scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
            };

            _flushTimer = new Timer(
                async _ => await FlushPendingAsync(),
                null,
                TimeSpan.FromSeconds(Math.Max(1, _options.FlushIntervalSeconds)),
                TimeSpan.FromSeconds(Math.Max(1, _options.FlushIntervalSeconds)));
        }

        public async Task RaiseAsync(AlarmRequest req)
        {
            var now = UtcNow();
            AlarmChanged? changed = null;
            AlarmLog? stormSummary = null;

            await _gate.WaitAsync();
            try
            {
                var repo = _repoFactory();
                var active = await repo.GetActiveByKeyAsync(req.AlarmKey);

                AlarmLog row;
                AlarmChangeKind kind;

                if (active == null)
                {
                    row = new AlarmLog
                    {
                        AlarmKey = req.AlarmKey,
                        Severity = req.Severity,
                        Source = req.Source,
                        DeviceId = req.DeviceId,
                        BoardNumber = req.BoardNumber,
                        ChannelNumber = req.ChannelNumber,
                        Title = req.Title,
                        Message = req.Message,
                        FirstSeenUtc = now,
                        LastSeenUtc = now,
                        OccurrenceCount = 1
                    };

                    // Inserts are ALWAYS written synchronously - the debounce below applies
                    // only to repeat bumps of an already-persisted row.
                    await repo.InsertAsync(row);
                    _lastFlushed[req.AlarmKey] = now;
                    kind = AlarmChangeKind.Raised;
                }
                else
                {
                    active.OccurrenceCount++;
                    active.LastSeenUtc = now;
                    active.Message = req.Message;
                    row = active;
                    kind = AlarmChangeKind.Collapsed;

                    var due = !_lastFlushed.TryGetValue(req.AlarmKey, out var last)
                              || (now - last).TotalSeconds >= _options.FlushIntervalSeconds;

                    if (due)
                    {
                        await repo.SaveAsync(active);
                        _lastFlushed[req.AlarmKey] = now;
                        _pending.Remove(req.AlarmKey);
                    }
                    else
                    {
                        _pending[req.AlarmKey] = active;
                    }
                }

                var decision = Policy.Evaluate(req.AlarmKey, req.Severity, req.DeviceId, now);

                if (decision.StormTriggered)
                {
                    stormSummary = new AlarmLog
                    {
                        AlarmKey = req.AlarmKey + "/storm",
                        Severity = SeverityLevel.WARNING,
                        Source = req.Source,
                        DeviceId = req.DeviceId,
                        BoardNumber = req.BoardNumber,
                        ChannelNumber = req.ChannelNumber,
                        Title = "Alarm storm suppressed",
                        Message = $"{req.AlarmKey} flapping - {decision.WindowCount} events in " +
                                  $"{_options.StormWindowMinutes} min. Escalation muted for " +
                                  $"{_options.StormCooloffMinutes} min.",
                        FirstSeenUtc = now,
                        LastSeenUtc = now,
                        OccurrenceCount = 1
                    };

                    await repo.InsertAsync(stormSummary);
                    _log.Warning("Alarm storm suppressed for {AlarmKey}: {Count} events",
                        req.AlarmKey, decision.WindowCount);
                }

                changed = new AlarmChanged(row, kind, decision.Escalate);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "RaiseAsync failed for {AlarmKey}", req.AlarmKey);
            }
            finally
            {
                _gate.Release();
            }

            // Fan out outside the lock so a slow UI handler cannot block producers.
            if (changed != null) OnChanged?.Invoke(changed);
            if (stormSummary != null)
                OnChanged?.Invoke(new AlarmChanged(stormSummary, AlarmChangeKind.Raised, true));
        }

        public async Task<bool> AcknowledgeAsync(long id, string user)
        {
            var now = UtcNow();
            bool ok;
            AlarmLog? row = null;

            await _gate.WaitAsync();
            try
            {
                var repo = _repoFactory();
                await FlushPendingNoLockAsync(repo);
                ok = await repo.AcknowledgeAsync(id, user, now);
                if (ok)
                {
                    var active = await repo.GetActiveAsync(_options.BellActiveTake);
                    row = active.FirstOrDefault(a => a.Id == id);
                }
            }
            finally
            {
                _gate.Release();
            }

            if (ok)
                OnChanged?.Invoke(new AlarmChanged(row ?? new AlarmLog { Id = id },
                    AlarmChangeKind.Acknowledged, false));

            return ok;
        }

        public async Task<int> AcknowledgeAllAsync(string user)
        {
            var now = UtcNow();
            int count;

            await _gate.WaitAsync();
            try
            {
                var repo = _repoFactory();
                await FlushPendingNoLockAsync(repo);
                count = await repo.AcknowledgeAllAsync(user, now);
            }
            finally
            {
                _gate.Release();
            }

            if (count > 0)
                OnChanged?.Invoke(new AlarmChanged(new AlarmLog(), AlarmChangeKind.Acknowledged, false));

            return count;
        }

        public async Task<bool> ClearAsync(string alarmKey)
        {
            var now = UtcNow();
            bool ok;

            await _gate.WaitAsync();
            try
            {
                var repo = _repoFactory();
                _pending.Remove(alarmKey);
                ok = await repo.ClearByKeyAsync(alarmKey, now);
            }
            finally
            {
                _gate.Release();
            }

            if (ok)
                OnChanged?.Invoke(new AlarmChanged(new AlarmLog { AlarmKey = alarmKey },
                    AlarmChangeKind.Cleared, false));

            return ok;
        }

        public async Task<IReadOnlyList<AlarmLog>> GetActiveAsync(int take)
        {
            var repo = _repoFactory();
            return await repo.GetActiveAsync(take);
        }

        public async Task<int> GetUnacknowledgedCountAsync()
        {
            var repo = _repoFactory();
            return await repo.CountUnacknowledgedAsync();
        }

        public async Task FlushPendingAsync()
        {
            await _gate.WaitAsync();
            try
            {
                await FlushPendingNoLockAsync(_repoFactory());
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Flushing pending alarm bumps failed");
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task FlushPendingNoLockAsync(IAlarmRepository repo)
        {
            if (_pending.Count == 0) return;

            var now = UtcNow();
            foreach (var kvp in _pending.ToList())
            {
                await repo.SaveAsync(kvp.Value);
                _lastFlushed[kvp.Key] = now;
            }
            _pending.Clear();
        }

        public void Dispose()
        {
            _flushTimer?.Dispose();
            // Best effort: never lose a counted repeat on shutdown.
            try { FlushPendingAsync().GetAwaiter().GetResult(); } catch { }
            _ownedScope?.Dispose();
            _gate.Dispose();
        }
    }
}
