# Session 35 — T-47 Sub-projects C & D: Dashboard data parity + TabViewer navigation

> Started: 2026-08-26T08:00:00Z
> Branch: `feat/workflow-canvas-experiment` (never merges to `main` — D14/D15)
> Agent: Claude (Sonnet 5)
> Status: ✅ Complete — sub-projects C1/C2/C3 and D all done. **All 4 sub-projects of the
> 2026-08-26 feedback batch are now closed.**

## Goal

Finish the 12-item feedback batch decomposed last session into A/B/C/D. A and B shipped in
sessions 33–34; this session covers:

- **C — Dashboard data parity**, split by the user into C1 (fields) → C2 (dialog) → C3 (DBC)
- **D — TabViewer navigation**, which had to revisit Phase 6's D17 login-redirect decision

## Outcome

| Sub-project | Commit | Tests |
|---|---|---|
| C1 — node-face field parity (all 28 dashboard fields) | `24b6e1b` | 568 |
| C2 + C3 — multi-tab channel detail dialog + DBC signals | `65c6e86` | 582 |
| D — canvas as the TabViewer landing tab | `faa2aa1` | 589 |

589 tests, up from 565 at the start of the session.

## What was built

### C1 — field parity (`24b6e1b`)

The node-face property picker offered 17 of the dashboard's 28 `CardPreviewData` keys. Widened
`WorkflowNodeProperties.AvailableKeys` to the full 28 by concatenating the three catalogs
(RealTime 12 + Config 3 + Program 13) instead of maintaining a hand-copied subset, added the 11
missing keys to `WorkflowTelemetryBridge.BuildNodeProperties`, and extended `ChannelTelemetry`
with the 9 fields those needed. The 6-visible cap stays (user's choice).

`ResolveOperatorName` reads the operator code off the *current step* via
`ExpandedProgramSteps ?? Program.ProgramStepModel`, then `OperatorConstants.ToName`.

### C2 / C3 — the detail dialog (`65c6e86`)

New `ChannelDetailDialog.razor` with 5 tabs (Live / Battery / Manufacturing / Factory / DBC),
opened from a new "All details…" button on `PropertiesDock`. Deliberately a **new** component,
not a reuse of the dashboard's — that one is embedded in a 2,000-line `DeviceChannel.razor` this
branch must never modify (D13).

Row content lives in a new pure `WorkflowChannelDetail.cs` so tests can count each tab against
its DTO **by reflection** (`PublicPropertyCount<T>()`) rather than a human diffing two large Razor
files. That is the whole defence against duplicating a surface and silently dropping a field.

Manufacturing and Factory are fetched from the DB on open (the same `IDeviceChannelServices` calls
the dashboard uses), not pushed per telemetry tick — they effectively never change.

`DbcParameterView.razor` is a **documented exception** to the branch's JS-owns-interaction rule:
the decoded signal set is not a fixed list, so there are no stable `data-role` slots to write
into and it must re-render through Blazor.

### D — TabViewer landing tab (`faa2aa1`)

New `WorkflowDefaultTab.cs` chooses the tab `TabService` seeds; `TabService` calls it (a ~2-line
change in that main-owned file); `Login.cshtml.cs` reverts both redirects from `/workflows` to `/`.

## Discoveries & gotchas

### The real reason "home still shows" (D's root cause)

`Features:LegacyDashboard` was **already** `false` in `appsettings.Development.json`, so the
"Display" nav entry was already hidden — yet the dashboard still greeted the user. Cause:
**`TabService` seeds its default tab in code, hard-coded to `DashboardView`, not from the nav
menu.** Hiding a menu entry can never affect the landing tab. Worth remembering: in this app the
landing page is a *tab decision*, not a *route decision*.

### Phase 6's D17 fix was wrong in two ways

`WorkflowCanvasPage` has `@page "/workflows"`, so Phase 6's `Redirect("/workflows")` rendered the
canvas **bare — outside the TabViewer shell**, with no tab bar and no navbar. The user's "we are
not using routing, we use TabViewer" note is exactly this. Login now lands on `/`.

### A string literal that was load-bearing in two places (bug I introduced and fixed)

Renaming the seeded tab from "Home" to "Workflows" produced **two identical non-closable
"Workflows" tabs**. `NotifyStateChanged` excluded the seed from persistence with
`store.RemoveAll(e => e.Title == "Home")` — a literal whose job was "don't persist the seed, the
constructor re-creates it". The rename slipped past that filter, so the seed was persisted and
then restored beside the fresh one. Now keyed on a `_defaultTabTitle` field used in both places,
so the two cannot drift again.

Also note `TabService.AddTab` dedupes `Unique` tabs **by title**, so the seed's title must stay
byte-identical to its menu entry's or clicking "Workflows" opens a second canvas tab. Two tests
pin this by selecting the menu entry on one axis and asserting the other, so neither can pass
tautologically.

### `await InvokeAsync(StateHasChanged)` does not mean the DOM is updated (C1)

Changing the selected properties left values under the **wrong labels** (`BatteryID=2.172 Ah`).
My first fix — force the telemetry push inside the change handler — **failed**, because
`InvokeAsync(StateHasChanged)` only *queues* a render batch over SignalR; the JS write raced onto
the old `data-role` attributes. Fixed by deferring via a `_repaintAfterRender` flag consumed in
`OnAfterRenderAsync`. Any DOM-dependent JS interop on this branch must go through
`OnAfterRenderAsync`, never straight after a `StateHasChanged`.

### I destroyed 24 tests and only caught it by the count going *down*

Used `Write` on `WorkflowNodePropertiesTests.cs` assuming it was new. The only signal was the
total dropping 565→548 while I had *added* tests. Diagnosed by per-file count diff against
`e24c574`; restored 8 verbatim, deliberately updated 3. **Check whether a test file exists before
`Write`, and treat a falling test count as a stop-the-line event.**

### "Blank in the UI" is not evidence of a bug

Channel `1-1-4` showed `--` for every secondary-board field while `1-1-1` had real data. The DB
was ground truth: that row genuinely has `SwVersion=''`, `SecondarySerialNumber=''`,
`AssemblyDate=NULL`. The decisive tell that refetch worked was that the values *differed* from the
previously-opened channel — stale data would have been identical.

One row did look wrong: `Last Synced` rendered a date where the channel's column is NULL. That is
**correct by design** — `DeviceChannelRepository.GetManufacturingAsync` deliberately falls back to
`device.LastSyncedAt`, with a comment saying the two are stamped together.

Related: that repository maps missing dates to `DateTime.MinValue`, so `WorkflowChannelDetail`'s
placeholder logic is load-bearing — a naive `ToString()` renders `0001-01-01`, which reads as a
real date.

### A grep for `error CS` gave a false "build is green"

The build had actually failed with `MSB3027`/`MSB3021` — the running app held a lock on
`BatteryTestingSystem.exe`, so compilation never ran and my new failing test appeared to compile.
**Stop the app before building, and grep for `error` (not `error CS`).**

### Test project still has no `ImplicitUsings`

`Dictionary<,>` not found in `WorkflowChannelDetailTests.cs` — recurring. Needed explicit
`using System.Collections.Generic;` (and later `System.Linq`).

## Files changed

**Created**
- `Services/Implementations/Workflow/WorkflowChannelDetail.cs` — pure row builders (Manufacturing / Factory / Battery / Dbc)
- `Components/UI/WorkflowCanvas/ChannelDetailDialog.razor` — the 5-tab dialog
- `Components/UI/WorkflowCanvas/DbcParameterView.razor` — decoded DBC signals
- `Components/Layout/WorkflowDefaultTab.cs` — which tab `TabService` seeds
- `BatteryTestingSystem.Tests/Services/Workflow/WorkflowChannelDetailTests.cs` (14)
- `BatteryTestingSystem.Tests/Components/WorkflowDefaultTabTests.cs` (7)

**Modified**
- `Services/Implementations/Workflow/WorkflowNodeProperties.cs` — catalog widened to all 28
- `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs` — 11 new keys
- `Models/DTOs/Workflow/TelemetryEntry.cs` — `ChannelTelemetry` +9 fields
- `Components/Pages/Workflows/WorkflowCanvasPage.razor` — `_repaintAfterRender`, `ResolveOperatorName`, `OpenChannelDetails`, dialog host
- `Components/UI/WorkflowCanvas/PropertiesDock.razor` — `OnOpenDetails` + "All details…"
- `Components/UI/TabViewer/TabService.cs` — seed via `WorkflowDefaultTab`; persistence exclusion keyed on `_defaultTabTitle`
- `Pages/Login.cshtml.cs` — both redirects `/workflows` → `/`
- `wwwroot/css/workflow-canvas.css` — `.wf-detail-*` dialog styles

## Verification

- **C2/C3 live** at 640-channel scale with the simulator and the "Test" layout: Live 28 rows,
  Battery 15, Manufacturing 10, Factory 16, DBC empty-state. Dialog within viewport, `z-index`
  41 above the toolbar dock's 21, body scrolls. Refetch confirmed across two channels, checked
  against the SQLite DB as ground truth.
- **D live**: exactly one non-closable "Workflows" tab at `/`, no "Display" nav entry, nav-click
  activates rather than duplicates, a user tab (Circuits) opens beside it, and **a reload with
  persisted state restores the user tab without duplicating the seed** — the exact scenario that
  produced the duplicate bug. Console clean.
- 589/589 tests.

## Follow-ups / not done

- The dashboard's dialog has tabs beyond these 5 (charts/records) not brought over — the user's
  ask was the *fields and the multi-tab dialog*, which is met; revisit if they want charts too.
- `DbcParameterView` was only verified against its **empty state** live (no channel on the bench
  had a transferred DBC file). Row rendering, ordering and invariant-culture formatting are
  covered by 4 unit tests but never seen with real signals.
- Stale restored tabs from an *older* session can still reference components no longer in the
  menu — pre-existing in `TabService`, untouched, not observed to misbehave.
