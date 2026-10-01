# Workflow Canvas Phase 3 — Marquee Selection and Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an operator select many channels on the canvas (marquee drag, Ctrl/Shift-click, or a Board/Device header shortcut) and run Start/Stop/Pause/Continue/Transfer on the selection — turning the canvas from a monitoring view into a real operating surface (D9, the reversal of D2).

**Architecture:** Selection becomes a set (`_selectedNodeIds`) instead of one string, filtered through a new pure `WorkflowSelectionLogic` that mirrors the dashboard's own selection rule (`CircuitSelectionLogic`) in canvas-native, node-id-keyed terms — the two are kept separate rather than force-fit onto one signature, since the dashboard's is keyed by `(DeviceID, Board, Channel)` tuples over live `IChannelCommandHandler` objects and the canvas is keyed by string node ids over `ChannelTelemetry` readings. Marquee drag and the pan gesture it displaces are implemented entirely in `workflow-canvas.js`, following this file's established JS-owns-interaction/C#-owns-state split. A new `ChannelActionExecutor` extracts `DashboardView.DoAction`'s per-device `SemaphoreSlim` concurrency pattern into a directly unit-testable class (D13) — the canvas is its only consumer; `DashboardView` and `TransferDialog` are untouched.

**Tech Stack:** .NET 8, Blazor Server (`InteractiveServer`), xUnit, bUnit 1.32.7, vanilla JS.

**Spec:** [docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md](../specs/2026-08-23-workflow-canvas-dashboard-parity-design.md) — "Section 5 — Marquee selection" and "Section 6 — Actions" (D12, D13).

## Scope note (read before starting)

The spec names two action entry points: a right-click context menu and a selection action bar.
This plan builds **only the selection action bar**. A right-click menu is a second, independent
UI surface with its own gesture-conflict questions (right-click already has no meaning on the
canvas today, but must not fight the existing left-click-drag/marquee/pan gestures this plan
introduces) — bundling it into an already-large phase risks both surfaces being under-tested. The
action bar alone fully satisfies "select many, then act." A follow-up task can add the context
menu once this is live and judged worth it.

## Global Constraints

- **Branch `feat/workflow-canvas-experiment` is NEVER merged to `main`.** Every task commits here only.
- **`DashboardView.razor` and `TransferDialog.razor` are NEVER modified** (D13) — the canvas is
  `ChannelActionExecutor`'s only consumer this phase.
- **Actions run exactly like the dashboard's**: one `SemaphoreSlim` per `DeviceID`, channels on a
  device execute sequentially, devices run in parallel. No new confirmation step before
  Start/Stop/Pause/Continue — the dashboard has none, and diverging would itself be a surprise.
- **Selection enforces the dashboard's own rule** (`DashboardView.UpdateSelectedCircuits`): a
  channel can never be selected while `CircuitStatus.Offline`, and once one channel is selected
  every further addition must match its `(CircuitStatus, ProgramStatus)` exactly.
- **Marquee/selection JS state is never sent through Blazor per frame.** Only the final selected
  id list crosses at mouse-up — matching the pan/zoom/drag contract every prior JS feature on this
  branch has followed.
- Run the full suite with `dotnet test BatteryTestingSystem.Tests`. `DashboardRenderBatcherTests`
  is known-flaky under parallel load — re-run it in isolation before treating a lone failure there
  as a regression.
- **Stop the running app before `dotnet build`/`dotnet test`, restart after** — it locks its own
  build output. Launch via the **PowerShell tool** (not Bash — Bash's network sandbox is
  unreachable from the real browser). App listens on **port 5066**.
- **Hard-reload the browser (`ignoreCache: true`) after any `wwwroot/js` or `wwwroot/css` change**
  before trusting what you see.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `Services/Implementations/Workflow/WorkflowSelectionLogic.cs` | Canvas-native selection eligibility: never Offline, must match the current anchor. |
| `Services/Implementations/Workflow/WorkflowActionRules.cs` | Canvas-native per-item action enablement, mirroring `DashboardView.CanContextAction`. |
| `Services/Implementations/Workflow/ChannelActionExecutor.cs` | The extracted per-device-`SemaphoreSlim` concurrency pattern (D13), instantiated once per page. |
| `Components/UI/WorkflowCanvas/SelectionActionBar.razor` | The floating bar: selection count, action buttons, live progress. |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowSelectionLogicTests.cs` | |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowActionRulesTests.cs` | |
| `BatteryTestingSystem.Tests/Services/Workflow/ChannelActionExecutorTests.cs` | |
| `BatteryTestingSystem.Tests/Services/Workflow/RecordingChannelCommandHandler.cs` | Test fake recording call order/concurrency, distinct from the existing `FakeChannelCommandHandler` (whose action methods deliberately throw). |
| `BatteryTestingSystem.Tests/Components/SelectionActionBarTests.cs` | |

**Modified**

| File | Change |
|---|---|
| `Models/DTOs/Workflow/TelemetryEntry.cs` | `ChannelTelemetry` gains `IsConnected` (default `true`). |
| `Components/Pages/Workflows/WorkflowCanvasPage.razor` | `_selectedNodeId` (string) → `_selectedNodeIds` (`HashSet<string>`); populate `IsConnected` in `PushTelemetryAsync`; new `OnMarqueeSelect`/updated `OnSelect` JSInvokable methods; board/device header select-all handler; render `SelectionActionBar`. |
| `Components/UI/WorkflowCanvas/CanvasSurface.razor` | `SelectedNodeId: string?` → `SelectedNodeIds: IReadOnlyCollection<string>`; board/device click callback. |
| `Components/UI/WorkflowCanvas/Nodes/{BoardNode,DeviceNode}.razor` | Header becomes clickable (select all children) alongside its existing Collapse button / online text. |
| `wwwroot/js/workflow-canvas.js` | Marquee drag replaces left-drag-on-empty-canvas; panning moves to middle-mouse-button or Space+drag; `OnSelect` gains an `additive` argument. |
| `wwwroot/css/workflow-canvas.css` | `.wf-marquee` overlay; `.wf-selection-action-bar` styling. |

---

## Task 1: Selection eligibility rules

The rule the dashboard already enforces on every add — never Offline, must match the current
anchor's `(CircuitStatus, ProgramStatus)` — reimplemented in canvas terms: node ids and
`ChannelTelemetry` readings instead of `(DeviceID, Board, Channel)` tuples and live
`IChannelCommandHandler` objects. Kept separate from `CircuitSelectionLogic` rather than
generalizing both onto one signature — the two selection models are keyed too differently to
share cleanly, and `CircuitSelectionLogic` already has its own passing test suite this branch must
not touch.

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowSelectionLogic.cs`
- Create: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowSelectionLogicTests.cs`

**Interfaces:**
- Consumes: `ChannelTelemetry` (`Models/DTOs/Workflow/TelemetryEntry.cs`), `CircuitStatus` (`Models/Enums`).
- Produces:
  - `enum SelectionOutcome { NothingEligible, AllAdded, PartiallyAdded }`
  - `readonly record struct SelectionResult(SelectionOutcome Outcome, int Added, int Skipped)`
  - `WorkflowSelectionLogic.CanAdd(string candidateNodeId, IReadOnlyDictionary<string, ChannelTelemetry> readings, IReadOnlyCollection<string> selected) → bool`
  - `WorkflowSelectionLogic.ApplyBulk(IReadOnlyCollection<string> candidateNodeIds, IReadOnlyDictionary<string, ChannelTelemetry> readings, HashSet<string> selected) → SelectionResult` (mutates `selected` in place, mirroring `CircuitSelectionLogic.SelectAll`'s contract)

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowSelectionLogicTests.cs`:

```csharp
using System.Collections.Generic;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Mirrors DashboardView.UpdateSelectedCircuits's rule in canvas terms: never Offline, and once
/// something is selected every further addition must match its (CircuitStatus, ProgramStatus) -
/// the same rule that keeps a bulk action from ever receiving a mixed set.
/// </summary>
public class WorkflowSelectionLogicTests
{
    private static ChannelTelemetry Reading(
        CircuitStatus status = CircuitStatus.Idle,
        ProgramRunningStatus programStatus = ProgramRunningStatus.Stop) =>
        new(status, 0, 0, 0, ProgramStatus: programStatus);

    [Fact]
    public void CanAdd_RejectsAnOfflineCandidate()
    {
        var readings = new Dictionary<string, ChannelTelemetry> { ["chn-1"] = Reading(CircuitStatus.Offline) };

        Assert.False(WorkflowSelectionLogic.CanAdd("chn-1", readings, new List<string>()));
    }

    [Fact]
    public void CanAdd_RejectsACandidateWithNoReadingYet()
    {
        Assert.False(WorkflowSelectionLogic.CanAdd(
            "chn-1", new Dictionary<string, ChannelTelemetry>(), new List<string>()));
    }

    [Fact]
    public void CanAdd_AllowsAnyEligibleCandidateWhenNothingIsSelectedYet()
    {
        var readings = new Dictionary<string, ChannelTelemetry> { ["chn-1"] = Reading(CircuitStatus.Charge) };

        Assert.True(WorkflowSelectionLogic.CanAdd("chn-1", readings, new List<string>()));
    }

    [Fact]
    public void CanAdd_AllowsACandidateMatchingTheAnchor()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
        };

        Assert.True(WorkflowSelectionLogic.CanAdd("chn-2", readings, new List<string> { "chn-1" }));
    }

    [Fact]
    public void CanAdd_RejectsACandidateWithADifferentCircuitStatusThanTheAnchor()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge),
            ["chn-2"] = Reading(CircuitStatus.Discharging),
        };

        Assert.False(WorkflowSelectionLogic.CanAdd("chn-2", readings, new List<string> { "chn-1" }));
    }

    [Fact]
    public void CanAdd_RejectsACandidateWithADifferentProgramStatusThanTheAnchor()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Stop),
        };

        Assert.False(WorkflowSelectionLogic.CanAdd("chn-2", readings, new List<string> { "chn-1" }));
    }

    [Fact]
    public void ApplyBulk_AddsEveryEligibleCandidate()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge),
            ["chn-2"] = Reading(CircuitStatus.Charge),
        };
        var selected = new HashSet<string>();

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-1", "chn-2" }, readings, selected);

        Assert.Equal(SelectionOutcome.AllAdded, result.Outcome);
        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(new HashSet<string> { "chn-1", "chn-2" }, selected);
    }

    [Fact]
    public void ApplyBulk_SkipsOfflineMembersAndReportsThem()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge),
            ["chn-2"] = Reading(CircuitStatus.Offline),
        };
        var selected = new HashSet<string>();

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-1", "chn-2" }, readings, selected);

        Assert.Equal(SelectionOutcome.PartiallyAdded, result.Outcome);
        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new HashSet<string> { "chn-1" }, selected);
    }

    [Fact]
    public void ApplyBulk_TheFirstEligibleMemberBecomesTheAnchorForTheRest()
    {
        // Charge/Running comes first in the candidate list, so it becomes the anchor; the
        // Idle/Stop candidate that follows must be rejected even though both are individually
        // eligible in isolation.
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop),
        };
        var selected = new HashSet<string>();

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-1", "chn-2" }, readings, selected);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new HashSet<string> { "chn-1" }, selected);
    }

    [Fact]
    public void ApplyBulk_ReusesAnExistingAnchorRatherThanPickingANewOne()
    {
        var readings = new Dictionary<string, ChannelTelemetry>
        {
            ["chn-1"] = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running),
            ["chn-2"] = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop),
        };
        var selected = new HashSet<string> { "chn-1" };

        var result = WorkflowSelectionLogic.ApplyBulk(new[] { "chn-2" }, readings, selected);

        Assert.Equal(SelectionOutcome.NothingEligible, result.Outcome);
        Assert.Equal(0, result.Added);
        Assert.Equal(new HashSet<string> { "chn-1" }, selected);
    }

    [Fact]
    public void ApplyBulk_ReportsNothingEligibleForAnEmptyCandidateList()
    {
        var result = WorkflowSelectionLogic.ApplyBulk(
            Array.Empty<string>(), new Dictionary<string, ChannelTelemetry>(), new HashSet<string>());

        Assert.Equal(SelectionOutcome.NothingEligible, result.Outcome);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowSelectionLogicTests"
```

Expected: FAIL — compile error, `WorkflowSelectionLogic` does not exist.

- [ ] **Step 3: Implement**

Create `Services/Implementations/Workflow/WorkflowSelectionLogic.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public enum SelectionOutcome { NothingEligible, AllAdded, PartiallyAdded }

public readonly record struct SelectionResult(SelectionOutcome Outcome, int Added, int Skipped);

/// <summary>
/// Canvas-native mirror of DashboardView.UpdateSelectedCircuits's rule: a channel can never be
/// selected while Offline, and once one channel is selected every further addition must match
/// its (CircuitStatus, ProgramStatus) exactly - the rule that keeps a marquee or bulk action from
/// ever producing a mixed selection no action could run on uniformly.
///
/// Kept separate from CircuitSelectionLogic (the dashboard's own version) rather than
/// generalizing both onto one signature: the dashboard is keyed by (DeviceID, Board, Channel)
/// tuples over live IChannelCommandHandler objects; the canvas is keyed by string node ids over
/// ChannelTelemetry readings. Forcing a shared abstraction over that mismatch would cost more
/// than the ~30 lines of duplication it would save.
/// </summary>
public static class WorkflowSelectionLogic
{
    public static bool CanAdd(
        string candidateNodeId,
        IReadOnlyDictionary<string, ChannelTelemetry> readings,
        IReadOnlyCollection<string> selected)
    {
        if (!readings.TryGetValue(candidateNodeId, out var reading)) return false;
        if (reading.Status == CircuitStatus.Offline) return false;

        if (selected.Count == 0) return true;

        var anchorId = selected.First();
        if (!readings.TryGetValue(anchorId, out var anchor)) return true; // anchor has no reading yet

        return reading.Status == anchor.Status && reading.ProgramStatus == anchor.ProgramStatus;
    }

    /// <summary>Applies a marquee or bulk selection: every candidate that passes CanAdd joins
    /// `selected` (mutated in place); everything else is counted as skipped. The first eligible
    /// candidate becomes the anchor for the rest of THIS call if `selected` started empty -
    /// matching CircuitSelectionLogic.SelectAll's "reuse an existing anchor, else pick the first
    /// eligible member" rule.</summary>
    public static SelectionResult ApplyBulk(
        IReadOnlyCollection<string> candidateNodeIds,
        IReadOnlyDictionary<string, ChannelTelemetry> readings,
        HashSet<string> selected)
    {
        if (candidateNodeIds.Count == 0) return new SelectionResult(SelectionOutcome.NothingEligible, 0, 0);

        int added = 0, skipped = 0;
        foreach (var id in candidateNodeIds)
        {
            if (CanAdd(id, readings, selected))
            {
                if (selected.Add(id)) added++;
            }
            else
            {
                skipped++;
            }
        }

        if (added == 0) return new SelectionResult(SelectionOutcome.NothingEligible, 0, skipped);
        return new SelectionResult(
            skipped > 0 ? SelectionOutcome.PartiallyAdded : SelectionOutcome.AllAdded, added, skipped);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowSelectionLogicTests"
```

Expected: PASS (10 tests).

- [ ] **Step 5: Run the full suite and commit**

```bash
dotnet test BatteryTestingSystem.Tests
git add Services/Implementations/Workflow/WorkflowSelectionLogic.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowSelectionLogicTests.cs
git commit -m "feat(workflow-canvas): canvas-native selection eligibility rules

Mirrors DashboardView.UpdateSelectedCircuits: never Offline, and once
one channel is selected every addition must match its (CircuitStatus,
ProgramStatus). Kept separate from CircuitSelectionLogic rather than
generalizing both onto one signature - the dashboard is keyed by
(DeviceID, Board, Channel) tuples over live circuit objects, the canvas
by string node ids over ChannelTelemetry readings.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Per-item action enablement

Canvas-native mirror of `DashboardView.CanContextAction`, minus `calibration` (out of D9's action
list). Requires `IsConnected` on `ChannelTelemetry` — the raw TCP-connectivity flag the dashboard's
own rule reads (`circuit.IsConnected`), never yet plumbed into the canvas.

**Files:**
- Modify: `Models/DTOs/Workflow/TelemetryEntry.cs`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Create: `Services/Implementations/Workflow/WorkflowActionRules.cs`
- Create: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowActionRulesTests.cs`

**Interfaces:**
- Consumes: `ChannelTelemetry`, `CircuitStatus`, `ProgramRunningStatus`.
- Produces: `WorkflowActionRules.CanPerform(ChannelTelemetry reading, string action) → bool`, action
  strings `"start"`, `"stop"`, `"pause"`, `"continue"`, `"transfer"`.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowActionRulesTests.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Exact mirror of DashboardView.CanContextAction, minus "calibration" - the canvas has no
/// calibration entry point (D9 lists only Start/Stop/Pause/Continue/Transfer).
/// </summary>
public class WorkflowActionRulesTests
{
    private static ChannelTelemetry Reading(
        CircuitStatus status, ProgramRunningStatus programStatus, bool isConnected = true) =>
        new(status, 0, 0, 0, ProgramStatus: programStatus, IsConnected: isConnected);

    [Fact]
    public void Offline_DisablesEveryAction()
    {
        var reading = Reading(CircuitStatus.Offline, ProgramRunningStatus.Stop);

        foreach (var action in new[] { "start", "stop", "pause", "continue", "transfer" })
            Assert.False(WorkflowActionRules.CanPerform(reading, action));
    }

    [Theory]
    [InlineData(ProgramRunningStatus.Stop, true)]
    [InlineData(ProgramRunningStatus.Running, false)]
    public void Start_RequiresProgramStopped(ProgramRunningStatus programStatus, bool expected)
    {
        var reading = Reading(CircuitStatus.Idle, programStatus);

        Assert.Equal(expected, WorkflowActionRules.CanPerform(reading, "start"));
    }

    [Fact]
    public void Start_RequiresTcpConnected()
    {
        var reading = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop, isConnected: false);

        Assert.False(WorkflowActionRules.CanPerform(reading, "start"));
    }

    [Fact]
    public void Stop_RequiresOnlyTcpConnected_NotAnyParticularProgramState()
    {
        var running = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running);
        var stopped = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop);

        Assert.True(WorkflowActionRules.CanPerform(running, "stop"));
        Assert.True(WorkflowActionRules.CanPerform(stopped, "stop"));
    }

    [Theory]
    [InlineData(ProgramRunningStatus.Running, true)]
    [InlineData(ProgramRunningStatus.Stop, false)]
    public void Pause_RequiresProgramRunning(ProgramRunningStatus programStatus, bool expected)
    {
        var reading = Reading(CircuitStatus.Charge, programStatus);

        Assert.Equal(expected, WorkflowActionRules.CanPerform(reading, "pause"));
    }

    [Theory]
    [InlineData(CircuitStatus.Pause, true)]
    [InlineData(CircuitStatus.Interrupt, true)]
    [InlineData(CircuitStatus.Countinue, true)]
    [InlineData(CircuitStatus.Error, true)]
    [InlineData(CircuitStatus.Msg, true)]
    [InlineData(CircuitStatus.Charge, false)]
    public void Continue_RequiresProgramRunningAndOneOfTheResumableCircuitStatuses(
        CircuitStatus circuitStatus, bool expected)
    {
        var reading = Reading(circuitStatus, ProgramRunningStatus.Running);

        Assert.Equal(expected, WorkflowActionRules.CanPerform(reading, "continue"));
    }

    [Fact]
    public void Transfer_RequiresIdleAndProgramStopped()
    {
        var eligible = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop);
        var ineligible = Reading(CircuitStatus.Charge, ProgramRunningStatus.Running);

        Assert.True(WorkflowActionRules.CanPerform(eligible, "transfer"));
        Assert.False(WorkflowActionRules.CanPerform(ineligible, "transfer"));
    }

    [Fact]
    public void UnknownAction_IsAlwaysDisabled()
    {
        var reading = Reading(CircuitStatus.Idle, ProgramRunningStatus.Stop);

        Assert.False(WorkflowActionRules.CanPerform(reading, "calibration"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowActionRulesTests"
```

Expected: FAIL — `WorkflowActionRules` does not exist, and `ChannelTelemetry` has no `IsConnected` parameter.

- [ ] **Step 3: Add `IsConnected` to `ChannelTelemetry`**

In `Models/DTOs/Workflow/TelemetryEntry.cs`, append to `ChannelTelemetry` (after `TimeStamp`):

```csharp
    DateTime TimeStamp = default,
    bool IsConnected = true);
```

- [ ] **Step 4: Implement `WorkflowActionRules`**

Create `Services/Implementations/Workflow/WorkflowActionRules.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Models.Enums;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Canvas-native mirror of DashboardView.CanContextAction, minus "calibration" - the canvas has
/// no calibration entry point (D9 lists only Start/Stop/Pause/Continue/Transfer).
/// </summary>
public static class WorkflowActionRules
{
    public static bool CanPerform(ChannelTelemetry reading, string action)
    {
        var cs = reading.Status;
        var ps = reading.ProgramStatus;
        var tcp = reading.IsConnected;

        if (cs == CircuitStatus.Offline) return false;

        return action switch
        {
            "start" => ps == ProgramRunningStatus.Stop && tcp,
            "stop" => tcp,
            "pause" => ps == ProgramRunningStatus.Running && tcp,
            "continue" => ps == ProgramRunningStatus.Running
                          && (cs == CircuitStatus.Pause
                              || cs == CircuitStatus.Interrupt
                              || cs == CircuitStatus.Countinue
                              || cs == CircuitStatus.Error
                              || cs == CircuitStatus.Msg) && tcp,
            "transfer" => cs == CircuitStatus.Idle && ps == ProgramRunningStatus.Stop && tcp,
            _ => false,
        };
    }
}
```

- [ ] **Step 5: Populate `IsConnected` in the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`'s `PushTelemetryAsync`, append to the
`new ChannelTelemetry(...)` construction, after `SystemErrorId:`/`ProgramStatus:`/`TimeStamp:`
(whichever is currently last):

```csharp
                IsConnected: circuit.IsConnected);
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowActionRulesTests"
```

Expected: PASS (11 tests).

- [ ] **Step 7: Run the full suite and commit**

```bash
dotnet test BatteryTestingSystem.Tests
git add Models/DTOs/Workflow/TelemetryEntry.cs Components/Pages/Workflows/WorkflowCanvasPage.razor \
  Services/Implementations/Workflow/WorkflowActionRules.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowActionRulesTests.cs
git commit -m "feat(workflow-canvas): per-item action enablement (WorkflowActionRules)

Exact mirror of DashboardView.CanContextAction, minus calibration -
D9 lists only Start/Stop/Pause/Continue/Transfer for the canvas.
Requires IsConnected, the raw TCP flag the dashboard's own rule reads
(circuit.IsConnected), added to ChannelTelemetry and populated in
PushTelemetryAsync alongside the reading it already builds each tick.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: ChannelActionExecutor

Extracts `DashboardView.DoAction`'s per-device `SemaphoreSlim` concurrency pattern — channels on
one device run sequentially, devices run in parallel — into a directly unit-testable class (D13).
The pattern currently exists twice (`DashboardView` and `TransferDialog`, independently
re-implemented) with zero direct tests, because both copies live inside Razor files. This is the
canvas's only consumer; neither existing copy is touched.

**Files:**
- Create: `Services/Implementations/Workflow/ChannelActionExecutor.cs`
- Create: `BatteryTestingSystem.Tests/Services/Workflow/RecordingChannelCommandHandler.cs`
- Create: `BatteryTestingSystem.Tests/Services/Workflow/ChannelActionExecutorTests.cs`

**Interfaces:**
- Consumes: `IChannelCommandHandler` (`Services/Interfaces/IChannelCommandHandler.cs`),
  `CommonResponse<T>` (`Models/DTOs/ResponseDTOs.cs`).
- Produces:
  - `record ChannelActionResult(int DeviceId, int SecondaryBoardNumber, int ChannelNumber, bool Success, string Message)`
  - `new ChannelActionExecutor()` — one instance per page, holding its own device-lock map for
    the page's lifetime (mirroring `DashboardView`'s own `_deviceLocks` field).
  - `ExecuteAsync(string action, IReadOnlyList<IChannelCommandHandler> circuits, Action? onStepCompleted = null, CancellationToken cancellationToken = default) → Task<IReadOnlyList<ChannelActionResult>>`

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Services/Workflow/RecordingChannelCommandHandler.cs` — a fake
distinct from the existing `FakeChannelCommandHandler` (whose action methods throw
`NotImplementedException` on purpose, since `CircuitSelectionLogic` never calls them). This one
implements the four action methods for real, with an artificial delay so overlap between calls is
observable:

```csharp
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.UI;
using BatteryTestingSystem.Models.DTOs;
using BatteryTestingSystem.Models.Enums;
using BatteryTestingSystem.Services;
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Unlike FakeChannelCommandHandler (whose action methods throw on purpose - CircuitSelectionLogic
/// never calls them), this fake implements Start/Stop/Pause/Continue for real, with a delay long
/// enough that two overlapping calls are reliably observable via a shared concurrency counter -
/// the only way to prove "same device runs sequentially, different devices run in parallel"
/// without depending on real timing precision.
/// </summary>
public class RecordingChannelCommandHandler : IChannelCommandHandler
{
    private readonly ConcurrencyProbe _probe;

    public static RecordingChannelCommandHandler Circuit(int deviceId, int board, int channel, ConcurrencyProbe probe) =>
        new(probe) { Channel = new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = board, ChannelNumber = channel } };

    private RecordingChannelCommandHandler(ConcurrencyProbe probe) => _probe = probe;

    public ChannelDto Channel { get; set; } = new();
    public ProgramDTO Program { get; set; } = null!;
    public List<StepModel> ExpandedProgramSteps => new();
    public BatteryDTO Battery { get; set; } = null!;
    public SessionRecordDto Session { get; set; } = null!;
    public CalibrationDto calibration { get; set; } = null!;
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() { }
    public recordRequest RealTime { get; set; } = new();
    public DbcRecord dbcData { get; set; } = null!;
    public bool commandinterrupt { get; set; }
    public bool IsConnected => true;
    public IAlarmService? Alarms { get; set; }
    public event Action? OnChannelChanged;
    public CancellationTokenSource _cts { get; set; } = null!;
    public Channel<recordStoreRequest> _StoreQueue { get; set; } = System.Threading.Channels.Channel.CreateUnbounded<recordStoreRequest>();

    public Task InitializeAsync() => throw new NotImplementedException();
    public void EnqueueForStore(recordStoreRequest request, Dictionary<string, object>? dbcRecord) => throw new NotImplementedException();
    public Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command) => throw new NotImplementedException();
    public async Task<CommonResponse<bool>> StartProgram() => await _probe.RunAsync(Channel.DeviceID);
    public async Task<CommonResponse<bool>> StopProgram() => await _probe.RunAsync(Channel.DeviceID);
    public async Task<CommonResponse<bool>> PauseProgram() => await _probe.RunAsync(Channel.DeviceID);
    public async Task<CommonResponse<bool>> ContinueProgram() => await _probe.RunAsync(Channel.DeviceID);
    public Task<CommonResponse<bool>> TimeSyn() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ResetSystem() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramStepsDTO) => throw new NotImplementedException();
    public Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingDetails() => throw new NotImplementedException();
    public Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryConfigDetails() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> UnregisterAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToCalibrationAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SendLiveCurrentandVoltage() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CalibrationPointPreset(CalibrationQueryId queryId, float value, CalibrationRange? range = null) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetGainOffset(CalibrationQueryId queryId, float gain, float offset, CalibrationRange? range = null) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CancelCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopCalibration(bool force = false) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopverifyCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<CalibrationData>> PreviousCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> TransferDbcFile(DbcFileRecordDto? port1Dbc = null, DbcFileRecordDto? port2Dbc = null, DbcFileRecordDto? port3Dbc = null) => throw new NotImplementedException();
}

/// <summary>Tracks how many calls for a given device are in flight at once. Two calls for the
/// SAME device must never overlap (peak count 1); calls for DIFFERENT devices must be able to
/// overlap (peak count > 1) - the whole point of the per-device SemaphoreSlim pattern.</summary>
public class ConcurrencyProbe
{
    private readonly object _lock = new();
    private readonly Dictionary<int, int> _activePerDevice = new();
    public readonly Dictionary<int, int> PeakPerDevice = new();

    public async Task<CommonResponse<bool>> RunAsync(int deviceId)
    {
        lock (_lock)
        {
            _activePerDevice[deviceId] = _activePerDevice.GetValueOrDefault(deviceId) + 1;
            PeakPerDevice[deviceId] = Math.Max(PeakPerDevice.GetValueOrDefault(deviceId), _activePerDevice[deviceId]);
        }

        await Task.Delay(50);

        lock (_lock) { _activePerDevice[deviceId]--; }
        return CommonResponse<bool>.Ok(true);
    }
}
```

Create `BatteryTestingSystem.Tests/Services/Workflow/ChannelActionExecutorTests.cs`:

```csharp
using System.Linq;
using BatteryTestingSystem.Services.Implementations.Workflow;
using BatteryTestingSystem.Services.Interfaces;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Pins the concurrency contract DashboardView.DoAction has always relied on but never had a
/// direct test for, because the pattern lived only inside a Razor file: channels on the same
/// device run one at a time, channels on different devices run in parallel.
/// </summary>
public class ChannelActionExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_RunsChannelsOnTheSameDeviceSequentially()
    {
        var probe = new ConcurrencyProbe();
        var circuits = new IChannelCommandHandler[]
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(1, 1, 2, probe),
        };

        await new ChannelActionExecutor().ExecuteAsync("start", circuits);

        Assert.Equal(1, probe.PeakPerDevice[1]);
    }

    [Fact]
    public async Task ExecuteAsync_RunsChannelsOnDifferentDevicesInParallel()
    {
        var probe = new ConcurrencyProbe();
        var circuits = new IChannelCommandHandler[]
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(2, 1, 1, probe),
        };

        await new ChannelActionExecutor().ExecuteAsync("start", circuits);

        Assert.True(probe.PeakPerDevice[1] >= 1 && probe.PeakPerDevice[2] >= 1);
        // Both devices' 50ms calls overlapping is what "parallel across devices" means in
        // practice - assert the wall-clock evidence rather than an internal counter that could
        // coincidentally read 1 on a slow CI box even when they did run concurrently.
    }

    [Fact]
    public async Task ExecuteAsync_CompletesBothDevicesFasterThanTheirCombinedSequentialTime()
    {
        // The real proof of cross-device parallelism: 2 devices x 50ms each finishes in ~50ms,
        // not ~100ms, when they are not forced to share one lock.
        var probe = new ConcurrencyProbe();
        var circuits = new IChannelCommandHandler[]
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(2, 1, 1, probe),
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await new ChannelActionExecutor().ExecuteAsync("start", circuits);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 90, $"took {sw.ElapsedMilliseconds}ms, expected well under 100ms");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOneResultPerCircuit_WithItsPhysicalAddress()
    {
        var probe = new ConcurrencyProbe();
        var circuits = new IChannelCommandHandler[] { RecordingChannelCommandHandler.Circuit(1, 8, 2, probe) };

        var results = await new ChannelActionExecutor().ExecuteAsync("start", circuits);

        var result = Assert.Single(results);
        Assert.Equal(1, result.DeviceId);
        Assert.Equal(8, result.SecondaryBoardNumber);
        Assert.Equal(2, result.ChannelNumber);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_DispatchesToTheCorrectMethodPerAction()
    {
        // A cheap way to prove routing without four near-identical fakes: PauseProgram is the
        // only one of the four whose CommonResponse.Message we control per-call via a second
        // probe wired only to Pause. If "pause" reached anything else the assertion would see the
        // wrong (empty) message.
        var calledMethods = new List<string>();
        var handler = new TrackingHandler(calledMethods);

        await new ChannelActionExecutor().ExecuteAsync("start", new IChannelCommandHandler[] { handler });
        await new ChannelActionExecutor().ExecuteAsync("stop", new IChannelCommandHandler[] { handler });
        await new ChannelActionExecutor().ExecuteAsync("pause", new IChannelCommandHandler[] { handler });
        await new ChannelActionExecutor().ExecuteAsync("continue", new IChannelCommandHandler[] { handler });

        Assert.Equal(new[] { "start", "stop", "pause", "continue" }, calledMethods);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsForAnUnsupportedAction()
    {
        var probe = new ConcurrencyProbe();
        var circuits = new IChannelCommandHandler[] { RecordingChannelCommandHandler.Circuit(1, 1, 1, probe) };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ChannelActionExecutor().ExecuteAsync("calibration", circuits));
    }

    [Fact]
    public async Task ExecuteAsync_InvokesOnStepCompletedOncePerCircuit()
    {
        var probe = new ConcurrencyProbe();
        var circuits = new IChannelCommandHandler[]
        {
            RecordingChannelCommandHandler.Circuit(1, 1, 1, probe),
            RecordingChannelCommandHandler.Circuit(1, 1, 2, probe),
        };
        var completedCount = 0;

        await new ChannelActionExecutor().ExecuteAsync(
            "start", circuits, onStepCompleted: () => Interlocked.Increment(ref completedCount));

        Assert.Equal(2, completedCount);
    }
}
```

`TrackingHandler` records which of the four action methods was invoked, needed only by the
dispatch test above. Add it to the bottom of `RecordingChannelCommandHandler.cs`:

```csharp
/// <summary>Records which action method was called, for ChannelActionExecutorTests' routing
/// test - the four action methods on RecordingChannelCommandHandler are otherwise
/// indistinguishable from outside.</summary>
public class TrackingHandler : IChannelCommandHandler
{
    private readonly List<string> _calls;
    public TrackingHandler(List<string> calls) => _calls = calls;

    public ChannelDto Channel { get; set; } = new() { DeviceID = 1, SecondaryBoardNumber = 1, ChannelNumber = 1 };
    public ProgramDTO Program { get; set; } = null!;
    public List<StepModel> ExpandedProgramSteps => new();
    public BatteryDTO Battery { get; set; } = null!;
    public SessionRecordDto Session { get; set; } = null!;
    public CalibrationDto calibration { get; set; } = null!;
    public TcpClient _tcpClient => null!;
    public DeviceConnection Connection { get; set; } = null!;
    public void MarkDisconnected() { }
    public recordRequest RealTime { get; set; } = new();
    public DbcRecord dbcData { get; set; } = null!;
    public bool commandinterrupt { get; set; }
    public bool IsConnected => true;
    public IAlarmService? Alarms { get; set; }
    public event Action? OnChannelChanged;
    public CancellationTokenSource _cts { get; set; } = null!;
    public Channel<recordStoreRequest> _StoreQueue { get; set; } = System.Threading.Channels.Channel.CreateUnbounded<recordStoreRequest>();

    public Task InitializeAsync() => throw new NotImplementedException();
    public void EnqueueForStore(recordStoreRequest request, Dictionary<string, object>? dbcRecord) => throw new NotImplementedException();
    public Task<CommonResponse<T>> SendAndWaitForResponseAsync<T>(byte[] command) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StartProgram() { _calls.Add("start"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> StopProgram() { _calls.Add("stop"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> PauseProgram() { _calls.Add("pause"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> ContinueProgram() { _calls.Add("continue"); return Task.FromResult(CommonResponse<bool>.Ok(true)); }
    public Task<CommonResponse<bool>> TimeSyn() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> ResetSystem() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToReadWriteAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetBatteryParamAsync(BatteryDTO BatteryParams) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramStepsDTO) => throw new NotImplementedException();
    public Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingDetails() => throw new NotImplementedException();
    public Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryConfigDetails() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> UnregisterAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> HWReadyToCalibrationAsync() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SendLiveCurrentandVoltage() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CalibrationPointPreset(CalibrationQueryId queryId, float value, CalibrationRange? range = null) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> SetGainOffset(CalibrationQueryId queryId, float gain, float offset, CalibrationRange? range = null) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> CancelCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopCalibration(bool force = false) => throw new NotImplementedException();
    public Task<CommonResponse<bool>> StopverifyCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<CalibrationData>> PreviousCalibration() => throw new NotImplementedException();
    public Task<CommonResponse<bool>> TransferDbcFile(DbcFileRecordDto? port1Dbc = null, DbcFileRecordDto? port2Dbc = null, DbcFileRecordDto? port3Dbc = null) => throw new NotImplementedException();
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~ChannelActionExecutorTests"
```

Expected: FAIL — `ChannelActionExecutor` does not exist.

- [ ] **Step 3: Implement**

Create `Services/Implementations/Workflow/ChannelActionExecutor.cs`:

```csharp
using BatteryTestingSystem.Services.Interfaces;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

public record ChannelActionResult(
    int DeviceId, int SecondaryBoardNumber, int ChannelNumber, bool Success, string Message);

/// <summary>
/// Extracted from DashboardView.DoAction (D13): one SemaphoreSlim per DeviceID, so channels on
/// the same device run sequentially (protecting a shared serial/TCP link) while different devices
/// run fully in parallel. The pattern existed twice before this - DashboardView and TransferDialog
/// each re-implemented it independently - with zero direct tests, because both copies lived only
/// inside Razor files. This class is the canvas's only consumer; neither existing copy changes.
///
/// Instantiate ONE of these per page and reuse it for the page's lifetime, mirroring
/// DashboardView's own _deviceLocks field - a fresh instance per call would create a fresh lock
/// per call too, defeating the point of serializing repeated actions against the same device.
/// </summary>
public class ChannelActionExecutor
{
    private readonly Dictionary<int, SemaphoreSlim> _deviceLocks = new();

    public async Task<IReadOnlyList<ChannelActionResult>> ExecuteAsync(
        string action,
        IReadOnlyList<IChannelCommandHandler> circuits,
        Action? onStepCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = circuits.Select(async circuit =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            SemaphoreSlim deviceLock;
            lock (_deviceLocks)
            {
                if (!_deviceLocks.TryGetValue(circuit.Channel.DeviceID, out deviceLock!))
                {
                    deviceLock = new SemaphoreSlim(1, 1);
                    _deviceLocks[circuit.Channel.DeviceID] = deviceLock;
                }
            }

            await deviceLock.WaitAsync(cancellationToken);
            try
            {
                var response = await RunAsync(action, circuit);
                onStepCompleted?.Invoke();
                return new ChannelActionResult(
                    circuit.Channel.DeviceID, circuit.Channel.SecondaryBoardNumber, circuit.Channel.ChannelNumber,
                    response.Success, response.Message);
            }
            finally
            {
                deviceLock.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private static Task<Models.DTOs.CommonResponse<bool>> RunAsync(string action, IChannelCommandHandler circuit) => action switch
    {
        "start" => circuit.StartProgram(),
        "stop" => circuit.StopProgram(),
        "pause" => circuit.PauseProgram(),
        "continue" => circuit.ContinueProgram(),
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ChannelActionExecutor only runs start/stop/pause/continue - transfer opens TransferDialog directly."),
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~ChannelActionExecutorTests"
```

Expected: PASS (7 tests). If `ExecuteAsync_CompletesBothDevicesFasterThan...` is flaky under CI
load, widen its threshold from 90ms rather than deleting it — the 50ms delay in `ConcurrencyProbe`
can be raised too if needed, but keep the assertion, since it is the only test that actually
proves cross-device parallelism rather than merely not-crashing.

- [ ] **Step 5: Run the full suite and commit**

```bash
dotnet test BatteryTestingSystem.Tests
git add Services/Implementations/Workflow/ChannelActionExecutor.cs \
  BatteryTestingSystem.Tests/Services/Workflow/RecordingChannelCommandHandler.cs \
  BatteryTestingSystem.Tests/Services/Workflow/ChannelActionExecutorTests.cs
git commit -m "feat(workflow-canvas): ChannelActionExecutor, directly testable at last

Extracts DashboardView.DoAction's per-device SemaphoreSlim pattern - the
one that has existed twice, independently, with zero direct tests,
because both copies lived only inside Razor files (D13). Same
semantics: one device's channels run sequentially, different devices
run in parallel. The canvas is this class's only consumer; neither
DashboardView nor TransferDialog is touched.

New RecordingChannelCommandHandler (distinct from the existing
FakeChannelCommandHandler, whose action methods deliberately throw)
implements the four methods for real with a shared ConcurrencyProbe,
proving same-device-sequential / cross-device-parallel via both a peak-
concurrency counter and a wall-clock timing assertion.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Multi-select data model and parent-header shortcuts

Replaces the page's single `_selectedNodeId` with a set, threads it through `CanvasSurface`, and
adds the Board/Device header shortcuts the spec calls for ("clicking a Board node's header selects
its 8 channels; a Device node selects all of its channels").

**Files:**
- Modify: `Components/UI/WorkflowCanvas/CanvasSurface.razor`
- Modify: `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`
- Modify: `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`

**Interfaces:**
- Consumes: `WorkflowSelectionLogic.ApplyBulk` (Task 1), `WorkflowNodeLabeller.ChannelIdsForNodeEntity` (Phase 1), `WorkflowGraphBuilder.ChannelNodeId` (existing).
- Produces: `CanvasSurface.SelectedNodeIds: IReadOnlyCollection<string>`; `BoardNode`/`DeviceNode.OnSelectChildren: EventCallback<string>` (raises the owning node's id); page fields `_selectedNodeIds: HashSet<string>`, `_selectedReadings: Dictionary<string, ChannelTelemetry>` (kept in sync from `PushTelemetryAsync` so selection logic never needs a fresh CM lookup).

- [ ] **Step 1: Rename the parameter on `CanvasSurface`**

In `Components/UI/WorkflowCanvas/CanvasSurface.razor`, replace:

```csharp
    [Parameter] public string? SelectedNodeId { get; set; }
```

with:

```csharp
    [Parameter] public IReadOnlyCollection<string> SelectedNodeIds { get; set; } = Array.Empty<string>();
```

and the per-node computation:

```csharp
        var selected = node.Id == SelectedNodeId;
```

with:

```csharp
        var selected = SelectedNodeIds.Contains(node.Id);
```

- [ ] **Step 2: Add the header select-children callback to Board and Device nodes**

In `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`, add a parameter and wire it onto
`CanvasNode`'s existing header (the header text is rendered by `CanvasNode` itself via its
`Title` parameter, so the click target is `CanvasNode`'s own `.wf-node__header` — add a new
`OnHeaderClick` passthrough on `CanvasNode` first):

In `Components/UI/WorkflowCanvas/CanvasNode.razor`, add a click handler to the header div:

```razor
    <div class="wf-node__header" @onclick="() => OnHeaderClick.InvokeAsync(Node.Id)" @onclick:stopPropagation="true">
```

(Replaces the existing `<div class="wf-node__header">` opening tag. `stopPropagation` matters: the
header is inside the draggable node body that `workflow-canvas.js`'s pointerdown handler also
inspects for `.wf-node__header` to start a drag — a plain click must not also fire the canvas's
own empty-space-click logic.)

Add the parameter in `CanvasNode.razor`'s `@code` block:

```csharp
    [Parameter] public EventCallback<string> OnHeaderClick { get; set; }
```

In `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`, pass it through:

```razor
<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale"
            OnHeaderClick="OnSelectChildren">
```

and add the parameter:

```csharp
    [Parameter] public EventCallback<string> OnSelectChildren { get; set; }
```

Do the same in `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor` (identical pattern — add
`OnHeaderClick="OnSelectChildren"` to its `<CanvasNode>` tag and the matching `OnSelectChildren`
parameter).

In `CanvasSurface.razor`, wire both:

```razor
            case NodeKind.Device:
                <DeviceNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                            IsSelected="selected" IsStale="stale"
                            BoardCount="@ChildCountFor(node.Id)"
                            OnSelectChildren="OnSelectNodeChildren" />
                break;

            case NodeKind.Board:
                <BoardNode @key="node.Id" Node="node" Title="@LabelFor(node)"
                           IsSelected="selected" IsStale="stale"
                           ChannelCount="@ChildCountFor(node.Id)"
                           IsCollapsed="@CollapsedBoardIds.Contains(node.Id)"
                           OnToggleCollapse="OnToggleBoardCollapse"
                           OnSelectChildren="OnSelectNodeChildren" />
                break;
```

and add the new passthrough parameter (near `OnToggleBoardCollapse`):

```csharp
    [Parameter] public EventCallback<string> OnSelectNodeChildren { get; set; }
```

- [ ] **Step 3: Replace `_selectedNodeId` with a set in the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, replace the field:

```csharp
    private string? _selectedNodeId;
```

with:

```csharp
    private readonly HashSet<string> _selectedNodeIds = new();

    /// <summary>The dock still shows one node's full detail; anything else (0 or >1 selected)
    /// falls back to its placeholder / the new SelectionActionBar (Task 6).</summary>
    private string? _primarySelectedNodeId => _selectedNodeIds.Count == 1 ? _selectedNodeIds.Single() : null;
```

Update every remaining `_selectedNodeId` reference:

`SelectedNode` getter:
```csharp
    private WorkflowNode? SelectedNode =>
        _primarySelectedNodeId is null ? null : _graph.Nodes.FirstOrDefault(n => n.Id == _primarySelectedNodeId);
```

The dock's `IsStale` binding:
```csharp
                                IsStale="@(_primarySelectedNodeId is not null && _staleNodeIds.Contains(_primarySelectedNodeId))"
```

`CanvasSurface`'s parameter:
```csharp
                                   SelectedNodeIds="_selectedNodeIds"
```

`HandleLoad` and `HandleDelete` (both currently do `_selectedNodeId = null;`):
```csharp
        _selectedNodeIds.Clear();
```

`HandleAttachmentChanged`:
```csharp
    private async Task HandleAttachmentChanged(NodeAttachment? attachment)
    {
        if (_primarySelectedNodeId is null) return;

        var check = WorkflowGraphValidator.CanAttach(_graph, _primarySelectedNodeId);
        if (!check.IsValid)
        {
            Toast.Error(check.Error!);
            return;
        }

        _graph = WorkflowGraphMutations.SetAttachment(_graph, _primarySelectedNodeId, attachment);
        _dirty = true;
        await InvokeAsync(StateHasChanged);
    }
```

`HandleDeleteNode` (currently `if (_selectedNodeId == nodeId) _selectedNodeId = null;`):
```csharp
        _selectedNodeIds.Remove(nodeId);
```

- [ ] **Step 4: Replace the `OnSelect` JSInvokable and add the two new selection handlers**

Replace the existing `OnSelect` JSInvokable:

```csharp
    [JSInvokable]
    public async Task OnSelect(string? nodeId)
    {
        if (_selectedNodeId == nodeId) return;
        _selectedNodeId = nodeId;
        await InvokeAsync(StateHasChanged);
    }
```

with:

```csharp
    [JSInvokable]
    public async Task OnSelect(string? nodeId, bool additive)
    {
        if (nodeId is null)
        {
            if (additive) return;   // ctrl/shift-clicking empty space is a no-op, not a clear
            _selectedNodeIds.Clear();
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (!additive)
        {
            // Plain click replaces the whole selection. Still enforced not-Offline: this
            // establishes a brand-new anchor, so no prior-selection compatibility check applies.
            _selectedNodeIds.Clear();
            var result = WorkflowSelectionLogic.ApplyBulk(new[] { nodeId }, _selectedReadings, _selectedNodeIds);
            if (result.Outcome == SelectionOutcome.NothingEligible)
                Toast.Warning("That channel cannot be selected (offline).");
            await InvokeAsync(StateHasChanged);
            return;
        }

        // Ctrl/Shift-click: toggle this one node in or out of the existing selection.
        if (_selectedNodeIds.Contains(nodeId))
        {
            _selectedNodeIds.Remove(nodeId);
        }
        else
        {
            var result = WorkflowSelectionLogic.ApplyBulk(new[] { nodeId }, _selectedReadings, _selectedNodeIds);
            if (result.Outcome == SelectionOutcome.NothingEligible)
                Toast.Warning("Cannot add that channel — it is offline or does not match the current selection.");
        }

        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnMarqueeSelect(string[] nodeIds, bool additive)
    {
        if (!additive) _selectedNodeIds.Clear();

        var result = WorkflowSelectionLogic.ApplyBulk(nodeIds, _selectedReadings, _selectedNodeIds);

        if (result.Skipped > 0)
            Toast.Warning($"{result.Added} selected, {result.Skipped} skipped (offline or a different state).");

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSelectNodeChildren(string parentNodeId)
    {
        var parent = _graph.Nodes.FirstOrDefault(n => n.Id == parentNodeId);
        if (parent is null || parent.EntityId is not { } entityId) return;

        var childChannelIds = _labeller.ChannelIdsForNodeEntity(parent.Kind, entityId)
            .Select(WorkflowGraphBuilder.ChannelNodeId)
            .ToArray();

        _selectedNodeIds.Clear();
        var result = WorkflowSelectionLogic.ApplyBulk(childChannelIds, _selectedReadings, _selectedNodeIds);

        if (result.Skipped > 0)
            Toast.Warning($"{result.Added} selected, {result.Skipped} skipped (offline or a different state).");

        await InvokeAsync(StateHasChanged);
    }
```

Wire `OnSelectNodeChildren` into `CanvasSurface`'s markup (from Step 2):

```razor
                                   OnSelectNodeChildren="OnSelectNodeChildren"
```

- [ ] **Step 5: Keep `_selectedReadings` in sync**

Add the field next to `_selectedNodeIds`:

```csharp
    private Dictionary<string, ChannelTelemetry> _selectedReadings = new();
```

In `PushTelemetryAsync`, after the `readings` dictionary (keyed by `long` channelId) is fully
built, add a line that re-keys it by canvas node id — `WorkflowSelectionLogic` and the two new
JSInvokable handlers operate on node ids, not database ids:

```csharp
        _selectedReadings = readings.ToDictionary(
            kv => WorkflowGraphBuilder.ChannelNodeId(kv.Key), kv => kv.Value);
```

Place this line immediately before `var entries = WorkflowTelemetryBridge.BuildEntries(...)`.

- [ ] **Step 6: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS. If any test constructs `<CanvasSurface SelectedNodeId="...">` directly (none did
as of Phase 2, per an explicit grep — re-check now, since this is exactly the class of gap this
branch has hit twice already), update it to `SelectedNodeIds="new[] { "..." }"`.

- [ ] **Step 7: Verify in the browser**

Start the app, place a full device (64 channels), and confirm: clicking a Board node's header
selects its 8 channels (all 8 gain the `.wf-node--selected` ring); clicking the Device node's
header selects all placed channels; Ctrl-clicking one already-selected channel removes just that
one; clicking empty canvas clears the selection. Because the simulator's channels sit `Idle`/`Stop`
homogeneously on a quiet bench, none of these should trigger the "skipped" warning — if one does,
investigate before moving on, since it would mean the anchor-matching rule is misfiring on
identical-state channels.

- [ ] **Step 8: Commit**

```bash
git add Components/UI/WorkflowCanvas/CanvasSurface.razor \
  Components/UI/WorkflowCanvas/CanvasNode.razor \
  Components/UI/WorkflowCanvas/Nodes/BoardNode.razor \
  Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor \
  Components/Pages/Workflows/WorkflowCanvasPage.razor
git commit -m "feat(workflow-canvas): multi-select data model and header select-all shortcuts

_selectedNodeId (string) becomes _selectedNodeIds (HashSet<string>),
threaded through CanvasSurface as SelectedNodeIds. Clicking a Board or
Device node's header now selects all its channels, filtered through
WorkflowSelectionLogic.ApplyBulk exactly like a marquee will (Task 5) -
same eligibility rule, same 'N selected, M skipped' reporting.

The properties dock keeps working unchanged for the single-selection
case via a new _primarySelectedNodeId (set only when exactly one node
is selected); HandleAttachmentChanged still operates on that one node,
since attaching a program is a per-channel action, not a bulk one (D5).

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Marquee drag and the pan gesture remap

Drag on empty canvas becomes a rubber-band marquee; panning moves to middle-mouse-button drag or
holding Space while dragging (D12). This is pure JS/CSS — no C# unit test surface exists for it,
matching every prior interaction feature on this branch (pan, zoom, node-drag, connect); verified
live in the browser instead.

**Files:**
- Modify: `wwwroot/js/workflow-canvas.js`
- Modify: `wwwroot/css/workflow-canvas.css`

**Interfaces:**
- Consumes: `dotNet.invokeMethodAsync("OnSelect", nodeId, additive)` and
  `dotNet.invokeMethodAsync("OnMarqueeSelect", nodeIds, additive)` (Task 4).
- Produces: no new public `workflowCanvas.*` functions — this only changes internal pointer
  dispatch.

- [ ] **Step 1: Track Space-held state**

In `workflowCanvas.init(host, dotNetRef, options)`, after the existing `host.addEventListener`
calls, add two document-level listeners (Space is a page-wide modifier, not scoped to canvas
focus) and store their handlers on `s` for cleanup:

```javascript
            s.handlers.keydown = (ev) => { if (ev.code === "Space") s.spaceHeld = true; };
            s.handlers.keyup = (ev) => { if (ev.code === "Space") s.spaceHeld = false; };
            document.addEventListener("keydown", s.handlers.keydown);
            document.addEventListener("keyup", s.handlers.keyup);
```

Initialize `spaceHeld: false` alongside the other fields in the `s` object literal earlier in
`init`.

In `workflowCanvas.dispose(host)`, remove them alongside the existing host listener removals:

```javascript
            document.removeEventListener("keydown", s.handlers.keydown);
            document.removeEventListener("keyup", s.handlers.keyup);
```

- [ ] **Step 2: Add marquee geometry helpers**

Add near the other coordinate-math helpers (alongside `moveConnect`'s inline formula), before the
`onPointerDown` section:

```javascript
    // ---- marquee selection ----------------------------------------------------

    function worldPoint(s, e) {
        const rect = s.host.getBoundingClientRect();
        return {
            x: (e.clientX - rect.left - s.panX) / s.zoom,
            y: (e.clientY - rect.top - s.panY) / s.zoom,
        };
    }

    function beginMarquee(s, e, additive) {
        const start = worldPoint(s, e);
        s.marquee = { startWorld: start, additive, el: document.createElement("div") };
        s.marquee.el.className = "wf-marquee";
        s.host.appendChild(s.marquee.el);
        positionMarqueeEl(s, e);
        capture(s.host, e.pointerId);
    }

    function positionMarqueeEl(s, e) {
        const hostRect = s.host.getBoundingClientRect();
        const startScreenX = s.marquee.startWorld.x * s.zoom + s.panX;
        const startScreenY = s.marquee.startWorld.y * s.zoom + s.panY;
        const curX = e.clientX - hostRect.left;
        const curY = e.clientY - hostRect.top;

        const left = Math.min(startScreenX, curX);
        const top = Math.min(startScreenY, curY);
        const width = Math.abs(curX - startScreenX);
        const height = Math.abs(curY - startScreenY);

        Object.assign(s.marquee.el.style, {
            left: `${left}px`, top: `${top}px`, width: `${width}px`, height: `${height}px`,
        });
        s.marquee.lastRect = { left, top, width, height };
    }

    // Below this, a pointerdown-and-up with negligible movement is treated as a click on empty
    // space (clear selection), not a zero-area marquee.
    const MARQUEE_CLICK_THRESHOLD_PX = 4;

    function endMarquee(s, e) {
        const m = s.marquee;
        s.marquee = null;
        release(s.host, e.pointerId);

        const rect = m.lastRect;
        const isRealDrag = rect.width > MARQUEE_CLICK_THRESHOLD_PX || rect.height > MARQUEE_CLICK_THRESHOLD_PX;
        m.el.remove();

        if (!isRealDrag) {
            if (s.dotNet) s.dotNet.invokeMethodAsync("OnSelect", null, m.additive);
            return;
        }

        // World-space box, from the same screen rect the overlay was drawn with.
        const worldLeft = (rect.left - s.panX) / s.zoom;
        const worldTop = (rect.top - s.panY) / s.zoom;
        const worldRight = worldLeft + rect.width / s.zoom;
        const worldBottom = worldTop + rect.height / s.zoom;

        const hitIds = [];
        s.world.querySelectorAll('.wf-node[data-node-id^="chn-"]').forEach(node => {
            const x = parseFloat(node.dataset.x || "0");
            const y = parseFloat(node.dataset.y || "0");
            const right = x + node.offsetWidth;
            const bottom = y + node.offsetHeight;
            const intersects = x < worldRight && right > worldLeft && y < worldBottom && bottom > worldTop;
            if (intersects) hitIds.push(node.dataset.nodeId);
        });

        if (s.dotNet) s.dotNet.invokeMethodAsync("OnMarqueeSelect", hitIds, m.additive);
    }
```

- [ ] **Step 3: Rewire the pointer dispatch**

Replace `onPointerDown`:

```javascript
    function onPointerDown(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;

        if (beginConnect(s, e)) return;

        if (e.target.closest(".wf-node__header") && beginNodeDrag(s, e)) return;

        const nodeEl = e.target.closest(".wf-node");
        if (nodeEl) {
            const additive = e.ctrlKey || e.shiftKey;
            if (s.dotNet) s.dotNet.invokeMethodAsync("OnSelect", nodeEl.dataset.nodeId, additive);
            return;
        }

        // Empty canvas: middle-button or Space+drag pans (unchanged pan mechanics); a plain
        // left-drag is now a marquee instead of a pan (D12) - panning moved here deliberately,
        // since selection is the primary verb on an operating surface.
        if (e.button === 1 || s.spaceHeld) {
            beginPan(s, e);
            return;
        }

        beginMarquee(s, e, e.ctrlKey || e.shiftKey);
    }
```

Replace `onPointerMove`:

```javascript
    function onPointerMove(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;
        if (s.connect) { moveConnect(s, e); return; }
        if (s.drag) { moveNodeDrag(s, e); return; }
        if (s.marquee) { positionMarqueeEl(s, e); return; }
        if (s.panning) movePan(s, e);
    }
```

Replace `onPointerUp`:

```javascript
    function onPointerUp(e) {
        const s = stateOf(e.currentTarget);
        if (!s) return;
        if (s.connect) { endConnect(s, e); return; }
        if (s.drag) { endNodeDrag(s, e); return; }
        if (s.marquee) { endMarquee(s, e); return; }
        if (s.panning) endPan(s, e);
    }
```

- [ ] **Step 4: Add the marquee overlay CSS**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- marquee selection */

.wf-marquee {
    position: absolute;
    border: 1px solid hsl(var(--primary));
    background: hsl(var(--primary) / 0.12);
    pointer-events: none;
    z-index: 5;
}
```

- [ ] **Step 5: Verify in the browser**

Stop the app, rebuild, restart, hard-reload. Place a full device. Confirm:
- Left-drag from empty canvas draws a visible blue-tinted rectangle, and releasing over several
  channel nodes selects all of them (rings appear on each).
- A plain left-click on empty canvas (no drag) clears the selection, as before.
- Middle-mouse-button drag still pans the canvas.
- Holding Space and left-dragging pans the canvas.
- Wheel-zoom is unaffected.

There is no automated test for this step — record the observations directly rather than skipping
verification, per this branch's established practice of catching JS-only regressions that no
xUnit/bUnit suite can see (the `SelectedNodeId` `@`-binding bug and the load-path telemetry bug
were both found exactly this way).

- [ ] **Step 6: Commit**

```bash
git add wwwroot/js/workflow-canvas.js wwwroot/css/workflow-canvas.css
git commit -m "feat(workflow-canvas): marquee selection, panning moves to middle-button/Space (D12)

Left-drag on empty canvas now draws a rubber-band marquee and reports
every intersecting channel node to OnMarqueeSelect on release; panning
moves to middle-mouse-button drag or Space+drag. A plain click with
negligible movement still clears the selection, matching the prior
click-to-deselect behaviour exactly.

No C# test exists for this - matches every prior interaction feature on
this branch (pan, zoom, node-drag, connect), all of which are JS-only
and browser-verified rather than unit-tested.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Selection action bar

The payoff: a floating bar appears whenever anything is selected, showing the count and
Start/Stop/Pause/Continue/Transfer buttons, each enabled per `WorkflowActionRules`, running
through `ChannelActionExecutor` with the existing `Processing.razor` overlay for progress and the
existing `TransferDialog` for Transfer — reused exactly as-is, no changes to either.

**Files:**
- Create: `Components/UI/WorkflowCanvas/SelectionActionBar.razor`
- Create: `BatteryTestingSystem.Tests/Components/SelectionActionBarTests.cs`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Modify: `wwwroot/css/workflow-canvas.css`

**Interfaces:**
- Consumes: `ChannelActionExecutor` (Task 3), `WorkflowActionRules.CanPerform` (Task 2),
  `Components.UI.Loading.Processing` (existing), `Components.UI.Dashboard.TransferDialog`
  (existing, unmodified).
- Produces: `SelectionActionBar` with `[Parameter] int SelectedCount`, `[Parameter] bool
  IsActionEnabled(string action)` — passed as a `Func<string,bool>` — and
  `[Parameter] EventCallback<string> OnAction`.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Components/SelectionActionBarTests.cs`:

```csharp
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The bar itself makes no eligibility decisions - it renders whatever IsActionEnabled reports
/// (WorkflowActionRules, already tested) and reports which button was clicked. These tests only
/// exercise that wiring.
/// </summary>
public class SelectionActionBarTests : TestContext
{
    [Fact]
    public void ShowsTheSelectionCount()
    {
        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 5)
            .Add(x => x.IsActionEnabled, (string _) => true));

        Assert.Contains("5", cut.Markup);
    }

    [Fact]
    public void DisablesAButtonWhenIsActionEnabledReturnsFalse()
    {
        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 1)
            .Add(x => x.IsActionEnabled, (string action) => action != "start"));

        Assert.True(cut.Find("button[data-action='start']").HasAttribute("disabled"));
        Assert.False(cut.Find("button[data-action='stop']").HasAttribute("disabled"));
    }

    [Fact]
    public void ClickingAnEnabledButtonRaisesOnActionWithItsName()
    {
        string? captured = null;

        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 1)
            .Add(x => x.IsActionEnabled, (string _) => true)
            .Add(x => x.OnAction, (string a) => captured = a));

        cut.Find("button[data-action='stop']").Click();

        Assert.Equal("stop", captured);
    }

    [Fact]
    public void RendersAllFiveActions()
    {
        var cut = RenderComponent<SelectionActionBar>(p => p
            .Add(x => x.SelectedCount, 1)
            .Add(x => x.IsActionEnabled, (string _) => true));

        foreach (var action in new[] { "start", "stop", "pause", "continue", "transfer" })
            Assert.NotNull(cut.Find($"button[data-action='{action}']"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~SelectionActionBarTests"
```

Expected: FAIL — `SelectionActionBar` does not exist.

- [ ] **Step 3: Implement the component**

Create `Components/UI/WorkflowCanvas/SelectionActionBar.razor`:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas

@* Makes no eligibility decisions itself - IsActionEnabled (WorkflowActionRules, already tested)
   decides what is clickable; this only renders that and reports which button fired. *@

<div class="wf-selection-action-bar">
    <span class="font-semibold">@SelectedCount selected</span>
    <button class="wf-btn" data-action="start" disabled="@(!IsActionEnabled("start"))"
            @onclick="@(() => OnAction.InvokeAsync("start"))">Start</button>
    <button class="wf-btn" data-action="stop" disabled="@(!IsActionEnabled("stop"))"
            @onclick="@(() => OnAction.InvokeAsync("stop"))">Stop</button>
    <button class="wf-btn" data-action="pause" disabled="@(!IsActionEnabled("pause"))"
            @onclick="@(() => OnAction.InvokeAsync("pause"))">Pause</button>
    <button class="wf-btn" data-action="continue" disabled="@(!IsActionEnabled("continue"))"
            @onclick="@(() => OnAction.InvokeAsync("continue"))">Continue</button>
    <button class="wf-btn" data-action="transfer" disabled="@(!IsActionEnabled("transfer"))"
            @onclick="@(() => OnAction.InvokeAsync("transfer"))">Transfer</button>
</div>

@code {
    [Parameter, EditorRequired] public int SelectedCount { get; set; }
    [Parameter, EditorRequired] public Func<string, bool> IsActionEnabled { get; set; } = _ => false;
    [Parameter] public EventCallback<string> OnAction { get; set; }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~SelectionActionBarTests"
```

Expected: PASS (4 tests).

- [ ] **Step 5: Add styling**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- selection action bar */

.wf-selection-action-bar {
    position: absolute;
    bottom: 1rem;
    left: 50%;
    transform: translateX(-50%);
    z-index: 20;
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem 0.75rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.5rem;
    background: hsl(var(--card));
    color: hsl(var(--card-foreground));
    box-shadow: 0 4px 16px hsl(0 0% 0% / 0.15);
}
```

- [ ] **Step 6: Wire it into the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, add the executor field near
`_telemetryRunner`:

```csharp
    private readonly ChannelActionExecutor _actionExecutor = new();
    private bool _actionInProgress;
    private string _actionName = string.Empty;
    private int _actionCompleted;
    private int _actionTotal;
    private bool _openTransferDialog;
```

Add the action-dispatch method, near `HandleDeleteNode`:

```csharp
    private bool CanRunAction(string action) =>
        _selectedNodeIds.Count > 0
        && _selectedNodeIds.All(id => _selectedReadings.TryGetValue(id, out var r) && WorkflowActionRules.CanPerform(r, action));

    private async Task HandleSelectionAction(string action)
    {
        if (action == "transfer")
        {
            _openTransferDialog = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        var byKey = CM._devices.Values.ToDictionary(
            c => (c.Channel.DeviceID, c.Channel.SecondaryBoardNumber, c.Channel.ChannelNumber));

        var circuits = new List<IChannelCommandHandler>();
        foreach (var nodeId in _selectedNodeIds)
        {
            var node = _graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node?.EntityId is not { } channelId) continue;
            if (!_labeller.TryGetChannelKey(channelId, out var key)) continue;
            if (byKey.TryGetValue(key, out var circuit)) circuits.Add(circuit);
        }

        if (circuits.Count == 0)
        {
            Toast.Error("None of the selected channels are reachable right now.");
            return;
        }

        _actionInProgress = true;
        _actionName = action;
        _actionCompleted = 0;
        _actionTotal = circuits.Count;
        await InvokeAsync(StateHasChanged);

        var results = await _actionExecutor.ExecuteAsync(
            action, circuits,
            onStepCompleted: () => { Interlocked.Increment(ref _actionCompleted); InvokeAsync(StateHasChanged); });

        _actionInProgress = false;

        var failures = results.Where(r => !r.Success).ToList();
        if (failures.Count > 0)
        {
            Toast.Error($"{failures.Count} of {results.Count} failed",
                string.Join("; ", failures.Select(f => $"{f.DeviceId}-{f.SecondaryBoardNumber}-{f.ChannelNumber}: {f.Message}")));
        }
        else
        {
            Toast.Success($"{action} succeeded on {results.Count} channel(s).");
        }

        await InvokeAsync(StateHasChanged);
    }

    private List<IChannelCommandHandler> SelectedCircuitsForTransfer()
    {
        var byKey = CM._devices.Values.ToDictionary(
            c => (c.Channel.DeviceID, c.Channel.SecondaryBoardNumber, c.Channel.ChannelNumber));

        var circuits = new List<IChannelCommandHandler>();
        foreach (var nodeId in _selectedNodeIds)
        {
            var node = _graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node?.EntityId is not { } channelId) continue;
            if (!_labeller.TryGetChannelKey(channelId, out var key)) continue;
            if (byKey.TryGetValue(key, out var circuit)) circuits.Add(circuit);
        }
        return circuits;
    }
```

Add the required `@using`/`@inject` at the top of the file (`ChannelManager CM` is already
injected as `CM`; `IChannelCommandHandler` needs its namespace):

```razor
@using BatteryTestingSystem.Services.Interfaces
```

(Already present per the existing `@using BatteryTestingSystem.Services.Interfaces` line — verify
before adding a duplicate.)

Render the bar and the progress overlay inside `.wf-canvas-host`, alongside the existing
`NodePropertyPicker` conditional block:

```razor
            @if (_selectedNodeIds.Count > 0)
            {
                <SelectionActionBar SelectedCount="_selectedNodeIds.Count"
                                    IsActionEnabled="CanRunAction"
                                    OnAction="HandleSelectionAction" />
            }
```

Render `Processing` and `TransferDialog` once, near the end of the top-level `<div class="wf-shell">`:

```razor
    <Processing IsLoading="_actionInProgress" ActionName="_actionName"
                Completed="_actionCompleted" Total="_actionTotal" />
    <TransferDialog @bind-Open="_openTransferDialog" selectedCircuits="SelectedCircuitsForTransfer()"
                    OnTransferCompleted="@(_ => Toast.Success("Transfer complete."))" />
```

- [ ] **Step 7: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 8: Verify in the browser end to end**

Stop the app, rebuild, restart, hard-reload, with the simulator running. Place a full device,
marquee-select several channels, and confirm:
- The action bar appears at the bottom, showing the correct count.
- Buttons reflect `WorkflowActionRules`: on an idle/stopped bench, **Start** and **Transfer** are
  enabled, **Stop**/**Pause**/**Continue** are disabled (matches `CanPerform`'s rules for
  `Idle`/`Stop`).
- Clicking **Start** shows the `Processing` overlay with a climbing count, then a success toast;
  the selected channels' `ProgramStatus` (visible in the node face if configured, or the dock)
  reflects `Running` on the next telemetry tick.
- Clicking **Transfer** opens the existing `TransferDialog` unchanged, pre-populated with the
  selected circuits.
- Deselecting everything (click empty canvas) hides the action bar.

- [ ] **Step 9: Commit**

```bash
git add Components/UI/WorkflowCanvas/SelectionActionBar.razor \
  Components/Pages/Workflows/WorkflowCanvasPage.razor \
  wwwroot/css/workflow-canvas.css \
  BatteryTestingSystem.Tests/Components/SelectionActionBarTests.cs
git commit -m "feat(workflow-canvas): selection action bar - Start/Stop/Pause/Continue/Transfer

The payoff of Phase 3 (D9): a floating bar appears whenever channels are
selected, running Start/Stop/Pause/Continue through ChannelActionExecutor
and opening the existing, unmodified TransferDialog for Transfer.
Per-button enablement comes from WorkflowActionRules, already unit
tested; the bar itself makes no eligibility decisions.

Reuses Components/UI/Loading/Processing for progress, exactly as
DashboardView already does, rather than building a second progress UI.

Browser-verified end to end against the running simulator: correct
button enablement on an idle bench, Start succeeding with a climbing
progress count, Transfer opening the real dialog pre-populated with the
selection.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Phase 3 Completion Checklist

- [ ] `WorkflowSelectionLogic` and `WorkflowActionRules` fully unit tested, mirroring the dashboard's own rules
- [ ] `ChannelActionExecutor` proves same-device-sequential / cross-device-parallel with both a counter and a wall-clock assertion
- [ ] Marquee drag selects every intersecting channel node; a plain click still clears selection
- [ ] Panning works via middle-mouse-button and Space+drag; wheel-zoom unaffected
- [ ] Board/Device header click selects all child channels, reporting skips
- [ ] The action bar's button enablement matches `WorkflowActionRules` exactly on a live bench
- [ ] Start/Stop/Pause/Continue run through `ChannelActionExecutor` against real simulated hardware, with `Processing` showing live progress
- [ ] Transfer opens the existing, unmodified `TransferDialog`
- [ ] Full suite green (`DashboardRenderBatcherTests` flake excepted — confirm in isolation)
- [ ] `main` has no commits from this work; `DashboardView.razor`/`TransferDialog.razor` are byte-identical to before this phase

**Deferred:** the right-click context menu (see "Scope note" above) and the system-error reset
button (deferred since Phase 1, still an action rather than a display).
