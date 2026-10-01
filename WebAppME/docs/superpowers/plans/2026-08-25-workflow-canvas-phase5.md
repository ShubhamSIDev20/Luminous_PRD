# Workflow Canvas Phase 5 — Toolbar, Legend & Refresh Rate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a user-configurable telemetry refresh rate (D21), a status-color legend (D23), and replace today's top toolbar row with a consolidated top-right icon-button box that also gains clickable status-color filter chips (D23).

**Architecture:** All three land as additive changes around the existing `WorkflowNodeConfig`/`ServerSessionStorageService` persistence pattern and the existing JS-owns-interaction/C#-computes-values split. The refresh rate becomes a pure throttle check gating the existing telemetry push; the legend and toolbar are new small Razor components reusing `LayoutSwitcher`/`NodePropertyPicker` as-is inside popovers; the status filter is a JS-only class toggle keyed off the `statusName` string already written into the DOM every tick — no new C# data path needed for it.

**Tech Stack:** C# / .NET 8, Blazor Server, xUnit, vanilla JS/CSS (no new dependencies). Icons via the existing `Blazicon`/`Lucide` components already used app-wide (not previously used inside the canvas, whose own CSS is hand-written per ADR-2).

**Spec:** `docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md` (Sections 3, 6, 7, decisions D21/D23)

## Global Constraints

- Never modify `DashboardView.razor` or `TransferDialog.razor` (standing constraint since Phase 1).
- `CoalescingRunner` (shared with the Dashboard's own hardware-event coalescing, ADR-6) is not modified — the refresh-rate throttle is entirely local to `WorkflowCanvasPage`, a new gate in front of the existing `PushTelemetryAsync`, not a change to the shared runner class.
- Follow strict TDD for pure-logic pieces: write failing test → verify red → implement → verify green. UI-only pieces (popovers, icon wiring, JS class toggling) are browser-verified instead, matching this branch's established practice for every prior interaction feature.
- Full suite green, then live browser verification, then commit — one commit per task.
- Update the `.claude/` memory files before ending the session, per this repo's mandatory workflow.

---

### Task 1: User-configurable telemetry refresh rate (D21)

**Files:**
- Modify: `Models/DTOs/Workflow/WorkflowNodeConfig.cs`
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`
- Test: `BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `WorkflowNodeConfig.RefreshRateMs: int` (new field, default `250` — preserves today's
  behavior for every existing persisted config, since old blobs deserialize with the default);
  `WorkflowTelemetryBridge.ShouldThrottle(DateTime lastPushUtc, DateTime nowUtc, int refreshRateMs): bool`.

- [ ] **Step 1: Write the failing tests**

In `BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs`, add:

```csharp
    [Fact]
    public void Default_HasA250MillisecondRefreshRate()
    {
        Assert.Equal(250, WorkflowNodeConfig.Default.RefreshRateMs);
    }
```

In `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`, add a new section:

```csharp
    // ============================================================ ShouldThrottle

    [Fact]
    public void ShouldThrottle_NeverThrottlesTheFirstEverPush()
    {
        // default(DateTime) means "no push has happened yet" - the very first tick must always
        // go through, or a freshly opened canvas would show nothing until the window elapses.
        Assert.False(WorkflowTelemetryBridge.ShouldThrottle(
            lastPushUtc: default, nowUtc: DateTime.UtcNow, refreshRateMs: 250));
    }

    [Fact]
    public void ShouldThrottle_ReturnsTrueWithinTheWindow()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMilliseconds(100);

        Assert.True(WorkflowTelemetryBridge.ShouldThrottle(last, now, refreshRateMs: 250));
    }

    [Fact]
    public void ShouldThrottle_ReturnsFalseOnceTheWindowHasElapsed()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMilliseconds(300);

        Assert.False(WorkflowTelemetryBridge.ShouldThrottle(last, now, refreshRateMs: 250));
    }

    [Fact]
    public void ShouldThrottle_ExactlyAtTheWindowBoundaryIsNotThrottled()
    {
        var last = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var now = last.AddMilliseconds(250);

        Assert.False(WorkflowTelemetryBridge.ShouldThrottle(last, now, refreshRateMs: 250));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowNodeConfigTests|FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: `Default_HasA250MillisecondRefreshRate` fails to compile (no `RefreshRateMs` member);
the four `ShouldThrottle_*` tests fail to compile (no such method).

- [ ] **Step 3: Add the field and the pure helper**

In `Models/DTOs/Workflow/WorkflowNodeConfig.cs`, replace:

```csharp
public record WorkflowNodeConfig(IReadOnlyList<string> VisibleProperties)
```

with:

```csharp
public record WorkflowNodeConfig(IReadOnlyList<string> VisibleProperties, int RefreshRateMs = 250)
```

(The trailing default keeps every existing single-argument call site — including
`WorkflowNodeConfig.Default`'s own constructor call — compiling unchanged.)

In `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, add near `FlowFor`:

```csharp
    /// <summary>
    /// Whether a telemetry push arriving at <paramref name="nowUtc"/> should be skipped because
    /// the user's configured refresh rate has not elapsed since the last one. default(DateTime)
    /// for <paramref name="lastPushUtc"/> means "never pushed yet" and is never throttled - a
    /// freshly opened canvas must not wait out the window before showing anything.
    /// </summary>
    public static bool ShouldThrottle(DateTime lastPushUtc, DateTime nowUtc, int refreshRateMs) =>
        lastPushUtc != default && (nowUtc - lastPushUtc).TotalMilliseconds < refreshRateMs;
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowNodeConfigTests|FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: PASS.

- [ ] **Step 5: Wire the throttle into the page's telemetry push**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, add a field next to `_subscribedChannelIds`:

```csharp
    private DateTime _lastTelemetryPushAt;
```

At the top of `PushTelemetryAsync`, change:

```csharp
        if (!_jsReady || _subscribedChannelIds.Count == 0) return;
```

to:

```csharp
        if (!_jsReady || _subscribedChannelIds.Count == 0) return;

        var now = DateTime.UtcNow;
        if (WorkflowTelemetryBridge.ShouldThrottle(_lastTelemetryPushAt, now, _nodeConfig.RefreshRateMs))
            return;
        _lastTelemetryPushAt = now;
```

This does not touch `CoalescingRunner` at all — `OnHardwareChanged`'s `_telemetryRunner.Request(...)`
call is unchanged, so a burst of hardware events still collapses into one follow-up run exactly as
before (ADR-6); this new check only decides whether that run's *own* work actually pushes to JS or
returns immediately.

- [ ] **Step 6: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Models/DTOs/Workflow/WorkflowNodeConfig.cs Services/Implementations/Workflow/WorkflowTelemetryBridge.cs Components/Pages/Workflows/WorkflowCanvasPage.razor BatteryTestingSystem.Tests/Models/WorkflowNodeConfigTests.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): user-configurable telemetry refresh rate (D21)

WorkflowNodeConfig gains RefreshRateMs (default 250, matching today's
behavior for every existing persisted config). PushTelemetryAsync now
skips a push if the configured window has not elapsed since the last
one, via the new pure WorkflowTelemetryBridge.ShouldThrottle. The
shared CoalescingRunner (ADR-6, also used by the Dashboard) is
untouched - this throttle lives entirely in the canvas page, in front
of the runner's own work."
```

---

### Task 2: Status color legend (D23, spec Section 6)

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Create: `Components/UI/WorkflowCanvas/StatusLegend.razor`
- Modify: `wwwroot/css/workflow-canvas.css`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `WorkflowStatusCss.Name(CircuitStatus)` (existing).
- Produces: `WorkflowTelemetryBridge.HslFor(CircuitStatus status): string`; `StatusLegend` component
  with `[Parameter] public bool Open`.

- [ ] **Step 1: Write the failing test**

In `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`, add:

```csharp
    // ============================================================ HslFor

    [Theory]
    [InlineData(CircuitStatus.Idle, "0 0% 62%")]
    [InlineData(CircuitStatus.Charge, "51 100% 50%")]
    [InlineData(CircuitStatus.Discharging, "33 100% 50%")]
    [InlineData(CircuitStatus.Pause, "207 90% 54%")]
    [InlineData(CircuitStatus.Countinue, "122 39% 49%")]
    [InlineData(CircuitStatus.Interrupt, "37 75% 35%")]
    [InlineData(CircuitStatus.Error, "4 90% 58%")]
    [InlineData(CircuitStatus.Msg, "187 100% 42%")]
    [InlineData(CircuitStatus.Offline, "0 0% 62%")]
    public void HslFor_MatchesTheSamePaletteBuildEntriesUses(CircuitStatus status, string expected)
    {
        // A legend that drifts from the actual node-face colors would be worse than no legend -
        // it must read from the exact same source BuildEntries does, not a second copy.
        Assert.Equal(expected, WorkflowTelemetryBridge.HslFor(status));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~HslFor"
```

Expected: FAIL — `HslFor` does not exist.

- [ ] **Step 3: Implement `HslFor`**

In `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, add near `FlowFor`:

```csharp
    /// <summary>The exact colour BuildEntries assigns a node in this status — the legend and any
    /// status-filter chip must read this, never a second copy of the palette.</summary>
    public static string HslFor(CircuitStatus status)
    {
        var name = WorkflowStatusCss.Name(status);
        return StatusHslByName.TryGetValue(name, out var hsl) ? hsl : StatusHslByName["idle"];
    }
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~HslFor"
```

Expected: PASS (9 theory rows).

- [ ] **Step 5: Create the legend component**

Create `Components/UI/WorkflowCanvas/StatusLegend.razor`:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Models.Enums
@using BatteryTestingSystem.Services.Implementations.Workflow

@* Reads colours from WorkflowTelemetryBridge.HslFor - the exact source BuildEntries uses - so
   this can never show a swatch that does not match a real node's actual colour. *@

@if (Open)
{
    <div class="wf-legend">
        <div class="wf-dock__title">Status colors</div>
        @foreach (CircuitStatus status in Enum.GetValues(typeof(CircuitStatus)))
        {
            <div class="wf-legend__row">
                <span class="wf-legend__swatch" style="background: hsl(@(WorkflowTelemetryBridge.HslFor(status)));"></span>
                <span>@WorkflowStatusCss.Name(status)</span>
            </div>
        }
    </div>
}

@code {
    [Parameter] public bool Open { get; set; }
}
```

- [ ] **Step 6: Add the legend CSS**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- status legend */

.wf-legend {
    position: absolute;
    top: 3rem;
    right: 1rem;
    z-index: 20;
    width: 160px;
    padding: 0.75rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.5rem;
    background: hsl(var(--card));
    color: hsl(var(--card-foreground));
    box-shadow: 0 4px 16px hsl(0 0% 0% / 0.15);
}

.wf-legend__row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.8125rem;
    padding: 0.125rem 0;
    text-transform: capitalize;
}

.wf-legend__swatch {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    flex-shrink: 0;
}
```

- [ ] **Step 7: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS. `StatusLegend` is not wired into the page yet (Task 3 does that) — this task only
adds the component and its data source.

- [ ] **Step 8: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowTelemetryBridge.cs Components/UI/WorkflowCanvas/StatusLegend.razor wwwroot/css/workflow-canvas.css BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): status color legend (D23)

New WorkflowTelemetryBridge.HslFor exposes the exact palette
BuildEntries already uses for node-face colours, so the new
StatusLegend component (not yet wired into the page - Task 3 does
that) can never drift from what a node actually looks like."
```

---

### Task 3: Consolidated top-right toolbar box (D23, spec Section 7)

**Files:**
- Create: `Components/UI/WorkflowCanvas/CanvasToolbar.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Modify: `wwwroot/css/workflow-canvas.css`

**Interfaces:**
- Consumes: `LayoutSwitcher` (existing, unmodified), `StatusLegend` (Task 2).
- Produces: `CanvasToolbar` with `[Parameter] bool AnimationsOn`, `[Parameter] EventCallback OnToggleAnimations`,
  `[Parameter] EventCallback OnFit`, `[Parameter] EventCallback OnToggleNodeFields`,
  `[Parameter] IReadOnlyList<LayoutSummary> Layouts`, `[Parameter] long? CurrentLayoutId`,
  `[Parameter] bool IsDirty`, `[Parameter] EventCallback<long> OnLoadLayout`,
  `[Parameter] EventCallback OnSaveLayout`, `[Parameter] EventCallback<string> OnSaveLayoutAs`,
  `[Parameter] EventCallback<long> OnDeleteLayout`, `[Parameter] int RefreshRateMs`,
  `[Parameter] EventCallback<int> OnRefreshRateChanged`. No new public `workflowCanvas.*` JS API.

This task moves control markup only — it does not add the status-filter chips (Task 4) or change
any handler's actual behavior, only where its button lives.

- [ ] **Step 1: Create the toolbar component**

Create `Components/UI/WorkflowCanvas/CanvasToolbar.razor`:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Services.Interfaces
@using Blazicons

@* Replaces the old always-visible top toolbar row (D23) with a compact icon-button box, same
   visual language as the bottom SelectionActionBar. Node fields / Legend / Layouts each toggle
   their own existing popover; this component owns none of that popover state itself except which
   one (if any) is currently open, so only one can be open at a time. *@

<div class="wf-canvas-toolbar">
    <button class="wf-icon-btn" title="@(AnimationsOn ? "Turn animations off" : "Turn animations on")"
            @onclick="OnToggleAnimations">
        <Blazicon Svg="@(AnimationsOn ? Lucide.Zap : Lucide.ZapOff)" class="w-4 h-4" />
    </button>
    <button class="wf-icon-btn" title="Fit to content" @onclick="OnFit">
        <Blazicon Svg="Lucide.Maximize" class="w-4 h-4" />
    </button>
    <button class="wf-icon-btn" title="Node fields" @onclick="OnToggleNodeFields">
        <Blazicon Svg="Lucide.SlidersHorizontal" class="w-4 h-4" />
    </button>
    <button class="wf-icon-btn" title="Layouts" @onclick="() => _open = _open == Popover.Layouts ? Popover.None : Popover.Layouts">
        <Blazicon Svg="Lucide.Save" class="w-4 h-4" />
    </button>
    <button class="wf-icon-btn" title="Status legend" @onclick="() => _open = _open == Popover.Legend ? Popover.None : Popover.Legend">
        <Blazicon Svg="Lucide.Palette" class="w-4 h-4" />
    </button>
    <label class="wf-icon-btn wf-refresh-rate" title="Telemetry refresh rate (ms)">
        <Blazicon Svg="Lucide.Timer" class="w-4 h-4" />
        <input type="number" min="50" max="5000" step="50" value="@RefreshRateMs"
               @onchange="@(e => OnRefreshRateChanged.InvokeAsync(int.TryParse(e.Value?.ToString(), out var v) ? v : RefreshRateMs))" />
    </label>
</div>

@if (_open == Popover.Layouts)
{
    <div class="wf-toolbar-popover">
        <LayoutSwitcher Layouts="Layouts" CurrentId="CurrentLayoutId" IsDirty="IsDirty"
                        OnLoad="OnLoadLayout" OnSave="OnSaveLayout"
                        OnSaveAs="OnSaveLayoutAs" OnDelete="OnDeleteLayout" />
    </div>
}

<StatusLegend Open="_open == Popover.Legend" />

@code {
    private enum Popover { None, Layouts, Legend }
    private Popover _open;

    [Parameter] public bool AnimationsOn { get; set; }
    [Parameter] public EventCallback OnToggleAnimations { get; set; }
    [Parameter] public EventCallback OnFit { get; set; }
    [Parameter] public EventCallback OnToggleNodeFields { get; set; }
    [Parameter] public IReadOnlyList<LayoutSummary> Layouts { get; set; } = Array.Empty<LayoutSummary>();
    [Parameter] public long? CurrentLayoutId { get; set; }
    [Parameter] public bool IsDirty { get; set; }
    [Parameter] public EventCallback<long> OnLoadLayout { get; set; }
    [Parameter] public EventCallback OnSaveLayout { get; set; }
    [Parameter] public EventCallback<string> OnSaveLayoutAs { get; set; }
    [Parameter] public EventCallback<long> OnDeleteLayout { get; set; }
    [Parameter] public int RefreshRateMs { get; set; }
    [Parameter] public EventCallback<int> OnRefreshRateChanged { get; set; }
}
```

- [ ] **Step 2: Add the toolbar CSS**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- canvas toolbar */

.wf-canvas-toolbar {
    position: absolute;
    top: 1rem;
    right: 1rem;
    z-index: 21;
    display: flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.375rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.5rem;
    background: hsl(var(--card));
    color: hsl(var(--card-foreground));
    box-shadow: 0 4px 16px hsl(0 0% 0% / 0.15);
}

.wf-icon-btn {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.375rem;
    border: none;
    border-radius: 0.375rem;
    background: transparent;
    cursor: pointer;
    color: inherit;
}

.wf-icon-btn:hover { background: hsl(var(--accent)); }

.wf-refresh-rate input {
    width: 3.5rem;
    background: transparent;
    border: 1px solid hsl(var(--border));
    border-radius: 0.25rem;
    color: inherit;
    font-size: 0.75rem;
    padding: 0.125rem 0.25rem;
}

.wf-toolbar-popover {
    position: absolute;
    top: 3rem;
    right: 1rem;
    z-index: 20;
    padding: 0.75rem;
    border: 1px solid hsl(var(--border));
    border-radius: 0.5rem;
    background: hsl(var(--card));
    color: hsl(var(--card-foreground));
    box-shadow: 0 4px 16px hsl(0 0% 0% / 0.15);
}
```

- [ ] **Step 3: Replace the old toolbar row and wire the new one into the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, replace the entire `<div class="wf-toolbar">`
block:

```razor
    <div class="wf-toolbar">
        <span class="font-semibold">Workflows</span>
        <span class="opacity-60">@_layouts.Count saved layout(s)</span>
        <span class="opacity-60">@_topology.Devices.Count device(s) available</span>
        <button class="wf-btn" @onclick="() => _animations = !_animations">
            @(_animations ? "Animations on" : "Animations off")
        </button>
        <button class="wf-btn" @onclick="FitToContent">Fit</button>
        <button class="wf-btn" @onclick="() => _propertyPickerOpen = !_propertyPickerOpen">
            Node fields
        </button>
        <LayoutSwitcher Layouts="_layouts" CurrentId="_currentLayoutId" IsDirty="_dirty"
                        OnLoad="HandleLoad" OnSave="HandleSave"
                        OnSaveAs="HandleSaveAs" OnDelete="HandleDelete" />
    </div>
```

with:

```razor
    <div class="wf-toolbar">
        <span class="font-semibold">Workflows</span>
        <span class="opacity-60">@_layouts.Count saved layout(s)</span>
        <span class="opacity-60">@_topology.Devices.Count device(s) available</span>
    </div>
```

Then add `<CanvasToolbar>` inside `.wf-canvas-host`, alongside the existing `Minimap`/`NodePropertyPicker`
conditionals:

```razor
            <CanvasToolbar AnimationsOn="_animations"
                           OnToggleAnimations="() => _animations = !_animations"
                           OnFit="FitToContent"
                           OnToggleNodeFields="() => _propertyPickerOpen = !_propertyPickerOpen"
                           Layouts="_layouts" CurrentLayoutId="_currentLayoutId" IsDirty="_dirty"
                           OnLoadLayout="HandleLoad" OnSaveLayout="HandleSave"
                           OnSaveLayoutAs="HandleSaveAs" OnDeleteLayout="HandleDelete"
                           RefreshRateMs="_nodeConfig.RefreshRateMs"
                           OnRefreshRateChanged="@(ms => HandleNodeConfigChanged(_nodeConfig with { RefreshRateMs = ms }))" />
```

(`HandleNodeConfigChanged` already exists from Phase 2 and already persists via
`SessionStorage.SetComponentState` — reused here exactly as-is, no change to that method.)

- [ ] **Step 4: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Verify in the browser**

Stop the app, rebuild, restart, hard-reload. Confirm:
- The old top toolbar row now shows only the title/counts, no buttons.
- A new icon-button box sits top-right of the canvas with 6 controls: animations toggle, fit,
  node fields, layouts, legend, refresh rate.
- Clicking Layouts opens a popover with the exact same combobox/Save/Save as/Delete controls as
  before, and Save/Save as/Delete/Load all still work identically.
- Clicking Legend opens the Task-2 popover showing all 9 status colors, matching real node colors.
- Clicking Node fields still opens the existing property picker.
- Changing the refresh-rate number and reloading the page shows the new value persisted (same
  `ServerSessionStorageService` key as the property picker's config, since both live on
  `WorkflowNodeConfig`).

- [ ] **Step 6: Commit**

```bash
git add Components/UI/WorkflowCanvas/CanvasToolbar.razor Components/Pages/Workflows/WorkflowCanvasPage.razor wwwroot/css/workflow-canvas.css
git commit -m "feat(workflow-canvas): consolidated top-right toolbar box (D23)

Replaces the always-visible top toolbar row's buttons with a compact
icon-button box in the same visual language as the bottom
SelectionActionBar: animations toggle, fit, node fields, a layouts
popover (LayoutSwitcher reused as-is), the new status legend (Task 2),
and the new refresh-rate control (Task 1). No handler's behavior
changed, only where its control lives.

Live-verified: all six controls present and functional, Layouts
popover preserves exact prior Save/Save as/Delete/Load behavior,
refresh-rate value persists across reload via the existing
WorkflowNodeConfig storage key."
```

---

### Task 4: Status-color filter chips (D23, spec Section 7)

**Files:**
- Modify: `Components/UI/WorkflowCanvas/CanvasToolbar.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Modify: `wwwroot/js/workflow-canvas.js`
- Modify: `wwwroot/css/workflow-canvas.css`

**Interfaces:**
- Consumes: `WorkflowTelemetryBridge.HslFor` (Task 2), the existing `e.statusName` field already
  present on every `TelemetryEntry` sent to `applyTelemetry` each tick.
- Produces: `window.workflowCanvas.setStatusFilter(host, statusNameOrNull)` (new JS entry point,
  same call convention as `workflowCanvas.fitToContent`/`setViewport`).

This is pure JS/CSS + a page-level pass-through method — no new C# logic, matching every other
JS-only interaction feature on this branch (browser-verified, no unit test surface).

- [ ] **Step 1: Add the JS filter function and per-tick dimming**

In `wwwroot/js/workflow-canvas.js`, inside `applyTelemetry`'s per-entry loop, change:

```javascript
                node.classList.toggle("wf-node--active", e.flow !== 0);

                setText(node, "status", e.statusName);
```

to:

```javascript
                node.classList.toggle("wf-node--active", e.flow !== 0);
                node.classList.toggle(
                    "wf-node--dimmed", !!s.statusFilter && e.statusName !== s.statusFilter);

                setText(node, "status", e.statusName);
```

Add a new method to the `window.workflowCanvas` object, alongside `setViewport`:

```javascript
        setStatusFilter(host, statusName) {
            const s = stateOf(host);
            if (!s) return;
            s.statusFilter = statusName || null;

            // Re-scan every already-rendered channel node immediately, using the status text the
            // last telemetry tick already wrote - the next tick would eventually reapply this
            // anyway, but a filter click should not wait for the next hardware event to visibly
            // take effect.
            s.world.querySelectorAll('.wf-node[data-node-id^="chn-"]').forEach(node => {
                const status = node.querySelector('[data-role="status"]')?.textContent;
                node.classList.toggle(
                    "wf-node--dimmed", !!s.statusFilter && status !== s.statusFilter);
            });
        },
```

- [ ] **Step 2: Add the dimmed-state CSS**

Append to `wwwroot/css/workflow-canvas.css`:

```css
/* ---------------------------------------------------------------- status filter */

.wf-node--dimmed {
    opacity: 0.25;
}

.wf-status-chip {
    width: 14px;
    height: 14px;
    border-radius: 50%;
    border: 1px solid hsl(var(--border));
    cursor: pointer;
    padding: 0;
}

.wf-status-chip--active {
    outline: 2px solid hsl(var(--primary));
    outline-offset: 1px;
}
```

- [ ] **Step 3: Add the chips to the toolbar and wire the page**

In `Components/UI/WorkflowCanvas/CanvasToolbar.razor`, add a chip row inside `.wf-canvas-toolbar`,
after the refresh-rate control:

```razor
    <div class="wf-status-chip-row">
        @foreach (CircuitStatus status in Enum.GetValues(typeof(CircuitStatus)))
        {
            var name = WorkflowStatusCss.Name(status);
            <button class="wf-status-chip @(ActiveStatusFilter == name ? "wf-status-chip--active" : "")"
                    style="background: hsl(@(WorkflowTelemetryBridge.HslFor(status)));"
                    title="@name"
                    @onclick="@(() => OnStatusFilterChanged.InvokeAsync(ActiveStatusFilter == name ? null : name))">
            </button>
        }
    </div>
```

Add the `@using` for `WorkflowStatusCss`/`CircuitStatus` (already present via the existing
`@using BatteryTestingSystem.Services.Implementations.Workflow` — add
`@using BatteryTestingSystem.Models.Enums` if not already there) and the two new parameters:

```csharp
    [Parameter] public string? ActiveStatusFilter { get; set; }
    [Parameter] public EventCallback<string?> OnStatusFilterChanged { get; set; }
```

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, add a field and handler near
`_animations`:

```csharp
    private string? _statusFilter;

    private async Task SetStatusFilter(string? statusName)
    {
        _statusFilter = statusName;
        if (_jsReady) await JS.InvokeVoidAsync("workflowCanvas.setStatusFilter", _canvasHost, statusName);
    }
```

Wire the two new `<CanvasToolbar>` parameters:

```razor
                           ActiveStatusFilter="_statusFilter"
                           OnStatusFilterChanged="SetStatusFilter"
```

- [ ] **Step 4: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS — this task adds no new C#-testable logic, so the count should not change from
Task 3's run.

- [ ] **Step 5: Verify in the browser**

Stop the app, rebuild, restart, hard-reload. Place a full device with mixed channel statuses (or
use the simulator's natural spread). Confirm:
- Clicking a status chip visibly dims (25% opacity) every channel node whose current status does
  not match, while matching channels stay full-opacity.
- Clicking the same chip again clears the filter (all channels return to full opacity).
- Clicking a different chip switches the filter directly (no need to clear first).
- A channel's telemetry updating while a filter is active (e.g. going from Idle to Charge) applies
  or removes the dim on its very next tick, without needing to re-click the chip.
- The active chip shows the outline/active styling.

- [ ] **Step 6: Commit**

```bash
git add Components/UI/WorkflowCanvas/CanvasToolbar.razor Components/Pages/Workflows/WorkflowCanvasPage.razor wwwroot/js/workflow-canvas.js wwwroot/css/workflow-canvas.css
git commit -m "feat(workflow-canvas): status-color filter chips in the toolbar (D23)

Clicking a status swatch in the toolbar dims every channel node whose
current status does not match (workflowCanvas.setStatusFilter),
mirroring the Dashboard's own status-chip filter behavior. Pure JS -
the filter state and comparison both live client-side, keyed off the
statusName string every telemetry tick already writes into the DOM,
so a channel updating while filtered applies/clears the dim on its
very next tick with no additional C# data path.

Live-verified: click-to-filter, click-again-to-clear, switching
directly between filters, and a live status change re-applying the
filter on its next tick all confirmed against the simulator."
```

---

## Phase 5 Completion Checklist

- [ ] `WorkflowNodeConfig.RefreshRateMs` persists per-user via the existing config storage key; a freshly opened canvas is never throttled on its first tick
- [ ] `WorkflowTelemetryBridge.HslFor` is the single source of truth the legend and the filter chips both read — no second color copy
- [ ] Top toolbar row shows only static info text; all controls live in the new top-right icon box
- [ ] Layouts popover preserves Save/Save as/Delete/Load exactly as before
- [ ] Status filter dims non-matching channels live, clears on second click of the same chip, and reapplies correctly as new telemetry arrives
- [ ] Full suite green
- [ ] `main` has no commits from this work; `DashboardView.razor`/`TransferDialog.razor` remain byte-identical to `main`
