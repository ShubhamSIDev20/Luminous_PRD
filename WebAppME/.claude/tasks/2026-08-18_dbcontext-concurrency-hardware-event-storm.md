# T-29 — Fix DbContext concurrency collisions under hardware-event storms

> Status: ✅ Done (code) — ⚠️ logged-in browser verification outstanding
> Session: #19 | Date: 2026-08-18 | Branch: `main`
> Session log: [sessions/2026-08-18_2130_dbcontext-concurrency-hardware-event-storm.md](../sessions/2026-08-18_2130_dbcontext-concurrency-hardware-event-storm.md)

## Problem

Running the app with `python run_sim.py -n 64` (or `-n 50`) flooded the log with
`InvalidOperationException: A second operation was started on this context instance before a
previous operation completed`, plus `GetChannelsAsync Error: ...` on the console.

## Root cause

Not `ServiceLocator` (it scopes correctly per call) and not `AlarmService` (singleton, scopes per
operation). It was the **Blazor circuit-scoped `AppDbContext`**: `DashboardView`'s
`async void HandleDeviceChange` was subscribed to `ChannelManager.HardwareManagerChanged`, which
is raised once per channel from hardware threads. 64 registrations ⇒ 64 unawaited overlapping
`GetChannelsAsync` calls on the one context every scoped service on that circuit shares.

## Fix

- **Producer** — `ChannelManager` raises now set a dirty flag; a 250 ms flush timer emits at most
  one event per tick. Chosen over a debounce, which a reconnect-looping device could starve.
- **Consumer** — new `CoalescingRunner` (lock-guarded single-flight, collapses a burst into one
  follow-up run) replaces `async void` in `DashboardView` and `MainLayout`; DB work moved inside
  `InvokeAsync` so the circuit-scoped context is only touched on the circuit's sync context.
- `MainLayout`'s toast + `AlarmSound.beep` JS interop also moved onto the circuit thread — they
  were running on hardware threads since session #18.

`DeviceList` and `Calibration` handlers verified safe and left unchanged.

## Commits

- `7f4b4bd` — `CoalescingRunner` + 5 unit tests
- `d1800af` — producer coalescing + consumer rewiring
- `330b092` — `SharedDbContextConcurrencyTests` (headless reproduction + guard proof)

## Verification

- Build 0 errors; tests 57/58 (the 1 failure is the pre-existing stale
  `ChannelAddressCodecTests.Encode_OutOfRange_Throws(board: 0)` from T-19).
- 640-channel A/B load test from an identical DB snapshot: baseline and fixed both clean, no
  regression. ⚠️ Headless runs cannot reproduce the bug — no circuit is subscribed.
- `SharedDbContextConcurrencyTests` proves the mechanism and the guard deterministically.

## Still needed

Logged-in browser check at `-n 64`: dashboard open, confirm no `second operation` errors, the
grid still reflects every registered channel, and the alarm bell still toasts/beeps. Blocked this
session — Chrome extension not connected, and credentials must be entered by the user.

## Spawned

- **T-30** — long-lived scope in `StartStoreWorkerAsync` (user deferred deliberately)
- **T-31** — 640-circuit registration ceiling (~104 accepted, pre-existing in both builds)

---

## Session #20 follow-up — T-32 closed ✅

Verified in-browser on 2026-08-18 (22:00–22:15Z), build `0715160`.

**Method:** app started fresh, **logged in as `admin` and landed on the Dashboard first**, only
then `python run_sim.py -n 64`. That ordering is essential — session #19's headless load test
could not reproduce the bug at all because no Blazor circuit was subscribed to
`ChannelManager.HardwareManagerChanged`.

**Result:** clean on every stream.

| Probe | Count |
|---|---|
| `second operation` / `InvalidOperationException` / `ConcurrencyDetector` | 0 |
| `GetChannelsAsync Error` / `SQLite Error` | 0 |
| app `ERR` / `FTL` lines, stderr bytes | 0 |
| browser console messages (error + warn) | 0 |
| Blazor reconnect modal | never shown |

The dashboard header also updated correctly under load — `Offline: 104 / Online: 0` →
`Online: 104, Stop: 104, Offline: 0` — confirming the 250 ms coalescing flush suppresses the
*storm* without suppressing the *update*. A second concurrent dashboard circuit was opened
mid-run and stayed clean too.

⚠️ **Scope of the pass:** the storm topped out at ~104 concurrent registrations, not the
intended 640, because of the pre-existing **T-31** ceiling (897 × `WinError 10061 actively
refused`). 104 is comfortably above the ~64 that originally triggered the bug, so the result is
meaningful — but "verified at 640" is still not claimable until T-31 is lifted.

Side finding: `Register All` on `/device/list` is a silent no-op → **T-33**.
