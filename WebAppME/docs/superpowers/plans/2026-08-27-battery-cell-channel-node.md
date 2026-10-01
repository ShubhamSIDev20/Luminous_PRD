# Battery-Cell Channel Node Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redraw the workflow canvas's channel node as a battery cell that carries its own live data — cap-mounted channel number, the dashboard card's section arrangement inside, and a segment-march fill animation driven by circuit status.

**Architecture:** The cell's frame is CSS (a cap element plus a bordered body plus an inner well), and all text sits in normal flex flow inside it, so content can never overflow the outline. The fill, surface line, segment separators and march are absolutely-positioned layers *behind* the content at `z-0`, driven entirely by two CSS custom properties (`--wf-soc`, `--wf-status`) that the existing JS telemetry bridge writes — no Blazor re-render per tick. State of charge does not exist in the schema and is derived from the battery's rated capacity by net coulomb count.

**Tech Stack:** .NET 8, Blazor Server, Razor components, Tailwind utility classes (pre-built — see Global Constraints), hand-written CSS in `wwwroot/css/workflow-canvas.css`, vanilla JS in `wwwroot/js/workflow-canvas.js`, xUnit + bUnit.

**Spec:** [`docs/superpowers/specs/2026-08-27-workflow-canvas-battery-cell-node-design.md`](../specs/2026-08-27-workflow-canvas-battery-cell-node-design.md)

## Global Constraints

- **Branch:** `feat/workflow-canvas-experiment`. **Never merge to `main`** without an explicit decision (T-47 D14/D15).
- **Never modify** `Components/UI/Dashboard/DeviceChannel.razor` or `Components/Pages/Home/DashboardView.razor` (T-47 D13). This work *borrows* their arrangement; it does not change them.
- **Stop the running app before building.** A running app locks the exe and the build fails with `MSB3027`. When checking build output, grep for `error` — **not** `error CS`, which reports `MSB3027` as clean.
- **No new Tailwind utility combinations.** `wwwroot/css/app.min.css` has no rebuild pipeline (ADR-2), so an unprecedented class silently does nothing. Any class not already used in this repo must instead be written into `wwwroot/css/workflow-canvas.css` by hand. This plan writes the cell's styling as hand-written CSS for exactly that reason.
- **Status CSS tokens come from `WorkflowStatusCss.Var(status)`**, never `status.ToString().ToLower()` — `CircuitStatus.Countinue` is misspelled in the enum while the variable is `--status-continue`.
- **Only `transform`, `opacity` and `stroke-dashoffset` may be animated.** Animating height/box-shadow repaints the whole node; at 64+ nodes that is the difference between smooth and unusable.
- **Invariant culture for every number written into CSS or markup.** A comma decimal separator makes a custom property unparseable and the fill silently sits at zero.
- **The cap must carry the `wf-node__header` class.** `wwwroot/js/workflow-canvas.js:373` starts a node drag only on `e.target.closest(".wf-node__header")`.
- **Class prefix is `.wf-bcell*`.** `.wf-cell`, `.wf-cell__fill` and `.wf-cell__shimmer` already belong to `BatteryGlyph.razor` (the Battery node).
- **Bump `?version=` on the canvas JS and CSS in `Components/App.razor`** whenever either is edited. It is hand-maintained; forgetting it ships the new markup against cached assets.
- **Test command:** `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`. Filter with `--filter "FullyQualifiedName~ClassName"`.
- **Baseline:** 597 tests passing before this plan starts.

## File Structure

| File | Responsibility |
|---|---|
| `Services/Implementations/Workflow/StateOfChargeEstimator.cs` | **new.** Pure SoC formula. Isolated so it can be replaced without touching rendering. |
| `Services/Implementations/Workflow/WorkflowNodeProperties.cs` | **modify.** Add `PropertySection` + `SectionsFor` — the single place that decides which section a key belongs to and whether it packs two per row. |
| `Models/DTOs/Workflow/TelemetryEntry.cs` | **modify.** `ChannelTelemetry` carries the battery's rated figures and a nullable SoC; `TelemetryEntry` carries the pre-formatted SoC text, a known/unknown flag, and the battery line. |
| `Components/Pages/Workflows/WorkflowCanvasPage.razor` | **modify.** Call the estimator instead of passing `0.0`; read the battery figures off `circuit.Battery`. |
| `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs` | **modify.** Format `SocText` and `BatteryLine`. |
| `Components/UI/WorkflowCanvas/CanvasNode.razor` | **modify.** Optional `HeaderContent` slot so a node kind can replace the default title row. |
| `Components/UI/WorkflowCanvas/Nodes/ChannelBatteryCell.razor` | **new.** The cell interior: fill layers + header + sections + battery line + footer. |
| `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` | **rewrite.** Composes the cap and the cell inside `CanvasNode`. |
| `wwwroot/css/workflow-canvas.css` | **modify.** `.wf-bcell*` block, march keyframes, rewritten LOD tiers. |
| `wwwroot/js/workflow-canvas.js` | **modify.** Write `--wf-soc` on the channel node, toggle march-direction classes, write the circuit-status word and SoC chip. |
| `Services/Implementations/Workflow/WorkflowAutoLayout.cs` | **modify.** `ChannelNodeHeight(IReadOnlyList<string>)` with measured constants. |
| `Services/Implementations/Workflow/WorkflowEdgeGeometry.cs` | **modify.** Thread the property list instead of a count. |
| `Services/Implementations/Workflow/MinimapProjection.cs` | **modify.** Same. |
| `Components/UI/WorkflowCanvas/EdgeLayer.razor`, `CanvasSurface.razor` | **modify.** Same. |

---

### Task 1: SoC estimator

The whole design renders empty without this. `WorkflowCanvasPage.razor:357` currently passes a hardcoded `0.0` for SoC, with a comment recording that no state-of-charge percentage exists anywhere in the schema.

**Files:**
- Create: `Services/Implementations/Workflow/StateOfChargeEstimator.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/StateOfChargeEstimatorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `StateOfChargeEstimator.Estimate(double chargeCapacity, double dischargeCapacity, float? nominalCapacity) → double?`. `null` means *unknown*, which is not the same as `0`.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Services/Workflow/StateOfChargeEstimatorTests.cs`:

```csharp
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// SoC is DERIVED - the hardware sends capacity in Ah, never a percentage. Net coulomb count
/// against the battery's rated capacity, chosen over an AccumulatedCapacity-only variant and
/// voltage interpolation (spec 6.5.1).
///
/// The null cases are the point of the type: a bench operator must be able to tell "we do not
/// know this cell's charge" from "this cell is flat".
/// </summary>
public class StateOfChargeEstimatorTests
{
    [Fact]
    public void HalfTheRatedCapacityMovedInIsHalfCharged()
    {
        Assert.Equal(0.5, StateOfChargeEstimator.Estimate(25, 0, 50));
    }

    [Fact]
    public void DischargeIsSubtractedFromCharge()
    {
        // 40 Ah in, 15 Ah back out, on a 50 Ah cell = 25/50.
        Assert.Equal(0.5, StateOfChargeEstimator.Estimate(40, 15, 50));
    }

    [Fact]
    public void ClampsAboveFull()
    {
        Assert.Equal(1.0, StateOfChargeEstimator.Estimate(80, 0, 50));
    }

    [Fact]
    public void ClampsBelowEmptyWhenMoreWasDrawnThanPutIn()
    {
        Assert.Equal(0.0, StateOfChargeEstimator.Estimate(10, 30, 50));
    }

    [Fact]
    public void NoBatteryMeansUnknownNotEmpty()
    {
        Assert.Null(StateOfChargeEstimator.Estimate(25, 0, null));
    }

    [Fact]
    public void ZeroRatedCapacityMeansUnknownNotEmpty()
    {
        // An unset NominalCapacity on a battery record would otherwise divide by zero and
        // render a confident, wrong 0%.
        Assert.Null(StateOfChargeEstimator.Estimate(25, 0, 0f));
    }

    [Fact]
    public void NegativeRatedCapacityMeansUnknown()
    {
        Assert.Null(StateOfChargeEstimator.Estimate(25, 0, -5f));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~StateOfChargeEstimatorTests"`
Expected: FAIL — build error, `StateOfChargeEstimator` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `Services/Implementations/Workflow/StateOfChargeEstimator.cs`:

```csharp
namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// State of charge, derived. No SoC percentage exists anywhere in this schema - RealTimeRecord
/// tracks capacity in Ah - so the canvas computed nothing and the battery fill rendered empty in
/// live mode until this existed.
///
/// Net coulomb count against the battery's rated capacity:
///
///     soc = clamp((ChargeCapacity - DischargeCapacity) / NominalCapacity, 0, 1)
///
/// Chosen because it uses charge the hardware actually measured moving, and because it is
/// direction-correct: it rises while charging and falls while discharging, which is what the
/// cell's march animation depicts.
///
/// KNOWN LIMITS, deliberately not papered over:
///  - It assumes the cell began the session near empty. If it started part-charged, the reading
///    is low by that offset. This measures charge moved THIS SESSION, not absolute SoC.
///  - It resets whenever the hardware clears its accumulators.
///
/// Returns null - not 0 - when there is no battery or its rated capacity is unusable. On a test
/// bench "unknown" and "flat" must not look the same, and a nominal capacity of 0 would otherwise
/// divide by zero into a confident wrong answer.
///
/// Pure and DI-free so it is testable without hardware, and isolated so the formula above can be
/// replaced without touching any rendering code - the limits make that likely.
/// </summary>
public static class StateOfChargeEstimator
{
    public static double? Estimate(double chargeCapacity, double dischargeCapacity, float? nominalCapacity)
    {
        if (nominalCapacity is not { } nominal || nominal <= 0) return null;

        return Math.Clamp((chargeCapacity - dischargeCapacity) / nominal, 0d, 1d);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~StateOfChargeEstimatorTests"`
Expected: PASS, 7 tests.

- [ ] **Step 5: Commit**

```bash
git add Services/Implementations/Workflow/StateOfChargeEstimator.cs BatteryTestingSystem.Tests/Services/Workflow/StateOfChargeEstimatorTests.cs
git commit -m "feat(workflow-canvas): derive state of charge by net coulomb count"
```

---

### Task 2: Carry SoC and the battery figures through telemetry

**Files:**
- Modify: `Models/DTOs/Workflow/TelemetryEntry.cs` (`ChannelTelemetry` record, `TelemetryEntry` record)
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs:129`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor:356-363`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `StateOfChargeEstimator.Estimate` from Task 1.
- Produces:
  - `ChannelTelemetry.Soc` is now `double?`; new trailing params `float? NominalCapacity = null, float? NominalVoltage = null, int? NumberOfCells = null, string? BatteryTypeName = null`.
  - `TelemetryEntry` gains, immediately after `Soc`: `bool SocKnown`, `string SocText`, `string? BatteryLine`. `TelemetryEntry.Soc` stays `double` and is `0` when unknown, so the CSS variable always parses.

- [ ] **Step 1: Write the failing test**

Append to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs` (inside the existing class):

```csharp
    [Fact]
    public void UnknownSocIsFlaggedAndTextedAsPlaceholderWithAZeroFill()
    {
        var graph = GraphWithOneChannel(channelId: 1);
        var data = new Dictionary<long, ChannelTelemetry>
        {
            // No battery attached: Soc null, no nominal figures.
            [1] = new(CircuitStatus.Charge, null, Voltage: 3.6, Current: 12.4),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.False(entry.SocKnown);
        Assert.Equal("--%", entry.SocText);
        Assert.Equal(0d, entry.Soc);      // the CSS var must still parse
        Assert.Null(entry.BatteryLine);
    }

    [Fact]
    public void KnownSocIsRoundedToWholePercent()
    {
        var graph = GraphWithOneChannel(channelId: 1);
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [1] = new(CircuitStatus.Charge, 0.624, Voltage: 3.6, Current: 12.4),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.True(entry.SocKnown);
        Assert.Equal("62%", entry.SocText);
        Assert.Equal(0.624, entry.Soc);
    }

    [Fact]
    public void BatteryLineJoinsOnlyTheFiguresThatArePresent()
    {
        var graph = GraphWithOneChannel(channelId: 1);
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [1] = new(CircuitStatus.Charge, 0.5, Voltage: 3.6, Current: 12.4,
                      NominalCapacity: 50f, NominalVoltage: 3.7f, NumberOfCells: 1,
                      BatteryTypeName: "LFP"),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("LFP · 50 Ah · 3.7 V · 1 cell", entry.BatteryLine);
    }

    [Fact]
    public void BatteryLinePluralisesCellsAndOmitsMissingFigures()
    {
        var graph = GraphWithOneChannel(channelId: 1);
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [1] = new(CircuitStatus.Charge, 0.5, Voltage: 3.6, Current: 12.4,
                      NominalCapacity: 100f, NumberOfCells: 4),
        };

        var entry = WorkflowTelemetryBridge.BuildEntries(graph, data)[0];

        Assert.Equal("100 Ah · 4 cells", entry.BatteryLine);
    }
```

If the test class has no `GraphWithOneChannel` helper, add this private helper to it, mirroring the existing tests' graph construction:

```csharp
    private static WorkflowGraph GraphWithOneChannel(long channelId) =>
        new(new List<WorkflowNode>
            {
                new($"chn-{channelId}", NodeKind.Channel, channelId, 0, 0, null),
            },
            new List<WorkflowEdge>());
```

> Before writing the helper, open `WorkflowTelemetryBridgeTests.cs` and reuse whatever graph-building helper it already has — the existing test at line 252 (`BuildEntries(graph, data)[0].Soc`) already builds one. Duplicating it is a plan failure; reuse it.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"`
Expected: FAIL — build errors: `ChannelTelemetry` has no `NominalCapacity`; `TelemetryEntry` has no `SocKnown`/`SocText`/`BatteryLine`; `null` is not valid for `double Soc`.

- [ ] **Step 3: Widen `ChannelTelemetry`**

In `Models/DTOs/Workflow/TelemetryEntry.cs`, change the `Soc` parameter and append the battery figures:

```csharp
public record ChannelTelemetry(
    CircuitStatus Status,

    /// <summary>Null means UNKNOWN, not empty - see StateOfChargeEstimator. Nothing in the
    /// hardware protocol carries a percentage, so this is always a derived value.</summary>
    double? Soc,

    double Voltage,
    double Current,
    // ... every existing parameter unchanged ...
    string? ProgramName = null,
    string? SessionId = null,

    // The attached battery's rated figures. Static for the session, so they ride along here and
    // are written to the DOM once rather than recomputed per tick. They feed BOTH the cell's
    // battery line and the SoC derivation.
    float? NominalCapacity = null,
    float? NominalVoltage = null,
    int? NumberOfCells = null,
    string? BatteryTypeName = null);
```

- [ ] **Step 4: Widen `TelemetryEntry`**

In the same file, insert three parameters immediately after `Soc`:

```csharp
public record TelemetryEntry(
    string NodeId,
    string StatusHsl,
    string StatusName,

    /// <summary>0 when unknown, so hsl()/calc() in the stylesheet always parse. Check
    /// SocKnown before believing it.</summary>
    double Soc,

    /// <summary>False when there is no battery or no usable rated capacity. The cell renders an
    /// explicit unknown state rather than a misleading empty one.</summary>
    bool SocKnown,

    /// <summary>Pre-formatted, e.g. "62%" or "--%". Formatted here because the JS layer is a
    /// dumb text-setter by design.</summary>
    string SocText,

    /// <summary>e.g. "LFP · 50 Ah · 3.7 V · 1 cell", or null when nothing is known.</summary>
    string? BatteryLine,

    int Flow,
    // ... every existing parameter unchanged ...
    IReadOnlyDictionary<string, string> Properties);
```

- [ ] **Step 5: Format both in the bridge**

In `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`, replace the single `Soc:` line (currently `Soc: Math.Clamp(reading.Soc, 0, 1),`) with:

```csharp
                Soc: reading.Soc is { } soc ? Math.Clamp(soc, 0, 1) : 0d,
                SocKnown: reading.Soc is not null,
                SocText: reading.Soc is { } pct
                    ? Math.Round(Math.Clamp(pct, 0, 1) * 100).ToString("0", CultureInfo.InvariantCulture) + "%"
                    : "--%",
                BatteryLine: FormatBatteryLine(reading),
```

Add the formatter as a private static method on the same class:

```csharp
    /// <summary>
    /// The attached battery's rated figures as one compact line, e.g.
    /// "LFP · 50 Ah · 3.7 V · 1 cell". Each part is dropped when unknown rather than shown as a
    /// zero, and the whole line is null when nothing is known - a bench user reading "0 Ah"
    /// would reasonably conclude the record was wrong rather than absent.
    ///
    /// Invariant culture throughout: a comma decimal separator here would read as a list
    /// separator inside a line that is itself separator-delimited.
    /// </summary>
    private static string? FormatBatteryLine(ChannelTelemetry reading)
    {
        var parts = new List<string>(4);

        if (!string.IsNullOrWhiteSpace(reading.BatteryTypeName))
            parts.Add(reading.BatteryTypeName!.Trim());

        if (reading.NominalCapacity is { } ah && ah > 0)
            parts.Add(ah.ToString("0.##", CultureInfo.InvariantCulture) + " Ah");

        if (reading.NominalVoltage is { } v && v > 0)
            parts.Add(v.ToString("0.##", CultureInfo.InvariantCulture) + " V");

        if (reading.NumberOfCells is { } cells && cells > 0)
            parts.Add(cells + (cells == 1 ? " cell" : " cells"));

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
```

Ensure `using System.Globalization;` is present at the top of the file.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"`
Expected: PASS. The pre-existing `Assert.Equal(1.0, ...[0].Soc)` test at line 252 must still pass — if it now fails, its fixture is passing `1.0` into `double? Soc`, which is valid; investigate rather than editing the assertion.

- [ ] **Step 7: Feed real values from the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, delete the four-line comment beginning "No state-of-charge percentage exists anywhere in this schema" — it is now false — and replace the first two positional arguments plus append the battery figures:

```csharp
            readings[channelId] = new ChannelTelemetry(
                record.CircuitStatus,

                // SoC is derived: nothing in the protocol carries a percentage. Null when no
                // battery is attached or its rated capacity is unusable, which the cell renders
                // as an explicit unknown rather than as empty.
                StateOfChargeEstimator.Estimate(
                    record.ChargeCapacity, record.DischargeCapacity, circuit.Battery?.NominalCapacity),

                record.Voltage, record.Current,
                // ... every existing named argument unchanged, through SessionId ...
                SessionId: circuit.Session?.SessionID.ToString(),

                NominalCapacity: circuit.Battery?.NominalCapacity,
                NominalVoltage: circuit.Battery?.NominalVoltage,
                NumberOfCells: circuit.Battery?.NumberOfCells,
                BatteryTypeName: circuit.Battery?.BatteryType?.Name);
```

Two things to verify while editing, both cheap and both silent if wrong:

1. `circuit.Battery` is already used in this loop for `BatteryName: circuit.Battery?.Name`, so no new query is needed.
2. **Check whether `circuit.Battery.BatteryType` is actually loaded.** If the navigation property is not `Include`d it will be `null` at runtime and the type name silently disappears from every battery line. If it is not loaded, use `circuit.Battery?.BatteryTypeId` to look the name up from whatever type list the page already holds, or drop the type from the line — do **not** add an `Include` to a hot telemetry path.

- [ ] **Step 8: Build and run the full suite**

Stop the running app first. Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: PASS, 601 tests (597 + 4 new bridge tests). Task 1's 7 are already counted in the 604 total by the end of Task 2 — confirm the number only rises.

- [ ] **Step 9: Commit**

```bash
git add Models/DTOs/Workflow/TelemetryEntry.cs Services/Implementations/Workflow/WorkflowTelemetryBridge.cs Components/Pages/Workflows/WorkflowCanvasPage.razor BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): push derived SoC and battery ratings to the canvas"
```

---

### Task 3: Section grouping

The single place that decides which section a property belongs to and whether that section packs two values per row. Both the component (Task 5) and the height formula (Task 8) read it, so they cannot disagree.

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowNodeProperties.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs`

**Interfaces:**
- Consumes: `CardPreviewData.RealTimeProperties` / `ConfigProperties` / `ProgramProperties`.
- Produces:
  - `public sealed record PropertySection(string Title, IReadOnlyList<string> Keys, bool TwoPerRow)`
  - `WorkflowNodeProperties.SectionsFor(IReadOnlyList<string> selected) → IReadOnlyList<PropertySection>` — empty sections omitted, catalog order preserved.

- [ ] **Step 1: Write the failing test**

Append to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs`:

```csharp
    [Fact]
    public void SectionsFollowTheCatalogueOrderAndCarryTheirRowShape()
    {
        var sections = WorkflowNodeProperties.SectionsFor(
            new[] { "CycleNumber", "Voltage", "BatteryID", "Current" });

        Assert.Collection(sections,
            s =>
            {
                Assert.Equal("Primary Data", s.Title);
                Assert.True(s.TwoPerRow);
                Assert.Equal(new[] { "Current", "Voltage" }, s.Keys);   // catalogue order, not input order
            },
            s =>
            {
                Assert.Equal("Configuration", s.Title);
                Assert.False(s.TwoPerRow);
                Assert.Equal(new[] { "BatteryID" }, s.Keys);
            },
            s =>
            {
                Assert.Equal("Program Data", s.Title);
                Assert.False(s.TwoPerRow);
                Assert.Equal(new[] { "CycleNumber" }, s.Keys);
            });
    }

    [Fact]
    public void EmptySectionsAreOmittedEntirely()
    {
        var sections = WorkflowNodeProperties.SectionsFor(new[] { "Voltage" });

        Assert.Single(sections);
        Assert.Equal("Primary Data", sections[0].Title);
    }

    [Fact]
    public void AnEmptySelectionProducesNoSections()
    {
        Assert.Empty(WorkflowNodeProperties.SectionsFor(Array.Empty<string>()));
    }

    [Fact]
    public void UnknownKeysAreDroppedRatherThanCrashing()
    {
        // A stale saved WorkflowNodeConfig can hold a key that no longer exists in the catalogue.
        var sections = WorkflowNodeProperties.SectionsFor(new[] { "Voltage", "NoSuchKey" });

        Assert.Single(sections);
        Assert.Equal(new[] { "Voltage" }, sections[0].Keys);
    }

    [Fact]
    public void EverySectionsKeysAreASubsetOfTheOfferedKeys()
    {
        var sections = WorkflowNodeProperties.SectionsFor(WorkflowNodeProperties.AvailableKeys);

        Assert.Equal(3, sections.Count);
        Assert.Equal(
            WorkflowNodeProperties.AvailableKeys.Count,
            sections.Sum(s => s.Keys.Count));
    }
```

Add `using System;`, `using System.Linq;` at the top of the file if not already present.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowNodePropertiesTests"`
Expected: FAIL — `SectionsFor` does not exist.

- [ ] **Step 3: Write minimal implementation**

Add to `Services/Implementations/Workflow/WorkflowNodeProperties.cs`, above the closing brace:

```csharp
    /// <summary>
    /// One rendered group on a channel cell. TwoPerRow is a LAYOUT fact, not a presentation
    /// preference: the height formula multiplies by it, so the component and
    /// WorkflowAutoLayout.ChannelNodeHeight must read it from here rather than each deciding
    /// for itself.
    /// </summary>
    public sealed record PropertySection(string Title, IReadOnlyList<string> Keys, bool TwoPerRow);

    /// <summary>
    /// The selected keys grouped into the dashboard card's three sections, in the catalogue's own
    /// order, with empty sections omitted.
    ///
    /// Section membership is read from CardPreviewData's three dictionaries rather than restated,
    /// for the same reason AvailableKeys concatenates them: a key added to the dashboard's
    /// catalogue must not be able to land in the wrong section here, or in none.
    ///
    /// Primary Data packs two per row because its twelve realtime measurements are
    /// self-identifying through their unit token (Ah vs AhCha vs AhDch vs AhStep) and labelling
    /// them one-per-row would roughly double the cell's height. Configuration and Program Data
    /// values carry no unit and are meaningless without a name, so they get labelled rows.
    ///
    /// Keys not present in any catalogue are dropped: a WorkflowNodeConfig saved before a
    /// catalogue change can still hold one.
    /// </summary>
    public static IReadOnlyList<PropertySection> SectionsFor(IReadOnlyList<string> selected)
    {
        var chosen = new HashSet<string>(selected);
        var sections = new List<PropertySection>(3);

        void Add(string title, IEnumerable<string> catalogueKeys, bool twoPerRow)
        {
            var keys = catalogueKeys.Where(chosen.Contains).ToList();
            if (keys.Count > 0) sections.Add(new PropertySection(title, keys, twoPerRow));
        }

        Add("Primary Data", CardPreviewData.RealTimeProperties.Keys, twoPerRow: true);
        Add("Configuration", CardPreviewData.ConfigProperties.Keys, twoPerRow: false);
        Add("Program Data", CardPreviewData.ProgramProperties.Keys, twoPerRow: false);

        return sections;
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowNodePropertiesTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowNodeProperties.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodePropertiesTests.cs
git commit -m "feat(workflow-canvas): group node properties into the card's three sections"
```

---

### Task 4: `CanvasNode` header slot

The cap replaces the default title row. Every other node kind must render byte-identically afterwards.

**Files:**
- Modify: `Components/UI/WorkflowCanvas/CanvasNode.razor`
- Test: `BatteryTestingSystem.Tests/Components/CanvasNodeTests.cs` (create if absent)

**Interfaces:**
- Produces: `CanvasNode` parameter `[Parameter] public RenderFragment? HeaderContent { get; set; }`. When null, the existing default header renders unchanged.

- [ ] **Step 1: Write the failing test**

Create or append to `BatteryTestingSystem.Tests/Components/CanvasNodeTests.cs`:

```csharp
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

public class CanvasNodeHeaderSlotTests : TestContext
{
    private static WorkflowNode Node() => new("chn-1", NodeKind.Channel, 1, 0, 0, null);

    [Fact]
    public void WithoutACustomHeaderTheTitleStillRenders()
    {
        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "1-1-1"));

        Assert.Contains("1-1-1", cut.Find(".wf-node__header").TextContent);
    }

    [Fact]
    public void ACustomHeaderReplacesTheTitleRowButKeepsTheDragHandleClass()
    {
        // wf-node__header is load-bearing: workflow-canvas.js starts a node drag only on
        // e.target.closest(".wf-node__header"). A custom header that drops the class makes the
        // node undraggable, and no other test can see that.
        var cut = RenderComponent<CanvasNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "1-1-1")
            .Add(x => x.HeaderContent, b => b.AddMarkupContent(0, "<span>CAP</span>")));

        var header = cut.Find(".wf-node__header");
        Assert.Contains("CAP", header.TextContent);
        Assert.DoesNotContain("1-1-1", header.TextContent);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~CanvasNodeHeaderSlotTests"`
Expected: FAIL — `CanvasNode` has no `HeaderContent`.

- [ ] **Step 3: Write minimal implementation**

In `Components/UI/WorkflowCanvas/CanvasNode.razor`, replace the header block:

```razor
    <div class="wf-node__header" @onclick="() => OnHeaderClick.InvokeAsync(Node.Id)" @onclick:stopPropagation="true">
        @if (HeaderContent is not null)
        {
            @* A node kind supplying its own header (the channel cell's cap) still renders INSIDE
               .wf-node__header, never instead of it: workflow-canvas.js gates node dragging on
               e.target.closest(".wf-node__header"), so moving the cap outside this element makes
               the node undraggable with no error anywhere. *@
            @HeaderContent
        }
        else
        {
            @if (IsStale)
            {
                <span title="This hardware no longer exists in the database">&#9888;</span>
            }
            <span class="truncate">@Title</span>
        }
    </div>
```

And add the parameter to the `@code` block:

```csharp
    /// <summary>Replaces the default title row. Rendered inside .wf-node__header so the drag
    /// gesture keeps working — see the comment at the call site.</summary>
    [Parameter] public RenderFragment? HeaderContent { get; set; }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~Components"`
Expected: PASS, including the existing Device/Battery/Channel node tests — none pass `HeaderContent`, so all must render exactly as before.

- [ ] **Step 5: Commit**

```bash
git add Components/UI/WorkflowCanvas/CanvasNode.razor BatteryTestingSystem.Tests/Components/CanvasNodeTests.cs
git commit -m "feat(workflow-canvas): let a node kind supply its own header content"
```

---

### Task 5: The cell component

**Files:**
- Create: `Components/UI/WorkflowCanvas/Nodes/ChannelBatteryCell.razor`
- Rewrite: `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`
- Test: `BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs` (rewrite)

**Interfaces:**
- Consumes: `WorkflowNodeProperties.SectionsFor` (Task 3), `CanvasNode.HeaderContent` (Task 4), `WorkflowNodeProperties.LabelWithoutUnit`, `WorkflowNodeProperties.UnitFor`.
- Produces: DOM contract the JS bridge (Task 7) writes into —
  `[data-role="circuit-status"]`, `[data-role="soc"]`, `[data-role="soc-fill"]`, `[data-role="battery-line"]`, `[data-role="program-status"]`, `[data-role="last-update"]`, `[data-role="prop-{key}"]`, and the root `.wf-bcell` element carrying `--wf-soc`.

- [ ] **Step 1: Write the failing tests**

Replace the body of `BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.UI.WorkflowCanvas;
using BatteryTestingSystem.Models.DTOs.Workflow;
using Bunit;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// The channel node is a battery cell. Its interior mirrors the dashboard card's RenderNormal
/// arrangement: Primary Data as two unlabelled columns (the unit token identifies each value),
/// Configuration and Program Data as labelled rows.
///
/// Every live value is written by workflow-canvas.js into a data-role slot, never by a Blazor
/// re-render, so these tests assert the SLOTS exist and start as placeholders.
/// </summary>
public class ChannelNodeTests : TestContext
{
    private static WorkflowNode Node() => new("chn-1", NodeKind.Channel, 1, 0, 0, null);

    private IRenderedComponent<ChannelNode> Render(params string[] properties) =>
        RenderComponent<ChannelNode>(p => p
            .Add(x => x.Node, Node())
            .Add(x => x.Title, "1-1-1")
            .Add(x => x.SelectedProperties, properties.ToList()));

    [Fact]
    public void TheChannelNumberIsStampedOnTheCapInsideTheDragHandle()
    {
        var cut = Render("Voltage");

        var header = cut.Find(".wf-node__header");
        Assert.Contains("1-1-1", header.TextContent);
        Assert.NotNull(header.QuerySelector(".wf-bcap"));
    }

    [Fact]
    public void FixedSlotsArePresentRegardlessOfSelection()
    {
        var cut = Render();

        Assert.NotNull(cut.Find("[data-role='circuit-status']"));
        Assert.NotNull(cut.Find("[data-role='soc']"));
        Assert.NotNull(cut.Find("[data-role='soc-fill']"));
        Assert.NotNull(cut.Find("[data-role='battery-line']"));
        Assert.NotNull(cut.Find("[data-role='program-status']"));
        Assert.NotNull(cut.Find("[data-role='last-update']"));
    }

    [Fact]
    public void TheBatteryLineStripIsReservedEvenWithNoBattery()
    {
        // Height must not depend on whether a battery happens to be attached: the layout computes
        // ChannelNodeHeight from the property list alone, before any telemetry exists.
        var cut = Render();

        Assert.NotNull(cut.Find("[data-role='battery-line']"));
    }

    [Fact]
    public void EverySelectedKeyGetsASlotInItsOwnSection()
    {
        var cut = Render("Voltage", "BatteryID", "CycleNumber");

        var primary = cut.Find(".wf-bcell__grid2");
        Assert.NotNull(primary.QuerySelector("[data-role='prop-Voltage']"));

        var labelled = cut.FindAll(".wf-bcell__row");
        Assert.Contains(labelled, r => r.QuerySelector("[data-role='prop-BatteryID']") is not null);
        Assert.Contains(labelled, r => r.QuerySelector("[data-role='prop-CycleNumber']") is not null);
    }

    [Fact]
    public void PrimaryDataValuesCarryNoVisibleLabelButAlternateAlignment()
    {
        var cut = Render("Voltage", "Current");

        var cells = cut.FindAll(".wf-bcell__grid2 > *");
        Assert.Equal(2, cells.Count);
        Assert.Contains("wf-bcell__v--left", cells[0].ClassList);
        Assert.Contains("wf-bcell__v--right", cells[1].ClassList);

        // No label text - only the value placeholder and its unit token.
        Assert.DoesNotContain("Voltage", cut.Find(".wf-bcell__grid2").TextContent);
    }

    [Fact]
    public void ConfigurationAndProgramRowsShowAVisibleLabelWithoutItsParenthesisedUnit()
    {
        var cut = Render("BatteryID", "StepRunningTime");

        var text = string.Join("|", cut.FindAll(".wf-bcell__row").Select(r => r.TextContent));
        Assert.Contains("Battery ID", text);
        Assert.Contains("Step Time", text);
    }

    [Fact]
    public void SectionHeadingsOnlyAppearForSectionsThatHaveKeys()
    {
        var cut = Render("Voltage");

        var headings = cut.FindAll(".wf-bcell__sec").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(new[] { "Primary Data" }, headings);
    }

    [Fact]
    public void AnEmptySelectionRendersNoSectionsAndNoPropertySlots()
    {
        var cut = Render();

        Assert.Empty(cut.FindAll(".wf-bcell__sec"));
        Assert.Empty(cut.FindAll("[data-role^='prop-']"));
    }

    [Fact]
    public void PropertySlotsStartAsPlaceholders()
    {
        var cut = Render("Voltage");

        Assert.Equal("--", cut.Find("[data-role='prop-Voltage']").TextContent);
    }

    [Fact]
    public void TheUnitTokenIsTheHardwaresOwnNotTheCataloguesFlattenedOne()
    {
        // The catalogue labels all four capacities "(Ah)"; the hardware distinguishes them.
        var cut = Render("ChargeCapacity");

        Assert.Contains("AhCha", cut.Find(".wf-bcell__grid2").TextContent);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~ChannelNodeTests"`
Expected: FAIL — no `.wf-bcap`, no `.wf-bcell__grid2`, no `circuit-status` slot.

- [ ] **Step 3: Create the cell component**

Create `Components/UI/WorkflowCanvas/Nodes/ChannelBatteryCell.razor`:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Services.Implementations.Workflow

@* The cell interior. Structure is the load-bearing decision here (spec D10): the frame is a
   bordered body with an inner well, and EVERY text block is an ordinary flex child inside
   padding. The fill/march/segment layers are the only absolutely-positioned elements and they
   sit BEHIND the content at z-0. Consequence: adding fields can only make the cell taller, it
   can never push text outside the outline. An earlier design positioned each block at an
   absolute offset inside a fixed SVG viewBox and text escaped as soon as the field list grew.

   Live values are never Blazor-bound. Each is a data-role slot that workflow-canvas.js writes
   into, so a telemetry tick costs zero re-renders. *@

<div class="wf-bcell" data-role="cell">
    <div class="wf-bcell__body">
        <div class="wf-bcell__well">
            @* --wf-soc drives all three geometry layers as a percentage, so they track the real
               charge at ANY cell height - which changes with the property selection. *@
            <div class="wf-bcell__fill" data-role="soc-fill"></div>
            <div class="wf-bcell__surface"></div>
            <div class="wf-bcell__march"></div>
            <div class="wf-bcell__segs"></div>

            <div class="wf-bcell__content">
                <div class="wf-bcell__hd">
                    <span class="wf-bcell__tcp" data-role="tcp" title="TCP status"></span>
                    <span class="wf-bcell__cs" data-role="circuit-status">--</span>
                    <span class="wf-bcell__soc">
                        <span data-role="soc">--%</span>
                        <span class="wf-bcell__socicon" aria-hidden="true"></span>
                    </span>
                </div>

                @foreach (var section in WorkflowNodeProperties.SectionsFor(SelectedProperties))
                {
                    <div class="wf-bcell__sec">@section.Title</div>

                    @if (section.TwoPerRow)
                    {
                        <div class="wf-bcell__grid2">
                            @for (var i = 0; i < section.Keys.Count; i++)
                            {
                                var key = section.Keys[i];
                                var unit = WorkflowNodeProperties.UnitFor(key);
                                <span class="wf-bcell__v @(i % 2 == 0 ? "wf-bcell__v--left" : "wf-bcell__v--right")"
                                      title="@WorkflowNodeProperties.LabelWithoutUnit(key)">
                                    <span data-role="prop-@key">--</span>@if (unit.Length > 0)
                                    {<small>@unit</small>}
                                </span>
                            }
                        </div>
                    }
                    else
                    {
                        <div class="wf-bcell__rows">
                            @foreach (var key in section.Keys)
                            {
                                <div class="wf-bcell__row">
                                    <span class="wf-bcell__k">@WorkflowNodeProperties.LabelWithoutUnit(key)</span>
                                    <span class="wf-bcell__v" data-role="prop-@key">--</span>
                                </div>
                            }
                        </div>
                    }
                }

                @* Always reserved, even with no battery: cell height must not depend on whether one
                   is attached, because ChannelNodeHeight is computed from the property list alone,
                   before any telemetry exists. A conditional strip would desynchronise every edge
                   attachment on a board where only some channels have batteries. *@
                <div class="wf-bcell__spec" data-role="battery-line"></div>

                <div class="wf-bcell__ft">
                    <span data-role="last-update">--</span>
                    <span data-role="program-status">--</span>
                </div>
            </div>
        </div>
    </div>
</div>

@code {
    [Parameter] public IReadOnlyList<string> SelectedProperties { get; set; } =
        WorkflowNodeProperties.DefaultVisibleProperties;
}
```

- [ ] **Step 4: Rewrite `ChannelNode.razor`**

Replace `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` entirely:

```razor
@namespace BatteryTestingSystem.Components.UI.WorkflowCanvas
@using BatteryTestingSystem.Models.DTOs.Workflow
@using BatteryTestingSystem.Services.Implementations.Workflow

@* A channel is drawn as a battery cell: the channel number is stamped on the cap, the live data
   fills the body, and the fill animates by circuit status. The cap goes through CanvasNode's
   HeaderContent slot rather than replacing the header element, because
   workflow-canvas.js gates node dragging on .wf-node__header. *@

<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale">
    <HeaderContent>
        <span class="wf-bcap">
            @if (IsStale)
            {
                <span title="This hardware no longer exists in the database">&#9888;</span>
            }
            <span class="truncate">@Title</span>
        </span>
    </HeaderContent>
    <ChildContent>
        <ChannelBatteryCell SelectedProperties="@SelectedProperties" />

        @if (ProgramName is not null || DbcName is not null)
        {
            <div class="wf-bcell__badges">
                @if (ProgramName is not null)
                {
                    <span class="wf-badge" title="Attached program">@ProgramName</span>
                }
                @if (DbcName is not null)
                {
                    <span class="wf-badge" title="Attached DBC file">@DbcName</span>
                }
            </div>
        }
    </ChildContent>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Channel";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public string? ProgramName { get; set; }
    [Parameter] public string? DbcName { get; set; }
    [Parameter] public IReadOnlyList<string> SelectedProperties { get; set; } =
        WorkflowNodeProperties.DefaultVisibleProperties;
}
```

> **Note:** the old `InitialStatusText` parameter is gone — the status word is now a JS-written slot. Grep for `InitialStatusText` and remove the argument from `CanvasSurface.razor` if it is passed there.

> **Note:** the program/DBC badges are *conditional*, which by the reasoning above would make height non-deterministic. They are rendered **outside** `.wf-bcell` on purpose. Task 8 must therefore either include a reserved badge strip in the height formula or confirm during measurement that the badges sit within already-reserved space. Measure both with and without badges.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~ChannelNodeTests"`
Expected: PASS, 10 tests.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: PASS. Expect failures only in tests asserting the *old* node face (they may reference `data-role='status'`, which is now `circuit-status`). Update those assertions to the new contract; do not reintroduce the old slots.

- [ ] **Step 7: Commit**

```bash
git add Components/UI/WorkflowCanvas/Nodes/ChannelBatteryCell.razor Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor BatteryTestingSystem.Tests/Components/ChannelNodeTests.cs
git commit -m "feat(workflow-canvas): render the channel node as a battery cell"
```

---

### Task 6: Cell styling and LOD tiers

**Files:**
- Modify: `wwwroot/css/workflow-canvas.css`
- Modify: `Components/App.razor` (version bump)

**Interfaces:**
- Consumes: the class names produced by Task 5.
- Produces: `--wf-soc` (0..1) and `--wf-status` (raw HSL triplet) as the cell's entire styling API; classes `wf-bcell--charging`, `wf-bcell--discharging`, `wf-bcell--fault`, `wf-bcell--soc-unknown` for Task 7 to toggle.

- [ ] **Step 1: Add the `.wf-bcell` block**

Append to `wwwroot/css/workflow-canvas.css`, after the existing battery-glyph block:

```css
/* ---------------------------------------------------------------- channel battery cell

   Prefix is wf-bcell, NOT wf-cell: .wf-cell/.wf-cell__fill/.wf-cell__shimmer already belong to
   BatteryGlyph (the Battery node), and reusing them would restyle every battery on the canvas.

   The whole API is two custom properties written by the telemetry bridge: --wf-soc (0..1) and
   --wf-status (a raw HSL triplet). No Blazor re-render is involved in a live update. */

.wf-bcell {
    --wf-soc: 0;
    --wf-status: var(--status-idle);
    padding: 2px;
}

/* The cap. Lives inside .wf-node__header (see ChannelNode.razor) so dragging still works. */
.wf-bcap {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.25rem;
    min-width: 76px;
    margin: 0 auto;
    padding: 1px 8px;
    border-radius: 6px 6px 0 0;
    background: hsl(var(--wf-status));
    color: hsl(var(--background));
    font-weight: 800;
    font-size: 0.75rem;
    letter-spacing: 0.06em;
    font-variant-numeric: tabular-nums;
}

.wf-bcell__body {
    border: 3px solid hsl(var(--wf-status));
    border-radius: 12px;
    background: hsl(var(--card));
    padding: 4px;
    box-shadow: 0 0 14px hsl(var(--wf-status) / 0.18);
}

.wf-bcell__well {
    position: relative;
    overflow: hidden;
    border-radius: 8px;
    background: hsl(var(--muted) / 0.55);
}

/* ---- geometry layers, all behind the content ----
   Percentages rather than pixels: the cell's height changes with the property selection, and a
   pixel fill would desynchronise from the real charge the moment a user picks more fields. */

.wf-bcell__fill {
    position: absolute;
    inset-inline: 0;
    bottom: 0;
    z-index: 0;
    height: calc(var(--wf-soc) * 100%);
    background: linear-gradient(to top, hsl(var(--wf-status) / 0.55), hsl(var(--wf-status) / 0.95));
    /* Alpha is capped low ON PURPOSE. Status colours are user-changeable, so text legibility
       cannot depend on which hue someone picks; at this alpha any hue only lightly tints the
       well and never approaches the text's contrast. */
    opacity: 0.20;
    transition: height 400ms ease-out;
}

.wf-bcell__surface {
    position: absolute;
    inset-inline: 0;
    bottom: calc(var(--wf-soc) * 100%);
    z-index: 0;
    height: 1.5px;
    background: hsl(var(--wf-status));
    opacity: 0.75;
    transition: bottom 400ms ease-out;
}

/* The march. steps(10, end) on a scaleY transform is what makes the fill climb ONE SEGMENT AT A
   TIME instead of interpolating - one animation, no per-segment CSS, composited on the GPU.
   Paused rather than removed, so toggling state costs nothing. */
.wf-bcell__march {
    position: absolute;
    inset-inline: 0;
    z-index: 0;
    background: hsl(var(--wf-status));
    opacity: 0;
    animation: wf-bcell-march 2.6s steps(10, end) infinite;
    animation-play-state: paused;
}

/* Charging: motion lives in the EMPTY region above the real charge surface, growing upward. */
.wf-bcell--charging .wf-bcell__march {
    top: 0;
    height: calc(100% - var(--wf-soc) * 100%);
    transform-origin: 50% 100%;
    opacity: 0.15;
    animation-play-state: running;
}

/* Discharging: the mirror - motion lives INSIDE the fill, growing downward from the surface. */
.wf-bcell--discharging .wf-bcell__march {
    bottom: 0;
    height: calc(var(--wf-soc) * 100%);
    transform-origin: 50% 0%;
    opacity: 0.15;
    animation-play-state: running;
}

@keyframes wf-bcell-march {
    from { transform: scaleY(0); }
    to   { transform: scaleY(1); }
}

/* Fixed 18px segments, so the COUNT derives itself from the cell's height and a tall cell simply
   has more of them - no arithmetic anywhere. */
.wf-bcell__segs {
    position: absolute;
    inset: 0;
    z-index: 0;
    pointer-events: none;
    background: repeating-linear-gradient(
        to top,
        transparent 0 16px,
        hsl(var(--card) / 0.75) 16px 18px);
}

/* Fault: hatching, no motion. An error state that moves reads as activity. */
.wf-bcell--fault .wf-bcell__well {
    background-image: repeating-linear-gradient(
        45deg,
        hsl(var(--status-error) / 0.35) 0 3px,
        transparent 3px 6px);
}

/* Unknown charge is not empty charge: hide the fill and its surface entirely rather than showing
   a 0% that reads as a flat cell. */
.wf-bcell--soc-unknown .wf-bcell__fill,
.wf-bcell--soc-unknown .wf-bcell__surface,
.wf-bcell--soc-unknown .wf-bcell__march { display: none; }

/* ---- content, in normal flow ---- */

.wf-bcell__content {
    position: relative;
    z-index: 10;
    display: flex;
    flex-direction: column;
    padding: 4px 6px;
}

.wf-bcell__hd {
    display: flex;
    align-items: center;
    gap: 0.375rem;
    font-size: 0.625rem;
    padding-bottom: 3px;
    margin-bottom: 2px;
    border-bottom: 1px solid hsl(var(--border) / 0.6);
}

.wf-bcell__tcp {
    width: 6px;
    height: 6px;
    border-radius: 50%;
    flex: none;
    background: hsl(var(--status-continue));
}

.wf-bcell__cs {
    font-weight: 700;
    color: hsl(var(--wf-status));
    text-transform: capitalize;
}

.wf-bcell__soc {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    margin-left: auto;
    font-weight: 800;
    font-variant-numeric: tabular-nums;
}

/* A phone status-bar battery: body, terminal nub, and an inner bar tracking --wf-soc. */
.wf-bcell__socicon {
    position: relative;
    width: 20px;
    height: 10px;
    border: 1.2px solid hsl(var(--wf-status));
    border-radius: 2px;
}

.wf-bcell__socicon::after {
    content: "";
    position: absolute;
    top: 2px;
    right: -3.4px;
    width: 2px;
    height: 4px;
    border-radius: 0 1px 1px 0;
    background: hsl(var(--wf-status));
}

.wf-bcell__socicon::before {
    content: "";
    position: absolute;
    inset: 1.4px;
    width: calc((100% - 2.8px) * var(--wf-soc));
    background: hsl(var(--wf-status));
    transition: width 400ms ease-out;
}

.wf-bcell--soc-unknown .wf-bcell__socicon::before { display: none; }

.wf-bcell__sec {
    font-size: 0.5rem;
    font-weight: 800;
    letter-spacing: 0.1em;
    text-transform: uppercase;
    opacity: 0.6;
    margin: 5px 0 2px;
}

/* Primary Data: two columns, value + unit, no labels. Left column aligns left and right column
   right, so each pair spans the cell's two edges in the same rhythm as the labelled rows. */
.wf-bcell__grid2 {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1px 10px;
}

.wf-bcell__v--left  { text-align: left; }
.wf-bcell__v--right { text-align: right; }

.wf-bcell__rows { display: grid; gap: 1px; }

.wf-bcell__row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    font-size: 0.625rem;
}

.wf-bcell__k {
    opacity: 0.62;
    white-space: nowrap;
}

.wf-bcell__v {
    font-size: 0.65rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.wf-bcell__v small {
    opacity: 0.55;
    font-size: 0.5rem;
    margin-left: 2px;
    font-weight: 500;
}

.wf-bcell__spec {
    font-size: 0.5rem;
    opacity: 0.58;
    text-align: center;
    letter-spacing: 0.04em;
    /* min-height keeps the strip reserved when empty - see ChannelBatteryCell.razor. */
    min-height: 0.75rem;
}

.wf-bcell__ft {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.375rem;
    font-size: 0.5625rem;
    margin-top: 5px;
    padding-top: 3px;
    border-top: 1px solid hsl(var(--border) / 0.6);
    font-variant-numeric: tabular-nums;
}

.wf-bcell__badges {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    flex-wrap: wrap;
    padding: 2px 4px 0;
}
```

- [ ] **Step 2: Extend the reduced-motion block**

Find the existing `@media (prefers-reduced-motion: reduce)` block (around line 490, listing `.wf-cell__shimmer`) and add the cell's animated layer to it:

```css
    .wf-bcell__march { animation: none; }
    .wf-bcell__fill,
    .wf-bcell__surface,
    .wf-bcell__socicon::before { transition: none; }
```

- [ ] **Step 3: Rewrite the LOD tiers**

Replace the tier-1 rule. The old one works by hiding `.wf-node__body > :nth-child(n+2)`; the new body has exactly **one** child, so it would silently do nothing and tier 1 would render the full cell at small zoom.

```css
/* Tier 1 (zoom 0.4-0.8): the cell keeps its cap, outline and fill, but drops the data. What is
   left is a battery whose fill level and colour are still readable at distance.

   The old rule hid .wf-node__body > :nth-child(n+2) - it relied on the body having one child per
   row. The battery cell's body has exactly ONE child, so that selector matched nothing and this
   tier silently stopped working. */
.wf-world[data-lod="1"] .wf-bcell__sec,
.wf-world[data-lod="1"] .wf-bcell__grid2,
.wf-world[data-lod="1"] .wf-bcell__rows,
.wf-world[data-lod="1"] .wf-bcell__spec,
.wf-world[data-lod="1"] .wf-bcell__ft,
.wf-world[data-lod="1"] .wf-bcell__badges { display: none; }

.wf-world[data-lod="1"] .wf-bcell__well { min-height: 46px; }

/* Non-channel nodes keep the original tier-1 behaviour. */
.wf-world[data-lod="1"] .wf-node:not(.wf-node--channel) .wf-node__body > :nth-child(n+2) {
    display: none;
}

/* Tier 2 (zoom < 0.4): a pure colour tile. Unchanged for other kinds; for a channel the cell
   chrome must also go, or a 32px tile renders a 3px border and a cap. */
.wf-world[data-lod="2"] .wf-bcell { display: none; }
```

Verify the existing tier-2 rule still hides `.wf-node__header` and `.wf-node__body` — the cap lives inside the header, so it is already covered.

- [ ] **Step 4: Bump the asset version**

In `Components/App.razor`, increment the `?version=` on the canvas CSS (and the JS if Task 7 lands in the same build). This is hand-maintained; without it the browser serves the cached stylesheet and the cell renders unstyled while the build looks correct.

- [ ] **Step 5: Verify the CSS actually loaded**

Start the app, open the canvas, and in the browser console:

```js
getComputedStyle(document.querySelector('.wf-bcell__body')).borderWidth
```

Expected: `"3px"`. If it returns `"0px"`, the version bump did not take — do not proceed.

- [ ] **Step 6: Commit**

```bash
git add wwwroot/css/workflow-canvas.css Components/App.razor
git commit -m "feat(workflow-canvas): style the battery cell and rewrite the LOD tiers"
```

---

### Task 7: Drive the cell from the telemetry bridge

**Files:**
- Modify: `wwwroot/js/workflow-canvas.js` (the `applyTelemetry` loop, ~line 640-665)
- Modify: `Components/App.razor` (version bump)

**Interfaces:**
- Consumes: `TelemetryEntry.Soc`, `.SocKnown`, `.SocText`, `.BatteryLine`, `.StatusName` (Task 2); the `data-role` slots and `wf-bcell--*` classes (Tasks 5, 6).
- Produces: nothing downstream.

- [ ] **Step 1: Write the cell updates into `applyTelemetry`**

In `wwwroot/js/workflow-canvas.js`, inside the `for (const e of entries)` loop, after the existing `node.style.setProperty("--wf-status", e.statusHsl);`, add:

```js
                // The channel battery cell. --wf-soc drives the fill height, the surface line and
                // the header icon's inner bar, all as percentages, so they stay correct at any
                // cell height. Idle/pause/error deliberately get NO march: an error state that
                // moves reads as activity.
                const cell = node.querySelector('[data-role="cell"]');
                if (cell) {
                    cell.style.setProperty("--wf-soc", String(e.soc));
                    cell.classList.toggle("wf-bcell--soc-unknown", !e.socKnown);
                    cell.classList.toggle(
                        "wf-bcell--charging",
                        e.flow > 0 && (e.statusName === "charge" || e.statusName === "continue"));
                    cell.classList.toggle(
                        "wf-bcell--discharging", e.flow < 0 && e.statusName === "discharging");
                    cell.classList.toggle("wf-bcell--fault", e.statusName === "error");
                }

                // Idle reads as STOP on the cell face, per the user's requirement. This is a
                // CELL-ONLY relabel: the status legend and the filter bar still say "idle", and
                // matchesStatusFilter keys off e.statusName, which is untouched.
                setText(node, "circuit-status", e.statusName === "idle" ? "STOP" : e.statusName);
                setText(node, "soc", e.socText);
                setText(node, "battery-line", e.batteryLine || "");
```

Then update the two now-stale writes in the same loop:

```js
                // was: setText(node, "status", e.statusName);   -> the slot is now circuit-status
                // was: setText(node, "soc", Math.round(e.soc * 100) + "%");
                //      -> SocText is pre-formatted server-side and knows about the unknown case
```

Delete those two original lines. Keep `setText(node, "program-status", ...)` and `setText(node, "last-update", ...)` exactly as they are — those slot names did not change.

- [ ] **Step 2: Leave `applyFlowToEdges` alone**

It writes `--wf-soc` and `wf-battery--charging` onto `[data-role="battery"]` — the **Battery node's** glyph, a different element with a different class prefix. It also calls `setText(other, "soc", …)` on the battery node, which still has a plain `soc` slot. No change needed. Confirm by reading the function before moving on; do not "unify" the two paths.

- [ ] **Step 3: Bump the asset version**

Increment the `?version=` on the canvas **JS** in `Components/App.razor`.

- [ ] **Step 4: Verify live**

Start the app with a bench (or the HardwareSimulator) attached and a channel running. In the console:

```js
const c = document.querySelector('.wf-bcell');
[getComputedStyle(c).getPropertyValue('--wf-soc'),
 c.className,
 document.querySelector('[data-role="circuit-status"]').textContent,
 document.querySelector('[data-role="soc"]').textContent]
```

Expected: a non-zero `--wf-soc` for a channel with a battery whose `NominalCapacity > 0`; `wf-bcell--charging` present while charging; the status word; a `NN%` string. On a channel with **no** battery: `wf-bcell--soc-unknown` present and `--%`.

- [ ] **Step 5: Commit**

```bash
git add wwwroot/js/workflow-canvas.js Components/App.razor
git commit -m "feat(workflow-canvas): drive the battery cell from the telemetry bridge"
```

---

### Task 8: Measure the height, then encode it

**This task is measurement-first on purpose.** Session 36's constants (`106` base, `16` per row) reproduced the correct height at *exactly three rows and nowhere else*, because one measurement cannot pin a two-parameter linear model. The new formula has more terms, so it needs more measurements than parameters.

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowAutoLayout.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs`

**Interfaces:**
- Consumes: `WorkflowNodeProperties.SectionsFor` (Task 3), the rendered cell (Tasks 5-6).
- Produces: `WorkflowAutoLayout.ChannelNodeHeight(IReadOnlyList<string> visibleProperties) → double`. The `int` overload is **removed**, not kept — a silent fallback to a count would reintroduce exactly the desynchronisation this fixes.

- [ ] **Step 1: Measure in a real browser**

Start the app, open the canvas with at least one channel placed, and for each selection below set it in the node-property picker, wait for the render to settle, then read the height. **Measure only after the render settles** — heights read ~70px mid-render.

```js
// Run once per selection, after the canvas has visibly repainted.
Math.round(document.querySelector('.wf-node--channel').getBoundingClientRect().height /
           (new DOMMatrixReadOnly(getComputedStyle(document.querySelector('.wf-world')).transform).a))
```

Record every result in this table before writing any code:

| Selection | Primary | Config | Program | Sections | Measured height |
|---|---|---|---|---|---|
| none | 0 | 0 | 0 | 0 | |
| `Voltage` | 1 | 0 | 0 | 1 | |
| `Voltage`,`Current` | 2 | 0 | 0 | 1 | |
| `Voltage`,`Current`,`Power` | 3 | 0 | 0 | 1 | |
| `Voltage`,`Current`,`Power`,`Temperature` | 4 | 0 | 0 | 1 | |
| `BatteryID` | 0 | 1 | 0 | 1 | |
| `BatteryID`,`ProgramID`,`SessionID` | 0 | 3 | 0 | 1 | |
| `CycleNumber`,`StepNumber` | 0 | 0 | 2 | 1 | |
| default 4 + `BatteryID` | 4 | 1 | 0 | 2 | |
| all 28 (`AvailableKeys`) | 12 | 3 | 13 | 3 | |

Then solve for the five constants and **check them against every row, not just the row that produced them**:

- `ChannelCellChromeHeight` — the `none` row (cap + frame + well padding + header + battery strip + footer)
- `ChannelSectionHeadingHeight` — from the 2-section vs 1-section rows at equal key counts
- `ChannelPrimaryRowHeight` — from the 1→3 primary rows (note 1 and 2 keys are the same row count)
- `ChannelLabelledRowHeight` — from the config and program rows
- Whether the program/DBC badge strip adds height — measure a channel **with** an attached program and one without

If any row disagrees with the solved constants by more than 1px, the model is wrong: do not average it away. Re-derive, and if the disagreement is structural (e.g. a section heading's margin collapses against its neighbour) encode that instead.

- [ ] **Step 2: Write the failing test using the measured numbers**

Add to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs`, substituting the **measured** values for every `MEASURED_*` placeholder — the plan cannot supply these, they must come from Step 1:

```csharp
    // Every expected value below was measured in the browser (plan Task 8 Step 1), not derived
    // on paper. A previous version of these constants was correct at exactly one row count.
    [Theory]
    [InlineData(new string[0], MEASURED_NONE)]
    [InlineData(new[] { "Voltage" }, MEASURED_1_PRIMARY)]
    [InlineData(new[] { "Voltage", "Current" }, MEASURED_2_PRIMARY)]
    [InlineData(new[] { "Voltage", "Current", "Power" }, MEASURED_3_PRIMARY)]
    [InlineData(new[] { "BatteryID" }, MEASURED_1_CONFIG)]
    [InlineData(new[] { "BatteryID", "ProgramID", "SessionID" }, MEASURED_3_CONFIG)]
    [InlineData(new[] { "Voltage", "Current", "Power", "Temperature", "BatteryID" }, MEASURED_4P_1C)]
    public void ChannelNodeHeightMatchesTheMeasuredRenderedHeight(string[] keys, double expected)
    {
        Assert.Equal(expected, WorkflowAutoLayout.ChannelNodeHeight(keys));
    }

    [Fact]
    public void TwoPrimaryKeysAreNoTallerThanOneBecauseTheyShareARow()
    {
        Assert.Equal(
            WorkflowAutoLayout.ChannelNodeHeight(new[] { "Voltage" }),
            WorkflowAutoLayout.ChannelNodeHeight(new[] { "Voltage", "Current" }));
    }

    [Fact]
    public void RowHeightLeavesRoomForTheTallestPossibleCard()
    {
        // The anti-overlap invariant. Raising the property cap must not be able to make a card
        // spill into the row beneath it.
        Assert.True(WorkflowAutoLayout.RowHeight > WorkflowAutoLayout.MaxChannelNodeHeight);
    }

    [Fact]
    public void MaxChannelNodeHeightIsTheHeightOfEveryOfferedKey()
    {
        Assert.Equal(
            WorkflowAutoLayout.ChannelNodeHeight(WorkflowNodeProperties.AvailableKeys),
            WorkflowAutoLayout.MaxChannelNodeHeight);
    }
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowAutoLayoutTests"`
Expected: FAIL — `ChannelNodeHeight` does not accept a string array.

- [ ] **Step 4: Replace the formula**

In `Services/Implementations/Workflow/WorkflowAutoLayout.cs`, delete `ChannelNodeBaseHeight`, `ChannelPropertyRowHeight` and the `int` overload of `ChannelNodeHeight`, and add — with the measured values in place of each `/* measured */` marker:

```csharp
    /// <summary>
    /// A channel cell with no sections at all: cap, 3px frame, well padding, header row, the
    /// always-reserved battery-spec strip, and the footer. Measured in the browser.
    /// </summary>
    public const double ChannelCellChromeHeight = /* measured */ 0;

    /// <summary>One section heading, its margins included. Measured.</summary>
    public const double ChannelSectionHeadingHeight = /* measured */ 0;

    /// <summary>One Primary Data row. It holds TWO values, so N primary keys cost
    /// ceil(N / 2) rows. Measured.</summary>
    public const double ChannelPrimaryRowHeight = /* measured */ 0;

    /// <summary>One labelled Configuration/Program Data row, holding a single key. Measured.</summary>
    public const double ChannelLabelledRowHeight = /* measured */ 0;

    /// <summary>
    /// Rendered height of a channel cell showing exactly these properties.
    ///
    /// This takes the KEYS, not a count, and that is not incidental: Primary Data packs two
    /// values per row while Configuration and Program Data take one each, and every non-empty
    /// section adds a heading. Six primary keys and six program keys therefore produce visibly
    /// different cells, so a count cannot express the height and the previous
    /// ChannelNodeHeight(int) had to go rather than gain a fallback.
    ///
    /// The single source of truth: WorkflowEdgeGeometry needs it to attach an edge at the cell's
    /// vertical centre, MinimapProjection to scale the node, and Apply to space the rows.
    /// </summary>
    public static double ChannelNodeHeight(IReadOnlyList<string> visibleProperties)
    {
        var height = ChannelCellChromeHeight;

        foreach (var section in WorkflowNodeProperties.SectionsFor(visibleProperties))
        {
            height += ChannelSectionHeadingHeight;
            height += section.TwoPerRow
                ? Math.Ceiling(section.Keys.Count / 2d) * ChannelPrimaryRowHeight
                : section.Keys.Count * ChannelLabelledRowHeight;
        }

        return height;
    }

    public static readonly double MaxChannelNodeHeight =
        ChannelNodeHeight(WorkflowNodeProperties.AvailableKeys);
```

Leave `RowHeight = MaxChannelNodeHeight + 26` as it is — it is already derived, which is what keeps the anti-overlap invariant true when the catalogue grows.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowAutoLayoutTests"`
Expected: PASS. The rest of the solution will not compile yet — Task 9 fixes the call sites.

- [ ] **Step 6: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowAutoLayout.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowAutoLayoutTests.cs
git commit -m "feat(workflow-canvas): height from the selected keys, not their count"
```

---

### Task 9: Thread the property list through the geometry consumers

**Files:**
- Modify: `Services/Implementations/Workflow/WorkflowEdgeGeometry.cs`
- Modify: `Services/Implementations/Workflow/MinimapProjection.cs`
- Modify: `Components/UI/WorkflowCanvas/EdgeLayer.razor`
- Modify: `Components/UI/WorkflowCanvas/CanvasSurface.razor`
- Check: `Services/Implementations/Workflow/WorkflowPlacement.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowEdgeGeometryTests.cs`, `MinimapProjectionTests.cs`

**Interfaces:**
- Consumes: `WorkflowAutoLayout.ChannelNodeHeight(IReadOnlyList<string>)` (Task 8).
- Produces:
  - `WorkflowEdgeGeometry.NodeHeightFor(NodeKind kind, IReadOnlyList<string>? visibleChannelProperties = null)`
  - `WorkflowEdgeGeometry.PortPoints(WorkflowNode from, WorkflowNode to, IReadOnlyList<string>? visibleChannelProperties = null)`
  - `EdgeLayer` parameter `VisibleProperties` (was `VisiblePropertyCount`).

- [ ] **Step 1: Find every caller before editing**

```bash
grep -rn "ChannelNodeHeight\|NodeHeightFor\|VisiblePropertyCount\|MaxChannelNodeHeight" --include=*.cs --include=*.razor . | grep -viE "obj/|bin/"
```

Write the list down. The spec names `WorkflowAutoLayout`, `WorkflowEdgeGeometry`, `MinimapProjection`, `WorkflowPlacement`, `EdgeLayer` and `CanvasSurface`; treat anything else the grep finds as a gap in the spec, not as noise.

- [ ] **Step 2: Write the failing test**

Add to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowEdgeGeometryTests.cs`:

```csharp
    [Fact]
    public void AChannelEdgeAttachesAtTheCellsRealVerticalCentre()
    {
        var device = new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null);
        var channel = new WorkflowNode("chn-1", NodeKind.Channel, 1, 560, 200, null);
        var keys = new[] { "Voltage", "Current", "BatteryID" };

        var p = WorkflowEdgeGeometry.PortPoints(device, channel, keys);

        Assert.Equal(
            channel.Y + WorkflowAutoLayout.ChannelNodeHeight(keys) / 2,
            p.Y2);
    }

    [Fact]
    public void TheSameKeyCountInDifferentSectionsGivesDifferentAttachPoints()
    {
        // This is the whole reason the signature takes keys rather than a count: three primary
        // keys share two rows, three program keys take three labelled rows plus a heading.
        var device = new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null);
        var channel = new WorkflowNode("chn-1", NodeKind.Channel, 1, 560, 200, null);

        var primary = WorkflowEdgeGeometry.PortPoints(
            device, channel, new[] { "Voltage", "Current", "Power" });
        var program = WorkflowEdgeGeometry.PortPoints(
            device, channel, new[] { "CycleNumber", "StepNumber", "CycleStatus" });

        Assert.NotEqual(primary.Y2, program.Y2);
    }

    [Fact]
    public void NoPropertiesIsTreatedAsAnEmptySelectionNotACrash()
    {
        Assert.Equal(
            WorkflowAutoLayout.ChannelNodeHeight(System.Array.Empty<string>()),
            WorkflowEdgeGeometry.NodeHeightFor(NodeKind.Channel));
    }
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~WorkflowEdgeGeometryTests"`
Expected: FAIL to build — `PortPoints` has no overload taking a string array.

- [ ] **Step 4: Change `WorkflowEdgeGeometry`**

```csharp
    /// <summary>
    /// Rendered height, used to attach an edge to a card's vertical centre.
    ///
    /// A channel cell's height depends on WHICH properties are shown, not how many - Primary Data
    /// packs two per row, the other sections one each, and each non-empty section adds a heading -
    /// so this takes the key list and defers to WorkflowAutoLayout.ChannelNodeHeight, the same
    /// formula the layout uses. Passing a count here used to be enough and is not any more.
    /// </summary>
    public static double NodeHeightFor(
        NodeKind kind, IReadOnlyList<string>? visibleChannelProperties = null) => kind switch
    {
        NodeKind.Channel => WorkflowAutoLayout.ChannelNodeHeight(
            visibleChannelProperties ?? Array.Empty<string>()),
        NodeKind.Battery => 72,
        _ => 88,
    };

    public static EdgeEndpoints PortPoints(
        WorkflowNode from, WorkflowNode to,
        IReadOnlyList<string>? visibleChannelProperties = null) =>
        PortPoints(from, to,
            NodeWidthFor(from.Kind),
            NodeHeightFor(from.Kind, visibleChannelProperties),
            NodeHeightFor(to.Kind, visibleChannelProperties));
```

Add `using System;` and `using System.Collections.Generic;` if absent. Leave the four-argument `PortPoints(from, to, fromWidth, fromHeight, toHeight)` overload and `BezierPath` unchanged.

- [ ] **Step 5: Change `MinimapProjection`**

Its two `NodeHeightFor(n.Kind)` calls currently rely on the `int` default of `0`, which silently measured every channel as having no properties. Give it the real list:

```csharp
    // The projection needs the SAME height the canvas renders, or the minimap's channel
    // rectangles drift from the nodes they represent as soon as a user picks more fields. The
    // previous code relied on NodeHeightFor's default of "no properties".
    public static MinimapProjectionResult Project(
        WorkflowGraph graph,
        /* existing parameters */
        IReadOnlyList<string> visibleChannelProperties)
```

Pass `visibleChannelProperties` into both `NodeHeightFor` calls (lines 23 and 46). Update `Minimap.razor` and any test to supply it; `WorkflowNodeProperties.DefaultVisibleProperties` is the correct default for a caller that has no user selection to hand.

- [ ] **Step 6: Change `EdgeLayer` and `CanvasSurface`**

In `EdgeLayer.razor`, rename the parameter:

```csharp
    [Parameter] public IReadOnlyList<string> VisibleProperties { get; set; } = Array.Empty<string>();
```

and pass it to every `PortPoints` / `NodeHeightFor` call in that file.

In `CanvasSurface.razor`, replace the count with the list and drop the removed parameter:

```razor
<EdgeLayer Graph="Graph" StaleNodeIds="StaleNodeIds"
           VisibleProperties="SelectedProperties" />
```

Also remove `InitialStatusText="..."` from the `<ChannelNode>` element if it is present — Task 5 deleted that parameter.

- [ ] **Step 7: Check `WorkflowPlacement`**

Its two uses are `WorkflowAutoLayout.RowHeight`, which is still a `static readonly double`. **No change should be needed.** Confirm by reading lines 72 and 80; if it calls `ChannelNodeHeight` anywhere, thread the list.

- [ ] **Step 8: Build and run the full suite**

Stop the app. Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: PASS, everything green. Grep the build output for `error` (**not** `error CS`).

- [ ] **Step 9: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowEdgeGeometry.cs Services/Implementations/Workflow/MinimapProjection.cs Components/UI/WorkflowCanvas/EdgeLayer.razor Components/UI/WorkflowCanvas/CanvasSurface.razor Components/UI/WorkflowCanvas/Minimap.razor BatteryTestingSystem.Tests/Services/Workflow/
git commit -m "feat(workflow-canvas): thread the selected keys through canvas geometry"
```

---

### Task 10: Live verification

Unit tests cannot see most of what matters here. Session 36 established that a programmatic `el.click()` dispatches a click directly and therefore always passes, while a real pointer sequence goes through `setPointerCapture` and does not — **use real CDP-level interaction, not scripted `.click()`**.

**Files:** none — this task changes nothing. It either passes or it sends you back to a previous task.

- [ ] **Step 1: Confirm the assets actually shipped**

```js
[...document.querySelectorAll('link[href*="workflow-canvas"],script[src*="workflow-canvas"]')]
  .map(e => e.href || e.src)
```

Both must carry the bumped `?version=`. If not, fix `Components/App.razor` before anything else — every check below would otherwise be testing cached assets.

- [ ] **Step 2: No overlap at any field count**

At 0, 4, 12 and all 28 selected properties, confirm no channel card touches the one below:

```js
const r = [...document.querySelectorAll('.wf-node--channel')].map(n => n.getBoundingClientRect());
r.some((a, i) => r.some((b, j) => i !== j && a.left === b.left && a.bottom > b.top && a.top < b.bottom))
```

Expected: `false` every time.

- [ ] **Step 3: Text stays inside the cell**

```js
[...document.querySelectorAll('.wf-bcell__content')].every(c => {
  const o = c.closest('.wf-bcell__well').getBoundingClientRect(), i = c.getBoundingClientRect();
  return i.top >= o.top - 1 && i.bottom <= o.bottom + 1 && i.left >= o.left - 1 && i.right <= o.right + 1;
})
```

Expected: `true` at every field count, including all 28. This is the defect the user reported.

- [ ] **Step 4: Legibility across statuses and themes**

With channels in charge, discharging, pause and error, in each of the four theme blocks, confirm by eye that every value is readable over the fill. Then confirm the fill is doing something at all — if `--wf-soc` is `0` everywhere, Task 2 is not feeding real values.

- [ ] **Step 5: The march**

Watch a charging channel: segments must step **one at a time from the charge surface upward**, then restart at the surface — not slide smoothly. Watch a discharging channel: the mirror, **downward from the surface**. Confirm an idle channel shows `STOP` and does not move at all, and that an error channel hatches without motion.

- [ ] **Step 6: Channel nodes still drag**

Drag a channel node **by its cap** with a real pointer sequence. It must move. This is the `wf-node__header` risk; a scripted click cannot test it.

- [ ] **Step 7: Edges attach at the cell's centre**

```js
const n = document.querySelector('.wf-node--channel'), b = n.getBoundingClientRect();
const p = document.querySelector(`path[data-to="${n.dataset.nodeId}"]`);
// compare the path's end point in screen space against b.top + b.height / 2
```

Expected: within ~3px, at 4 properties **and** at all 28.

- [ ] **Step 8: LOD tiers**

Zoom to ~0.6: cap, outline and fill remain, sections and footer gone. Zoom below 0.4: a plain colour tile, no 3px border or cap peeking out.

- [ ] **Step 9: Unknown vs empty**

Find a channel with no battery attached, or one whose battery has `NominalCapacity = 0`. It must show `--%` with no fill and no march — **not** `0%` with an empty well.

- [ ] **Step 10: Update `.claude/` memory and commit**

Per `CLAUDE.md`: update the current session file, `SESSION.md`, `TASKS.md` (T-48 → done), `CODEBASE_MAP.md` (the new files and the `.wf-bcell*` prefix rule), and `AGENT.md`'s header and live-state table. Record the measured height constants — they are the most expensive thing in this plan to recover.

```bash
git add .claude
git commit -m "docs(memory): record the battery-cell channel node implementation"
```

---

## Self-Review

**Spec coverage:** §4.1 structure → Tasks 4, 5. §4.2 sections and labels → Tasks 3, 5. §4.3 fill/surface/march → Task 6. §4.4 state mapping → Tasks 6, 7. §4.5 contrast → Task 6 (capped alpha), Task 10 Step 4. §4.6 naming/reuse → Task 6 (`.wf-bcell` prefix), Task 7 Step 2 (`BatteryGlyph` untouched). §5.1 signature → Tasks 8, 9. §5.2 measured constants → Task 8 Step 1. §5.3 width 200 → unchanged, no task needed. §5.4 LOD → Task 6 Step 3. §6 bridge → Task 7. §6.5 SoC → Tasks 1, 2. §6.5.4 battery details → Tasks 2, 5. §7 `?version=` → Tasks 6, 7. §8 testing → every task, plus Task 10.

**One gap found and closed:** the spec does not mention the program/DBC badges, which are conditional and therefore threaten the same height determinism the battery strip was fixed for. Task 5 Step 4 renders them outside `.wf-bcell` and Task 8 Step 1 measures with and without them.

**Placeholder scan:** the only intentional blanks are `MEASURED_*` in Task 8 Step 2 and `/* measured */` in Step 4. These are not placeholder failures — they are the deliverable of Step 1, and hard-coding a guess here is the specific mistake that caused session 36's defect round.

**Type consistency:** `Estimate(double, double, float?) → double?` (Task 1) is called with `circuit.Battery?.NominalCapacity`, a `float?` (Task 2) ✓. `ChannelTelemetry.Soc` is `double?` and `TelemetryEntry.Soc` is `double` with a separate `SocKnown` — deliberately different, so the CSS variable always parses ✓. `SectionsFor(IReadOnlyList<string>) → IReadOnlyList<PropertySection>` with `.Title` / `.Keys` / `.TwoPerRow` is used identically in Task 5 (component) and Task 8 (formula) ✓. `ChannelNodeHeight(IReadOnlyList<string>)` is called with `string[]` in tests and `IReadOnlyList<string>` in production — `string[]` implements it ✓. `data-role` names are consistent between Task 5 (emits) and Task 7 (writes): `cell`, `circuit-status`, `soc`, `soc-fill`, `battery-line`, `program-status`, `last-update`, `prop-{key}` ✓.
