# Alarm & Notification System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the navbar notification bell as a real operator alarm inbox — server-persisted alarms with an acknowledge trail, repeat/storm suppression, 30-day retention, audible + toast escalation, and a polished bell popover plus a working `/settings/notifications` page.

**Architecture:** A singleton `AlarmService` owns the whole alarm lifecycle: collapse-or-insert, persist, evaluate escalation policy, then fan out on its own thread-safe C# event. Producers (`ChannelManager`, `ChannelCommandHandler`) call `IAlarmService.RaiseAsync` and know nothing about the UI. `MainLayout` subscribes once and renders, replacing ~120 lines of per-device subscription bookkeeping and all IndexedDB storage.

**Tech Stack:** .NET 8, Blazor Server, EF Core (SQL Server, `AppDbContext`), Serilog, xunit 2.9.2, Tailwind CSS, Blazicons/Lucide.

**Spec:** `docs/superpowers/specs/2026-08-18-alarm-notification-system-design.md`

## Global Constraints

- Target framework `net8.0`. Do not add NuGet packages beyond what is listed in a task.
- Reuse the **existing** `SeverityLevel` enum (`Models/Enums/AuditLogEnums.cs:46` — `INFO=1, WARNING=2, ERROR=3, CRITICAL=4`). Do **not** create a new `AlarmSeverity` enum (this supersedes the spec's §4 naming).
- Repository methods return `CommonResponse<T>` (`.Ok(...)` / `.Fail(...)`) — follow `AuditRepository` exactly.
- Entities live in `Models/Entities`, use `[Table(name: "X", Schema = "Audit")]`, and are registered as a `DbSet` on `Data/AppDbContext.cs`.
- `AlarmService` is a **singleton** and must never inject `AppDbContext` or a repository directly — it resolves `IAlarmRepository` via `IServiceScopeFactory` per operation. Injecting a scoped service into it throws at startup.
- Logging is Serilog: `private readonly Serilog.ILogger _log = Log.ForContext<T>();`
- Acknowledge user comes from the existing static `Utils/CurrentUser.cs` (`CurrentUser.UserName`).
- All timestamps stored UTC (`DateTime.UtcNow`). Display converts to local.
- Config lives under the `Alarms` section of `appsettings.json`.
- Commit after every task. Never commit a non-building tree.

## Deviations from the spec (deliberate, agreed here)

1. `SeverityLevel` is reused instead of a new `AlarmSeverity` enum (it already exists with the exact four levels).
2. `AlarmChanged` carries a single `ShouldEscalate` flag rather than separate toast/beep flags. The server decides *whether this alarm deserves attention*; the client applies the user's toast/sound preferences. This keeps per-user preference out of the singleton.
3. Repository and UI layers are verified by build + manual smoke steps rather than automated tests. The test project currently contains two plain xunit files with no EF or bUnit harness; standing one up is a separate piece of work. All **logic** (collapse, re-arm, cooldown, storm guard, debounce, persist-before-fan-out) is covered by unit tests against a fake repository.

---

### Task 1: Alarm enums and entity

**Files:**
- Create: `Models/Enums/AlarmEnums.cs`
- Create: `Models/Entities/AlarmLog.cs`
- Modify: `Data/AppDbContext.cs:39` (add `DbSet`), `Data/AppDbContext.cs:50-81` (`OnModelCreating` indexes)

**Interfaces:**
- Consumes: existing `SeverityLevel` from `BatteryTestingSystem.Models.Enums`.
- Produces: `AlarmSource` enum; `AlarmLog` entity with the exact property names used by every later task: `Id`, `AlarmKey`, `Severity`, `Source`, `DeviceId`, `BoardNumber`, `ChannelNumber`, `Title`, `Message`, `FirstSeenUtc`, `LastSeenUtc`, `OccurrenceCount`, `AcknowledgedAtUtc`, `AcknowledgedBy`, `ClearedAtUtc`; `AppDbContext.AlarmLogs`.

- [ ] **Step 1: Create the source enum**

`Models/Enums/AlarmEnums.cs`:

```csharp
namespace BatteryTestingSystem.Models.Enums;

/// <summary>
/// Which subsystem produced an alarm. Used for filtering in the bell and the history page.
/// </summary>
public enum AlarmSource
{
    NONE = 0,
    /// <summary>Device TCP link lost / failed to reconnect.</summary>
    Comms = 1,
    /// <summary>Error or status code reported by the BMS for a channel.</summary>
    ChannelError = 2,
    /// <summary>Failure while persisting recorded data.</summary>
    DataStore = 3
}
```

- [ ] **Step 2: Create the entity**

`Models/Entities/AlarmLog.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.Entities
{
    /// <summary>
    /// One operator-facing alarm. A row is "active" while it is neither acknowledged nor
    /// cleared; repeats of the same AlarmKey collapse into the active row rather than
    /// inserting a new one.
    /// </summary>
    [Table(name: "AlarmLog", Schema = "Audit")]
    public class AlarmLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>Stable identity of the fault, e.g. "12/0/3/comms-loss".</summary>
        [Required]
        [MaxLength(200)]
        public string AlarmKey { get; set; } = string.Empty;

        public SeverityLevel Severity { get; set; } = SeverityLevel.INFO;

        public AlarmSource Source { get; set; } = AlarmSource.NONE;

        [MaxLength(50)]
        public string? DeviceId { get; set; }

        public int? BoardNumber { get; set; }

        public int? ChannelNumber { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

        public int OccurrenceCount { get; set; } = 1;

        public DateTime? AcknowledgedAtUtc { get; set; }

        [MaxLength(100)]
        public string? AcknowledgedBy { get; set; }

        public DateTime? ClearedAtUtc { get; set; }
    }
}
```

- [ ] **Step 3: Register the DbSet**

In `Data/AppDbContext.cs`, immediately after the `AuditLogs` line (`:39`):

```csharp
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<AlarmLog> AlarmLogs { get; set; } = null!;
```

- [ ] **Step 4: Add indexes in OnModelCreating**

In `Data/AppDbContext.cs`, directly above the existing `base.OnModelCreating(modelBuilder);` call:

```csharp
            modelBuilder.Entity<AlarmLog>(b =>
            {
                b.HasIndex(a => a.AlarmKey);
                b.HasIndex(a => a.LastSeenUtc);
                // Supports the "active rows" query the bell runs on every load.
                b.HasIndex(a => new { a.AcknowledgedAtUtc, a.ClearedAtUtc, a.LastSeenUtc });
            });
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Create the migration**

Run (from the project directory):

```bash
dotnet ef migrations add AlarmLogTable --context AppDbContext
```

If the EF CLI is unavailable, use the Package Manager Console equivalent documented at the top of `Data/AppDbContext.cs`: `Add-Migration AlarmLogTable -Context AppDbContext`.

Expected: two new files under `Migrations/` named `<timestamp>_AlarmLogTable.cs` and `.Designer.cs`.

- [ ] **Step 7: Inspect the generated migration**

Open the generated `Migrations/<timestamp>_AlarmLogTable.cs` and confirm the `Up` method **only** contains `CreateTable(name: "AlarmLog", schema: "Audit", ...)` and three `CreateIndex` calls. If it contains alters or drops against any other table, the model snapshot was stale — delete the migration, run `dotnet build`, and regenerate. Do not apply a migration that touches other tables.

- [ ] **Step 8: Commit**

```bash
git add Models/Enums/AlarmEnums.cs Models/Entities/AlarmLog.cs Data/AppDbContext.cs Migrations/
git commit -m "feat(alarms): add AlarmLog entity, AlarmSource enum and migration"
```

---

### Task 2: Alarm repository

**Files:**
- Create: `Repositories/Interfaces/IAlarmRepository.cs`
- Create: `Repositories/Implementations/AlarmRepository.cs`
- Create: `Models/ViewModels/AlarmQueryParameters.cs`
- Modify: `Extensions/ServiceCollectionExtensions.cs` (register in the repository region)

**Interfaces:**
- Consumes: `AlarmLog`, `AlarmSource`, `SeverityLevel` (Task 1); base `Repository<T>`; `CommonResponse<T>`.
- Produces: `IAlarmRepository` with exactly these members, relied on by Tasks 4, 5, 6 and 10:
  `Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey)`,
  `Task<AlarmLog> InsertAsync(AlarmLog alarm)`,
  `Task SaveAsync(AlarmLog alarm)`,
  `Task<List<AlarmLog>> GetActiveAsync(int take)`,
  `Task<int> CountUnacknowledgedAsync()`,
  `Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc)`,
  `Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc)`,
  `Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc)`,
  `Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request)`,
  `Task<int> PruneAsync(DateTime cutoffUtc, int batchSize)`.
  `AlarmQueryParameters` with `Severity`, `Source`, `DeviceId`, `FromDate`, `EndDate`, `OnlyUnacknowledged`.

- [ ] **Step 1: Create the query parameters view model**

`Models/ViewModels/AlarmQueryParameters.cs`:

```csharp
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Models.ViewModels
{
    public class AlarmQueryParameters
    {
        public SeverityLevel? Severity { get; set; }
        public AlarmSource? Source { get; set; }
        public string? DeviceId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool OnlyUnacknowledged { get; set; }
    }
}
```

- [ ] **Step 2: Create the interface**

`Repositories/Interfaces/IAlarmRepository.cs`:

```csharp
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Utils;

namespace BatteryTestingSystem.Repositories.Interfaces
{
    /// <summary>
    /// Persistence for operator alarms. An alarm is "active" while
    /// AcknowledgedAtUtc IS NULL AND ClearedAtUtc IS NULL.
    /// </summary>
    public interface IAlarmRepository
    {
        Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey);
        Task<AlarmLog> InsertAsync(AlarmLog alarm);
        Task SaveAsync(AlarmLog alarm);
        Task<List<AlarmLog>> GetActiveAsync(int take);
        Task<int> CountUnacknowledgedAsync();
        Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc);
        Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc);
        Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc);
        Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request);
        Task<int> PruneAsync(DateTime cutoffUtc, int batchSize);
    }
}
```

If `CommonResponse<T>` is not in `BatteryTestingSystem.Utils`, match the `using` block of `Repositories/Implementations/AuditRepository.cs`, which is the authority for that namespace.

- [ ] **Step 3: Create the implementation**

`Repositories/Implementations/AlarmRepository.cs`:

```csharp
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Repositories.Implementations
{
    public class AlarmRepository : Repository<AlarmLog>, IAlarmRepository
    {
        private readonly AppDbContext _appDbContext;

        public AlarmRepository(AppDbContext context) : base(context)
        {
            _appDbContext = context;
        }

        public async Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey)
        {
            return await _appDbContext.AlarmLogs
                .Where(a => a.AlarmKey == alarmKey
                            && a.AcknowledgedAtUtc == null
                            && a.ClearedAtUtc == null)
                .OrderByDescending(a => a.LastSeenUtc)
                .FirstOrDefaultAsync();
        }

        public async Task<AlarmLog> InsertAsync(AlarmLog alarm)
        {
            await _appDbContext.AlarmLogs.AddAsync(alarm);
            await _appDbContext.SaveChangesAsync();
            return alarm;
        }

        public async Task SaveAsync(AlarmLog alarm)
        {
            _appDbContext.AlarmLogs.Update(alarm);
            await _appDbContext.SaveChangesAsync();
        }

        public async Task<List<AlarmLog>> GetActiveAsync(int take)
        {
            return await _appDbContext.AlarmLogs
                .AsNoTracking()
                .Where(a => a.ClearedAtUtc == null)
                .OrderByDescending(a => a.AcknowledgedAtUtc == null)
                .ThenByDescending(a => a.LastSeenUtc)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> CountUnacknowledgedAsync()
        {
            return await _appDbContext.AlarmLogs
                .CountAsync(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null);
        }

        public async Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc)
        {
            var row = await _appDbContext.AlarmLogs.FirstOrDefaultAsync(a => a.Id == id);
            if (row == null || row.AcknowledgedAtUtc != null) return false;

            row.AcknowledgedAtUtc = whenUtc;
            row.AcknowledgedBy = user;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc)
        {
            var rows = await _appDbContext.AlarmLogs
                .Where(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null)
                .ToListAsync();

            foreach (var row in rows)
            {
                row.AcknowledgedAtUtc = whenUtc;
                row.AcknowledgedBy = user;
            }

            await _appDbContext.SaveChangesAsync();
            return rows.Count;
        }

        public async Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc)
        {
            var row = await GetActiveByKeyAsync(alarmKey);
            if (row == null) return false;

            row.ClearedAtUtc = whenUtc;
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request)
        {
            try
            {
                IQueryable<AlarmLog> query = _appDbContext.AlarmLogs.AsNoTracking();

                if (request != null)
                {
                    if (request.Severity.HasValue)
                        query = query.Where(a => a.Severity == request.Severity.Value);

                    if (request.Source.HasValue)
                        query = query.Where(a => a.Source == request.Source.Value);

                    if (!string.IsNullOrWhiteSpace(request.DeviceId))
                        query = query.Where(a => a.DeviceId == request.DeviceId);

                    if (request.FromDate.HasValue)
                        query = query.Where(a => a.LastSeenUtc >= request.FromDate.Value);

                    if (request.EndDate.HasValue)
                        query = query.Where(a => a.LastSeenUtc <= request.EndDate.Value);

                    if (request.OnlyUnacknowledged)
                        query = query.Where(a => a.AcknowledgedAtUtc == null);
                }

                var rows = await query.OrderByDescending(a => a.LastSeenUtc).ToListAsync();

                if (rows.Count == 0)
                    return CommonResponse<List<AlarmLog>>.Fail("No alarms found matching the criteria.");

                return CommonResponse<List<AlarmLog>>.Ok(rows);
            }
            catch (Exception ex)
            {
                return CommonResponse<List<AlarmLog>>.Fail($"Error fetching alarms: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes at most batchSize rows older than the cutoff. A row that is still
        /// unacknowledged and uncleared is NEVER pruned regardless of age — an unresolved
        /// fault must not disappear because it is old.
        /// </summary>
        public async Task<int> PruneAsync(DateTime cutoffUtc, int batchSize)
        {
            var ids = await _appDbContext.AlarmLogs
                .Where(a => a.LastSeenUtc < cutoffUtc
                            && (a.AcknowledgedAtUtc != null || a.ClearedAtUtc != null))
                .OrderBy(a => a.LastSeenUtc)
                .Select(a => a.Id)
                .Take(batchSize)
                .ToListAsync();

            if (ids.Count == 0) return 0;

            return await _appDbContext.AlarmLogs
                .Where(a => ids.Contains(a.Id))
                .ExecuteDeleteAsync();
        }
    }
}
```

- [ ] **Step 4: Register the repository**

In `Extensions/ServiceCollectionExtensions.cs`, in the repository region next to the other `AddScoped<I...Repository, ...Repository>()` lines:

```csharp
        services.AddScoped<IAlarmRepository, AlarmRepository>();
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: Build succeeded, 0 errors. If `ExecuteDeleteAsync` is unresolved, add `using Microsoft.EntityFrameworkCore;` (already present) and confirm the EF Core version is 7.0+.

- [ ] **Step 6: Commit**

```bash
git add Repositories/Interfaces/IAlarmRepository.cs Repositories/Implementations/AlarmRepository.cs Models/ViewModels/AlarmQueryParameters.cs Extensions/ServiceCollectionExtensions.cs
git commit -m "feat(alarms): add IAlarmRepository with collapse, acknowledge and prune queries"
```

---

### Task 3: Alarm options and escalation policy

This is the noise-control brain: cooldown, storm guard, mute. It is a pure class with no I/O so it is fully unit tested.

**Files:**
- Create: `Services/Alarms/AlarmOptions.cs`
- Create: `Services/Alarms/AlarmPolicy.cs`
- Create: `BatteryTestingSystem.Tests/Services/AlarmPolicyTests.cs`
- Modify: `appsettings.json`

**Interfaces:**
- Consumes: `SeverityLevel`.
- Produces: `AlarmOptions` (properties `RetentionDays`, `EscalationCooldownSec`, `StormThreshold`, `StormWindowMinutes`, `StormCooloffMinutes`, `FlushIntervalSeconds`, `BellActiveTake`); `EscalationDecision(bool Escalate, bool StormTriggered, int WindowCount)`; `AlarmPolicy` with `EscalationDecision Evaluate(string alarmKey, SeverityLevel severity, string? deviceId, DateTime nowUtc)`, `void MuteAll(DateTime untilUtc)`, `void MuteDevice(string deviceId, DateTime untilUtc)`, `void Unmute()`, `bool IsMuted(string? deviceId, DateTime nowUtc)`.

- [ ] **Step 1: Write the failing tests**

`BatteryTestingSystem.Tests/Services/AlarmPolicyTests.cs`:

```csharp
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Alarms;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class AlarmPolicyTests
{
    private static AlarmPolicy MakePolicy() => new(new AlarmOptions
    {
        EscalationCooldownSec = 60,
        StormThreshold = 3,
        StormWindowMinutes = 5,
        StormCooloffMinutes = 15
    });

    private static readonly DateTime T0 = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstCriticalAlarm_Escalates()
    {
        var policy = MakePolicy();

        var decision = policy.Evaluate("dev1/comms-loss", SeverityLevel.CRITICAL, "1", T0);

        Assert.True(decision.Escalate);
    }

    [Fact]
    public void InfoAndWarning_DoNotEscalate()
    {
        var policy = MakePolicy();

        Assert.False(policy.Evaluate("k1", SeverityLevel.INFO, "1", T0).Escalate);
        Assert.False(policy.Evaluate("k2", SeverityLevel.WARNING, "1", T0).Escalate);
    }

    [Fact]
    public void RepeatInsideCooldown_DoesNotEscalateAgain()
    {
        var policy = MakePolicy();
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0);

        var second = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(30));

        Assert.False(second.Escalate);
    }

    [Fact]
    public void RepeatAfterCooldown_EscalatesAgain()
    {
        var policy = MakePolicy();
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0);

        var later = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(61));

        Assert.True(later.Escalate);
    }

    [Fact]
    public void DifferentKeys_HaveIndependentCooldowns()
    {
        var policy = MakePolicy();
        policy.Evaluate("a", SeverityLevel.ERROR, "1", T0);

        var other = policy.Evaluate("b", SeverityLevel.ERROR, "1", T0.AddSeconds(1));

        Assert.True(other.Escalate);
    }

    [Fact]
    public void ExceedingStormThreshold_TriggersStormOnceAndSuppresses()
    {
        var policy = MakePolicy();

        // Threshold is 3 inside a 5 minute window.
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0);
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(10));
        policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(20));
        var storming = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(30));

        Assert.True(storming.StormTriggered);
        Assert.False(storming.Escalate);

        // Storm is announced exactly once, and escalation stays suppressed through cooloff.
        var afterStorm = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddMinutes(2));
        Assert.False(afterStorm.StormTriggered);
        Assert.False(afterStorm.Escalate);
    }

    [Fact]
    public void AfterStormCooloff_EscalationResumes()
    {
        var policy = MakePolicy();
        for (var i = 0; i < 4; i++)
            policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddSeconds(i * 10));

        var resumed = policy.Evaluate("k", SeverityLevel.ERROR, "1", T0.AddMinutes(16));

        Assert.True(resumed.Escalate);
    }

    [Fact]
    public void GlobalMute_SuppressesEscalation_UntilExpiry()
    {
        var policy = MakePolicy();
        policy.MuteAll(T0.AddMinutes(15));

        Assert.False(policy.Evaluate("k", SeverityLevel.CRITICAL, "1", T0.AddMinutes(1)).Escalate);
        Assert.True(policy.Evaluate("k", SeverityLevel.CRITICAL, "1", T0.AddMinutes(16)).Escalate);
    }

    [Fact]
    public void DeviceMute_SuppressesOnlyThatDevice()
    {
        var policy = MakePolicy();
        policy.MuteDevice("1", T0.AddMinutes(15));

        Assert.False(policy.Evaluate("k1", SeverityLevel.CRITICAL, "1", T0.AddMinutes(1)).Escalate);
        Assert.True(policy.Evaluate("k2", SeverityLevel.CRITICAL, "2", T0.AddMinutes(1)).Escalate);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~AlarmPolicyTests"`
Expected: compile error — `AlarmPolicy` / `AlarmOptions` do not exist.

- [ ] **Step 3: Create the options class**

`Services/Alarms/AlarmOptions.cs`:

```csharp
namespace BatteryTestingSystem.Services.Alarms
{
    /// <summary>
    /// Bound from the "Alarms" section of appsettings.json.
    /// </summary>
    public class AlarmOptions
    {
        public const string SectionName = "Alarms";

        /// <summary>How long alarms are kept before pruning.</summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>Minimum seconds between two escalations of the same alarm key.</summary>
        public int EscalationCooldownSec { get; set; } = 60;

        /// <summary>Occurrences of one key inside StormWindowMinutes that trip the storm guard.</summary>
        public int StormThreshold { get; set; } = 20;

        public int StormWindowMinutes { get; set; } = 5;

        /// <summary>How long escalation stays suppressed for a key after a storm trips.</summary>
        public int StormCooloffMinutes { get; set; } = 15;

        /// <summary>Minimum seconds between database writes for repeat bumps of one key.</summary>
        public int FlushIntervalSeconds { get; set; } = 5;

        /// <summary>How many alarms the bell loads.</summary>
        public int BellActiveTake { get; set; } = 50;
    }
}
```

- [ ] **Step 4: Create the policy**

`Services/Alarms/AlarmPolicy.cs`:

```csharp
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Alarms
{
    /// <summary>
    /// Result of the escalation decision for one raise.
    /// Escalate = interrupt the operator (toast / sound). StormTriggered = this raise tripped
    /// the storm guard and the caller should emit one summary alarm.
    /// </summary>
    public record EscalationDecision(bool Escalate, bool StormTriggered, int WindowCount);

    /// <summary>
    /// Decides how often an operator is interrupted. Deliberately separate from the alarm row
    /// itself: the row always records every occurrence (the truth), this class only governs
    /// attention. Pure and thread-safe; no I/O.
    /// </summary>
    public class AlarmPolicy
    {
        private sealed class KeyState
        {
            public DateTime? LastEscalatedUtc;
            public DateTime? StormSuppressedUntilUtc;
            public readonly List<DateTime> Window = new();
        }

        private readonly AlarmOptions _options;
        private readonly Dictionary<string, KeyState> _keys = new();
        private readonly Dictionary<string, DateTime> _mutedDevices = new();
        private readonly object _lock = new();

        private DateTime? _mutedAllUntilUtc;

        public AlarmPolicy(AlarmOptions options)
        {
            _options = options;
        }

        public void MuteAll(DateTime untilUtc)
        {
            lock (_lock) { _mutedAllUntilUtc = untilUtc; }
        }

        public void MuteDevice(string deviceId, DateTime untilUtc)
        {
            lock (_lock) { _mutedDevices[deviceId] = untilUtc; }
        }

        public void Unmute()
        {
            lock (_lock)
            {
                _mutedAllUntilUtc = null;
                _mutedDevices.Clear();
            }
        }

        public bool IsMuted(string? deviceId, DateTime nowUtc)
        {
            lock (_lock) { return IsMutedNoLock(deviceId, nowUtc); }
        }

        private bool IsMutedNoLock(string? deviceId, DateTime nowUtc)
        {
            if (_mutedAllUntilUtc.HasValue && _mutedAllUntilUtc.Value > nowUtc)
                return true;

            return deviceId != null
                   && _mutedDevices.TryGetValue(deviceId, out var until)
                   && until > nowUtc;
        }

        public EscalationDecision Evaluate(string alarmKey, SeverityLevel severity, string? deviceId, DateTime nowUtc)
        {
            lock (_lock)
            {
                if (!_keys.TryGetValue(alarmKey, out var state))
                {
                    state = new KeyState();
                    _keys[alarmKey] = state;
                }

                // Slide the storm window.
                var windowStart = nowUtc.AddMinutes(-_options.StormWindowMinutes);
                state.Window.RemoveAll(t => t < windowStart);
                state.Window.Add(nowUtc);
                var windowCount = state.Window.Count;

                // Trip the storm guard once, on the raise that crosses the threshold.
                var stormTriggered = false;
                if (windowCount > _options.StormThreshold
                    && (state.StormSuppressedUntilUtc == null || state.StormSuppressedUntilUtc <= nowUtc))
                {
                    stormTriggered = true;
                    state.StormSuppressedUntilUtc = nowUtc.AddMinutes(_options.StormCooloffMinutes);
                }

                if (stormTriggered)
                    return new EscalationDecision(false, true, windowCount);

                if (state.StormSuppressedUntilUtc.HasValue && state.StormSuppressedUntilUtc.Value > nowUtc)
                    return new EscalationDecision(false, false, windowCount);

                // Only Error and Critical interrupt the operator.
                if (severity < SeverityLevel.ERROR)
                    return new EscalationDecision(false, false, windowCount);

                if (IsMutedNoLock(deviceId, nowUtc))
                    return new EscalationDecision(false, false, windowCount);

                if (state.LastEscalatedUtc.HasValue
                    && (nowUtc - state.LastEscalatedUtc.Value).TotalSeconds < _options.EscalationCooldownSec)
                    return new EscalationDecision(false, false, windowCount);

                state.LastEscalatedUtc = nowUtc;
                return new EscalationDecision(true, false, windowCount);
            }
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~AlarmPolicyTests"`
Expected: 9 passed.

- [ ] **Step 6: Add the config section**

In `appsettings.json`, add a top-level section:

```json
  "Alarms": {
    "RetentionDays": 30,
    "EscalationCooldownSec": 60,
    "StormThreshold": 20,
    "StormWindowMinutes": 5,
    "StormCooloffMinutes": 15,
    "FlushIntervalSeconds": 5,
    "BellActiveTake": 50
  }
```

- [ ] **Step 7: Commit**

```bash
git add Services/Alarms/AlarmOptions.cs Services/Alarms/AlarmPolicy.cs BatteryTestingSystem.Tests/Services/AlarmPolicyTests.cs appsettings.json
git commit -m "feat(alarms): add escalation policy with cooldown, storm guard and mute"
```

---

### Task 4: AlarmService — raise, collapse, acknowledge, clear

**Files:**
- Create: `Services/Interfaces/IAlarmService.cs`
- Create: `Services/Alarms/AlarmService.cs`
- Create: `BatteryTestingSystem.Tests/Services/FakeAlarmRepository.cs`
- Create: `BatteryTestingSystem.Tests/Services/AlarmServiceTests.cs`
- Modify: `Extensions/ServiceCollectionExtensions.cs:73-82` (service region)

**Interfaces:**
- Consumes: `IAlarmRepository` (Task 2), `AlarmPolicy` + `AlarmOptions` (Task 3), `AlarmLog` (Task 1).
- Produces: `AlarmRequest` record, `AlarmChangeKind` enum, `AlarmChanged` record, and `IAlarmService` with `RaiseAsync`, `AcknowledgeAsync`, `AcknowledgeAllAsync`, `ClearAsync`, `GetActiveAsync`, `GetUnacknowledgedCountAsync`, `FlushPendingAsync`, `Policy`, and `event Action<AlarmChanged>? OnChanged` — consumed by Tasks 6, 7, 8, 9, 10.

- [ ] **Step 1: Write the fake repository the tests use**

`BatteryTestingSystem.Tests/Services/FakeAlarmRepository.cs`:

```csharp
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.ViewModels;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Utils;

namespace BatteryTestingSystem.Tests.Services;

/// <summary>
/// In-memory stand-in for IAlarmRepository. Also counts writes so tests can assert the
/// debounce actually prevents database churn.
/// </summary>
public class FakeAlarmRepository : IAlarmRepository
{
    public readonly List<AlarmLog> Rows = new();
    public int InsertCount;
    public int SaveCount;

    private long _nextId = 1;

    public Task<AlarmLog?> GetActiveByKeyAsync(string alarmKey) =>
        Task.FromResult(Rows.FirstOrDefault(a =>
            a.AlarmKey == alarmKey && a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null));

    public Task<AlarmLog> InsertAsync(AlarmLog alarm)
    {
        alarm.Id = _nextId++;
        Rows.Add(alarm);
        InsertCount++;
        return Task.FromResult(alarm);
    }

    public Task SaveAsync(AlarmLog alarm)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<List<AlarmLog>> GetActiveAsync(int take) =>
        Task.FromResult(Rows.Where(a => a.ClearedAtUtc == null)
            .OrderByDescending(a => a.LastSeenUtc).Take(take).ToList());

    public Task<int> CountUnacknowledgedAsync() =>
        Task.FromResult(Rows.Count(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null));

    public Task<bool> AcknowledgeAsync(long id, string user, DateTime whenUtc)
    {
        var row = Rows.FirstOrDefault(a => a.Id == id);
        if (row == null || row.AcknowledgedAtUtc != null) return Task.FromResult(false);
        row.AcknowledgedAtUtc = whenUtc;
        row.AcknowledgedBy = user;
        return Task.FromResult(true);
    }

    public Task<int> AcknowledgeAllAsync(string user, DateTime whenUtc)
    {
        var rows = Rows.Where(a => a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null).ToList();
        foreach (var row in rows)
        {
            row.AcknowledgedAtUtc = whenUtc;
            row.AcknowledgedBy = user;
        }
        return Task.FromResult(rows.Count);
    }

    public Task<bool> ClearByKeyAsync(string alarmKey, DateTime whenUtc)
    {
        var row = Rows.FirstOrDefault(a =>
            a.AlarmKey == alarmKey && a.AcknowledgedAtUtc == null && a.ClearedAtUtc == null);
        if (row == null) return Task.FromResult(false);
        row.ClearedAtUtc = whenUtc;
        return Task.FromResult(true);
    }

    public Task<CommonResponse<List<AlarmLog>>> QueryAsync(AlarmQueryParameters? request) =>
        Task.FromResult(CommonResponse<List<AlarmLog>>.Ok(Rows.ToList()));

    public Task<int> PruneAsync(DateTime cutoffUtc, int batchSize)
    {
        var doomed = Rows.Where(a => a.LastSeenUtc < cutoffUtc
                                     && (a.AcknowledgedAtUtc != null || a.ClearedAtUtc != null))
                         .Take(batchSize).ToList();
        foreach (var row in doomed) Rows.Remove(row);
        return Task.FromResult(doomed.Count);
    }
}
```

- [ ] **Step 2: Write the failing service tests**

`BatteryTestingSystem.Tests/Services/AlarmServiceTests.cs`:

```csharp
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Alarms;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class AlarmServiceTests
{
    private static readonly DateTime T0 = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    private static (AlarmService Service, FakeAlarmRepository Repo) Make(DateTime now)
    {
        var repo = new FakeAlarmRepository();
        var options = new AlarmOptions
        {
            EscalationCooldownSec = 60,
            StormThreshold = 100,
            StormWindowMinutes = 5,
            FlushIntervalSeconds = 5,
            BellActiveTake = 50
        };
        var service = new AlarmService(() => repo, options, new AlarmPolicy(options))
        {
            UtcNow = () => now
        };
        return (service, repo);
    }

    private static AlarmRequest Request(string key = "1/0/3/channel-error") => new()
    {
        AlarmKey = key,
        Severity = SeverityLevel.ERROR,
        Source = AlarmSource.ChannelError,
        DeviceId = "1",
        BoardNumber = 0,
        ChannelNumber = 3,
        Title = "Channel error",
        Message = "Over voltage"
    };

    [Fact]
    public async Task FirstRaise_InsertsOneRow()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());

        Assert.Single(repo.Rows);
        Assert.Equal(1, repo.Rows[0].OccurrenceCount);
        Assert.Equal(1, repo.InsertCount);
    }

    [Fact]
    public async Task RepeatRaise_CollapsesIntoOneRowWithCount()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());
        await service.RaiseAsync(Request());
        await service.RaiseAsync(Request());
        await service.FlushPendingAsync();

        Assert.Single(repo.Rows);
        Assert.Equal(3, repo.Rows[0].OccurrenceCount);
    }

    [Fact]
    public async Task RepeatRaise_IsDebounced_AndFlushedOnDemand()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());   // insert, always written
        await service.RaiseAsync(Request());   // bump inside flush interval -> queued
        await service.RaiseAsync(Request());   // bump inside flush interval -> queued

        Assert.Equal(0, repo.SaveCount);

        await service.FlushPendingAsync();

        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(3, repo.Rows[0].OccurrenceCount);
    }

    [Fact]
    public async Task RaiseAfterAcknowledge_OpensANewRow()
    {
        var (service, repo) = Make(T0);
        await service.RaiseAsync(Request());
        await service.AcknowledgeAsync(repo.Rows[0].Id, "operator1");

        await service.RaiseAsync(Request());

        Assert.Equal(2, repo.Rows.Count);
    }

    [Fact]
    public async Task Acknowledge_RecordsUserAndTimestamp()
    {
        var (service, repo) = Make(T0);
        await service.RaiseAsync(Request());

        await service.AcknowledgeAsync(repo.Rows[0].Id, "operator1");

        Assert.Equal("operator1", repo.Rows[0].AcknowledgedBy);
        Assert.Equal(T0, repo.Rows[0].AcknowledgedAtUtc);
    }

    [Fact]
    public async Task Clear_RemovesAlarmFromActiveList()
    {
        var (service, _) = Make(T0);
        await service.RaiseAsync(Request("1/comms-loss"));

        await service.ClearAsync("1/comms-loss");
        var active = await service.GetActiveAsync(50);

        Assert.Empty(active);
    }

    [Fact]
    public async Task RaiseWithNoSubscriber_StillPersists()
    {
        var (service, repo) = Make(T0);

        await service.RaiseAsync(Request());

        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task Raise_NotifiesSubscribersWithEscalationFlag()
    {
        var (service, _) = Make(T0);
        AlarmChanged? received = null;
        service.OnChanged += c => received = c;

        await service.RaiseAsync(Request());

        Assert.NotNull(received);
        Assert.Equal(AlarmChangeKind.Raised, received!.Kind);
        Assert.True(received.ShouldEscalate);
    }

    [Fact]
    public async Task StormGuard_EmitsOneSummaryAlarm()
    {
        var repo = new FakeAlarmRepository();
        var options = new AlarmOptions
        {
            EscalationCooldownSec = 0,
            StormThreshold = 3,
            StormWindowMinutes = 5,
            StormCooloffMinutes = 15,
            FlushIntervalSeconds = 0
        };
        var service = new AlarmService(() => repo, options, new AlarmPolicy(options))
        {
            UtcNow = () => T0
        };

        for (var i = 0; i < 5; i++)
            await service.RaiseAsync(Request("1/0/3/flapping"));

        var summaries = repo.Rows.Count(r => r.AlarmKey.EndsWith("/storm"));
        Assert.Equal(1, summaries);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~AlarmServiceTests"`
Expected: compile error — `AlarmService`, `AlarmRequest`, `AlarmChanged` do not exist.

- [ ] **Step 4: Create the service contract**

`Services/Interfaces/IAlarmService.cs`:

```csharp
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Alarms;

namespace BatteryTestingSystem.Services.Interfaces
{
    /// <summary>One request to raise an alarm. Producers build this and nothing else.</summary>
    public class AlarmRequest
    {
        /// <summary>Stable identity of the fault; repeats of the same key collapse.</summary>
        public string AlarmKey { get; set; } = string.Empty;
        public SeverityLevel Severity { get; set; } = SeverityLevel.WARNING;
        public AlarmSource Source { get; set; } = AlarmSource.NONE;
        public string? DeviceId { get; set; }
        public int? BoardNumber { get; set; }
        public int? ChannelNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public enum AlarmChangeKind
    {
        Raised = 1,
        Collapsed = 2,
        Acknowledged = 3,
        Cleared = 4
    }

    /// <summary>
    /// Fan-out payload. ShouldEscalate means "this deserves the operator's attention now";
    /// the client decides whether that becomes a toast, a sound, or both, from user prefs.
    /// </summary>
    public record AlarmChanged(AlarmLog Alarm, AlarmChangeKind Kind, bool ShouldEscalate);

    public interface IAlarmService
    {
        Task RaiseAsync(AlarmRequest req);
        Task<bool> AcknowledgeAsync(long id, string user);
        Task<int> AcknowledgeAllAsync(string user);
        Task<bool> ClearAsync(string alarmKey);
        Task<IReadOnlyList<AlarmLog>> GetActiveAsync(int take);
        Task<int> GetUnacknowledgedCountAsync();
        /// <summary>Writes any debounced repeat bumps. Called by the flush timer and on shutdown.</summary>
        Task FlushPendingAsync();
        AlarmPolicy Policy { get; }
        event Action<AlarmChanged>? OnChanged;
    }
}
```

- [ ] **Step 5: Implement the service**

`Services/Alarms/AlarmService.cs`:

```csharp
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
    /// scoped AppDbContext — it resolves IAlarmRepository per operation instead.
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

                    // Inserts are ALWAYS written synchronously — the debounce below applies
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
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~AlarmServiceTests"`
Expected: 9 passed.

- [ ] **Step 7: Register in DI**

In `Extensions/ServiceCollectionExtensions.cs`, in the `#region Service` block right after `services.AddSingleton<EventBusService>();`:

```csharp
        services.Configure<AlarmOptions>(configuration.GetSection(AlarmOptions.SectionName));
        services.AddSingleton<AlarmPolicy>(sp =>
            new AlarmPolicy(sp.GetRequiredService<IOptions<AlarmOptions>>().Value));
        services.AddSingleton<IAlarmService, AlarmService>();
```

Add `using BatteryTestingSystem.Services.Alarms;`, `using BatteryTestingSystem.Services.Interfaces;` and `using Microsoft.Extensions.Options;` at the top of the file. If the extension method does not already receive `IConfiguration configuration`, add it as a parameter and pass `builder.Configuration` from the call site in `Program.cs`.

- [ ] **Step 8: Verify the app still starts**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: Build succeeded. A DI misconfiguration here (scoped into singleton) surfaces at startup, not build — Task 7 exercises it at runtime.

- [ ] **Step 9: Commit**

```bash
git add Services/Interfaces/IAlarmService.cs Services/Alarms/AlarmService.cs BatteryTestingSystem.Tests/Services/ Extensions/ServiceCollectionExtensions.cs
git commit -m "feat(alarms): add AlarmService with collapse, re-arm, debounce and storm summary"
```

---

### Task 5: Retention background service

**Files:**
- Create: `Services/Alarms/AlarmRetentionService.cs`
- Create: `BatteryTestingSystem.Tests/Services/AlarmRetentionTests.cs`
- Modify: `Extensions/ServiceCollectionExtensions.cs` (hosted service registration)

**Interfaces:**
- Consumes: `IAlarmRepository.PruneAsync` (Task 2), `AlarmOptions.RetentionDays` (Task 3).
- Produces: `AlarmRetentionService` hosted service; `AlarmRetention.CutoffUtc(DateTime nowUtc, int retentionDays)` static helper.

- [ ] **Step 1: Write the failing test**

`BatteryTestingSystem.Tests/Services/AlarmRetentionTests.cs`:

```csharp
using BatteryTestingSystem.Models.Entities;
using BatteryTestingSystem.Services.Alarms;
using Xunit;

namespace BatteryTestingSystem.Tests.Services;

public class AlarmRetentionTests
{
    private static readonly DateTime Now = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Cutoff_IsRetentionDaysBeforeNow()
    {
        var cutoff = AlarmRetention.CutoffUtc(Now, 30);

        Assert.Equal(new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc), cutoff);
    }

    [Fact]
    public async Task Prune_DeletesOldAcknowledgedRows()
    {
        var repo = new FakeAlarmRepository();
        await repo.InsertAsync(new AlarmLog
        {
            AlarmKey = "old-acked",
            LastSeenUtc = Now.AddDays(-40),
            AcknowledgedAtUtc = Now.AddDays(-40)
        });

        var deleted = await repo.PruneAsync(AlarmRetention.CutoffUtc(Now, 30), 500);

        Assert.Equal(1, deleted);
        Assert.Empty(repo.Rows);
    }

    [Fact]
    public async Task Prune_KeepsOldRowThatIsStillUnacknowledgedAndUncleared()
    {
        var repo = new FakeAlarmRepository();
        await repo.InsertAsync(new AlarmLog
        {
            AlarmKey = "old-unresolved",
            LastSeenUtc = Now.AddDays(-99)
        });

        var deleted = await repo.PruneAsync(AlarmRetention.CutoffUtc(Now, 30), 500);

        Assert.Equal(0, deleted);
        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task Prune_KeepsRecentRows()
    {
        var repo = new FakeAlarmRepository();
        await repo.InsertAsync(new AlarmLog
        {
            AlarmKey = "recent",
            LastSeenUtc = Now.AddDays(-2),
            AcknowledgedAtUtc = Now.AddDays(-2)
        });

        var deleted = await repo.PruneAsync(AlarmRetention.CutoffUtc(Now, 30), 500);

        Assert.Equal(0, deleted);
        Assert.Single(repo.Rows);
    }
}
```

Note: `FakeAlarmRepository.PruneAsync` mirrors the real EF predicate in `AlarmRepository.PruneAsync`. These tests pin the retention *rule*; Step 6 verifies the real SQL predicate against the database.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~AlarmRetentionTests"`
Expected: compile error — `AlarmRetention` does not exist.

- [ ] **Step 3: Implement the retention service**

`Services/Alarms/AlarmRetentionService.cs`:

```csharp
using BatteryTestingSystem.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BatteryTestingSystem.Services.Alarms
{
    public static class AlarmRetention
    {
        public static DateTime CutoffUtc(DateTime nowUtc, int retentionDays)
            => nowUtc.AddDays(-retentionDays);

        /// <summary>Rows deleted per batch, to avoid a long write lock on the first run.</summary>
        public const int BatchSize = 500;

        /// <summary>Safety stop so one pass cannot run unbounded.</summary>
        public const int MaxBatchesPerPass = 200;
    }

    /// <summary>
    /// Prunes alarms older than Alarms:RetentionDays. Runs once shortly after startup, then
    /// daily. Never deletes a row that is still unacknowledged and uncleared.
    /// </summary>
    public class AlarmRetentionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AlarmOptions _options;
        private readonly Serilog.ILogger _log = Log.ForContext<AlarmRetentionService>();

        public AlarmRetentionService(IServiceScopeFactory scopeFactory, IOptions<AlarmOptions> options)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the app finish starting before touching the database.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PruneOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Alarm retention pass failed");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task PruneOnceAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();

            var cutoff = AlarmRetention.CutoffUtc(DateTime.UtcNow, _options.RetentionDays);
            var total = 0;

            for (var i = 0; i < AlarmRetention.MaxBatchesPerPass && !token.IsCancellationRequested; i++)
            {
                var deleted = await repo.PruneAsync(cutoff, AlarmRetention.BatchSize);
                total += deleted;
                if (deleted < AlarmRetention.BatchSize) break;
            }

            if (total > 0)
                _log.Information("Alarm retention pruned {Count} rows older than {Cutoff:u}", total, cutoff);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~AlarmRetentionTests"`
Expected: 4 passed.

- [ ] **Step 5: Register the hosted service**

In `Extensions/ServiceCollectionExtensions.cs`, next to the existing `services.AddHostedService(...)` line for `ChannelManager`:

```csharp
        services.AddHostedService<AlarmRetentionService>();
```

- [ ] **Step 6: Verify the real SQL predicate**

After Task 7 is running and alarms exist, run this against the database and confirm zero rows come back — it is the query the prune would delete, restricted to rows that must survive:

```sql
SELECT COUNT(*) FROM Audit.AlarmLog
WHERE LastSeenUtc < DATEADD(day, -30, GETUTCDATE())
  AND AcknowledgedAtUtc IS NULL AND ClearedAtUtc IS NULL
  AND 1 = 0; -- guard clause mirror: these rows are excluded by PruneAsync
```

Then confirm the prune target query returns only acknowledged/cleared rows:

```sql
SELECT TOP 10 Id, AlarmKey, LastSeenUtc, AcknowledgedAtUtc, ClearedAtUtc
FROM Audit.AlarmLog
WHERE LastSeenUtc < DATEADD(day, -30, GETUTCDATE())
  AND (AcknowledgedAtUtc IS NOT NULL OR ClearedAtUtc IS NOT NULL);
```

- [ ] **Step 7: Commit**

```bash
git add Services/Alarms/AlarmRetentionService.cs BatteryTestingSystem.Tests/Services/AlarmRetentionTests.cs Extensions/ServiceCollectionExtensions.cs
git commit -m "feat(alarms): add 30-day retention service that never prunes unresolved alarms"
```

---

### Task 6: Producers — comms loss, store failures, channel errors

**Files:**
- Modify: `Services/ChannelManager.cs:97-102` (constructor), `:833-853` (`TrySendFailureNotification`), `:982-1000` (disconnect cleanup), registration/reconnect path in `ProcessRegistrationPacketAsync`
- Modify: `Services/Implementations/ChannelCommandHandler.cs:91` (remove `OnNotify`), `:246-249` (raise), `:290-296` (remove `SendNotification`)
- Modify: `Services/Interfaces/IChannelCommandHandler.cs` (remove the notification members)

**Interfaces:**
- Consumes: `IAlarmService.RaiseAsync` / `ClearAsync`, `AlarmRequest`, `AlarmSource`, `SeverityLevel`.
- Produces: alarm keys in the canonical format `"{deviceId}/{board}/{channel}/{kind}"` where kind is one of `comms-loss`, `channel-error`, `store-failed`. Task 8's deep-link parsing relies on `DeviceId`/`BoardNumber`/`ChannelNumber` being populated, not on parsing the key.

- [ ] **Step 1: Inject the alarm service into ChannelManager**

`ChannelManager` is a singleton, and `IAlarmService` is a singleton, so it injects directly. Change the constructor at `Services/ChannelManager.cs:97`:

```csharp
        private readonly Func<IChannelCommandHandler> _handlerFactory;

        private readonly EventBusService _eventBus;

        private readonly IAlarmService _alarms;

        public ChannelManager(Func<IChannelCommandHandler> handlerFactory, EventBusService eventBus, IAlarmService alarms)
        {
            _handlerFactory = handlerFactory;
            _eventBus = eventBus;
            _alarms = alarms;
            _lifecycleCts = new CancellationTokenSource();
        }
```

Add `using BatteryTestingSystem.Services.Interfaces;` and `using BatteryTestingSystem.Models.Enums;` if not already present.

- [ ] **Step 2: Replace the UDP store-failure notification**

Replace the body of `TrySendFailureNotification` (`Services/ChannelManager.cs:833-853`) with:

```csharp
        private async Task TrySendFailureNotification(byte[]? payload, string message)
        {
            if (payload == null || payload.Length < 2) return;

            int deviceId = payload[0];
            // NOTE: unverified whether payload[1] is genuinely a full board/channel address byte
            // or a bare legacy channel number — confirm what actually produces this packet before
            // trusting this decode. See design doc risk #7.
            var (boardNumber, channelNumber) = Utils.ChannelAddressCodec.Decode(payload[1]);

            var handler = Get(new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = boardNumber, ChannelNumber = channelNumber });
            if (!handler.Success || handler.Data == null) return;

            await _alarms.RaiseAsync(new AlarmRequest
            {
                AlarmKey = $"{deviceId}/{boardNumber}/{channelNumber}/store-failed",
                Severity = SeverityLevel.ERROR,
                Source = AlarmSource.DataStore,
                DeviceId = deviceId.ToString(),
                BoardNumber = (int)boardNumber,
                ChannelNumber = (int)channelNumber,
                Title = $"Data store failed - device {deviceId} ch {channelNumber}",
                Message = message
            });
        }
```

If `boardNumber` / `channelNumber` are already `int`, drop the casts.

- [ ] **Step 3: Raise a comms-loss alarm on disconnect**

In the `finally` block of `HandleCommandClientAsync` (`Services/ChannelManager.cs:982-1000`), inside the existing `if (deviceConnection != null && ReferenceEquals(...))` guard, after the `foreach` that marks slots disconnected:

```csharp
                    foreach (var slotKey in deviceConnection.ChannelSlotKeys)
                    {
                        if (_devices.TryGetValue(slotKey, out var slotHandler))
                            slotHandler.MarkDisconnected();
                    }

                    // One alarm per device link, not per channel slot — a dead cable is one
                    // fault, and raising it per channel is exactly the noise the storm guard
                    // would otherwise have to clean up.
                    var lostDeviceId = deviceConnection.ChannelSlotKeys
                        .Select(k => k.Split('-').FirstOrDefault())
                        .FirstOrDefault(d => !string.IsNullOrEmpty(d));

                    if (!string.IsNullOrEmpty(lostDeviceId))
                    {
                        await _alarms.RaiseAsync(new AlarmRequest
                        {
                            AlarmKey = $"{lostDeviceId}/comms-loss",
                            Severity = SeverityLevel.CRITICAL,
                            Source = AlarmSource.Comms,
                            DeviceId = lostDeviceId,
                            Title = $"Device {lostDeviceId} disconnected",
                            Message = "TCP command connection closed. No data is being recorded " +
                                      "for this device until it reconnects."
                        });
                    }
```

`MakeKey` builds slot keys as `"{deviceId}-{boardNumber}-{channelId}"`, which is why the device id is the first `-`-separated segment.

- [ ] **Step 4: Clear the comms alarm on successful registration**

In `ProcessRegistrationPacketAsync`, immediately after the registration packet is confirmed valid and the channel is accepted (right after the `newChannel` null-check passes), add:

```csharp
            // A device that registers again has a working link: retire the outstanding
            // comms-loss alarm rather than leaving a stale fault in the bell.
            await _alarms.ClearAsync($"{newChannel.DeviceID}/comms-loss");
```

- [ ] **Step 5: Move channel errors onto the alarm service**

In `Services/Implementations/ChannelCommandHandler.cs`, replace the block at `:246-249`:

```csharp
                    if (!string.IsNullOrEmpty(noti))
                    {
                        OnNotify?.Invoke(new NotificationItem { Id = $"{Channel?.DeviceID}-{Channel?.SecondaryBoardNumber}-{Channel?.ChannelNumber}-Errors", Message = noti, Timestamp = DateTime.Now });
                    }
```

with:

```csharp
                    if (!string.IsNullOrEmpty(noti) && Alarms != null)
                    {
                        await Alarms.RaiseAsync(new AlarmRequest
                        {
                            AlarmKey = $"{Channel?.DeviceID}/{Channel?.SecondaryBoardNumber}/{Channel?.ChannelNumber}/channel-error",
                            Severity = SeverityLevel.ERROR,
                            Source = AlarmSource.ChannelError,
                            DeviceId = Channel?.DeviceID.ToString(),
                            BoardNumber = (int?)Channel?.SecondaryBoardNumber,
                            ChannelNumber = (int?)Channel?.ChannelNumber,
                            Title = $"Channel error - device {Channel?.DeviceID} ch {Channel?.ChannelNumber}",
                            Message = noti
                        });
                    }
```

- [ ] **Step 6: Give the handler an alarm service reference**

`ChannelCommandHandler` is constructed by a factory (`Extensions/ServiceCollectionExtensions.cs:75-78`), not by DI, so it takes the service as a settable property. Replace the `OnNotify` event declaration at `ChannelCommandHandler.cs:91`:

```csharp
        public event Action<NotificationItem>? OnNotify;
```

with:

```csharp
        /// <summary>Set by the factory in ServiceCollectionExtensions; null in preview instances.</summary>
        public IAlarmService? Alarms { get; set; }
```

Then update the factory registration:

```csharp
        services.AddSingleton<Func<IChannelCommandHandler>>(sp =>
        {
            return () => new ChannelCommandHandler
            {
                Alarms = sp.GetRequiredService<IAlarmService>()
            };
        });
```

Add `IAlarmService? Alarms { get; set; }` to `Services/Interfaces/IChannelCommandHandler.cs` and remove `event Action<NotificationItem>? OnNotify;` and `Task SendNotification(NotificationItem notification);` from it.

- [ ] **Step 7: Delete SendNotification**

Remove the `#region SendNotifications` block (`ChannelCommandHandler.cs:290-296`) entirely.

- [ ] **Step 8: Build**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: errors ONLY in `Components/Layout/MainLayout.razor` (it still subscribes to `OnNotify`) — Task 7 fixes those. If `Components/UI/Dashboard/PreviewChannelCommandHandler.cs` errors, give it the same `Alarms` property, left null.

- [ ] **Step 9: Commit**

```bash
git add Services/ChannelManager.cs Services/Implementations/ChannelCommandHandler.cs Services/Interfaces/IChannelCommandHandler.cs Extensions/ServiceCollectionExtensions.cs Components/UI/Dashboard/PreviewChannelCommandHandler.cs
git commit -m "feat(alarms): raise comms-loss, store-failure and channel-error alarms via IAlarmService"
```

---

### Task 7: MainLayout — subscribe once, drop IndexedDB

**Files:**
- Modify: `Components/Layout/MainLayout.razor:1-192` and `:294-345`
- Modify: `Components/Layout/AppLayout.razor:1-66`
- Create: `wwwroot/js/alarm-sound.js`
- Modify: `Components/App.razor:19-25` (script tag)

**Interfaces:**
- Consumes: `IAlarmService` (Task 4), `AlarmLog` (Task 1), existing scoped `ToastService`.
- Produces: `AppLayout` parameters `Alarms` (`List<AlarmLog>?`), `OnAlarmAcknowledge` (`EventCallback<long>`), `OnAlarmAcknowledgeAll` (`EventCallback`), `OnAlarmActivate` (`EventCallback<AlarmLog>`) — consumed by Task 8's `Navbar`.

- [ ] **Step 1: Add the beep interop**

`wwwroot/js/alarm-sound.js`:

```javascript
// Short alarm beep generated with WebAudio - no asset file, no autoplay of media elements.
// Browsers block audio before the first user gesture; a blocked beep must never break
// rendering, so every failure is swallowed.
window.AlarmSound = {
    beep: function (severity) {
        try {
            const Ctx = window.AudioContext || window.webkitAudioContext;
            if (!Ctx) return;

            if (!window.__alarmAudioCtx) {
                window.__alarmAudioCtx = new Ctx();
            }
            const ctx = window.__alarmAudioCtx;
            if (ctx.state === 'suspended') {
                ctx.resume();
            }

            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'sine';
            osc.frequency.value = severity === 'CRITICAL' ? 880 : 620;
            gain.gain.value = 0.08;

            osc.connect(gain);
            gain.connect(ctx.destination);

            const now = ctx.currentTime;
            osc.start(now);
            gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.35);
            osc.stop(now + 0.36);
        } catch (e) {
            // Deliberately silent.
        }
    }
};
```

- [ ] **Step 2: Register the script**

In `Components/App.razor`, after the `elementSize.js` line (`:25`):

```html
    <script src="js/alarm-sound.js?version=0.1"></script>
```

- [ ] **Step 3: Update AppLayout parameters**

In `Components/Layout/AppLayout.razor`, replace the `Notifications` parameter and the notification callback:

```razor
    [Parameter]
    public List<AlarmLog>? Alarms { get; set; }

    [Parameter]
    public EventCallback<long> OnAlarmAcknowledge { get; set; }

    [Parameter]
    public EventCallback OnAlarmAcknowledgeAll { get; set; }

    [Parameter]
    public EventCallback<AlarmLog> OnAlarmActivate { get; set; }
```

Delete the `Notifications` and `OnNotificationClick` parameters. Update the `<Navbar ... />` call at `:4-11` to forward the new ones:

```razor
    <Navbar BrandName="@BrandName"
            MenuItems="@MenuItems"
            Alarms="@Alarms"
            CurrentTheme="@CurrentTheme"
            OnNavigate="@OnNavigate"
            OnLogout="@OnLogout"
            OnThemeChange="@OnThemeChange"
            OnAlarmAcknowledge="@OnAlarmAcknowledge"
            OnAlarmAcknowledgeAll="@OnAlarmAcknowledgeAll"
            OnAlarmActivate="@OnAlarmActivate" />
```

Add `@using BatteryTestingSystem.Models.Entities` at the top.

- [ ] **Step 4: Rewrite the MainLayout notification region**

In `Components/Layout/MainLayout.razor`, add the injects:

```razor
@inject IAlarmService AlarmSvc
@inject ToastService Toasts
```

Replace the ENTIRE `#region Notifications` block (`:38-192` — `_subscribedDevices`, `_notifications`, `MaxNoti`, `OnInitializedAsync`, `OnAfterRenderAsync`'s IndexedDB load, `SubscribeToDevices`, `UnsubscribeFromDevices`, the device re-sync handler, `AddNotification`, `SaveNotificationsToIndexedDb`) with:

```razor
    #region Alarms
    private List<AlarmLog> _alarms = new();

    protected override async Task OnInitializedAsync()
    {
        AlarmSvc.OnChanged += HandleAlarmChanged;
        await ReloadAlarmsAsync();
    }

    private async Task ReloadAlarmsAsync()
    {
        try
        {
            var active = await AlarmSvc.GetActiveAsync(50);
            _alarms = active.ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load alarms");
        }
    }

    private async void HandleAlarmChanged(AlarmChanged change)
    {
        try
        {
            await ReloadAlarmsAsync();

            if (change.ShouldEscalate)
            {
                Toasts.Error(change.Alarm.Title, change.Alarm.Message);
                await JS.InvokeVoidAsync("AlarmSound.beep", change.Alarm.Severity.ToString());
            }

            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HandleAlarmChanged failed");
        }
    }

    private async Task HandleAlarmAcknowledge(long id)
    {
        await AlarmSvc.AcknowledgeAsync(id, CurrentUser.UserName);
        await ReloadAlarmsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleAlarmAcknowledgeAll()
    {
        await AlarmSvc.AcknowledgeAllAsync(CurrentUser.UserName);
        await ReloadAlarmsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void HandleAlarmActivate(AlarmLog alarm)
    {
        // Deep link: open the Circuits tab so the operator lands on the faulty device.
        tbService.AddTab(new NavMenuItem
        {
            Title = "Circuits",
            Url = "/device/list",
            Icon = Lucide.Settings2,
            ComponentType = typeof(DeviceList),
            Unique = true
        });
    }
    #endregion
```

- [ ] **Step 5: Update the AppLayout usage and Dispose**

Change the `<AppLayout ...>` attributes at `MainLayout.razor:18` and `:27`:

```razor
Alarms="@_alarms"
```
```razor
OnAlarmAcknowledge="@HandleAlarmAcknowledge"
OnAlarmAcknowledgeAll="@HandleAlarmAcknowledgeAll"
OnAlarmActivate="@HandleAlarmActivate">
```

Delete `Notifications="@_notifications"` and `OnNotificationClick="@HandleNotificationClick"`.

In the existing `Dispose()` method, replace any `device.OnNotify -= AddNotification;` loop with:

```csharp
        AlarmSvc.OnChanged -= HandleAlarmChanged;
```

Also delete `HandleNotificationClick` (`:294-306`) and `RemoveNotificationAfterDelay` (`:308-328`).

- [ ] **Step 6: Build**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: errors ONLY in `Components/Layout/Navbar.razor` (still expects `Notifications`) — Task 8 fixes those.

- [ ] **Step 7: Commit**

```bash
git add Components/Layout/MainLayout.razor Components/Layout/AppLayout.razor wwwroot/js/alarm-sound.js Components/App.razor
git commit -m "refactor(alarms): subscribe MainLayout to AlarmService, drop IndexedDB and per-device wiring"
```

---

### Task 8: Bell popover redesign

**Files:**
- Modify: `Components/Layout/Navbar.razor:69-119` (popover), `:375-448` (code block)

**Interfaces:**
- Consumes: `AlarmLog`, `SeverityLevel`, and the `Alarms` / `OnAlarmAcknowledge` / `OnAlarmAcknowledgeAll` / `OnAlarmActivate` parameters from Task 7.
- Produces: nothing consumed downstream.

- [ ] **Step 1: Replace the popover markup**

In `Components/Layout/Navbar.razor`, replace lines 69-119 (the whole `@* Notifications Popover *@` block) with:

```razor
            @* Alarms Popover *@
            <Popover>
                <PopoverTrigger>
                    <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon" Class="relative" title="Alarms">
                        <Blazicon Svg="@(HasCritical ? Lucide.BellRing : Lucide.Bell)"
                                  class="@($"h-5 w-5 {(HasCritical ? "animate-pulse text-destructive" : "")}")" />
                        @if (UnackedCount > 0)
                        {
                            <span class="@BadgeClasses">
                                @(UnackedCount > 99 ? "99+" : UnackedCount.ToString())
                            </span>
                        }
                    </Button>
                </PopoverTrigger>
                <PopoverContent Class="w-96 p-0 rounded-xl shadow-lg overflow-hidden" Align="PopoverAlign.End">

                    @* Header *@
                    <div class="flex items-center justify-between px-3 py-2.5 border-b border-border bg-muted/30">
                        <div class="flex items-center gap-2">
                            <h3 class="font-semibold text-sm">Alarms</h3>
                            @if (UnackedCount > 0)
                            {
                                <span class="text-xs text-muted-foreground">@UnackedCount unacknowledged</span>
                            }
                        </div>
                        <div class="flex items-center gap-0.5">
                            <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon"
                                    title="Acknowledge all" Class="h-7 w-7"
                                    OnClick="@AcknowledgeAll">
                                <Blazicon Svg="Lucide.CheckCheck" class="h-4 w-4" />
                            </Button>
                            <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Icon"
                                    title="Alarm settings" Class="h-7 w-7"
                                    OnClick="@(() => Navigate(new NavMenuItem { Url = "/settings/notifications", Icon = Lucide.BellRing, Title = "Alarm Settings", ComponentType = typeof(NotificationSettings), Unique = true }))">
                                <Blazicon Svg="Lucide.Settings" class="h-4 w-4" />
                            </Button>
                        </div>
                    </div>

                    @* Filter *@
                    <div class="flex gap-1 px-2 py-2 border-b border-border">
                        @foreach (var f in new[] { AlarmFilter.All, AlarmFilter.Unacked, AlarmFilter.Critical })
                        {
                            var filter = f;
                            <button class="@FilterChipClasses(filter)" @onclick="@(() => _filter = filter)">
                                @filter.ToString()
                            </button>
                        }
                    </div>

                    @* List *@
                    <ScrollArea Class="max-h-[420px]">
                        @if (!FilteredAlarms.Any())
                        {
                            <div class="flex flex-col items-center justify-center gap-2 py-12 text-muted-foreground">
                                <Blazicon Svg="Lucide.BellOff" class="h-8 w-8 opacity-40" />
                                <p class="text-sm">No active alarms</p>
                            </div>
                        }
                        else
                        {
                            @foreach (var group in FilteredAlarms.Take(_displayedAlarms)
                                                                 .GroupBy(a => a.LastSeenUtc.ToLocalTime().Date))
                            {
                                <div class="sticky top-0 z-10 px-3 py-1 text-[11px] font-medium uppercase tracking-wide text-muted-foreground bg-background/95 backdrop-blur border-b border-border/50">
                                    @DayLabel(group.Key)
                                </div>

                                @foreach (var alarm in group)
                                {
                                    var current = alarm;
                                    <div class="@RowClasses(current)" @onclick="@(() => ActivateAlarm(current))">
                                        <span class="@AccentClasses(current.Severity)"></span>

                                        <div class="flex-1 min-w-0 pl-2.5 pr-1 py-2.5">
                                            <div class="flex items-start gap-2">
                                                <Blazicon Svg="@SeverityIcon(current.Severity)"
                                                          class="@($"h-4 w-4 mt-0.5 shrink-0 {SeverityTextClass(current.Severity)}")" />
                                                <div class="flex-1 min-w-0">
                                                    <div class="flex items-center gap-1.5">
                                                        <p class="text-sm font-medium truncate">@current.Title</p>
                                                        @if (current.OccurrenceCount > 1)
                                                        {
                                                            <span class="shrink-0 rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-semibold text-muted-foreground">
                                                                x@current.OccurrenceCount
                                                            </span>
                                                        }
                                                    </div>
                                                    <p class="text-xs text-muted-foreground line-clamp-2 mt-0.5">@current.Message</p>
                                                </div>
                                                <div class="flex flex-col items-end gap-1 shrink-0">
                                                    <span class="text-[11px] text-muted-foreground">@RelativeTime(current.LastSeenUtc)</span>
                                                    @if (current.AcknowledgedAtUtc == null)
                                                    {
                                                        <button class="hidden group-hover:inline-flex text-[11px] font-medium text-primary hover:underline"
                                                                @onclick:stopPropagation="true"
                                                                @onclick="@(() => Acknowledge(current.Id))">
                                                            Ack
                                                        </button>
                                                    }
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                }
                            }

                            @if (FilteredAlarms.Count() > _displayedAlarms)
                            {
                                <Button Variant="ButtonVariant.Ghost" Class="w-full rounded-none" OnClick="@LoadMoreAlarms">
                                    Load More
                                </Button>
                            }
                        }
                    </ScrollArea>

                    @* Footer *@
                    <div class="border-t border-border px-3 py-2 bg-muted/20">
                        <button class="text-xs font-medium text-primary hover:underline"
                                @onclick="@(() => Navigate(new NavMenuItem { Url = "/settings/notifications", Icon = Lucide.BellRing, Title = "Alarm Settings", ComponentType = typeof(NotificationSettings), Unique = true }))">
                            View all alarms →
                        </button>
                    </div>
                </PopoverContent>
            </Popover>
```

- [ ] **Step 2: Replace the notification members in the code block**

In the `@code` block of `Navbar.razor`, delete the `Notifications` parameter (`:383`), `OnNotificationClick` (`:398`), `_displayedNotifications` (`:401`), `UnreadCount` (`:403`), `GetNotificationClasses` (`:405-409`), `HandleNotificationClick` (`:438-442`) and `LoadMoreNotifications` (`:444-447`). Add:

```csharp
    [Parameter]
    public List<AlarmLog>? Alarms { get; set; }

    [Parameter]
    public EventCallback<long> OnAlarmAcknowledge { get; set; }

    [Parameter]
    public EventCallback OnAlarmAcknowledgeAll { get; set; }

    [Parameter]
    public EventCallback<AlarmLog> OnAlarmActivate { get; set; }

    private enum AlarmFilter { All, Unacked, Critical }

    private AlarmFilter _filter = AlarmFilter.All;
    private int _displayedAlarms = 10;

    private IEnumerable<AlarmLog> FilteredAlarms => (Alarms ?? new List<AlarmLog>())
        .Where(a => _filter switch
        {
            AlarmFilter.Unacked => a.AcknowledgedAtUtc == null,
            AlarmFilter.Critical => a.Severity == SeverityLevel.CRITICAL,
            _ => true
        })
        .OrderByDescending(a => a.AcknowledgedAtUtc == null)
        .ThenByDescending(a => a.LastSeenUtc);

    private int UnackedCount => Alarms?.Count(a => a.AcknowledgedAtUtc == null) ?? 0;

    private bool HasCritical => Alarms?.Any(a => a.AcknowledgedAtUtc == null
                                                 && a.Severity == SeverityLevel.CRITICAL) ?? false;

    private string BadgeClasses
    {
        get
        {
            var tone = HasCritical
                ? "bg-destructive text-destructive-foreground"
                : "bg-amber-500 text-white";
            return "absolute -top-1 -right-1 min-w-[1.15rem] h-[1.15rem] px-1 rounded-full " +
                   "ring-2 ring-background flex items-center justify-center text-[10px] font-bold " + tone;
        }
    }

    private string FilterChipClasses(AlarmFilter filter)
    {
        var baseClasses = "flex-1 rounded-md px-2 py-1 text-xs font-medium transition-colors";
        return _filter == filter
            ? $"{baseClasses} bg-primary text-primary-foreground"
            : $"{baseClasses} text-muted-foreground hover:bg-accent";
    }

    private string RowClasses(AlarmLog alarm)
    {
        var baseClasses = "group relative flex cursor-pointer border-b border-border/40 transition-colors hover:bg-accent";
        return alarm.AcknowledgedAtUtc == null ? $"{baseClasses} bg-accent/40" : baseClasses;
    }

    private string AccentClasses(SeverityLevel severity) =>
        "absolute left-0 top-0 bottom-0 w-[2px] " + severity switch
        {
            SeverityLevel.CRITICAL => "bg-destructive",
            SeverityLevel.ERROR => "bg-destructive/70",
            SeverityLevel.WARNING => "bg-amber-500",
            _ => "bg-muted-foreground/30"
        };

    private string SeverityTextClass(SeverityLevel severity) => severity switch
    {
        SeverityLevel.CRITICAL => "text-destructive",
        SeverityLevel.ERROR => "text-destructive/80",
        SeverityLevel.WARNING => "text-amber-500",
        _ => "text-muted-foreground"
    };

    private SvgIcon SeverityIcon(SeverityLevel severity) => severity switch
    {
        SeverityLevel.CRITICAL => Lucide.OctagonAlert,
        SeverityLevel.ERROR => Lucide.CircleAlert,
        SeverityLevel.WARNING => Lucide.TriangleAlert,
        _ => Lucide.Info
    };

    private static string DayLabel(DateTime localDate)
    {
        if (localDate == DateTime.Today) return "Today";
        if (localDate == DateTime.Today.AddDays(-1)) return "Yesterday";
        return localDate.ToString("d MMM yyyy");
    }

    private static string RelativeTime(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalSeconds < 60) return "now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h";
        return $"{(int)delta.TotalDays}d";
    }

    private async Task Acknowledge(long id)
    {
        if (OnAlarmAcknowledge.HasDelegate)
            await OnAlarmAcknowledge.InvokeAsync(id);
    }

    private async Task AcknowledgeAll()
    {
        if (OnAlarmAcknowledgeAll.HasDelegate)
            await OnAlarmAcknowledgeAll.InvokeAsync();
    }

    private async Task ActivateAlarm(AlarmLog alarm)
    {
        if (OnAlarmActivate.HasDelegate)
            await OnAlarmActivate.InvokeAsync(alarm);
    }

    private void LoadMoreAlarms()
    {
        _displayedAlarms = Math.Min(_displayedAlarms + 10, FilteredAlarms.Count());
    }
```

Add `@using BatteryTestingSystem.Models.Entities`, `@using BatteryTestingSystem.Models.Enums` and `@using BatteryTestingSystem.Components.Pages.Settings` at the top of the file if not already present.

- [ ] **Step 3: Fix the Settings dropdown link**

Replace the dead link at `Navbar.razor:129-131` so it opens the real component:

```razor
                    <DropdownMenuItem OnClick="@(() => Navigate(new NavMenuItem { Url = "/settings/notifications", Icon = Lucide.BellRing, Title = "Alarm Settings", ComponentType = typeof(NotificationSettings), Unique = true }))">
                        Alarm Settings
                    </DropdownMenuItem>
```

Do the same for the mobile menu button at `:350`.

- [ ] **Step 4: Build**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: one error — `NotificationSettings` does not exist yet (Task 9 creates it). To keep this task independently committable, temporarily create the page stub now as part of Task 9's Step 1 and run Task 9 immediately after; do not commit a non-building tree.

- [ ] **Step 5: Verify the Lucide icons resolve**

`Lucide.BellRing`, `Lucide.BellOff`, `Lucide.CheckCheck`, `Lucide.OctagonAlert`, `Lucide.CircleAlert`, `Lucide.TriangleAlert` and `Lucide.Info` must all exist in the installed Blazicons package. If any fails to compile, substitute: `OctagonAlert` → `Lucide.CircleAlert`, `CheckCheck` → `Lucide.Check`, `TriangleAlert` → `Lucide.TriangleAlert` or `Lucide.AlertTriangle` depending on package version.

- [ ] **Step 6: Commit (after Task 9 Step 1 makes it build)**

```bash
git add Components/Layout/Navbar.razor
git commit -m "feat(alarms): redesign bell popover with severity, grouping, filters and acknowledge"
```

---

### Task 9: Alarm settings and history page

**Files:**
- Create: `Components/Pages/Settings/NotificationSettings.razor`

**Interfaces:**
- Consumes: `IAlarmService` (`Policy`, `GetActiveAsync`, `AcknowledgeAllAsync`), `IAlarmRepository.QueryAsync`, `AlarmQueryParameters`, `AlarmOptions`.
- Produces: the `NotificationSettings` component type referenced by `Navbar.razor`.

- [ ] **Step 1: Create the page**

`Components/Pages/Settings/NotificationSettings.razor`:

```razor
@page "/settings/notifications"
@using BatteryTestingSystem.Models.Entities
@using BatteryTestingSystem.Models.Enums
@using BatteryTestingSystem.Models.ViewModels
@using BatteryTestingSystem.Repositories.Interfaces
@using BatteryTestingSystem.Services.Alarms
@using BatteryTestingSystem.Services.Interfaces
@using Microsoft.Extensions.Options
@inject IAlarmService AlarmSvc
@inject IAlarmRepository AlarmRepo
@inject IOptions<AlarmOptions> AlarmCfg

<div class="h-full bg-background text-foreground p-6 flex flex-col gap-4 overflow-y-auto">

    <div>
        <h1 class="text-xl font-semibold">Alarm Settings</h1>
        <p class="text-sm text-muted-foreground">Control how alarms interrupt you, and review alarm history.</p>
    </div>

    @* Escalation *@
    <div class="rounded-lg border border-border bg-card p-4">
        <h2 class="text-sm font-semibold mb-3">Escalation</h2>
        <div class="grid gap-3 sm:grid-cols-3">
            <label class="flex items-center gap-2 text-sm">
                <input type="checkbox" @bind="_toastEnabled" class="h-4 w-4 rounded border-border" />
                Show toast for Error and Critical
            </label>
            <label class="flex items-center gap-2 text-sm">
                <input type="checkbox" @bind="_soundEnabled" class="h-4 w-4 rounded border-border" />
                Play sound
            </label>
            <div class="text-sm text-muted-foreground">
                Cooldown: <span class="font-medium text-foreground">@AlarmCfg.Value.EscalationCooldownSec s</span>
                per alarm
            </div>
        </div>
        <p class="text-xs text-muted-foreground mt-3">
            Browsers block audio until you have interacted with the page — the first beep after a
            fresh page load may be silent.
        </p>
    </div>

    @* Mute *@
    <div class="rounded-lg border border-border bg-card p-4">
        <h2 class="text-sm font-semibold mb-3">Mute</h2>
        <div class="flex flex-wrap items-center gap-2">
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Sm" OnClick="@(() => Mute(15))">Mute 15 min</Button>
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Sm" OnClick="@(() => Mute(60))">Mute 1 hour</Button>
            <Button Variant="ButtonVariant.Outline" Size="ButtonSize.Sm" OnClick="@(() => Mute(60 * 24 * 3650))">Until restart</Button>
            <Button Variant="ButtonVariant.Ghost" Size="ButtonSize.Sm" OnClick="@Unmute">Unmute</Button>
            <span class="text-xs @(IsMuted ? "text-amber-500 font-medium" : "text-muted-foreground")">
                @(IsMuted ? "Muted — alarms are still recorded, you just are not interrupted." : "Not muted")
            </span>
        </div>
    </div>

    @* Retention *@
    <div class="rounded-lg border border-border bg-card p-4">
        <h2 class="text-sm font-semibold mb-1">Retention</h2>
        <p class="text-sm text-muted-foreground">
            Alarms are kept for <span class="font-medium text-foreground">@AlarmCfg.Value.RetentionDays days</span>.
            Alarms that are still unacknowledged are never deleted, however old they are.
        </p>
    </div>

    @* History *@
    <div class="rounded-lg border border-border bg-card p-4 flex-1 min-h-0 flex flex-col">
        <div class="flex flex-wrap items-end gap-3 mb-3">
            <div class="flex flex-col gap-1">
                <label class="text-xs font-medium text-muted-foreground">Severity</label>
                <select class="h-9 w-[140px] rounded-md border border-border bg-background px-2 text-sm"
                        @bind="_severityFilter">
                    <option value="">All</option>
                    <option value="@SeverityLevel.CRITICAL">Critical</option>
                    <option value="@SeverityLevel.ERROR">Error</option>
                    <option value="@SeverityLevel.WARNING">Warning</option>
                    <option value="@SeverityLevel.INFO">Info</option>
                </select>
            </div>
            <div class="flex flex-col gap-1">
                <label class="text-xs font-medium text-muted-foreground">Source</label>
                <select class="h-9 w-[150px] rounded-md border border-border bg-background px-2 text-sm"
                        @bind="_sourceFilter">
                    <option value="">All</option>
                    <option value="@AlarmSource.Comms">Comms</option>
                    <option value="@AlarmSource.ChannelError">Channel error</option>
                    <option value="@AlarmSource.DataStore">Data store</option>
                </select>
            </div>
            <div class="flex flex-col gap-1">
                <label class="text-xs font-medium text-muted-foreground">Device</label>
                <input class="h-9 w-[120px] rounded-md border border-border bg-background px-3 text-sm"
                       @bind="_deviceFilter" placeholder="Device id" />
            </div>
            <label class="flex items-center gap-2 text-sm h-9">
                <input type="checkbox" @bind="_onlyUnacked" class="h-4 w-4 rounded border-border" />
                Unacknowledged only
            </label>
            <Button Variant="ButtonVariant.Default" Size="ButtonSize.Sm" OnClick="@LoadHistory">Apply</Button>
        </div>

        <div class="flex-1 overflow-y-auto rounded-md border border-border">
            <table class="w-full text-sm">
                <thead class="sticky top-0 bg-muted/50 text-xs uppercase text-muted-foreground">
                    <tr>
                        <th class="text-left px-3 py-2 font-medium">Severity</th>
                        <th class="text-left px-3 py-2 font-medium">Title</th>
                        <th class="text-left px-3 py-2 font-medium">Device</th>
                        <th class="text-right px-3 py-2 font-medium">Count</th>
                        <th class="text-left px-3 py-2 font-medium">Last seen</th>
                        <th class="text-left px-3 py-2 font-medium">Acknowledged</th>
                    </tr>
                </thead>
                <tbody>
                    @if (_history.Count == 0)
                    {
                        <tr><td colspan="6" class="px-3 py-8 text-center text-muted-foreground">No alarms found</td></tr>
                    }
                    else
                    {
                        @foreach (var row in _history)
                        {
                            <tr class="border-t border-border/50 hover:bg-accent/40">
                                <td class="px-3 py-2">@row.Severity</td>
                                <td class="px-3 py-2">
                                    <div class="font-medium">@row.Title</div>
                                    <div class="text-xs text-muted-foreground line-clamp-1">@row.Message</div>
                                </td>
                                <td class="px-3 py-2">@row.DeviceId</td>
                                <td class="px-3 py-2 text-right">@row.OccurrenceCount</td>
                                <td class="px-3 py-2">@row.LastSeenUtc.ToLocalTime().ToString("g")</td>
                                <td class="px-3 py-2">
                                    @if (row.AcknowledgedAtUtc != null)
                                    {
                                        <span class="text-xs">@row.AcknowledgedBy — @row.AcknowledgedAtUtc.Value.ToLocalTime().ToString("g")</span>
                                    }
                                    else
                                    {
                                        <span class="text-xs text-muted-foreground">—</span>
                                    }
                                </td>
                            </tr>
                        }
                    }
                </tbody>
            </table>
        </div>
    </div>
</div>

@code {
    private bool _toastEnabled = true;
    private bool _soundEnabled = true;

    private string _severityFilter = string.Empty;
    private string _sourceFilter = string.Empty;
    private string _deviceFilter = string.Empty;
    private bool _onlyUnacked;

    private List<AlarmLog> _history = new();

    private bool IsMuted => AlarmSvc.Policy.IsMuted(null, DateTime.UtcNow);

    protected override async Task OnInitializedAsync() => await LoadHistory();

    private void Mute(int minutes) => AlarmSvc.Policy.MuteAll(DateTime.UtcNow.AddMinutes(minutes));

    private void Unmute() => AlarmSvc.Policy.Unmute();

    private async Task LoadHistory()
    {
        var query = new AlarmQueryParameters
        {
            DeviceId = string.IsNullOrWhiteSpace(_deviceFilter) ? null : _deviceFilter.Trim(),
            OnlyUnacknowledged = _onlyUnacked
        };

        if (Enum.TryParse<SeverityLevel>(_severityFilter, out var severity))
            query.Severity = severity;

        if (Enum.TryParse<AlarmSource>(_sourceFilter, out var source))
            query.Source = source;

        var result = await AlarmRepo.QueryAsync(query);
        _history = result.Success && result.Data != null ? result.Data : new List<AlarmLog>();
    }
}
```

`_toastEnabled` / `_soundEnabled` are wired to the UI here; persisting them per user via `ConfigStorageRepository` is a follow-up and is intentionally not in this task — the switches take effect for the session.

- [ ] **Step 2: Build**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: Build succeeded, 0 errors. If `CommonResponse<T>` exposes `IsSuccess` rather than `Success`, match the property name used in `Components/Pages/Settings/ApplicationErrorLogs.razor`.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: all tests pass (2 pre-existing + 22 new).

- [ ] **Step 4: Commit**

```bash
git add Components/Pages/Settings/NotificationSettings.razor
git commit -m "feat(alarms): add alarm settings and history page at /settings/notifications"
```

---

### Task 10: Remove the old notification plumbing and verify end to end

**Files:**
- Modify: `Components/Layout/LayoutModels.cs:22-31` (delete `NotificationItem`)
- Modify: `wwwroot/js/indexedDb.js:27-70` (delete `saveNotifications` / `getNotifications`)
- Modify: `Components/UI/UserManual/UserManual.razor`, `Components/UI/WindowManager/WindowManagerExample.razor`, `Components/UI/Dashboard/ColorsSettings.razor` if they reference `NotificationItem`

- [ ] **Step 1: Find every remaining reference**

Run: `grep -rn "NotificationItem\|OnNotify\|saveNotifications\|getNotifications\|OnNotificationClick" --include=*.cs --include=*.razor --include=*.js .`
Expected: only the definition sites listed above. Any other hit must be migrated before deleting.

- [ ] **Step 2: Delete the model**

Remove the `NotificationItem` class and its doc comment from `Components/Layout/LayoutModels.cs:22-31`. Leave `NavMenuItem`, `ErrorItem`, `EventLogItem` and `SettingsMenuItem` untouched.

- [ ] **Step 3: Delete the IndexedDB functions**

In `wwwroot/js/indexedDb.js`, delete the `saveNotifications` and `getNotifications` functions (`:27` and `:57`) and any now-unused object store they alone used. Leave every other store and function intact.

- [ ] **Step 4: Build and test**

Run: `dotnet build BatteryTestingSystem.csproj && dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: Build succeeded, all tests pass.

- [ ] **Step 5: Apply the migration to the dev database**

Run: `dotnet ef database update --context AppDbContext`
Expected: `Audit.AlarmLog` table created.

- [ ] **Step 6: Manual verification against the running app**

Start the app and confirm each of these:

1. Bell renders with no badge and shows "No active alarms".
2. Disconnect a device (or stop the simulator) → a **Critical** "Device N disconnected" alarm appears within a second, the badge turns red and pulses, a toast appears, and a beep plays (after you have clicked once on the page).
3. Reconnect the device → the comms-loss alarm disappears from the bell (cleared, not deleted — it is still visible in the history table with a cleared timestamp).
4. Force the same channel error repeatedly → the bell shows **one** row whose `xN` count climbs, not N rows, and only one toast fires per minute.
5. Click **Ack** on a row → the row stops being highlighted, and `/settings/notifications` shows your username and a timestamp in the Acknowledged column.
6. Reload the browser → acknowledged state persists (this is the bug that started all of this).
7. Open a second browser as a different user → the same alarms are visible there.
8. `Settings → Alarm Settings` opens the new page; Mute 15 min stops toasts while the badge keeps counting.

- [ ] **Step 7: Commit**

```bash
git add Components/Layout/LayoutModels.cs wwwroot/js/indexedDb.js
git commit -m "chore(alarms): remove NotificationItem and IndexedDB notification storage"
```

---

## Self-Review

**Spec coverage:**

| Spec section | Task |
|---|---|
| §4 data model, indexes, acknowledge-is-not-delete | 1 |
| §4 collapse / re-arm / clear rules | 2 (queries), 4 (logic + tests) |
| §5.1 row collapse | 4 |
| §5.2 escalation cooldown | 3 |
| §5.3 storm guard + summary alarm | 3 (policy), 4 (summary row) |
| §5.4 write debounce, first-insert always synchronous | 4 |
| §6 retention, batching, never-prune-unresolved | 5 |
| §7 service contract, persist-before-fan-out | 4 |
| §8 producers (comms, store, channel errors) | 6 |
| §9 MainLayout cleanup, bell redesign, settings page | 7, 8, 9 |
| §10 configuration | 3 |
| §11 testing | 3, 4, 5 (logic); 9, 10 (manual) |
| §12 risks | mitigations in Tasks 1 (migration inspection), 4 (scope factory), 7 (silent beep failure), 10 (single-commit removal) |

**Known gaps, stated rather than hidden:**

- Per-user persistence of the toast/sound switches is deliberately deferred (Task 9 Step 1 note).
- `Repository` and Razor layers have no automated tests; covered by build + the Task 10 Step 6 checklist.
- `AlarmRepository.PruneAsync`'s SQL predicate is mirrored — not shared — with `FakeAlarmRepository`. Task 5 Step 6 verifies the real one against the database.
