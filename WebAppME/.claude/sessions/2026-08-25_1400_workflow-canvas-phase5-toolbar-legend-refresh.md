# Session — T-47 Phase 5: Toolbar, Legend & Refresh Rate

**Date:** 2026-08-25
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` (permanent, never merged to `main` — D14/D15)

## Goal

Implement Sections 3, 6, 7 (D21/D23) of `docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md`:
a user-configurable telemetry refresh rate, a status-color legend, and a consolidated top-right
icon-button toolbar replacing today's top bar (including status-color filter chips).

## Status

✅ All 4 Phase 5 tasks complete, live-verified end to end against the simulator. Found and fixed
two real bugs during live verification (both documented below). 531 tests (up from 517 at Phase
4's end), 4 commits `1e4fbc0`..`d45586b`.

## What was done

1. **`WorkflowNodeConfig.RefreshRateMs`** (default 250) + pure `WorkflowTelemetryBridge
   .ShouldThrottle(lastPushUtc, nowUtc, refreshRateMs)` gating `PushTelemetryAsync` — skips a push
   if the configured window hasn't elapsed. Deliberately does **not** touch the shared
   `CoalescingRunner` (ADR-6, also used by the Dashboard) — this throttle is a separate check in
   front of the runner's own work, entirely local to the canvas page. Commit `1e4fbc0`.
2. **`WorkflowTelemetryBridge.HslFor(CircuitStatus)`** — exposes the exact palette `BuildEntries`
   already uses, so the legend/filter chips can never show a color that doesn't match a real node.
   New `StatusLegend.razor` popover (not wired into the page until Task 3). Commit `c2fc12f`.
3. **`CanvasToolbar.razor`** — replaces the old always-visible top toolbar row's buttons (Fit, Node
   fields, Layouts+Save+SaveAs+Delete, Animations) with a compact icon-button box top-right of the
   canvas (same visual language as the bottom `SelectionActionBar`), using `Blazicon`/`Lucide`
   icons for the first time inside the canvas (verified icon names — `Zap`/`ZapOff`/`Maximize`/
   `SlidersHorizontal`/`Save`/`Palette`/`Timer` — actually exist in the installed Blazicons.Lucide
   package before writing the plan). Layouts and Legend each open as a popover; `LayoutSwitcher` is
   reused completely unmodified inside its popover. Commit `2b3f46d`.
4. **Status-color filter chips** — one per `CircuitStatus`, added to the toolbar. Clicking a chip
   dims (25% opacity) every channel node whose current status doesn't match, via a new
   `workflowCanvas.setStatusFilter(host, statusName)` JS entry point. Pure JS/CSS — filter state
   and comparison both live client-side, keyed off the `statusName` string every telemetry tick
   already writes into the DOM, so no new C# data path was needed at all. Commit `d45586b`.

## Two real bugs found and fixed during live verification

1. **Config migration gap (Task 3).** A `WorkflowNodeConfig` saved before `RefreshRateMs` existed
   (i.e. every user who used the canvas before this session) deserializes that missing JSON field
   as the CLR default `0`, **not** the record's declared C# default of `250` — JSON deserialization
   does not honour a record's optional-constructor-parameter default for an absent property, only
   the record's own direct-construction call sites get that default. Confirmed live: the refresh-
   rate input showed `0` on first load. Fixed with a normalization guard right after loading
   (`if (_nodeConfig.RefreshRateMs <= 0) _nodeConfig = _nodeConfig with { RefreshRateMs = ... }`).
   **Worth remembering for any future field added to a record persisted via
   `ServerSessionStorageService`/raw JSON: the declared default only protects new construction, not
   old deserialized blobs — always normalize after load if the new field's absence would produce a
   meaningfully wrong value (here, 0 happened to be harmless functionally — `ShouldThrottle` never
   throttles when the window is 0 — but was still visibly wrong in the UI).**
2. **String-parameter binding without `@` (Task 4) — the exact class of bug this codebase's own
   Phase-1 report already documented once for `SelectedNodeId="_selectedNodeId"`.** Wrote
   `ActiveStatusFilter="_statusFilter"` in `WorkflowCanvasPage.razor` — since `ActiveStatusFilter`
   is a `string?` parameter, Razor accepted this as the **literal string** `"_statusFilter"`
   instead of an expression referencing the field, because a bare string is already a valid value
   for a string parameter (no compile error to catch it). The bug was invisible in the dimming
   effect itself (which reads pure JS-side state set correctly via the `EventCallback`), only the
   Razor-rendered `wf-status-chip--active` CSS class silently never appeared. Diagnosed by adding a
   temporary `data-debug-filter="@(ActiveStatusFilter ?? "(null)")"` attribute to the toolbar div
   and reading it back — it rendered the literal text `_statusFilter`, immediately confirming the
   literal-vs-expression theory. Fixed to `ActiveStatusFilter="@_statusFilter"`; the debug attribute
   was removed before committing. **This is now the second time this exact bug class has bitten
   this branch (Phase 1's `SelectedNodeId` bug, now `ActiveStatusFilter`) — any future `string`-
   typed Razor component parameter bound to a C# field/property MUST use the `@` prefix; a plain
   `Param="_field"` compiles silently wrong every time.**

## Verification

- Full suite: 531/531 passing (started at 517). One `ChannelActionExecutorTests` wall-clock-overlap
  timing flake hit once mid-phase, confirmed pre-existing (from Phase 3) and unrelated by rerunning
  in isolation (10/10 passed).
- Live-verified against the simulator: refresh-rate input shows/persists 250 → changed to 500 →
  survives a page reload (same `WorkflowNodeConfig` storage key the property picker already used);
  Layouts popover opens with the exact same combobox/Save/Save as/Delete controls, Load still works
  (loaded the "Test" saved layout successfully mid-session); Legend popover shows all 9 status
  colors exactly matching `HslFor`'s values; all 9 status filter chips render with correct swatch
  colors; clicking a non-matching-status chip dimmed all 64 placed channels (all reporting
  "offline" during this session due to an intermittent simulator connection issue, itself
  unrelated to this work); clicking the matching-status chip left 0 dimmed; clicking the same chip
  twice correctly toggles the filter off (0 dimmed, no active chip) after the `@`-prefix fix.

## Next

Phase 6 (Sections 4-5: canvas-as-login-landing + per-user cross-workflow channel exclusivity) is
the last phase in the D17-D24 spec — not started yet.
