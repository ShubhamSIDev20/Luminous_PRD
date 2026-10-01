# Session 37 — Workflow Canvas: channel node as a battery cell

> Started: 2026-08-27T11:00:00Z
> Agent: Claude (Opus 5, 1M context)
> Branch: `feat/workflow-canvas-experiment`
> Goal: Turn the user's description of a battery-cell channel node into an approved design spec.
> Status: ✅ Spec, plan AND full implementation done and live-verified. 639 tests (up from 597).
> Commits: `d32c8f8` spec, `b36abdd`+`d37f67e` spec amendments, `4d8203a` plan, then
> `b76ca26` `e8efa69` `79b80b3` `8abf8b2` `c0ba8c4` `18cb7b7` `3787d4e` `5f1dcde` `69168ed`,
> then follow-up rounds `932c7a7` `5c89e52`/`3eff027` `ac186a6` `d109ed7` `2f571dd` `fe50801`
> `1c83501`.

---

## What was asked

The channel node should become a battery cell: the login page's battery SVG, channel number
`1-1-1` on the cap, program status + timestamp on the end, all parameters inside and growable,
labels so the user can tell what the values are, and charge/discharge animation driven by circuit
status — with idle showing **STOP** — using translucent colours so the data stays readable.

## What was produced

- `docs/superpowers/specs/2026-08-27-workflow-canvas-battery-cell-node-design.md`
- Commit `d32c8f8` (spec + `.gitignore` entry for `.superpowers/`)
- Eight iterations of rendered mockups via the brainstorming visual companion, in
  `.superpowers/brainstorm/90-1787803167/content/` (gitignored, persist locally for reference)

## Decisions the user made (against rendered alternatives)

Shape **A** (the battery *is* the node, stretching to fit its data) over a full-fidelity battery
beside a data panel, and over a battery-framed card. Channel number **on the cap**. Segment march
variant **A2** (motion confined to the empty region above the real SoC) over a full-height march.
Fill treatment **T1** (20% alpha, text directly on it) over per-row opaque strips. Primary Data
**two columns, unlabelled, left-aligned then right-aligned**; Configuration and Program Data
**labelled rows**. Discharge is the mirror of A2 — motion **inside the filled region**, marching
down from the charge surface.

Full table of 10 decisions with rejected alternatives is in §3 of the spec.

---

## Discoveries worth keeping

### ⚠️ The login battery is blue/cyan, not green — and the canvas must not copy its palette

`Pages/Login.cshtml` draws a `120×200` SVG: cap `50×12 rx4`, body `rx12` with a 3px `--primary`
stroke, inner well in `--muted`, a `chargeGradient` running bottom→top through
`--primary → --accent → --secondary`, a `feGaussianBlur stdDeviation=3` glow, the `%` text
*above* the fill line, and a bolt at 0.8 opacity. Wrapped in `animate-float` (3s bob) with
`animate-spark` and pulsing particles. **Its JS is pure decoration** — a `setInterval` walking
20→100% every 400ms, no real data.

Those are *theme* tokens, so in the default theme `--primary: 199 89% 42%` makes it blue and
`--accent` a near-white grey. The canvas's own status tokens are **fixed across all four theme
blocks** and mean something operationally: `--status-charge: 51 100% 50%` (yellow),
`--status-discharging: 33 100% 50%` (orange), `--status-idle: 0 0% 62%`, `--status-pause`,
`--status-continue: 122 39% 49%`, `--status-interrupt`, `--status-error: 4 90% 58%`,
`--status-msg`, `--status-offline`. Copying the login gradient onto a channel node would put it
in direct contradiction with the legend and the status filter bar. **Decision: borrow the
geometry, gradient structure and glow; take the hue from `--wf-status`.**

### ⚠️ `ChannelNodeHeight(int)` cannot survive this change

Today height depends only on the *count* of visible properties, because every row held two
unlabelled values: `94 + ((n+1)/2) * 20`. With the new layout, height depends on **which**
properties are selected — Primary Data packs two per row, Configuration and Program Data take
one per row, and each non-empty section adds a heading. Six Primary keys and six Program keys
produce visibly different cells.

So the signature has to become `ChannelNodeHeight(IReadOnlyList<string>)`, which ripples to
`WorkflowAutoLayout` (`MaxChannelNodeHeight`, `RowHeight`), `WorkflowEdgeGeometry.NodeHeightFor`
(and the `int visibleChannelProperties` parameter threaded through `PortPoints` and the
device-fanout helper), `MinimapProjection`, `WorkflowPlacement`, and `CanvasSurface.razor`
(which currently passes `SelectedProperties.Count`). Run `verify_change` before editing.

### ⚠️ The cap must carry the `wf-node__header` class or channel nodes stop dragging

`wwwroot/js/workflow-canvas.js:373` begins a node drag only when
`e.target.closest(".wf-node__header")` matches. The cap replaces the default header row, so
without that class channel nodes silently become undraggable — and **no unit test can see it**
(session 36 established that a programmatic `.click()` cannot reproduce this pointer-capture
behaviour; it needs a real CDP-level interaction).

### ⚠️ The LOD tier-1 rule becomes a silent no-op

```css
.wf-world[data-lod="1"] .wf-node__body > :nth-child(n+2) { display: none; }
```

This works today by hiding all but the first child of `.wf-node__body`. After the change the body
has exactly **one** child (the cell), so the selector matches nothing and tier 1 renders the full
cell at small zoom. The spec defines replacement tiers (§5.4). Related to session 36's finding
that card height is *partly a CSS concern* because LOD tiers collapse cards — the same coupling
bites here.

### ⚠️ CSS class collision: `.wf-cell*` is already taken

`BatteryGlyph.razor` (used by the Battery node) owns `.wf-cell`, `.wf-cell__fill` and
`.wf-cell__shimmer`. The new component must use a distinct prefix (`.wf-bcell*`); reusing
`.wf-cell` would restyle every battery node as a side effect. `BatteryGlyph` stays as-is — it is
a multi-cell pack glyph with a different job, and the new cell does not consume it.

### The height constants must be measured, not derived on paper

Session 36's lesson applies directly: the previous pair (`106 base, 16 per row`) reproduced the
right answer at *exactly three rows and nowhere else*, because one measurement cannot pin a
two-parameter linear model. The new formula has more terms (cap, frame, well padding, header,
footer, three per-section headings, two distinct row heights), so the plan must include an
explicit measure-then-encode step in a real browser plus a test pinning each measured constant.

### Structural fix for the overlap the user reported

The first mockups positioned each text block at an absolute pixel offset over a fixed-size SVG
`viewBox`, so text ran outside the outline as soon as the field list grew — exactly what the user
saw. The fix is not tuning offsets: build the frame as a cap element plus a bordered body, and let
every text block be an ordinary flex child inside padding. Then the cell's height is whatever its
content needs, the fill layers are the only absolutely-positioned things and they sit *behind* the
content at `z-0`, and adding all 28 fields can only make the battery taller. **This is why the
frame is CSS/Tailwind rather than a single stretched SVG.**

### Segment march without per-segment CSS

`steps(10, end)` on a `scaleY(0) → scaleY(1)` transform makes the fill climb in ten discrete
increments — "one segment at a time" — with one animation, no per-segment rules, and GPU
compositing, consistent with the canvas's existing rule that only `transform`, `opacity` and
`stroke-dashoffset` are animated. Segment separators are a `repeating-linear-gradient` at a fixed
18px, so the segment *count* derives itself from the cell's height with no arithmetic anywhere.

### The three sections already exist in the data model

`CardPreviewData` has exactly three dictionaries — `RealTimeProperties` (12), `ConfigProperties`
(3), `ProgramProperties` (13) = the 28 keys, and `GroupedProperties()` already yields them as
"RealTime Data" / "Configuration" / "Program Data". So the section split needs no new taxonomy,
and `WorkflowNodeProperties.AvailableKeys` is already the concatenation of the same three.

### Reversal recorded deliberately

The node face has carried **no visible labels** since sub-project C1, on the grounds that the unit
token (`Ah` vs `AhCha` vs `AhDch` vs `AhStep`) identifies each measurement and the `title` gives
the full name. The user has now asked for labels. The compromise in the spec keeps Primary Data
unlabelled (its 12 values *are* unit-identified, and labelling them one-per-row would double the
cell height) and labels Configuration and Program Data, whose values carry no unit and are
meaningless without a name. `title` stays on every value regardless.

---

## Open questions carried into review

1. Does `Idle` read as **STOP** everywhere, or only inside the cell? Spec assumes cell-only;
   the legend and filter bar keep `idle`. Global rename would touch `StatusLegend.razor`,
   `StatusFilterBar.razor` and persisted filter state.
2. The battery spec line (`LFP · 50 Ah · 3.7 V · 1 cell`) shown in the mockups comes from
   `Batteries.NominalCapacity` / `NominalVoltage` / `NumberOfCells` / `BatteryType`, none of which
   are in `ChannelTelemetry` or the property catalog — it needs a new data path and is **out of
   scope** in the spec as written. The user mentioned wanting battery details, so this may need to
   come back.
3. Does the SoC `%` chip stay in the header, or move onto the cap beside the channel number?

## Files changed

| File | Change |
|---|---|
| `docs/superpowers/specs/2026-08-27-workflow-canvas-battery-cell-node-design.md` | new — the spec |
| `.gitignore` | ignore `.superpowers/` so brainstorming mockups can't be committed by accident |

No source, CSS, JS or test files were touched. Test count unchanged at 597.

## Next step

User reviews the spec. On approval, invoke the `writing-plans` skill to produce the phased
implementation plan — starting with the measure-then-encode task for the height constants, since
every geometry change downstream depends on those numbers being right.

---

## Implementation (same session, all 10 plan tasks)

Plan: `docs/superpowers/plans/2026-08-27-battery-cell-channel-node.md` (`4d8203a`), executed
inline. 597 → 635 tests.

| Task | Commit | Note |
|---|---|---|
| 1 SoC estimator | `b76ca26` | 7 tests |
| 2 SoC + battery ratings through telemetry | `e8efa69` | 5 tests |
| 3 `SectionsFor` | `79b80b3` | 5 tests |
| 4 `CanvasNode.HeaderContent` | `8abf8b2` | 3 tests; other node kinds unchanged |
| 5 cell component + `ChannelNode` rewrite | `c0ba8c4` | 13 tests |
| 6 cell CSS + LOD rewrite | `18cb7b7` | |
| 7 JS bridge drives the cell | `3787d4e` | |
| 8+9 measured height + geometry threading | `5f1dcde` | one commit: task 8 alone cannot compile |
| 10 live verification + 4 defect fixes | `69168ed` | |

### ⚠️ SoC was hardcoded 0.0 and the battery fill had NEVER rendered in live mode

`WorkflowCanvasPage.razor` passed `0.0` with a comment saying no SoC exists in the schema. So the
whole design would have rendered empty. **Derivation was a prerequisite, not an addition.** The
user chose net coulomb count over an `AccumulatedCapacity`-only variant and voltage interpolation:

    soc = clamp((ChargeCapacity - DischargeCapacity) / NominalCapacity, 0, 1)

Returns `double?` — null is UNKNOWN, not empty. `BatteryDTO.NominalCapacity` **defaults to 0**, so
an unconfigured battery record would otherwise divide by zero into a confident wrong `0%`, and on a
bench "unknown" must not look like "flat". Live-verified: `--wf-soc: 0.082`, `8%`, real fill.

### ⚠️ `circuit.Battery` is a `BatteryDTO`, not the `Batteries` entity

The spec assumed the entity. The DTO does carry `NominalCapacity` / `NominalVoltage` /
`NumberOfCells`, but only `BatteryTypeId` — **no type name**. So the battery line ships as
`2 Ah · 2 V · 1 cell` with no chemistry prefix; adding it would put a lookup on a per-tick path.
Also: the DTO's numeric fields are non-nullable and default to 0, so the formatter treats a
non-positive figure as ABSENT rather than printing "0 V".

### ⚠️ Razor: a loop variable named `section` breaks the file

`@section.Title` in markup parses as the **`@section` directive** → `RZ2005` + `RZ1011`. Renamed to
`sec`. Fifth instance on this branch of a naming collision failing silently or cryptically.

### ⚠️ The unit is ALREADY in the value the bridge writes — do not render it again

`e.properties[key]` is pre-formatted as `"0.164 AhCha"`. Rendering `UnitFor(key)` beside it shipped
`0.164 AhChaAhCha`. That is exactly why the OLD node face rendered no unit element. Caught live, not
by a test — a unit test now pins it. **The component supplies the label (title) only; the bridge
supplies value+unit.**

### ⚠️ A channel node must not also be a card

`CanvasNode`'s shared chrome (node border/background, header padding+border, body padding+gap) is
right for Device/Battery but on a channel draws a SECOND frame around the battery outline and pushed
the cap **15.6px** clear of the cell — the user saw "2 cols" and "card borders".
`.wf-node--channel` now suppresses all of it and `.wf-bcell` has no top padding, so the cap is
flush (gap measured 0).

### ⚠️ Conditional rows break the height contract — measured, not theorised

The program/DBC badge strip added **22.5px** that `ChannelNodeHeight` cannot predict, because
attachment is not derivable from the property list. Channels with a program running would have had
edges attach off-centre. Both the badge strip and the battery-spec strip are now **always
reserved**; only their contents are conditional. The spec had missed the badge case — the plan's
self-review caught it.

### The measured height model (nine points, all exact)

Line boxes are **pinned with explicit `line-height`** so the C# constants are integers by design
rather than whatever `line-height: normal` resolves to. Measured with `normal` they were `15.61` and
`17.5` — correct today, fragile across fonts, and the same class of fragility that produced session
36's wrong `106 + 16`.

    chrome 114 | section heading 19 | row 15 | row gap 1
    block = rows * 16 - 1,  rows = ceil(n/2) for Primary, n for the labelled sections

Verified live at: none 114 · 1 primary 148 · 2 primary 148 · 3 primary 164 · 1 config 148 ·
3 config 180 · 2 program 164 · 4 primary + 1 config 198 · all 28 **520**. Primary and labelled rows
measured **identical** at 15px, so there is one constant, not two. Chrome was 151 before the card
chrome was suppressed; the 37px delta is exactly node border 2 + header padding 12 + header border 1
+ body padding 16 + body gap 4 + cell top padding 2.

### Live verification results

Edges attach at **0px** deviation from the cell centre at 520px tall. No overlap and text fully
contained at every field count including all 28. LOD tier 1 renders **115px** (the rewritten rule
works — the old `nth-child(n+2)` selector would have shown the full cell); tier 2 a 32×32 tile.
March verified on a clone at 40% SoC: charging = 60% box above the surface growing upward,
discharging = 40% box below it growing downward, idle **paused**, `steps(10)` in both.

⚠️ **Not observed live:** the charge/discharge march on real hardware — the bench was idle
throughout, and starting a program energises a battery, which is not an agent's call. Verified
mechanically instead.

⚠️ The cap colour looking "red and fixed" was **not a bug**: driving `--wf-status` gives yellow for
charge and orange for discharging. The colour seen is `hsl(0 93% 79%)` against a theme
`--status-idle` of `0 0% 62%` — a **saved user override for idle** (sub-project B), and with all 8
channels idle every cap showed that one colour.

### Gotchas about the tooling, not the code

- The Bash tool is Git Bash: PowerShell here-strings (`@'...'@`) do not work, and an apostrophe in a
  single-quoted `git commit -m` closes the quote. Use `git commit -F <file>`.
- The app logs `Listening on: http://localhost:5000` but Kestrel actually binds **5066** from
  `launchSettings`. Trust `netstat`, not that log line.
- Synthetic `PointerEvent`s cannot drive the node drag: `setPointerCapture` needs a real pointer, so
  a scripted sequence proves nothing either way. Session 36's lesson in reverse.
- Toggling a `wf-bcell--*` class then awaiting a frame loses it — the 500ms telemetry tick correctly
  removes it. Measure synchronously, or on a detached clone.


---

## Follow-up round: six issues found on the user's own live test (same session)

After T-48 shipped, the user hand-tested it and reported six items. All fixed, `932c7a7`, 631 tests.

1. **Fill/march alpha too low to read as status-coloured** — bumped 0.20→0.32 / 0.15→0.22.
   Everything was already wired to `--wf-status`; the complaint was visibility, not wiring.
2. **March too fast** — 2.6s → 6.5s.
3. **Device-click-select-all already existed** (`DeviceNode` → `OnSelectNodeChildren` →
   `WorkflowSelectionLogic.ApplyBulk`), and bulk actions already existed on the selection bar —
   confirmed live, no feature gap. What WAS real: **every skip toast blamed "offline" even when
   the true cause was "no telemetry has arrived yet"** — `CanAdd` rejects a channel with no
   reading (by design, a test pins it: `CanAdd_RejectsACandidateWithNoReadingYet`), and right
   after page load, before the first tick populates `_selectedReadings`, every channel looks
   unreadable. The user's own test caught this exact race and reported all 8 connected channels
   as "offline". Now the toast checks `_selectedReadings.Count == 0` and says "still waiting for
   telemetry" instead.
4. **⚠️ The open layout never survived a refresh — a real gap, not a misunderstanding.**
   `ServerSessionStorageService` already persisted `_nodeConfig` via `SetComponentState(...,
   Permanent: true)`; nothing did the same for which layout was loaded. Added
   `CurrentLayoutState(long? LayoutId)` (wrapped because `GetComponentState<T>` requires `class`)
   persisted at every `HandleLoad`/`PersistAsync`/`HandleDelete`, restored in `OnInitializedAsync`
   before first render, guarded against a since-deleted layout. **Live-verified with a real
   `ignoreCache` reload**, not a soft one: loaded "Test", hard-refreshed, canvas came back with
   all 8 channels and the dropdown showing "Test" with no dirty marker.
5. **Dock's "Live data" section was pure duplication** now that the battery cell face shows all
   28 fields itself — deleted, plus its JS writer `applyDockLiveData` (now fully dead) and the
   `.wf-dock__live` CSS rule. Kept: attach program/DBC, "All details…", "Remove from canvas" —
   real functionality the cell face still can't provide.
6. **Dock and palette now default collapsed**, not open, so the canvas gets full width on load.
   Both already had their own "‹"/"›" toggle. Placing a device still needs the palette (open on
   demand); removing one already works via a selected node's dock → "Remove from canvas" — no new
   UI was needed for that half of the ask.

### Gortex note
Earlier in this session gortex reported "inactive" for this directory. It was a stale check — the
daemon has `WebAppME` tracked at the PARENT path (`D:\WorkArea\Projects\WebAppME`), which
covers this `Application` subfolder fine; `gortex call get_active_project` / `search_symbols` /
`find_usages` all worked correctly once tried directly. Use the CLI form
(`gortex call <tool> --arg k=v`) since this session's MCP tool list came up empty for gortex.


---

## Follow-up round 2: device-select bug, group drag, and delete-key (same session)

Commits `5c89e52`, `3eff027`. 631 tests.

- **Device click -> "offline"**: real bug. `OnSelect` routed every click through
  `WorkflowSelectionLogic.ApplyBulk`, which rejects any id absent from `_selectedReadings`
  (channel-only). A Device/Board/Battery id can never be in that dict, so selecting one has
  *always* failed with a misleading offline toast. `SelectSingleNode` now bypasses the
  eligibility check for non-channel nodes.
- **⚠️ Found while fixing that: header clicks never worked via a real mouse, only via a scripted
  `.click()`.** `beginNodeDrag` takes `setPointerCapture` on every header pointerdown
  unconditionally, which diverts native click synthesis away from the header — the *exact*
  mechanism session 36 found for the toolbar. My own earlier "confirmation" that device-header
  click selects all channels used `header.click()` and was invalid for the same reason.
  `endNodeDrag` now mirrors `endMarquee`'s click-vs-drag threshold and dispatches a new
  `OnHeaderClick` JSInvokable directly on a real click, instead of depending on a click event.
  Removed the now-dead Blazor `OnHeaderClick`/`OnSelectChildren` EventCallback plumbing
  (`CanvasNode`, `DeviceNode`, `CanvasSurface`, the page).
- **Group drag was entirely missing** — dragging one node of a multi-selection left the rest in
  place. `beginNodeDrag` now drags every `.wf-node--selected` element together when the grabbed
  node is part of a multi-selection; new `OnNodesMoved` JSInvokable applies all positions in one
  call.
- **"Can't remove a channel from a saved layout"**: investigated and could NOT reproduce with the
  current build — verified the full flow (select → dock → Remove from canvas → Save → hard
  reload) actually persists correctly. What the user was really pointing at: removal only lived
  in the dock (opposite panel from the palette where you place a channel), one node at a time,
  and both panels now default collapsed. Added **Delete/Backspace removes the whole selection
  directly on canvas** (guarded against firing while a form control has focus) — the natural
  counterpart to placing from the palette.

### Verification technique note, worth keeping
`.click()` does **not** reach this canvas's selection or drag pipeline at all — both are driven
by raw `pointerdown`/`pointerup` listeners on the canvas host, not `click` listeners on the
nodes. A real `PointerEvent('pointerdown', {bubbles:true, composed:true, pointerId, ...})`
dispatched on the target element *does* work reliably for the no-capture paths (plain selection);
genuine drag/header-click paths that call `setPointerCapture` still need a real CDP-level
interaction to trust fully, per session 36.


---

## Follow-up round 3: the fill/march never carried live status colour (same session)

Commit `ac186a6`. 631 tests.

⚠️ **Real, long-standing bug, finally caught with a live charging channel**: the cap correctly
showed the user's own custom charge colour (green, `hsl(127 90% 45%)`, set via the status
legend), but the fill/march/surface *inside* the cell stayed the idle grey token regardless of
actual circuit status - confirmed live with a real running program, not reproducible from an
idle bench (which is why 8 idle channels all matching each other earlier looked like "correctness"
rather than "the bug can't show with only one status present").

Root cause: `applyTelemetry` only ever wrote `--wf-status` onto `.wf-node`. `.wf-bcap` has no
`--wf-status` declaration of its own, so it genuinely **inherits** the live value and always
coloured correctly. `.wf-bcell` DOES declare its own `--wf-status: var(--status-idle)` as a
fallback default (so an unrendered cell isn't colourless) - and a property declared directly on
an element, even as a fallback, always wins over an inherited value from an ancestor. That
fallback has shadowed every real status update for the fill/march since the cell shipped in
T-48 - it was never wired correctly, the design was just never tested against two different
live statuses side by side until now.

Fixed the same way `--wf-soc` already avoids depending on inheritance: write `--wf-status`
explicitly onto the cell too, not just the node. Verified live on an actual charging channel
(node and cell values matched after the fix) and confirmed structurally across all 8 channels
once the program stopped.

### Lesson worth keeping
CSS custom-property fallback defaults declared directly on a component's own selector
(`--wf-status: var(--status-idle);` on `.wf-bcell`) are **not just fallbacks** - they
permanently block inheritance from any ancestor, even one that's actively being updated by JS.
Any future custom property meant to flow from a JS-owned ancestor into a component must either
be written explicitly onto that component too, or the component's own CSS must not declare a
same-named property at all.


---

## Follow-up round 4: double-click opens the dashboard's own dataviewer (same session)

Commit `d109ed7`. 631 tests.

User asked for the same double-click behaviour `DeviceChannel.razor` already has on the
dashboard. Implemented as the EXACT same call, not a new dialog: `TabService.AddTab` opening
`BmsDashboard` (from `Components.UI.DataViewer`) with `SQLFilePath` = `circuit.Session?.
SessionFilePath` and `Key` = `"{DeviceId}-{BoardNumber}-{ChannelNumber}"`, `Unique: true`,
`keepAlive: true` - so double-clicking the same channel twice focuses the existing tab rather
than opening a duplicate, matching the dashboard's own dedupe.

Bound `@ondblclick` on the cell BODY (`ChannelBatteryCell`'s wrapper in `ChannelNode.razor`), not
the header/drag handle — deliberately, per this session's own finding that `setPointerCapture` on
the header swallows native click/dblclick synthesis. The body has no such capture, so this needed
none of that machinery; a plain native double-click just works.

`WorkflowNodeLabeller.TryGetChannelKey`'s tuple field names are `(DeviceId, BoardNumber,
ChannelNumber)` — different casing from `ChannelDto`'s own `DeviceID`/`SecondaryBoardNumber`/
`ChannelNumber`, worth remembering before assuming one matches the other by name.

Live-verified: a real dblclick opened "Realtime: 1-1-2" with a working chart, program table and
live data grid — the actual `BmsDashboard`, not a stub.


---

## Follow-up round 5: viewport (pan/zoom) never actually auto-saved (same session)

Commit `2f571dd`. 631 tests.

Two stacked bugs, both real.

1. Panning/zooming was tracked in memory (`OnViewportChanged`) and marked `_dirty`, but nothing
   persisted it outside the explicit Save button - so refreshing or switching layouts without
   saving always reverted the view. Fixed by auto-saving on every viewport change via
   `ServerSessionStorageService` (`Permanent: true`, same mechanism `_nodeConfig` and
   `_currentLayoutId` already use), keyed **per layout id** so two layouts keep their own
   last-viewed position.

2. ⚠️ **Fixing that surfaced a deeper, pre-existing gap**: even with a correct viewport in
   `_graph`, the auto-restore-on-refresh path (`OnInitializedAsync` → `HandleLoad`) runs
   **before `_jsReady` is ever true**, and `HandleLoad`'s own `workflowCanvas.setViewport` call
   is gated on `_jsReady` — so it silently no-op'd on every auto-load since round 4's
   auto-restore feature shipped. Nodes still rendered correctly (Blazor draws their X/Y directly,
   independent of the canvas's own pan/zoom transform), which is exactly why this was invisible —
   the graph looked right, only the camera was wrong. Fixed by applying the viewport explicitly
   once `_jsReady` becomes true in `OnAfterRenderAsync`, for whichever layout already auto-loaded.

### Testing gotcha worth keeping
`workflowCanvas.setViewport(host, x, y, zoom)` is **server→client only** and does **not** call
`notifyViewport` — using it to simulate a user pan (as I first tried) never touches
`OnViewportChanged` at all, so it can't be used to test the autosave path. A real pan needs an
actual `pointerdown`/`pointermove`/`pointerup` sequence on the canvas host so the JS pan-end
handler calls `notifyViewport` itself. Verified end to end this way: dispatched a real pan, hard
reloaded, got the exact same `matrix(...)` back to the pixel.


---

## Follow-up round 6: 2-board simulator + a channel's offline dot always green (same session)

No commit for the simulator investigation itself (operational only); fix commit `fe50801`. 637
tests (631 → 637, +3 pinning this fix's regression + connected/disconnected behaviour).

### 2-board (16-channel) simulator registration

User asked to run the simulator with 2 secondary boards (`-n 16`) to test at 16-channel scale.
First restart logged all 8 of board 2's channels rejected with `status=0`
(`CommandStatus.Failed`). Traced through `ChannelManager.ProcessRegistrationPacketAsync`
(`Services/ChannelManager.cs`): **by design**, a channel unseen in the DB gets auto-inserted on
its very first registration attempt but the response for that same attempt is unconditionally
`CommandStatus.Failed` — only a channel already marked `IsRegistered = true` gets accepted. This
is the same "operator must Allow a new channel" gate `DeviceList.razor`'s `RegisterCircuitAsync`
guards (`ChannelManager.Add`'s own message: *"Please allow Channel is to registered..."*). Not a
bug. On the next simulator restart all 16/16 channels registered cleanly (board 2's channels had
since been approved), confirming the gate, not a defect, was the cause.

### Channel's connection dot stuck green after going offline

User reported: the small online/offline indicator next to a channel's circuit-status colour kept
showing green (or whatever colour the channel was charging) even once the channel was actually
offline — pointed at `DeviceChannel.razor` as the source of truth for correct behaviour.

Found it there at line 1867:
```csharp
CircuitStatus? circuitStatus = Channel.IsConnected
    ? Channel?.RealTime?.RealTimeRecord?.CircuitStatus
    : (Channel?.RealTime?.RealTimeRecord?.ProgramStatus == ProgramRunningStatus.Running
        ? CircuitStatus.Error : CircuitStatus.Offline);
```
The dashboard never trusts the raw hardware `CircuitStatus` once `IsConnected` is false — it
forces `Offline` (or `Error` if a program was left running mid-disconnect). ⚠️ **The canvas never
replicated this.** `ChannelTelemetry.IsConnected` already rode along in the DTO (added for
`BuildRollups`'s device/board online summaries, which DO use it correctly — confirmed by an
existing test, `BuildRollups_BoardIsOfflineWhenNoChannelIsConnected`) but
`WorkflowTelemetryBridge.BuildEntries` never read it for the channel's own cell colour — it just
forwarded `reading.Status` straight through to `HslFor`/`WorkflowStatusCss.Name`/`FlowFor`. A
channel that went offline mid-charge kept its cell green forever.

Fixed with `WorkflowTelemetryBridge.EffectiveStatus(ChannelTelemetry)`, the same rule as
`DeviceChannel.razor` above, and routed it through `BuildEntries`'s `StatusHsl`/`StatusName`/
`Flow` in place of the raw `reading.Status` (left `ErrorText`'s own `ResolveErrorText` on the raw
status — that's an unrelated `errorId`/`SystemErrorId` catalog lookup, not a colour decision).

⚠️ Not yet live-checked against a real hardware disconnect in-browser this session — build/test
verified only (634 → 637 tests, all green), plus the app was restarted and confirmed serving all
16 channels live.


---

## Follow-up round 7: field-driven row spacing + beside-not-below channel placement (same session)

Commit `1c83501`. 639 tests (637 → 639).

Three more issues reported together at 2-board/16-channel scale, all traced to one root: row
spacing was a fixed WORST-CASE constant, and layout was only ever computed relative to a fresh
subgraph's own local origin.

1. **Field add/remove left the row gap wrong.** `WorkflowAutoLayout.RowHeight` was a
   `static readonly` sized for the tallest possible card (28 fields); picking fewer fields shrank
   the card but never the gap, so *fewer* fields meant a *bigger* visible gap between board rows -
   exactly backwards from what the user expected. `RowHeight` is now a function of the actually
   selected property list, and `WorkflowCanvasPage.HandleNodeConfigChanged` now diffs the new
   selection against the old one and re-runs `Apply` over the WHOLE graph automatically when it
   changed - not only when the pre-existing "Re-arrange nodes" toolbar button is clicked (found
   while investigating: that button already existed, `ReArrange()`/`OnReArrange`, explicitly
   documented as manual-only "so it is an explicit action rather than something triggered by
   changing the field count" — the user's fresh request here explicitly chose automatic instead,
   accepting that a channel dragged off-grid gets snapped back onto it on the next repack).

2. **A channel added to an already-placed device landed in a brand new lane below everything,
   not beside its board's siblings.** Two stacked causes: `WorkflowCanvasPage.HandlePlaceDevice`
   always computed the drop origin via `WorkflowPlacement.NextFreeLaneY(_graph)` — a value meant
   only for a device never seen before — even when the device already had a lane; and
   `WorkflowPlacement.PlaceDevice` built its subgraph from only the channels THIS call was adding,
   so a single new channel always computed as column 0/row 0 (its board's "only" channel) before
   being translated to the wrong place. Fixed: an already-placed device now reuses its own
   existing (X, Y) as the origin (`WorkflowAutoLayout.Apply` always parks the device node itself
   at local (0,0), so its current graph position literally IS that origin), and `PlaceDevice`
   always lays out the device's FULL physical channel set so a later addition gets exactly the
   slot its channel number always implied — a channel claimed by another workflow still reserves
   its slot (siblings never shift left to fill the gap) without ever being inserted itself.

3. **Zoom-level spacing "not matching"** turned out to be the same bug, not a third one — once row
   spacing tracks the real card height there's nothing left to mismatch at any zoom tier, since
   pan/zoom is one uniform CSS transform over an already-correct layout. No separate fix needed.

⚠️ **Found and fixed a latent bug along the way, not reported by the user:** `WorkflowAutoLayout.
Apply` only ever positioned the FIRST device node it found and silently left every OTHER device's
boards/channels untouched — invisible until now because its only production caller
(`WorkflowPlacement.PlaceDevice`) always passed a single-device subgraph. The pre-existing
`ReArrange()` button calling `Apply(_graph)` directly on a MULTI-device canvas would have hit this
the moment a second device was ever present. `Apply` now anchors each device independently at its
own already-existing (X, Y) rather than a shared origin — which is also what makes today's
automatic per-field repack safe to fire on a real multi-device canvas without collapsing every
device into one lane.

Live-verified in browser against the running 2-board/16-channel simulator via chrome-devtools:
trimmed the property picker from 13 fields down to 1 and watched board 2's row snap from a 528px
gap to the correct 174px (`ChannelNodeHeight(["Current"]) + 26`); selected channel 9 (real
pointerdown/pointerup on the header, not `.click()` — this branch's usual gotcha), deleted it with
the Delete key, re-placed it from the palette, and it landed in board 1's own row (Y=-168) beside
channel 8, not in a new lane below the whole device; clicked Re-arrange afterward and confirmed it
still repacks identically. Restored the property picker back to the `DefaultVisibleProperties`
(Voltage/Current/Power/Temperature) before finishing, since testing had stripped it down.
