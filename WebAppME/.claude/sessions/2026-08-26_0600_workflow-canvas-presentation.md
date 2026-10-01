# Session — T-47 Sub-project B: Canvas Presentation

**Date:** 2026-08-26
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` (permanent, never merged to `main` — D14/D15)

## Goal

Sub-project B of the 12-item feedback batch (see
`sessions/2026-08-26_0500_workflow-canvas-regression-fixes.md` for the full A-D decomposition):
status-color labels explaining purpose, user-changeable colors, remove the top bar and put the
color bar there, line grid instead of dots, and node spacing so channels stop overlapping.

## Status

✅ Complete. 565 tests (up from 540), 1 commit `e24c574`. Sub-projects C (Dashboard data parity)
and D (TabViewer navigation) remain.

## What shipped

1. **Legend explains meaning, not just color.** Each row states what the status tells an operator
   ("Idle — connected, no program running", "Interrupt — stopped early by a fault or limit"). A
   color key nobody can interpret is decoration, not information.
2. **User-changeable colors, canvas-only + per-user.** `<input type="color">` per status in the
   legend. Overrides live on the existing `WorkflowNodeConfig` (so they reuse the same
   `ServerSessionStorageService` key the property picker and refresh rate already use) and resolve
   through a **new `HslFor(status, overrides)` overload**. That single call feeds node faces, the
   legend swatches, the filter bar, and the board pins — deliberately one path, so a custom color
   can never apply to only some surfaces. Recoloring calls `PushTelemetryAsync(force: true)` to
   bypass the refresh-rate throttle: without it a new color would not reach node faces until the
   next hardware event, which on a quiet bench is a long wait.
3. **Top bar removed, color bar in its place.** The `Workflows / N saved layout(s) / N device(s)
   available` strip displayed nothing actionable. New `StatusFilterBar` occupies it with a
   labelled click-to-filter key — swatch **and** label visible without opening anything, which the
   cramped top-right chip row could not show. The chips were removed from `CanvasToolbar`
   accordingly; the toolbar keeps the icon buttons.
4. **Line grid** replaces the dot grid — two `linear-gradient`s at the same 24px pitch, so the
   existing JS `background-position` panning contract is untouched.
5. **Node overlap fixed at the source.**

## The node-overlap root cause (measured, not guessed)

Before changing any constant I placed a device and measured the real rendered card in the browser:
**106px of fixed chrome** (status pill row + program-status/last-update row + attachment badges)
**+ 16px per property row**, and `ChannelNode` renders properties two per row, so the 6-property
cap = 3 rows = **154px**. `RowHeight` was **120**. Every channel therefore spilled **34px** into
the row beneath it as soon as a user picked 5-6 node fields.

What made this easy to miss: `WorkflowAutoLayout.NodeHeight = 96` — a stale constant that was
**never referenced anywhere**, pure dead code, but which made 120 look like a reasonable row
height. Deleted it. `RowHeight` is now *derived*:

```
ChannelNodeBaseHeight = 106
ChannelPropertyRowHeight = 16
MaxChannelNodeHeight = 106 + ceil(MaxVisible / 2) * 16   // = 154 at the 6-property cap
RowHeight = MaxChannelNodeHeight + 26
```

Three tests pin it: the measured constant equals 154, `RowHeight > MaxChannelNodeHeight`, and a
two-board layout leaves every channel row at least a full card-height apart. Deriving from
`WorkflowNodeProperties.MaxVisible` means raising the property cap later cannot silently
reintroduce the overlap.

## Also extracted

`WorkflowColorConversion` (HSL triplet ↔ `#rrggbb`) went into a **`.cs` file, not inline in the
Razor component**, for two reasons: the Razor parser reads a leading `<` in a relational pattern
(`< 60 => ...`) as the start of a tag and fails to compile, and the round-trip is fiddly enough to
deserve tests. Round-trip tests cover the entire built-in palette — a silent drift there would
change a user's chosen color on reload, which is exactly the kind of bug nobody reports precisely.

## Verification — measured assertions, not existence checks

This is the specific discipline change from sub-project A's lesson (Phase 5 shipped an overlap
because I only checked that panels *opened*, never *where*):

- Old top bar absent; 9 labelled status-bar items present with correct names.
- Canvas background confirmed `linear-gradient` present **and** `radial-gradient` absent.
- **Zero overlapping node pairs** — computed pairwise box intersection across all 56 placed nodes,
  with the max node height still measuring 154 (so the spacing genuinely clears it rather than the
  cards having shrunk).
- Recolored `charge` → magenta: reached the status-bar swatch (`rgb(255,0,255)`), the legend
  swatch, **survived a page reload**, and `idle` correctly kept its default (only the changed
  status overrides).
- ⚠️ **A gap I found and closed rather than glossing:** the node-face color path could not be
  verified at first because no telemetry was flowing — every channel read `offline` with no
  `--wf-status` written at all. Rather than call the feature verified, I started the simulator
  (`run_sim.py -d 1 -n 8`), confirmed all 56 nodes then had `--wf-status`, and recolored `offline`
  specifically: node `--wf-status` became `240 100% 50%` and the status pill's computed color
  became `rgb(0,0,255)`. Only then was the color→node-face path actually proven.

## Next

Sub-project C (Dashboard data parity: missing node fields, the multi-tab detail dialog, the DBC
parameter view) then D (TabViewer navigation, which must revisit Phase 6's D17 login-redirect
decision since the user chose TabViewer over per-page routing).
