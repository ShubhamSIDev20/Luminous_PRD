# Session #19 — DbContext concurrency under 64-channel registration storms

> Date: 2026-08-18 (21:30–22:00Z) | Agent: Claude (Opus 5) | Branch: `main` (user chose direct-to-main again)
> Task: [T-29](../tasks/2026-08-18_dbcontext-concurrency-hardware-event-storm.md)

## Goal

User ran the app plus `python run_sim.py -n 64` (also tried `-n 50`) and got a flood of:

```
System.InvalidOperationException: A second operation was started on this context instance
before a previous operation completed.
   at Microsoft.EntityFrameworkCore.Infrastructure.Internal.ConcurrencyDetector.EnterCriticalSection()
   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
GetChannelsAsync Error: A second operation was started on this context instance ...
```

They asked specifically whether the repeated `GetChannelsAsync` work and the `ServiceLocator`
pattern were to blame, and to check the session-#18 notification code for the same defect.

## Root cause

**`ServiceLocator` was exonerated.** `ServiceLocator.GetScoped<T>()` creates a fresh
`IServiceScope` per call and disposes it (`Services/Implementations/ServiceLocator.cs:37-44`),
so every call already gets its own `AppDbContext`. Same for `AlarmService`, which scopes per
operation. Neither was the source.

The culprit was the **Blazor circuit-scoped** `DbContext`:

- `DashboardView.razor` subscribes `CM.HardwareManagerChanged += HandleDeviceChange` (`:502`).
- `HandleDeviceChange` was `public async void` with no re-entrancy guard, and called
  `SyncDevicesWithDatabase()` → `DCService.GetChannelsAsync()`.
- `DCService` is `@inject`ed, therefore **scoped**, therefore in Blazor Server it is one
  instance per circuit sharing **one** `AppDbContext` with every other scoped service on that
  circuit.
- `ChannelManager` raised `HardwareManagerChanged` once per channel from TCP accept/registration
  threads (`ChannelManager.cs:534, 569, 913, 1029, 1154`).

64 registrations in a second ⇒ ~64 fire-and-forget handler invocations ⇒ ~64 overlapping
queries on one `AppDbContext` ⇒ `ConcurrencyDetector` throws. The handler also never ran on the
circuit's synchronization context, since it executed on whichever hardware thread raised.

The `SocketException 10054` spam in the user's log is unrelated — the simulator closing sockets.

## The notification code had the same shape, milder

`MainLayout.razor:78 private async void HandleAlarmChanged(...)` (added session #18) is the same
fire-and-forget pattern. It could **not** corrupt a DbContext — `AlarmSvc` is a singleton that
scopes per call — but under a comms-loss burst it fired one full 50-row `GetActiveAsync()` per
event, and called `Toasts.Error` + `JS.InvokeVoidAsync("AlarmSound.beep")` **from a hardware
thread**. JS interop off the circuit's sync context is genuinely unsafe. Fixed in the same pass.

## Changes

| Commit | What |
|---|---|
| `7f4b4bd` | New `Services/Implementations/CoalescingRunner.cs` + 5 unit tests |
| `d1800af` | `ChannelManager` producer coalescing; `DashboardView` + `MainLayout` consumers rewired |
| `330b092` | `SharedDbContextConcurrencyTests` — headless reproduction + proof of the guard |

**Producer** (`ChannelManager.cs`): all five raise sites now route through
`RaiseHardwareManagerChanged()`, which only sets `_hwDirty`. A 250 ms `_hwFlushTimer` calls
`FlushHardwareManagerChanged()`, which fires subscribers at most once per tick. Timer created in
`ExecuteAsync` **before** `LoadDevicesAsync` (that call adds handlers, which raise) and disposed
in `Dispose`.

**Consumers**: `DashboardView.HandleDeviceChange` and `MainLayout.HandleAlarmChanged` dropped
`async void` for `CoalescingRunner.Request(...)`, and do their DB work inside `InvokeAsync` so a
circuit-scoped `DbContext` is only touched on the circuit's synchronization context. MainLayout's
toast + JS beep moved inside `InvokeAsync` too.

`DeviceList.razor:391` and `Calibration.razor:479` were checked and **deliberately left alone** —
both handlers are plain `void` and touch only the `ConcurrentDictionary`, no DB on the event path.

## Verification

- `dotnet build`: 0 errors. `dotnet test`: **57/58** — the one failure,
  `ChannelAddressCodecTests.Encode_OutOfRange_Throws(board: 0, channel: 1)`, is **pre-existing**
  and stale since T-19 deliberately made board `0` legal. Untouched this session.
- **A/B load test at 640 channels** (`-n 64` × 10 devices), both runs restored from an identical
  DB snapshot so they were actually comparable:

  | | BASELINE (`b510399`) | FIXED (`d1800af`) |
  |---|---|---|
  | second-operation errors | 0 | 0 |
  | SQLite Error 5 | 0 | 0 |
  | ERR lines | 0 | 0 |
  | sim 10054 disconnects | 0 | 0 |

  No regression. Note the headless run **cannot prove the fix** — without a browser there is no
  circuit subscribed to `HardwareManagerChanged`, so neither build reproduces the bug.
- **`SharedDbContextConcurrencyTests`** closes that gap: 64 overlapping queries on one real
  `AppDbContext` throw `"second operation"` unguarded, and never throw through `CoalescingRunner`.

## Discoveries / gotchas

- 🪤 **A naive `SemaphoreSlim.WaitAsync(0)` + volatile-flag single-flight has a lost-update
  race**: a request arriving between the final pending-check and `Release()` disappears.
  `CoalescingRunner` takes a short `lock` around *both* the flag check and the running-state
  transition instead. Do not "simplify" it back.
- 🪤 **Periodic flush, not a debounce.** A restart-the-window-on-every-event debounce can be
  starved indefinitely by a device in a reconnect loop. The dirty-flag + fixed-interval flush has
  a hard 250 ms ceiling and always fires.
- ⚠️ **Reproducing this class of bug needs an open Blazor circuit.** A headless
  server+simulator run is blind to it. Prefer an integration test over a load test here.
- ⚠️ `StartStoreWorkerAsync` (`ChannelCommandHandler.cs:209`) holds
  `using var StoreService = ServiceLocator.GetScoped<...>()` for the **entire worker-loop
  lifetime** — 64 channels means 64 permanently-open scopes/DbContexts. Not a correctness bug
  (single-threaded per loop) but real handle pressure. User chose to defer → **T-30**.
- ⚠️ At `-n 64` × 10 devices (640 circuits) the simulator only ever completes **104**
  registrations before the server stops accepting, in **both** baseline and fixed builds. A
  pre-existing capacity ceiling, unrelated to this fix → **T-31**.
- 🪤 Escalation must stay per-event, not coalesced: `AlarmPolicy` already rate-limits it
  server-side (cooldown + storm guard), so collapsing it client-side would suppress it twice.
- Dev DB is SQLite at `D:\MEWebApp\BtsAppdb.db` (WAL mode — a snapshot needs `.db`, `.db-wal`
  **and** `.db-shm` to be consistent).

## Environment note

The Chrome extension was not connected this session, so no browser verification was possible —
and I cannot type credentials into the login form regardless. The logged-in dashboard check is
left to the user → **T-29 follow-up**.

## Left running / cleanup

Nothing. App and all 6 leftover simulator processes stopped; tree clean on `main` apart from the
pre-existing untracked `docs/BM_Manual_eng.pdf` and `docs/UATBUG.xlsx`. ⚠️ The dev DB now
contains simulator channels registered during testing.
