# T-47 — Workflow Canvas Playground (experimental)

**Started:** 2026-08-22 | **Implementation complete:** 2026-08-23 | **Live-data follow-up:** 2026-08-23 | **Phase 1 (dashboard parity):** 2026-08-24 | **Phase 2 (layout/LOD/properties):** 2026-08-24 | **Phase 3 (selection + actions):** 2026-08-25 | **Phase 4 (board consolidation + animation):** 2026-08-25 | **Phase 5 (toolbar/legend/refresh-rate):** 2026-08-25 | **Phase 6 (home landing + exclusivity, LAST of D17-D24):** 2026-08-25
**Branch:** `feat/workflow-canvas-experiment` — ⚠️ **DO NOT MERGE to `main`** — per D14/D15 this is now a permanent, separately-maintained side branch, rebased onto `main` only on request
**Status:** ✅ **All 17 plan tasks complete, live-verified.** Verdict: **keep, do not merge yet — fix the auto-layout scale problem, then re-evaluate.** 360 tests (up from 227), 17 feature commits `03e2766`..`272faea`. **Follow-up (commit `90d9b8d`):** dashboard-depth live telemetry (power/temperature/capacity/energy/cycle/table/running-time/error text) added to channel nodes + properties dock, no hardware actions — see [sessions/2026-08-23_workflow-canvas-live-data-richness.md](../sessions/2026-08-23_workflow-canvas-live-data-richness.md). **Phase 3 complete (2026-08-25):** marquee selection + real hardware actions (Start/Stop/Pause/Continue via `ChannelActionExecutor`, Transfer via the unmodified `TransferDialog`) — the canvas is now a genuine operating surface, reversing D2. 505 tests, 6 commits `8c36f65`..`70788c9`. **New spec (2026-08-25, D17-D24):** 8 further usability follow-ups — board consolidation, animation correctness, refresh rate, cross-workflow channel exclusivity, canvas-as-login-landing (Dashboard stays untouched at `/Dashboard`), status-color legend, consolidated top-right toolbar. **Phase 4 complete:** Board node shrunk to a compact online/offline chip (position/layout/persisted-graph untouched); flow animation now requires `ProgramStatus.Running`. 510 tests, 4 commits `783fe03`..`ad783bd`.

**D2 reversed (2026-08-24):** a live review of the running canvas found 6 gaps and the user chose
full dashboard parity over the original "layout-only" scope. New spec:
[docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md](../../docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md)
(decisions D9-D16). **Phase 1 (isolation + correctness) complete**, commits `103d189`..`7335b81`,
408 tests — see [sessions/2026-08-24_workflow-canvas-phase1-isolation.md](../sessions/2026-08-24_workflow-canvas-phase1-isolation.md)
and the plan at [docs/superpowers/plans/2026-08-23-workflow-canvas-phase1.md](../../docs/superpowers/plans/2026-08-23-workflow-canvas-phase1.md).
**Phase 2 (lane layout + level-of-detail + node property config) complete**, commits
`adae69f`..`9813434`, 465 tests — see
[sessions/2026-08-24_workflow-canvas-phase2-layout-lod-properties.md](../sessions/2026-08-24_workflow-canvas-phase2-layout-lod-properties.md)
and the plan at [docs/superpowers/plans/2026-08-24-workflow-canvas-phase2.md](../../docs/superpowers/plans/2026-08-24-workflow-canvas-phase2.md).
**Phase 3 (marquee select + `ChannelActionExecutor` + Start/Stop/Pause/Continue/Transfer actions)
complete**, commits `8c36f65`..`70788c9`, 505 tests — see
[sessions/2026-08-25_workflow-canvas-phase3-selection-actions.md](../sessions/2026-08-25_workflow-canvas-phase3-selection-actions.md)
and the plan at [docs/superpowers/plans/2026-08-25-workflow-canvas-phase3.md](../../docs/superpowers/plans/2026-08-25-workflow-canvas-phase3.md).
This was the last phase in the original dashboard-parity spec (D1-D16); `DashboardView.razor`/
`TransferDialog.razor` remain byte-identical to `main`, and `main` has received no commits from
this branch.

**New spec (2026-08-25, D17-D24):** [docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md](../../docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md)
— 8 further usability follow-ups from live use: board-node consolidation into a compact chip,
flow-animation correctness, a user-configurable telemetry refresh rate, per-user cross-workflow
channel exclusivity, making the canvas the login landing page (Dashboard stays reachable,
untouched, at `/Dashboard` — this reverses only the *product* decision that the Dashboard is the
default view, not the *git* decision D14/D15 that this branch never merges to `main`), a
status-color legend, and a consolidated top-right icon-button toolbar. Suggested phasing: Phase 4
(board + animation) → Phase 5 (toolbar/legend/refresh-rate) → Phase 6 (home-landing/exclusivity).

**Phase 4 (board consolidation + animation correctness) complete**, commits `783fe03`..`526c3ca`,
517 tests — see [sessions/2026-08-25_1200_workflow-canvas-phase4-board-and-animation.md](../sessions/2026-08-25_1200_workflow-canvas-phase4-board-and-animation.md)
and the plan at [docs/superpowers/plans/2026-08-25-workflow-canvas-phase4.md](../../docs/superpowers/plans/2026-08-25-workflow-canvas-phase4.md)
(plan's Task 3 shipped, then was directly corrected by the user before this phase closed — see the
session file's "Mid-session correction" section). The Board node's presence in the persisted graph
and `WorkflowAutoLayout`'s column math are unchanged, but it is no longer rendered as any
independent canvas element at all — each board is a small pin dot on its own Device card's edge,
with channel edges redirected to start there.

**Phase 5 (toolbar, legend, refresh rate) complete**, commits `1e4fbc0`..`d45586b`, 531 tests — see
[sessions/2026-08-25_1400_workflow-canvas-phase5-toolbar-legend-refresh.md](../sessions/2026-08-25_1400_workflow-canvas-phase5-toolbar-legend-refresh.md)
and the plan at [docs/superpowers/plans/2026-08-25-workflow-canvas-phase5.md](../../docs/superpowers/plans/2026-08-25-workflow-canvas-phase5.md).
User-configurable telemetry refresh rate, a status-color legend, and the old top toolbar row's
buttons consolidated into a top-right icon box with clickable status-filter chips. Two real bugs
found and fixed during live verification: a config-migration default gap (old persisted configs
lacking the new field deserialize it as 0, not its declared default), and a second occurrence of
this branch's known Razor gotcha — a `string`-typed component parameter bound without an `@`
prefix silently becomes a literal string instead of an expression (first hit in the original
17-task implementation phase; now documented as a pattern to actively watch for on this branch).

**Phase 6 (home landing + cross-workflow exclusivity) complete — the LAST phase in the D17-D24
spec**, commits `6b9a8a9`..`5ed6fdb`, 536 tests — see
[sessions/2026-08-25_1600_workflow-canvas-phase6-home-and-exclusivity.md](../sessions/2026-08-25_1600_workflow-canvas-phase6-home-and-exclusivity.md)
and the plan at [docs/superpowers/plans/2026-08-25-workflow-canvas-phase6.md](../../docs/superpowers/plans/2026-08-25-workflow-canvas-phase6.md).
A channel already placed in one of a user's saved layouts is now disabled (with a naming tooltip)
in every other layout's palette, enforced at placement time so even the bulk "All" button cannot
place a claimed channel; login now redirects to `/workflows` instead of `/`, with the Dashboard
completely untouched at its own `/Dashboard` route. Found during planning that the spec's
originally-sketched D17 mechanism (a `NavMenuItem`/`TabService` change) was an ambiguous-route
conflict with `TabView.razor`'s own `@page "/"` — corrected to a 2-line `Login.cshtml.cs` fix
before any code was written. Live-verified against real leftover cross-session data (a channel
already in the "Test" saved layout showed correctly disabled/skipped when placing in a new
layout) and a real login cycle (user confirmed landing on `/workflows`).

**All 7 sections of the D17-D24 spec are now implemented across Phases 4-6.** Both the original
D1-D16 dashboard-parity spec and this D17-D24 usability spec are fully complete.

---

## New feedback batch (2026-08-26) — 4 sub-projects

A 12-item batch of live-use feedback was decomposed (via brainstorming) into 4 sub-projects rather
than one spec:

- **A — Regression fixes: ✅ DONE**, commit `cc557c7`, 540 tests — see
  [sessions/2026-08-26_0500_workflow-canvas-regression-fixes.md](../sessions/2026-08-26_0500_workflow-canvas-regression-fixes.md).
  Toolbar popup overlap, toolbar actions not working, canvas hang. Two of three were Phase 5
  regressions; the hang was pre-existing but only bit at the scale later phases enabled.
- **B — Canvas presentation: ✅ DONE**, commit `e24c574`, 565 tests — see
  [sessions/2026-08-26_0600_workflow-canvas-presentation.md](../sessions/2026-08-26_0600_workflow-canvas-presentation.md).
  Legend states each status's operational meaning and carries an editable color swatch; overrides
  live on `WorkflowNodeConfig` (canvas-only, per-user) and resolve through one new
  `HslFor(status, overrides)` overload feeding node faces / legend / filter bar / board pins.
  Top bar replaced by `StatusFilterBar`. Line grid. **Node overlap root-caused by measurement:**
  the real card is 106px chrome + 16px per property row = 154px at the 6-property cap, against a
  `RowHeight` of 120 — masked by a stale, entirely unreferenced `NodeHeight = 96` constant (now
  deleted). `RowHeight` is derived from `MaxChannelNodeHeight` so raising the property cap cannot
  reintroduce the overlap. Also extracted `WorkflowColorConversion` to a `.cs` file (Razor cannot
  parse a leading `<` in a relational pattern) with palette round-trip tests.
- **C — Dashboard data parity: not started.** Scope confirmed as "data + dialog first": all
  missing telemetry/config fields on the node, the full multi-tab detail dialog
  (Manufacturing/Factory/Battery/Calibration/DBC), and the DBC parameter view in the properties
  panel. Chart / per-card gear / context-menu deferred to a later pass.
- **D — TabViewer navigation: not started.** ⚠️ The user decided the real navigation model is
  **TabViewer, not per-page browser routes**. This **reopens Phase 6's D17 decision** — the
  `Login.cshtml.cs` redirect-to-`/workflows` fix assumed per-page routing is the model, so D must
  revisit whether that is still the right mechanism.
**Spec:** [docs/superpowers/specs/2026-08-22-workflow-canvas-design.md](../../docs/superpowers/specs/2026-08-22-workflow-canvas-design.md)
**Plan:** [docs/superpowers/plans/2026-08-22-workflow-canvas.md](../../docs/superpowers/plans/2026-08-22-workflow-canvas.md) — 17 TDD tasks, ~7100 lines
**Report:** [docs/workflow-canvas-experiment.md](../../docs/workflow-canvas-experiment.md) — scale measurements, failure-path checks, verdict
**Sessions:** [sessions/2026-08-22_workflow-canvas-design.md](../sessions/2026-08-22_workflow-canvas-design.md) (design+plan), [sessions/2026-08-23_workflow-canvas-implementation.md](../sessions/2026-08-23_workflow-canvas-implementation.md) (build)

---

## Goal

An n8n-style pan/zoom canvas where an operator drags devices, secondary boards, channels and
batteries onto a surface, wires them, attaches a program + optional DBC per channel, and saves
the arrangement as a named layout. Live telemetry animates the graph (status colours, flowing
power edges, filling/shimmering battery SVGs).

The branch exists to answer one question: **does this feel good enough to replace or supplement
the existing dashboard?** If the answer is no, the branch is deleted.

## Decisions (full rationale in the spec, §2)

| # | Decision |
|---|---|
| D1 | Scope = canvas + persistence + nav change. **Analytics page is a separate later spec.** |
| D2 | Canvas records **intent only** — saving never transfers a program or touches hardware. |
| D3 | Styling = one standalone hand-written `wwwroot/css/workflow-canvas.css` (ADR-2: no Tailwind rebuild). |
| D4 | Blazor nodes + SVG edges + custom `workflow-canvas.js` for pan/zoom/drag. |
| D5 | Device/Board/Channel/Battery are **nodes**; Program and DBC are **properties** on the channel node. |
| D6 | Live data reuses `ChannelManager` + `DashboardRenderBatcher`, filtered to on-canvas channels. |
| D7 | Persistence = one `WorkflowLayout` table with a `LayoutJson` document column. |
| D8 | v1 = auto-import, snap-to-grid, minimap, multiple named layouts. **Undo/redo deferred.** |

## Gotchas discovered while designing

- **`app.min.css` has no rebuild pipeline (ADR-2)** — this is what forced the standalone-CSS
  decision. Theme vars are raw HSL triplets (`--status-charge: 51 100% 50%`), so plain CSS can
  write `hsl(var(--status-charge) / .55)` and keep light/dark theming for free.
- **Nav is not route-based.** `MainLayout.razor:147` holds a hardcoded `_menuItems` list of
  `NavMenuItem { Url, Title, ComponentType, Unique, keepAlive }` consumed by `TabService`. A new
  menu entry is a tab registration, not an `@page`.
- **`Display` → `DashboardView` is both the first menu entry and the logo-click target**
  (`Navbar.razor:12`). Hiding the dashboard means gating both, behind `Features:LegacyDashboard`.
- **Telemetry must not trigger a Blazor re-render.** At 64 channels / 5 Hz on Blazor Server, the
  design pushes CSS custom properties via one batched interop call per tick; Blazor re-renders
  only on structural graph change.
- **Animate `stroke-dashoffset`, pseudo-element `opacity`, and `transform: scaleY()`** — never
  `box-shadow` or `height` directly. The naive version of each is a per-frame repaint.
- **`EntityId` is a soft reference, not an FK.** Deleting a device must not cascade into or break
  saved layouts; unresolved ids render as dimmed *stale* nodes. This is the highest-value unit test.

## Spec errors found while planning (corrected in the spec, commit `1978942`)

- **DI is registered in `Extensions/ServiceCollectionExtensions.cs` (~line 101), not `Program.cs`.**
  `Program.cs` only binds the options object.
- **`Components/App.razor` is a fifth modified shared file** — every `<link>` and `<script>` is
  hardcoded there, so the new CSS and JS must be registered in it.
- **`WorkflowNode.EntityId` must be `long?`, not `int?`** — `Channel.Id` and `SecondaryBoard.Id`
  are `long`; `Device.DeviceID` is `int` and widens cleanly.
- Two spec requirements had no task and were added to plan task 13: side-panel collapse (spec 5.1)
  and board-node collapse (spec 5.3, named in the risk table as the mitigation for an unreadable
  64-channel graph). Board collapse is a **view projection**, never a graph edit — a test pins
  that the saved document is untouched.

## Plan shape

Tasks 1-6 pure logic + persistence (no UI, ~66 unit tests). Task 7 is the first browser-visible
milestone. Tasks 8-16 each add one interaction and end with an explicit manual browser check,
because "does this feel good" cannot be asserted in a test. Task 17 is the scale run, the
failure-path checks, and the keep-or-delete verdict.

## Implementation results (2026-08-23)

**The one real design flaw:** `WorkflowAutoLayout` places every node of a kind in one column.
Fine for 64 channels; at 640 channels (10 devices) the column is ~13,000px tall, forcing
`fitToContent` to a 0.2x zoom floor that's illegible while the horizontal viewport sits empty.
Pan/telemetry/edges all measurably held up fine at this scale (3.2ms/60 pan frames at 730 nodes;
zero DOM mutations during 4s of live telemetry at 640 channels) — this is a layout-algorithm gap,
not a performance one. Bounded fix: grid or per-device-lane layout instead of one column.

**Two bugs live browser verification caught that 360 passing tests could not:**
1. `SelectedNodeId="_selectedNodeId"` (missing `@`) — for a `string`-typed Razor parameter, no
   `@` means literal string, not expression. bUnit's `.Add(x => x.Prop, value)` API bypasses
   Razor markup parsing entirely, so this class of bug is invisible to any unit/bUnit test.
2. No initial telemetry paint on an idle bench — `HardwareManagerChanged` only fires on a state
   CHANGE, so a freshly placed node showed static placeholders indefinitely until something else
   happened. Fixed by forcing one push whenever the subscribed channel set changes.

Full write-up, all scale numbers, and every failure-path check: see the report linked above.

## Next step

The branch stays as-is pending a decision on the auto-layout fix. If pursued: replace the
single-column placement, re-run the 640-channel scale check, re-evaluate readability.

---

## Sub-projects C & D complete (2026-08-26, session 35)

Closes the 12-item feedback batch decomposed in session 33 into A/B/C/D. A and B shipped in
sessions 33–34; C and D shipped here. Commits `24b6e1b` (C1), `65c6e86` (C2+C3), `faa2aa1` (D).
589 tests.

### C — dashboard data parity

**C1** widened the node-face property picker from a hand-copied 17 keys to all 28
`CardPreviewData` keys, by *concatenating the three catalogs* (RealTime 12 + Config 3 + Program 13)
rather than maintaining a subset — so it cannot drift from the dashboard again. `ChannelTelemetry`
gained 9 fields, `BuildNodeProperties` 11 keys. The 6-visible cap stays (user's choice).

**C2** added `ChannelDetailDialog.razor` — 5 tabs (Live / Battery / Manufacturing / Factory / DBC)
opened from a new "All details…" button on `PropertiesDock`. Deliberately a **new** component
rather than a reuse of the dashboard's, which is embedded in the 2,000-line `DeviceChannel.razor`
this branch must never modify (D13). The obvious risk of duplicating a surface is silently omitting
a field, so the row content lives in pure `WorkflowChannelDetail.cs` where tests count each tab
against its DTO **by reflection** (`PublicPropertyCount<T>()`) instead of a human diffing two large
Razor files. Manufacturing and Factory are fetched from the DB on open (the same
`IDeviceChannelServices` calls the dashboard uses), not pushed per telemetry tick.

**C3** added `DbcParameterView.razor`, a **documented exception** to this branch's
JS-owns-interaction rule: the decoded signal set is not a fixed list, so there are no stable
`data-role` slots to write into and it must re-render through Blazor. Signals are ordered by name
(an unordered dictionary reshuffles between ticks) and formatted invariant-culture (a de-DE machine
would render `51.2` as `51,2`, read as `512` by everyone else).

### D — TabViewer navigation

The root cause was **not** routing. `TabService` seeds its landing tab **in code**, hard-coded to
`DashboardView` — so `Features:LegacyDashboard=false`, already set in
`appsettings.Development.json`, hid the dashboard's *menu entry* but could never stop it greeting
users. In this app the landing page is a **tab decision, not a route decision.**

Phase 6's D17 fix was wrong twice over: `WorkflowCanvasPage` carries `@page "/workflows"`, so
`Redirect("/workflows")` rendered the canvas **bare — outside the TabViewer shell**, with no tab
bar and no navbar. Both redirects now go to `/`, and the seed comes from a new testable
`WorkflowDefaultTab`, placed outside main-owned `TabService.cs` for the same reason `WorkflowMenu`
exists. Defaults still reproduce main's behaviour exactly (canvas off → `DashboardView`/"Home").

### The bug my own rename exposed

Retitling the seed produced **two identical non-closable "Workflows" tabs**.
`NotifyStateChanged` excluded the seed from persistence with
`store.RemoveAll(e => e.Title == "Home")` — a literal whose real job was "don't persist the seed,
the constructor re-creates it every circuit". The rename slipped past that filter, so the seed was
persisted and then restored beside the fresh one. Now keyed on the `_defaultTabTitle` field the
constructor actually used, so the two cannot drift again.

Related invariant: `TabService.AddTab` dedupes `Unique` tabs **by title**, so the seed's title must
stay byte-identical to its menu entry's or clicking "Workflows" opens a second canvas tab. Two
tests pin this by selecting the menu entry on one axis and asserting the other, so neither can pass
tautologically. (An early version of those tests wrongly assumed `items[0]` — `LegacyDashboard`
defaults to *true*, so "Display" still leads the menu in that configuration.)

### Verification

- **C2/C3** live at 640-channel scale: Live 28 rows, Battery 15, Manufacturing 10, Factory 16,
  DBC empty-state. Dialog inside the viewport, `z-index` 41 above the toolbar dock's 21, body
  scrolls. Refetch-on-different-channel confirmed against the SQLite DB as ground truth — a
  channel showing all `--` turned out to hold genuinely empty seed data, and the one row that
  looked wrong (`Last Synced` populated where the channel's column is NULL) is correct by design:
  `GetManufacturingAsync` deliberately falls back to `device.LastSyncedAt`.
- **D** live: one non-closable Workflows tab at `/`, no Display nav entry, nav-click activates
  rather than duplicates, a user tab opens beside it, and **a reload with persisted state restores
  the user tab without duplicating the seed** — the exact scenario that produced the bug.
- 589/589 tests, console clean.

### Outstanding

- `DbcParameterView` was verified live only in its **empty state** — no channel on the bench had a
  transferred DBC file. Row rendering, ordering and culture formatting rest on 4 unit tests alone.
- The dashboard's dialog has further tabs (charts/records) not brought over; the user's ask was the
  fields and the multi-tab dialog, which is met.
- Stale restored tabs from an older session can still reference components no longer in the menu —
  pre-existing in `TabService`, untouched, not observed to misbehave.

---

## Live-test defect round (2026-08-26/27, session 36)

Eleven defects from the user hand-testing at a 1-device / 1-board / 8-channel bench. Commits
`b5cbfe2`, `dce999a`, `17c256e`, `4ba9672`. 597 tests.

### The two findings that matter most

**`RefreshRateMs` was a throttle ceiling, not a clock.** Nothing on the canvas ever pushed telemetry
on a timer — pushes happened only on `HardwareManagerChanged` (registration / accept / link-drop /
command-send) or `OnChannelChanged`. On an idle bench neither fires, so the canvas froze until the
page was reloaded, and *setting the refresh rate could not cause an update*, which is precisely why
the control looked broken. A `System.Threading.Timer` now drives pushes at `RefreshRateMs` and is
retimed when the user changes it. Measured: exactly 3000ms cadence at 3000ms; live movement on an
idle bench with no program running.

**Scripted clicking cannot test click handling.** Toolbar / minimap / action-bar buttons were dead
to real clicks: they are DOM children of `.wf-canvas-host`, so their `pointerdown` bubbled into the
canvas handler, fell through to `beginMarquee`, and `setPointerCapture` on the host stole the
`pointerup` — so the browser never synthesised a `click`. It also fired `OnSelect(null)` on release,
silently clearing the selection. A programmatic `el.click()` dispatches a click directly and
therefore always succeeded, which is why this survived every prior verification. **Use the CDP-level
click for any "does this button work" check on this branch.**

### The rest

- **Panning was unreachable**, not broken: Phase 3 (D12) made left-drag a marquee and moved panning
  to middle-button / Space+drag, neither discoverable. Left-drag now pans; marquee sits behind an
  explicit toolbar toggle; middle-button and Space+drag pan in either mode; the cursor reports the
  armed gesture (grab / grabbing / crosshair).
- **The field cap is gone** — 6 → 12 → all 28, tied to `AvailableKeys.Count` so a new
  `CardPreviewData` field can never be permanently unselectable. Safe only because card height,
  `RowHeight` and the edge attachment point all derive from it; `MaxChannelNodeHeight` / `RowHeight`
  became `static readonly`.
- **The measured card constants were wrong and had looked right.** Re-measuring 2/4/6/8/10/12
  properties gave 114/134/154/174/194/214 — `94 + 20 per row`. The previous `106 + 16` reproduces
  154 at three rows *and only at three rows*, so sub-project B's single six-property measurement
  validated cleanly while being wrong at every other count. **One measurement cannot pin a
  two-parameter linear model.**
- **Units**: the four capacities and four energies are `Ah`/`AhCha`/`AhDch`/`AhStep` and
  `Wh`/`WhCha`/`WhDch`/`WhStep` (`OperatorConstants.ValidUnits`, `MeasurementData` 0x26–0x2D), not
  four identical Ah and four identical Wh. The node face renders values with **no visible labels**,
  so the token is the only thing distinguishing eight rows. New `WorkflowNodeProperties.UnitFor`
  plus `LabelWithoutUnit` for the detail dialog, and a hover title per value. (I first mis-read the
  report and *removed* the units as duplication; the user supplied the real vocabulary.)
- **Edges** had three faults: a hardcoded channel height of 108 against a real 154+; pin edges keyed
  on an unrendered board so a device drag stranded them (now `data-pin-owner` + `data-pin-frac`);
  and — the deeper one — **card height is partly a CSS concern**, because the zoom-driven LOD tiers
  collapse cards and no server-side formula can know that, leaving edges 152px low. JS now
  re-derives every path from measured DOM on a tier change and after a render that resizes cards.
  0px error at full detail and at the reduced tier.
- **Palette listed 11 devices** for a 1-device bench — Circuits-page delete marks CHANNELS deleted
  and leaves every `Devices` row live. New pure `TopologySnapshot.WithPlaceableDevicesOnly()`,
  applied to the **palette only**: the labeller and validators resolve existing nodes and would
  report already-placed hardware as stale.
- **Dock/palette collapse button dead when nothing was selected**: `.wf-collapse` was `float: right`,
  which leaves normal flow, so the following block overlapped its box and — being later in DOM order
  — painted on top and swallowed the click. That block is the "Select a node…" placeholder, which is
  exactly why it failed only with an empty selection. Both panels are flex columns now.
- **Missing `online` filter chip** — not a `CircuitStatus` member (the dashboard defines it as the
  complement of Offline), so the enum loop could never produce it. Both dim paths share one matcher.
- **The device→channel flow animation** had three stacked blockers: it targeted `.wf-edge--power`
  exclusively (so the only edge most layouts have was never considered); `stroke-dasharray` lived
  only on that class, making an animated `stroke-dashoffset` invisible on a solid line; and
  `FlowFor` required `|current| >= 0.001`, which killed it on a channel reporting Running + Charge
  at 0.00 A. Direction comes from `CircuitStatus`, never the sign of the current, so that parameter
  is gone. D20 still holds: animation ⇔ program running.
- **Battery palette read the wrong table** (`BatteryTypes`, empty) instead of `Batteries`, so it
  reported "No battery types configured" and no battery could ever be placed.

### Environment gotchas worth repeating

- **`?version=` on `workflow-canvas.js` / `.css` in `App.razor` is hand-maintained.** An asset edit
  ships to nobody until it is bumped — a correct build tested as *not applied* because the browser
  served the cached `version=0.4`. Now js `0.8` / css `0.5`. Bump it in the same edit as the asset.
- **Grep build output for `error`, not `error CS`** — a running app locks the exe and fails with
  `MSB3027`/`MSB3021`, which an `error CS` filter silently reports as a clean build.
- **Measure after the render settles** — card heights read 70px mid-render, 154/374px once the
  telemetry paint lands. Two "bugs" this session were transient reads.
- **Compare against `/Dashboard`** (still routable with its nav entry hidden) before blaming the
  canvas.
- **Transfer must precede Start** (user-confirmed). That, not a bug, explains the earlier
  "channels report Stop immediately".

### Outstanding

- 10 stale `Devices` rows plus junk `X⌂DEVICE_001_3` (DeviceID 19) remain at `IsDeleted=0`. Hidden
  from the palette but not deleted — a raw SQL delete was blocked by the permission classifier and
  the user has not picked a route. DB backed up to `D:/MEWebApp/BtsAppdb.backup-20260826.db`.
- Per-user status-colour overrides saved 2026-08-24 are active (charge magenta `300 100% 50%`, idle
  pink, offline grey-blue). Deliberate, left alone — flowing wires render magenta because of it,
  which is the single-`HslFor`-path design working.
- `DbcParameterView` still verified live only in its empty state.
