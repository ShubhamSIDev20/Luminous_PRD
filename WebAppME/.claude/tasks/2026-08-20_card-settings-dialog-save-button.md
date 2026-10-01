# T-41 — Card Settings dialog: buffer edits until Save

> Started: 2026-08-20
> Completed: 2026-08-20
> Session: [sessions/2026-08-19_1044_dashboard-card-dialog-fixes.md](../sessions/2026-08-19_1044_dashboard-card-dialog-fixes.md)

## Request
User: "hey on dashboard CardSettings dialog want save button (currenlty when i change there any its direct reflicate on dashboard. but i want when user click on save then its save and reficate on dashboard until save if any changes are made on card view those are temporoy it will impact until save those."

Translation: the per-card "gear" quick-settings dialog (`Components/UI/Dashboard/CardSettings.razor`, opened from the dashboard toolbar, distinct from the full-page `/settings/CardConfiguration`) applied every control change live to the dashboard. Wanted edits to be a local/temporary preview until an explicit Save commits them.

## What was done
- Introduced a `_draft` working copy (`CardConfig`) inside `CardSettings.razor`. All controls (Card Size, Font Size, Decimal Places, Display Mode, Quick Properties toggles/Select All/Clear All) and the dialog's own live preview (`DeviceChannel`) now read/write `_draft`, not the bound `Config` parameter.
- `OpenDialog()` (wired to the gear button) clones `Config` into `_draft` each time the dialog opens, so a discarded edit never leaks into the next session.
- Added a **Save** button. `SaveConfigAsync()` copies `_draft` onto `Config`, persists to per-user localStorage, invokes `ConfigChanged` (which is what `DashboardView`'s `@bind-Config=@CConfig` uses to actually update the live dashboard), then closes the dialog.
- Changed `Reset` from an immediate-persist action to `ResetDraft()` — it now only resets the draft/preview; the dashboard stays unaffected until Save is also clicked.
- Closing via the dialog's X (its only close affordance; backdrop-click is already a no-op in `DialogContent.razor`) without Save simply abandons `_draft` — no separate "cancel" logic needed since the next open re-clones from `Config`.
- Removed the now-unused `ClearConfigFromLocalStorage()` method.

## Result
`dotnet build` → 0 errors (390 pre-existing warnings, unrelated).

**Live-verified 2026-08-20** via chrome-devtools MCP: ran the app (`dotnet run`, port 5066) + `HardwareSimulator/run_sim.py -n 16` (160 channels), logged in as `admin`. Confirmed all three legs of the save-gate end to end (driving the dialog's range input + Save/X buttons via `evaluate_script`, checking the real dashboard card's `style.width` behind the modal):
1. Dragging Card Size to 350 in the dialog updated the in-dialog preview immediately, while the real `1-1-1` dashboard card behind the modal stayed at its prior 210px — confirmed by DOM inspection excluding the dialog subtree.
2. Clicking **Save** closed the dialog and the dashboard re-flowed to the new 350px card width immediately (fewer, bigger cards per row) — confirmed via screenshot and `style.width` read.
3. Reopening, dragging to 500, then closing via the **X** (no Save) left the dashboard at 350px — the 500 draft was discarded. Reopening again afterward showed the draft freshly cloned at 350 (the last saved value), not the discarded 500.

Zero console errors/warnings throughout.

## Related bug found + fixed same session: Chart card lines invisible (T-42)
User pushed back after I reported the Chart display mode as "fully implemented" — screenshot showed the chart area rendering completely blank (no lines) for online circuits, not just the expected "Waiting for data..." placeholder for offline ones. Investigated with `evaluate_script`: the SVG `<polyline>` elements for Voltage/Current **were** present in the DOM with real, valid point data (`DeviceChannel.razor`'s `RenderMiniChart()`/`GetChartPoints` were working correctly) — but `getComputedStyle(document.documentElement).getPropertyValue('--chart-1')` returned `""`. Root cause: `stroke="hsl(var(--chart-1))"` and `text-chart-1`/`text-chart-2` (on the V/A legend icons) referenced a `--chart-1`/`--chart-2` CSS custom property and Tailwind color that **never existed anywhere** — not in `wwwroot/css/app.css`'s 6 theme blocks (`:root`, `.dark`, `.theme-shieldos`, `.theme-ironman`, `.theme-tech`, `.theme-future`), not in `tailwind.config.js`'s color palette, and consequently not in the actually-served `wwwroot/css/app.min.css` either (confirmed via `grep`/`wc` — same ADR-2 "no live rebuild pipeline" situation as prior sessions, but this time the variable was missing at the *source* level too, not just uncompiled). An invalid `hsl()` value with an empty variable makes the SVG stroke resolve to its initial value (effectively invisible), so the line was drawn but colorless.

**Fix:** added `--chart-1: 217 91% 60%` (blue, for Voltage) and `--chart-2: 27 96% 61%` (orange, for Current) to all 6 theme blocks in `wwwroot/css/app.css`, plus manual `.text-chart-1`/`.text-chart-2` utility classes (mirroring the existing hand-written `.bg-sidebar`/`.text-sidebar-foreground` pattern used elsewhere in that file for colors outside Tailwind's generated set, since `chart-1`/`chart-2` were never registered in `tailwind.config.js`'s `colors` extension either). Applied the identical edit directly to the served `wwwroot/css/app.min.css` (a single-line minified file, edited via a small Python script — same variable-insert-after-`--status-offline` + utility-class-append approach) since there is no build step to regenerate it from `app.css`. **Both files must be kept in sync by hand for any future CSS change** — this is the same constraint as ADR-2, just reconfirmed.

Live-reloaded (`ignoreCache: true`) and re-screenshotted: all cards now show real blue/orange Voltage/Current polylines that move over time as new telemetry ticks arrive. Zero console errors after the fix.

## Follow-up (T-43): chart had no scale — added min/max labels + live readout
User then asked, correctly, how anyone is supposed to read a *value* off the chart when each series (Voltage, Current) is independently auto-normalized to fill the plot height (`GetChartPoints`) — two visually-identical wiggly lines could represent completely different real ranges, and there was no number anywhere near the plot.

**Fix (`DeviceChannel.razor`'s `RenderMiniChart()`):** wrapped the `<svg>` in a `position:relative` div and added 4 absolutely-positioned corner labels sized off the same `voltageValues`/`currentValues` lists already used to draw the lines (so the label always matches the line's actual scale, no separate computation to drift out of sync): top-left/bottom-left = voltage max/min (blue, `text-chart-1`), top-right/bottom-right = current max/min (orange, `text-chart-2`). Also replaced the plain "V"/"A" legend text below the chart with the channel's *live* latest Voltage/Current reading (color-matched, using the existing `FormatValue()` helper so it respects the user's Decimal Places setting).

Live-verified: reran build (had to `taskkill` the running app PID first — `.razor` changes require a full rebuild+restart, not hot reload) + restarted `run_sim.py` reconnect, reloaded, and confirmed via screenshot that every online card shows e.g. "13.90" (top-left, blue) / "41.19" (top-right, orange) / "0.00"/"0.00" (bottom corners) plus a live "6.33 V" / "15.24 A" readout under the chart. Also checked readability at the smallest Card Size (150px) via the Save-gated dialog — labels stay legible, no overlap; restored the dashboard's card size back to 210px afterward. Zero console errors.

## Files changed (T-42 + T-43)
- `Components/UI/Dashboard/DeviceChannel.razor` (`RenderMiniChart()`: corner min/max labels + live V/A readout)
- `wwwroot/css/app.css` (`--chart-1`/`--chart-2` in all 6 theme blocks + `.text-chart-1`/`.text-chart-2` utilities)
- `wwwroot/css/app.min.css` (same, hand-patched — no build step links the two files, ADR-2)

## Follow-up (T-44): unified the two lines onto one shared Y-axis
User pushed back again, correctly: T-43's per-series min/max corner labels meant Voltage and Current were each independently normalized to fill the full plot height — so two lines with wildly different real magnitudes could look like they wiggle by the same amount, which is exactly the "which line means what" confusion the labels were supposed to resolve.

**Fix:** `GetChartPoints(List<double> data, double height)` → `GetChartPoints(List<double> data, double height, double min, double max)` — the min/max is now supplied by the caller instead of computed per-series internally. In `RenderMiniChart()`, both series now normalize against one shared `axisMin`/`axisMax` = `Min`/`Max` across the **combined** Voltage+Current values, so identical vertical movement means identical magnitude change no matter which line it's on. Replaced the 4 color-matched per-series corner labels with a single neutral-colored top/bottom pair showing the shared axis range; the live per-series V/A readout under the chart (color-matched, from T-43) is unchanged.

Required another full rebuild+restart (killed the running exe, `.razor` change). Live-verified: e.g. `1-1-1` shows shared axis `0.00`–`44.79`, with the Current line (real range up to ~44A) using most of the plot height and the Voltage line (real range ~9–14V) compressed near the bottom — an honest picture given the two units' real magnitude difference, no longer visually misleading. Zero console errors.

## Files changed (T-42 + T-43 + T-44)
- `Components/UI/Dashboard/DeviceChannel.razor` (`RenderMiniChart()` shared-axis normalization + `GetChartPoints` signature change)
- `wwwroot/css/app.css` / `wwwroot/css/app.min.css` (T-42's chart CSS vars, unchanged by T-44)

## Task sheet updated
Added 3 rows to `docs/WebAppME_TaskSheet.xlsx`'s "Task Sheet" (rows 58-60, S.No 57-59, continuing the "Dashboard Card & Dialog Enhancements" project group as Task No. 8-10 — customer-facing phrasing, no code/file references): Save button on the quick Card Settings dialog (T-41), fix invisible Chart-mode Voltage/Current lines (T-42), add a readable shared value scale to the Chart card (T-43+T-44 merged into one row since T-44 same-day superseded T-43's per-series approach). Updated the "Summary" sheet's `COUNTIF`/`COUNTIFS` range formulas from `$...$57` to `$...$60` (86 formulas) so the status/category/project rollups include the new rows — no new Category or Project row needed since "Web UI (Blazor Dashboard)" and "Dashboard Card & Dialog Enhancements" already existed. Verified no encoding corruption (an em dash briefly got mangled to U+FFFD via an inline `python -c` call through the bash tool — always write a `.py` script file with `# -*- coding: utf-8 -*-` instead of `-c` for any non-ASCII text going into this workbook).

## Files changed
- `Components/UI/Dashboard/CardSettings.razor`

## Follow-up (not done this session)
- Not touched: `Components/Pages/Settings/CardConfiguration.razor` (the full-page settings version) still applies changes immediately — out of scope, user's request was specific to the dashboard's quick dialog.
