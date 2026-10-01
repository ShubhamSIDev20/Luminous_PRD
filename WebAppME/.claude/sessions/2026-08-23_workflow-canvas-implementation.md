# Session #25 — Workflow Canvas Playground: full implementation (T-47)

**Date:** 2026-08-23
**Agent:** Claude (Opus 5)
**Branch:** `feat/workflow-canvas-experiment` — ⚠️ **NOT MERGED, do not merge without an explicit decision**
**Task:** [T-47](../tasks/2026-08-22_workflow-canvas-playground.md)
**Status:** ✅ All 17 plan tasks complete, live-verified in browser at every step. Experiment report written with a "keep, fix layout, re-evaluate" verdict — not a clean "ship it."

---

## Goal

Execute the 17-task implementation plan from session #24 for the experimental n8n-style workflow
canvas: drag/drop device→board→channel+battery topology, live animated telemetry, saved layouts,
a new Workflows menu entry, legacy dashboard hidden behind a flag.

## What was done

Executed the plan inline, task by task, each with: write failing tests → verify red → implement →
verify green → full suite → **live browser verification against the real app, real database, and
running hardware simulator** → commit. 17 commits, one per task, `03e2766`..`272faea`.

Test count grew 227 → 360 across the branch (133 new tests this session). Zero regressions. One
pre-existing test (`DashboardRenderBatcherTests.TheSameCardRequestingRepeatedly_RendersOncePerFlush`)
flaked once under full-suite parallel load, passed in isolation and on a full-suite rerun —
confirmed pre-existing timing flakiness, unrelated to this branch.

**Final scale check:** all 11 real devices placed simultaneously — 730 nodes (10 device + 80
board + 640 channel). Pan cost was flat (3.2ms for 60 frames at 730 nodes, same order as at 73).
Zero DOM mutations during 4s of live telemetry at 640 channels — the no-re-render architecture
held at 10x the scale it was designed against.

Full report: [docs/workflow-canvas-experiment.md](../../docs/workflow-canvas-experiment.md).
**Verdict: keep the branch, do not merge yet — fix the auto-layout scale problem first, then
re-evaluate.** Not a clean pass, not a delete-it either.

## The one real design flaw found

`WorkflowAutoLayout` places every node of a kind in ONE column. Fine for 64 channels (one
device). At 640 channels (10 devices) the column is ~13,000px tall and 200px wide, forcing
`fitToContent` to zoom to its 0.2x floor — illegible, and the layout never uses the horizontal
space that's sitting empty. This is a layout-algorithm limitation, not a performance one: pan,
telemetry, and edges all measurably held up fine at this scale. A production version needs a
grid or per-device-lane layout instead of a single column — a bounded fix, not a re-architecture.

## Two real bugs found ONLY by live browser verification (invisible to 360 passing tests)

1. **`SelectedNodeId="_selectedNodeId"` (Task 11).** For a `string`-typed Razor component
   parameter, a bare value without `@` is a LITERAL STRING, not an expression — every other
   parameter on the same page (non-string types) auto-detects as an expression regardless, which
   is exactly why this one silently broke while nine others worked. Every node compared its id
   against the literal text `"_selectedNodeId"` and never matched, so clicking a node visibly did
   nothing. bUnit's parameter API (`.Add(x => x.Prop, value)`) bypasses Razor markup parsing
   entirely, so this class of bug is uncatchable by any unit or bUnit test — only rendering the
   real page and clicking a real node found it. Fixed: `SelectedNodeId="@_selectedNodeId"`.

2. **No initial telemetry paint on an idle bench (Task 15).** `HardwareManagerChanged` only fires
   on a hardware STATE CHANGE. A node placed on a quiet simulator bench got no event to ride on
   and showed static placeholder text ("-- V", "OFFLINE") indefinitely — server-side
   `PushTelemetryAsync` was correctly producing entries the whole time, but nothing had triggered
   it since the last real hardware event. Fixed: `RefreshSubscription()` now forces one immediate
   telemetry push whenever the subscribed channel set changes, so a placed node always shows its
   current state right away.

## Other gotchas discovered while building

- **`@layout` is a reserved Razor directive keyword.** Naming a `@foreach` loop variable `layout`
  made `@layout.Id` parse as the `@layout` directive instead of a member access, producing four
  cascading RZ99xx/RZ1011/RZ2001/RZ2005 compiler errors with zero apparent connection to the real
  cause (`LayoutSwitcher.razor`). Renamed the loop variable to `saved`.
- **Real `ChannelManager` API differs from what the plan guessed.** `HardwareManagerChanged` is a
  plain `Action` (no `EventHandler` args), already coalesced by `ChannelManager`'s own flush
  timer before it fires. `CoalescingRunner.Request(Func<Task>, Action<Exception>?)` takes the
  work delegate per call — there is no constructor-interval overload. Circuits are matched by
  `(DeviceID, SecondaryBoardNumber, ChannelNumber)`, never by database `Channel.Id` — a lookup
  dictionary is built once from the already-loaded `TopologySnapshot`.
- **No state-of-charge percentage exists anywhere in this schema.** `RealTimeRecord.Capacity` is
  Ah, not %. Reported as `0.0` honestly in live telemetry rather than fabricated; flagged in the
  report as a real gap for any future SoC source integration.
- **Real entity property names differed from the plan's guesses:** `BtsPrograms.Id`/`ProgramName`
  (both `long`/`string`), `DbcFileRecord.Id`/`Name`, `BatteryType.Id` (the only `int` id of the
  three) /`Name`. Program and DBC ids are `long` in the DB; cast to `int` at the `NodeAttachment`
  boundary rather than widening the document schema.
- **`ToastService`'s real API is `Toast.Error/.Warning/.Success`**, not `ShowError` as the plan
  guessed — found by grepping `DashboardView.razor` before wiring, so it worked on the first try.
- The gortex `edit_file` MCP tool silently wrote to a stale/wrong location once this session
  (reported success, real file on disk untouched, confirmed by checking mtime). Abandoned it for
  the rest of the session in favor of direct Python/Bash file edits, which worked reliably
  throughout.
- The Bash tool runs in an isolated sandbox with its own network namespace — a `dotnet run`
  launched via Bash is unreachable from the real Chrome browser used for verification. All app
  launches for browser testing this session went through the PowerShell tool instead (which runs
  on the real host), started with `-WindowStyle Hidden` and `-RedirectStandardOutput/Error` to
  scratchpad log files, one PID file (`scratchpad/wf_app.pid.txt`) tracked and stopped/restarted
  before every rebuild (the running app locks its own build output).

## Files created this session

17 commits' worth — see `git log --oneline 03e2766..272faea` for the full list, or the plan file
for the per-task breakdown. Headline new pieces: `Services/Implementations/Workflow/*` (13
files), `Components/UI/WorkflowCanvas/**` (13 files), `Components/Pages/Workflows/
WorkflowCanvasPage.razor`, `wwwroot/{css,js}/workflow-canvas.*`, one migration
(`AddWorkflowLayout`), `docs/workflow-canvas-experiment.md`.

## Files modified (shared, non-workflow code)

`Components/Layout/MainLayout.razor`, `Components/Layout/Navbar.razor`, `Components/App.razor`,
`Extensions/ServiceCollectionExtensions.cs`, `Program.cs`, `Data/AppDbContext.cs`,
`appsettings.json`, `appsettings.Development.json` — all small, additive, and gated behind the
`Features:WorkflowCanvas` / `Features:LegacyDashboard` flags. `main` itself is untouched; this is
all on the experiment branch.

## Next step

The branch stays as-is until a decision is made on the auto-layout fix. If pursued: replace
`WorkflowAutoLayout`'s single-column-per-kind placement with a grid or per-device-lane layout,
re-run the same 640-channel scale check, and re-evaluate readability. If not pursued, the branch
and its four-to-five touched shared files revert cleanly (`git diff main...feat/workflow-canvas-experiment`
against those files is small) and the migration can be dropped with `dotnet ef migrations remove`.
