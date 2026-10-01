# Workflow Canvas Playground — Experiment Report

**Date:** 2026-08-23
**Branch:** `feat/workflow-canvas-experiment` — not merged, not to be merged without an explicit decision to do so
**Spec:** [docs/superpowers/specs/2026-08-22-workflow-canvas-design.md](superpowers/specs/2026-08-22-workflow-canvas-design.md)
**Plan:** [docs/superpowers/plans/2026-08-22-workflow-canvas.md](superpowers/plans/2026-08-22-workflow-canvas.md)

## What was built

An n8n-style pan/zoom canvas at `/workflows`, gated behind a `Features:WorkflowCanvas` flag, with the legacy dashboard hidden (not deleted) behind `Features:LegacyDashboard`. An operator can:

- auto-import a device's full topology (device → boards → channels) or place individual channels one at a time from a palette of real database entities;
- drag nodes, connect a battery to a channel, select a node and attach a program and/or DBC file to it from a properties dock;
- collapse a board (hiding its channels from view only — never from the saved document) and collapse either side panel;
- watch live telemetry animate the graph: a channel's status colour, a flowing/glowing power edge toward or away from its battery, and a battery glyph that fills and shimmers — all driven by real `ChannelManager` circuit data with **zero** Blazor re-renders;
- save, reload, and delete named layouts, including a minimap overview.

All 17 plan tasks are complete: 360 unit/bUnit tests (up from 227 at the start of this branch), 17 feature commits, every task live-verified in a real browser against the real database and the real hardware simulator — not just asserted by the automated suite.

## Scale measurements (Task 17)

Full topology: 10 simulated devices + 1 real device, 640 channels, all placed simultaneously.

| Question | Answer |
|---|---|
| Nodes at full scale | 730 (10 device + 80 board + 640 channel) |
| Pan cost, 60 frames, 730 nodes | 3.2 ms total — flat regardless of node count, confirmed by the single-transform architecture |
| DOM child-list mutations during 4s of live telemetry at 640 channels | **0** — the no-re-render design guarantee holds at full scale, not just at 64 channels |
| Console errors across every task, every scale | **0**, in every verification pass across all 17 tasks |
| Server memory with the canvas open at full scale | ~537 MB (`dotnet.exe`, in line with the existing 640-circuit dashboard load; no canvas-specific spike observed) |
| Is a 64-channel single-device graph readable? | **Yes.** Three clean columns, no overlaps, edges attach precisely, board collapse works as designed. |
| Is a 640-channel, 10-device graph readable? | **No, not as currently laid out.** See finding below. |

### The one real design flaw found at scale

`WorkflowAutoLayout` places every node of a given kind in a single vertical column (one column each for devices, boards, channels, batteries). This is fine for one device: 64 channels form a column comfortably fitted by `fitToContent`. At 640 channels across 10 devices, the channel column becomes ~13,000 px tall and only 200 px wide. `fitToContent` is then forced to zoom to its floor (0.2×) to fit the height, at which point every label is illegible — and the fit wastes almost all of the horizontal viewport, which sits empty because the layout never uses it.

This is a layout algorithm limitation, not a performance or correctness one: the pan/zoom/telemetry/edge machinery all measurably held up at this scale (see table above). Board collapse mitigates this somewhat (each collapsed board removes 8 channel rows from the column) but does not fix the fundamental one-column-per-kind shape. A production version of this idea would need a grid or per-device-lane layout instead of a single column.

## Failure-path checks

| Check | Result |
|---|---|
| Channel-to-channel connection (always illegal) | Rejected with a visible toast: "cannot connect a channel to a channel." End-to-end JS → C# validation → UI confirmed. |
| Board wired to a channel of a different device | Covered by 19 passing `WorkflowGraphValidatorTests` with a genuine two-device topology (not reproducible live in this single-primary-device simulator environment without risking real hardware state). |
| Saved layout referencing hardware later deleted (the stale-reference design's whole reason for existing) | Covered by 14 passing `WorkflowLayoutServiceTests`, including the exact "channel deleted after save" scenario. **Not reproduced against the live database** — deleting a real device/channel row to manufacture this state was judged out of scope without explicit direction, since it risks the user's real hardware registration data. The unit-level proof is direct and unambiguous: `WorkflowLayoutService.FindStaleNodeIds` is pure, has no live-DB dependency, and is exhaustively tested. |
| Two browser tabs editing and saving the same layout concurrently | Both tabs saved successfully in sequence with no error in either tab's console and no server exception. Last write wins at the row level — acceptable for an experimental single-operator feature; not something a production version could leave this loose. |
| `Features:WorkflowCanvas = false` | Confirmed in Task 7: the menu entry disappears entirely and the rest of the app is unaffected. |
| `Features:LegacyDashboard = true` | Confirmed in Task 7: the old dashboard returns to the menu and works exactly as before. |
| Empty topology (no devices registered) | Not separately reproduced this session — the simulator was running throughout — but `WorkflowGraphBuilderTests.BuildForDevice_HandlesADeviceWithZeroBoards_WithoutThrowing` and `PalettePanel`'s explicit "No devices registered yet." empty state cover the two halves of this path. |
| Stopping the simulator mid-session | Not performed — would have disrupted the user's other active work against the same running simulator/app instance without a clear benefit over the already-covered "channel deleted" stale-path tests. |

## What worked

- The core architectural bet — JS owns all transient interaction, C# owns committed state, telemetry writes CSS variables directly and never re-renders — held up exactly as designed, including at 10× the single-device scale it was first verified against.
- Every one of the plan's 17 tasks passed its own browser verification on the first or second attempt; the two real bugs that live verification caught (a Razor `string`-parameter binding gotcha, and a missing initial telemetry paint on an idle bench) were both outside what any unit or bUnit test could have found, which is exactly the case this plan's "browser check on every UI task" rule was written for.
- The soft-reference / stale-node design, the board-collapse view/edit separation, and the feature-flag hide-not-delete approach all did precisely what the spec intended, with no surprises.

## What did not work / needs a decision

- **Auto-layout does not scale past one device.** This is the one finding serious enough to block a "yes, ship this" verdict as-is. Fixing it is a bounded, well-understood change (a grid or lane-based layout instead of a single column) — not a re-architecture — but it has not been built.
- **State-of-charge has no real source.** `RealTimeRecord` tracks `Capacity` in Ah, not a percentage; the battery glyph currently renders 0% fill in live mode. Cosmetically fine for the experiment, not acceptable for real use without a real SoC calculation wired in.
- **The Save button's dirty state lags by one interaction after a pure pan or a pure node-drag**, by design (avoiding a re-render on every pan/zoom frame). This is a minor, intentional trade-off, not a bug, but worth knowing about if the interaction is refined further.
- **Concurrent multi-tab editing is last-write-wins with no conflict warning.** Fine for a single operator experimenting; not fine for a shared, production workflow tool.

## Recommendation

**Keep the branch, do not merge yet, and treat the auto-layout scale problem as the next thing to solve before asking anyone else to evaluate this.** The interaction model itself — drag, connect, watch it glow — works, feels responsive even at 640 channels, and the animation and telemetry mechanisms are sound. The thing standing between this and "worth showing people" is purely the layout algorithm, which is a scoped fix, not a rethink of the whole idea.

If the auto-layout issue is fixed and still doesn't feel right at scale, that is the point at which "delete the branch" becomes the right call — but that verdict cannot honestly be reached from today's state, because today's state has an easily-explained, easily-fixed reason for looking bad at 640 channels rather than an inherent one.
