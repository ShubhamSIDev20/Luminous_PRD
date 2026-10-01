# Workflow Canvas — Dashboard Parity Design

**Date:** 2026-08-23
**Branch:** `feat/workflow-canvas-experiment` — ⚠️ **never merged to `main`; maintained as a permanent side branch**
**Supersedes parts of:** [2026-08-22-workflow-canvas-design.md](2026-08-22-workflow-canvas-design.md) — specifically decision **D2**
**Status:** Approved in chat (all six sections), pending implementation plan

---

## 1. Context

The workflow canvas shipped as an experiment (T-47, 17 tasks, commits `03e2766`..`272faea`, plus
live-data follow-up `90d9b8d`). A live review of the running app against the real dashboard
surfaced six concrete gaps. All six trace to one root cause: **the canvas was designed as a
layout playground, and the user wants an operating surface.**

That reframing reverses D2 and is the reason this is a new spec rather than a patch to the old one.

### Gaps found in live review

| # | Symptom | Verified cause |
|---|---------|----------------|
| G1 | Every device node reads "offline" regardless of hardware | `DeviceNode.razor` declares an `IsOnline` parameter; `CanvasSurface.razor:24-26` never passes it, so it silently defaults to `false`. Same class of bug as the `SelectedNodeId` missing-`@` defect from session #25 — a parameter that exists but is unwired. |
| G2 | Channels read "Channel 579", boards read "Board 76" | `WorkflowCanvasPage.LabelFor` interpolates `node.EntityId` — the **database primary key** — instead of the physical address. The correct values (`TopologyDevice.DeviceId`, `TopologyBoard.BoardNumber`, `TopologyChannel.ChannelNumber`) are already loaded in `TopologySnapshot` and never consulted. |
| G3 | No way to see all channels on one screen | `WorkflowAutoLayout` places every node of a kind in one column. 640 channels ⇒ ~13,000px tall column, `fitToContent` pinned to its 0.2x floor, horizontal viewport unused. Known from the T-47 report; unresolved. |
| G4 | Operator cannot choose which properties a node shows | Never built. The dashboard has this (28 properties); the canvas hardcodes four. |
| G5 | No Start / Stop / Pause / Continue / Transfer | Deliberate, per D2. The user has now reversed that decision. |
| G6 | Missing card footer data (program status, last update) | Never built. `DeviceChannel.razor:1301-1327` shows both. |

### What the existing dashboard actually does

Established by direct source reading, not assumption. These are the behaviours the canvas must
match; every one of them is reused rather than reimplemented.

- **Channel address format** is an inline interpolation repeated at each render site —
  `@Channel.Channel.DeviceID-@Channel.Channel.SecondaryBoardNumber-@Channel.Channel.ChannelNumber`
  (`DeviceChannel.razor:39`, `:51`, `:1268`; also `DashboardView.razor:874`). There is no shared
  helper. Selection keys use the same 3-tuple everywhere.
- **Online/offline is defined three different ways** and they can disagree:
  1. the card's status badge — `GetCircuitStatus()` (`DeviceChannel.razor:1864-1869`) combines
     `IsConnected` + `CircuitStatus` + `ProgramStatus`; **disconnected while a program is still
     Running yields `Error`, not `Offline`**;
  2. the card's green/red dot — raw TCP `IsConnected` only (`DeviceChannel.razor:1274-1277`);
  3. the header "Online: N" chip — ignores `IsConnected`, counts `CircuitStatus != Offline`
     (`DashboardView.razor:60-62`).
- **28 selectable card properties**, gated by `ShouldRenderProperty` (`DeviceChannel.razor:1035-1039`,
  where an empty list means "show everything"). Canonical metadata lives in `CardPreviewData.cs`,
  whose own comment (lines 5-11) states it exists so `CardSettings` and `CardConfiguration`
  "never fall out of sync". Persisted server-side via `ServerSessionStorageService` under
  `{UserName}_card_config` (`CardConfiguration.razor:391`, `:415`).
- **Actions are already unified.** `DashboardView.DoAction(string)` (`:688-921`) is the single
  path for both one channel and many — right-clicking any card opens a `ContextMenu` that acts on
  the whole `selectedCircuits` set. Concurrency is one `SemaphoreSlim` per `DeviceID`
  (`DashboardView.razor:239`): channels on a device run sequentially, devices run in parallel.
  Per-item enablement comes from `CanContextAction` (`:1039-1058`).
- **That concurrency pattern already exists twice.** `TransferDialog.HandleTransfer`
  (`TransferDialog.razor:327-470`) re-implements it independently, its own comment noting it
  "matches the Start/Stop/Pause/Continue pattern". Neither copy has direct unit tests, because
  both live inside Razor files.
- **`ChannelActionBar.razor` appears orphaned.** `DashboardView` computes `EffectiveActionState`
  and `IncompatibleCircuitCount` for it (`:976-1030`), but no `<ChannelActionBar>` tag is rendered
  anywhere. Flagged for the team; not adopted by this design.

---

## 2. Decisions

Continues the numbering from the original spec (D1–D8).

| # | Decision |
|---|----------|
| **D9** | **D2 is reversed.** The canvas becomes a real operating surface with full dashboard parity: select one or many channels, then Start / Stop / Pause / Continue / Transfer. It is a dashboard *replacement* candidate, not a supplement. |
| **D10** | Layout is **one horizontal lane per device** with a channel grid inside, plus **three zoom-driven level-of-detail tiers**. LOD is implemented in **CSS only** — never as C# state. |
| **D11** | Node-face properties are **separately configured** (`{UserName}_canvas_node_config`, hard cap of 6) but drawn from the **same `CardPreviewData` catalog** as the dashboard, so labels and icons cannot drift. The dock always shows all 28. |
| **D12** | Multi-select is a **rubber-band marquee** on empty canvas, Ctrl/Shift+click to add or remove, and Device/Board headers as select-all shortcuts. Panning moves to middle-mouse and space+drag. |
| **D13** | The per-device concurrency pattern is extracted into a new **`ChannelActionExecutor`** service. **The canvas is its only consumer for now** — `DashboardView` and `TransferDialog` are deliberately left untouched (rationale below). |
| **D14** | The branch is **structurally isolated from `main`** before any feature work: own `WorkflowDbContext` with its own migration history, DI collapsed to a single call site, `MainLayout` intrusion minimised. |
| **D15** | The branch is **rebased onto `main` on request only**, never automatically. |
| **D16** | Channel online-state uses the **card's `GetCircuitStatus()` rule** (definition 1 above), including its disconnected-mid-run ⇒ `Error` case, because that is the definition operators already read. |

### Rationale for D13 (why not migrate the dashboard now)

Extracting the executor *and* migrating all three call sites would give the cleanest end state, but
it refactors working production code on a branch that by policy never merges. If the branch is
eventually deleted, that refactor is wasted; if it regresses, it regresses the live dashboard.
Building the service and pointing only the canvas at it captures the design benefit — one
testable implementation instead of a third copy-paste — while betting nothing on an experiment.
Migrating `DashboardView` and `TransferDialog` onto it is a follow-up task, conditional on the
branch being judged worth keeping.

---

## 3. Section 1 — Branch isolation

### Problem

The branch and `main` are on a collision course. Measured over `main`'s last 40 commits, `main`
modified **every file this branch touches**:

| Shared file | `main` edits / 40 commits | This branch's diff |
|---|---|---|
| `Extensions/ServiceCollectionExtensions.cs` | 8 | +6 |
| `Components/Layout/MainLayout.razor` | 5 | +55 / −11 |
| `Migrations/AppDbContextModelSnapshot.cs` | 4 | +49 |
| `Components/App.razor` | 4 | +2 |
| `Data/AppDbContext.cs` | 3 | +2 |
| `appsettings.json` | 3 | +6 |
| `Program.cs` | 2 | +5 |

`AppDbContextModelSnapshot.cs` is the worst of these. It is an EF-**generated** file rewritten
wholesale by every migration, and `main` has already shipped three. Git cannot merge it
meaningfully; the standard recovery is to delete the local migration, rebase, and regenerate.

### Design

**Own DbContext.** New `Data/WorkflowDbContext.cs` holding `DbSet<WorkflowLayout>`, targeting the
same SQLite file, with migrations under `Migrations/Workflow/`. `AppDbContext` reverts to
untouched; the `20260822152559_AddWorkflowLayout` migration and its snapshot changes are deleted.

> **EF gotcha that must be handled explicitly:** by default both contexts share one
> `__EFMigrationsHistory` table and will fight over it. `WorkflowDbContext` must declare
> `MigrationsHistoryTable("__EFMigrationsHistory_Workflow")`.

> **Destructive step, requires operator confirmation at execution time:** the `WorkflowLayout`
> table already exists from the old migration, so the new context's first migration fails on
> "table already exists". The table must be dropped and recreated under the new context, which
> **deletes all saved layouts**. Mitigation: dump existing rows to JSON first and re-import after.

**DI collapsed to one line.** New `Extensions/WorkflowServiceCollectionExtensions.cs` exposing
`AddWorkflowCanvas(this IServiceCollection, IConfiguration)`, absorbing the six existing DI
registrations, the options binding currently in `Program.cs`, and the new DbContext.
`ServiceCollectionExtensions` — `main`'s single hottest file — retains exactly one stable line.
`Program.cs` returns to zero canvas lines.

**MainLayout minimised.** Menu registration and legacy-dashboard gating move into a
`WorkflowMenu` helper in canvas-owned code; `MainLayout.razor` drops from +55/−11 to ~3 inserted
lines.

`App.razor` (+2 asset tags) and the two appsettings flag blocks are left as-is — too small to be
worth indirection.

**Target:** shared surface goes from 7 files / ~130 lines to ~4 files / ~10 lines, with **zero**
EF-snapshot involvement.

---

## 4. Section 2 — Phase 1: correctness fixes

Independently shippable; touches no layout, no selection, no hardware.

**Physical addressing (G2).** `LabelFor` stops using `EntityId`. Channels render as `1-8-2`,
byte-identical to the dashboard's format so the two surfaces read the same. Boards render as
`Board 8`; devices keep their name. The channel lookup already exists — `_channelKeys`, built for
telemetry — and needs only a board-level twin.

**Device and board online state (G1).** `CanvasSurface` begins passing the `IsOnline` parameter it
currently drops. Rather than a binary, device and board nodes show an **online count**
(`6 / 8 online`), which is more informative at a glance and degrades sensibly. Channel nodes use
the D16 rule.

**Card-footer parity (G6).** Channel nodes gain program status (Running/Stop, with the card's icon
and colour) and the last-update timestamp as `HH:mm:ss`. The timestamp is load-bearing: without a
clock, a frozen reading is indistinguishable from a live one. Error/message text already exists.
The system-error **reset button is deferred to Phase 3** — it is an action, not a display.

---

## 5. Section 3 — Lane layout and level-of-detail

**Layout.** `WorkflowAutoLayout`'s single-column placement is replaced by **one horizontal lane per
device**: device node at the lane's left edge, boards beside it, channels in a grid **8 across** —
mirroring the physical 8-channels-per-board reality, so the grid carries meaning rather than being
an arbitrary wrap. Lanes stack vertically.

For the 10 × 64 worst case this takes the graph from a ~13,000px column to roughly 8 tiles wide by
~7,200px tall at full detail. That alone does not achieve "one screen"; LOD does, bringing the same
640 channels to roughly ~2,000px at the smallest tier.

**Three tiers:**

| Zoom | Node shows |
|------|-----------|
| ≥ 0.8 | Full card — address, status pill, configured properties, badges |
| 0.4 – 0.8 | Address + status pill + first two configured properties |
| < 0.4 | Colour tile only; address on hover |

**LOD must be pure CSS.** If the tier were C# state, every zoom tick would re-render all 640 nodes
and destroy the no-re-render guarantee that makes the canvas viable on Blazor Server. Instead JS
stamps `data-lod="0|1|2"` on `.wf-world` when zoom crosses a threshold, and the stylesheet hides
rows. The node always renders its full content. This is the same mechanism already proven for
telemetry (zero DOM mutations measured at 640 channels).

---

## 6. Section 4 — Node property configuration

A new `{UserName}_canvas_node_config` — same `ServerSessionStorageService` mechanism as the card
config, separate key — holds a maximum of 6 properties for the node face, chosen from
**`CardPreviewData`'s existing dictionaries**. Sharing that catalog is the point: labels and icons
cannot drift between dashboard and canvas. Default is the current four (Voltage, Current, Power,
Temperature). The properties dock continues to show all 28 regardless.

Editing happens in a small dialog off the canvas toolbar, mirroring the dashboard's gear-icon →
`CardSettings` pattern rather than introducing a new interaction.

**Consequence for existing code.** `TelemetryEntry` currently has ~20 individually-named fields,
which only works because the visible set is hardcoded. User-configurable properties require it to
become a **key → formatted-value dictionary**, with node faces rendering `data-role="prop-{key}"`
slots. This is a better design — a 29th property becomes free rather than a change in five files —
but it **replaces rather than extends** the `TelemetryEntry` shape built in commit `90d9b8d`, and
its unit tests will need rewriting.

---

## 7. Section 5 — Marquee selection

**Gesture remap.** Drag on empty canvas becomes a rubber-band marquee; panning moves to
middle-mouse drag and space+drag. Wheel-zoom is unchanged. On an operating surface, selection is
the primary verb and earns the default gesture.

**Where selection lives.** `_selectedNodeId` (single string) becomes a set. The same
JS-owns-transient / C#-owns-committed split applies: the marquee rectangle and selected-outline
highlight are pure JS/CSS painted with no server round-trip; on mouse-up JS reports the final id
list to C# in **one** interop call. Dragging a box across 64 channels must not cause 64 re-renders.

**Rules are reused.** `CircuitSelectionLogic` already encodes them: offline circuits cannot be
selected (`DashboardView.razor:409`), and select-all only accepts circuits matching the anchor's
`CircuitStatus` + `ProgramStatus`, with `IncompatibleCircuitCount` reporting the remainder. The
canvas routes through it, so a marquee over mixed channels selects the valid ones and reports
plainly: *"12 selected, 3 skipped (offline)."* **Stale nodes are never selectable** — there is no
hardware behind them.

**Parent shortcuts.** A Board node's header selects its 8 channels; a Device node selects all of
its channels — both filtered by the same rules.

---

## 8. Section 6 — Actions

**`Services/Implementations/ChannelActionExecutor.cs`** (new). Owns the per-`DeviceID`
`SemaphoreSlim` map and exposes
`ExecuteAsync(action, circuits, progress, cancellationToken)` with the established semantics:
sequential within a device, parallel across devices. Because the pattern currently exists only
inside Razor files, extracting it makes it **directly unit-testable for the first time** — the
highest-value new test surface in this spec.

**Two entry points, one selection:**
- **Right-click context menu** — parity with the dashboard's `ContextMenu`: Transfer, Start, Stop,
  Interrupt, Continue, Calibration, with per-item enablement from the existing `CanContextAction`
  rules.
- **Selection action bar** — appears when anything is selected, showing the count, the action
  buttons, and live progress. Built fresh for the canvas rather than adopting the orphaned
  `ChannelActionBar.razor`, whose status must be confirmed with the team first.

**Reused wholesale:** `TransferDialog` already accepts a `selectedCircuits` list, so Transfer
merely opens it — no changes to that component. `Processing.razor` supplies the progress counter
(it already has `Completed`/`Total`); `TransferResultsDialog` reports failures. The deferred
system-error reset button lands here.

**Confirmation policy.** The dashboard fires Start/Stop across 64 channels with no confirmation,
because operators expect immediacy; the canvas matches that, since diverging would itself be a
surprise. Noted as a deliberate choice rather than an oversight: a canvas makes accidental
large selections easier than a card grid does, so a threshold-based confirm is a reasonable future
addition if operator feedback asks for it.

---

## 9. Phasing

| Phase | Sections | Deliverable |
|-------|----------|-------------|
| **1** | 1 + 2 | Branch isolated from `main`; labels, online state, and footer parity correct. Independently shippable and judgeable. |
| **2** | 3 + 4 | Lane layout, level-of-detail, configurable node properties. Answers "see it all on one screen". |
| **3** | 5 + 6 | Marquee selection and the full action set. Canvas becomes an operating surface. |

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Dropping `WorkflowLayout` deletes saved layouts | Dump rows to JSON before the migration switch; re-import after. Confirm with the operator at execution time. |
| Two DbContexts on one SQLite file collide over migration history | Explicit `MigrationsHistoryTable("__EFMigrationsHistory_Workflow")`. |
| LOD accidentally implemented as C# state | Pinned by design (§5) and by a test asserting zoom changes produce zero DOM mutations, matching the existing telemetry test. |
| Marquee selection re-renders per node | Single batched interop call on mouse-up; same measurement approach as telemetry. |
| Real hardware commands issued from a layout tool | Offline and stale nodes are unselectable; enablement reuses `CanContextAction`; execution reuses the proven per-device serialization. |
| `TelemetryEntry` rework regresses the `90d9b8d` live-data feature | Rewrite its tests alongside, not after; the dock's 28-row display is the regression canary. |
| Branch drifts from `main` | Isolation (§3) plus rebase-on-request (D15). |

---

## 11. Known gaps carried forward

- **No state-of-charge exists in this schema.** `RealTimeRecordDto` has `Capacity` in Ah, not a
  percentage. The canvas reports `Soc = 0.0` honestly rather than fabricating it. Unchanged from
  the original spec; still a real gap for any future SoC source.
- **The dashboard's three conflicting online definitions** are not reconciled by this spec — the
  canvas simply picks one (D16). Worth raising with the team separately.
- **`ChannelActionBar.razor` orphan status** needs a team answer.
- **`DashboardView` / `TransferDialog` still hold duplicate concurrency code** by deliberate choice
  (D13); migrating them is a conditional follow-up.
