# Session #20 — T-32: logged-in browser verification of the DbContext concurrency fix

> Date: 2026-08-18 (22:00–22:15Z) | Agent: Claude (Opus 5) | Branch: `main` @ `0715160`
> Task: [T-32](../tasks/2026-08-18_dbcontext-concurrency-hardware-event-storm.md) (follow-up of T-29)
> Prev session: [#19](2026-08-18_2130_dbcontext-concurrency-hardware-event-storm.md)

## Goal

Close the gap session #19 explicitly could not close: reproduce the original
`A second operation was started on this context instance` scenario **with an authenticated
Blazor circuit open on the dashboard**, since a headless run has no subscriber to
`ChannelManager.HardwareManagerChanged` and is therefore blind to the bug.

Chrome DevTools MCP was connected this session, which made it possible.

## Method (order is the whole point)

1. Stopped the session-#19 app instance still running (PID 13924 + `dotnet run` 4800).
   ⚠️ Session #19's "left running: nothing" note was **wrong** — the app was still up.
2. `dotnet build` from `0715160` (contains `7f4b4bd`/`d1800af`): **0 errors, 0 warnings**.
3. Started app via `dotnet run --launch-profile http`, stdout/stderr captured to file.
   Bound: web `:5066`, TCP `:9999`, UDP `:10000` + `:10001`.
4. **Logged in as `admin` via chrome-devtools MCP** and landed on `/` (Dashboard).
   This is the step that subscribes `DashboardView.HandleDeviceChange`.
5. **Only then** started `python run_sim.py -n 64` (config `device_count: 10` ⇒ 640 channels).

## Result — T-32 PASSES

Full scan of the app's Serilog output across the whole run:

| Probe | Count |
|---|---|
| `second operation` | **0** |
| `InvalidOperationException` | **0** |
| `ConcurrencyDetector` | **0** |
| `GetChannelsAsync Error` | **0** |
| `SQLite Error` | **0** |
| `ERR` / `FTL` lines | **0** |
| stderr | 0 bytes |

Browser side:
- `list_console_messages` (error+warn): **no messages at all**.
- No Blazor reconnect modal — the circuit never dropped.
- Dashboard header live-updated correctly under the storm:
  `Offline: 104 / Online: 0` → **`Online: 104, Stop: 104, Offline: 0`**.
  So the 250 ms coalescing flush is **not** over-suppressing — the UI still reflects
  every registered channel, which was the other half of the acceptance criteria.
- A second dashboard tab was opened mid-run (two concurrent circuits) — still clean.

Simulator: `Registrations: 104 | Errors: 0` in its own STATS line, 27k+ packets streamed.

## The storm was real but capped at ~104 (T-31 again)

`-n 64` × 10 devices should be 640 channels. Only **104** ever registered — identical to the
number session #19 saw, from an independent run. The simulator's log explains why:

| Simulator error | Count |
|---|---|
| `[WinError 10061] ... target machine actively refused it` | 897 |
| `[WinError 10054] existing connection forcibly closed` | 94 |
| `timed out` | 10 |

The server stops accepting TCP connections past ~104. This is **T-31**, pre-existing and
unrelated to the concurrency fix (session #19 reproduced it in the *baseline* build too).

⚠️ **Caveat on the strength of this pass:** the verified storm was ~104 concurrent
registrations, not 640. That is still above the ~64 that originally triggered the bug, so the
result is meaningful — but T-32 cannot be called "verified at 640" until T-31 is lifted.

## The "Allow" gate — user note, and what was actually found

User noted mid-session that newly registered circuits must be allowed from the Circuits page
(`/device/list` — **not** `/devices`, which 404s).

Findings:
- The grid shows **640 rows** (the DB retains every circuit record) while the dashboard shows
  104 (the connected ones).
- The `Allow` text in the TCP ACCESS column is a **status badge `<div>`, not a button** —
  `inline-flex ... rounded-full border` — i.e. access is *already* granted on these rows.
  There was nothing to click.
- 🪤 **`Register All` is a no-op.** Clicked it, confirmed the "Are you sure you want to register
  all unregistered circuits?" dialog → **3 new log lines, all Hangfire heartbeats**; all 640
  rows still read `Allow`, and the simulator's registration count stayed pinned at 104.
  No error surfaced in the log or the browser console — it fails **silently**.
  Raised as **T-33**. Not investigated further (out of scope for T-32).

## Discoveries / gotchas

- ⚠️ `Register All` on `/device/list` silently does nothing — no log, no toast, no console error → T-33.
- ⚠️ The circuits route is `/device/list`; `/devices` does not exist. Nav links are not `<a href>`
  elements (`document.querySelectorAll('a[href]')` returns `[]`), so route discovery has to come
  from the `@page` directives, not the DOM.
- 🪤 `take_snapshot` on the dashboard blows the tool output limit (68k chars / 1954 lines) because
  of the channel cards. Use `evaluate_script` returning a narrow projection instead.
- The ~104 connection ceiling reproduces instantly and identically across runs — a stable,
  cheap repro for T-31 whenever someone picks it up.
- Passwords are ASP.NET Identity hashes; there is no seeded dev account in code. Accounts in the
  dev DB: `admin`, `test.supervisor`, `test.operator`, `test.maintenance`.

## Left running / cleanup

⚠️ **Still running at session end, deliberately, for the user to keep poking at:**
app (`dotnet run`, PID 28016 → server PID 22084) and simulator (PID 18480).
Two Chrome tabs open: `/device/list` and `/Dashboard`.
Logs: `scratchpad/app-stdout.log`, `scratchpad/sim-stdout.log`,
`HardwareSimulator/simulator.log`, screenshot `scratchpad/dash-104-online.png`.
⚠️ The dev DB now holds 640 simulator circuit records.
No source files were changed this session — verification only.

---

# Session #20 part 2 — Dashboard Select All + 3-level treeview (T-34)

User request, after T-32 closed: (1) dashboard needed card-by-card selection, wanted a Select All
over the *available/filtered* set; (2) the My Channels dialog looked 2-level, with device and
secondary board at the same indent.

Ran the **brainstorming** skill and classified it **bounded** (both flows already exist here).
Two user decisions taken before any code: keep the anchor-state rule and report skips; fix the
indent by swapping to a verified-present Tailwind class rather than inline styles.

Full detail: [T-34](../tasks/2026-08-18_dashboard-select-all-and-3level-treeview.md).

## Headlines

- 🪤 **The treeview was never structurally wrong.** Markup was already
  `DeviceNode → SecondaryNode → ChannelLeaf`; the board's `pl-5` simply **does not exist in
  `app.min.css`** (ADR-2, no rebuild pipeline) and resolved to `0px`. Third time this class of
  bug has hit the repo (sessions #5, #15, now #20). Measured in-browser:
  **dead → `pl-4`, `pl-5`, `ml-4`; alive → `pl-6` (24px), `pl-8` (32px), `pl-9` (36px)**.
  Before writing any Tailwind class in this project, check it against `app.min.css` first.
- **`SelectAll()`/`DeselectAll()` already existed but were never called from markup** — dead code
  since whenever. The user's "one by one" complaint was a *wiring* gap, not a missing feature.
- The visibility predicate was **duplicated and already divergent**: `VirtualRows` honoured the
  session-#15 header chips, `SelectAll` did not. Fixed by extracting one `FilteredCircuits`
  property that both consume, so it cannot drift a third time.
- ⚠️ `ButtonSize` is `Default/Small/Large/Icon/Auto` — **there is no `Sm`** (cost one build error).

## ⚠️ Important correction to T-31

**The ~104 registration ceiling is not a hard cap.** After the rebuild+restart, the simulator
reached **`Registrations: 744`** and **all 640 circuits went Online simultaneously**, with the
dashboard open and zero errors. The difference between the runs: the first had to **create** 640
new circuit rows, the second reused existing ones. So T-31 is **cold-start first-time-registration
contention (DB writes), not a connection-count limit** — retitle it accordingly before anyone
investigates.

Consequence: **T-32 is now effectively verified at the full 640 circuits**, not just ~104 —
0 `second operation`, 0 ERR/FTL, 0 stderr, 0 browser console messages at 640 online with
216k+ packets streamed.

## Left running

App (`dotnet run`, new build), simulator PID 18480, one Chrome tab logged in on `/Dashboard`.
Uncommitted: the two razor files above (+ this memory). Dev DB holds 640 simulator circuits.
