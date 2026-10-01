# ADR-6 — Hardware events are coalesced at the producer and single-flighted at the consumer

> Date: 2026-08-18 | Session: #19 | Status: Accepted
> Supersedes nothing. Related: [ADR-1](2026-08-07_device-connection-multiplexing.md), [ADR-5](2026-08-18_alarmservice-bypasses-eventbusservice.md)

## Context

`ChannelManager.HardwareManagerChanged` is raised from hardware threads (TCP accept, registration,
client teardown) — historically once per channel. Its subscribers are Blazor components that
reload through `@inject`ed, i.e. **scoped**, services.

In Blazor Server, scoped means *per circuit*, so every scoped service on a circuit shares one
`AppDbContext`. Registering 64 channels therefore produced ~64 unawaited overlapping queries on a
single context, and EF Core's `ConcurrencyDetector` threw
`A second operation was started on this context instance before a previous operation completed`.

## Decision

Two independent guards, both required:

1. **Producer coalescing.** `RaiseHardwareManagerChanged()` only sets an `Interlocked` dirty flag.
   A 250 ms `_hwFlushTimer` invokes subscribers at most once per tick.
2. **Consumer single-flight.** UI subscribers use `Services/Implementations/CoalescingRunner.cs`
   instead of `async void`, and perform DB work inside `InvokeAsync` so a circuit-scoped
   `DbContext` is only ever touched on the circuit's synchronization context.

## Why a periodic flush rather than a debounce

A classic trailing-edge debounce restarts its window on every event, so a device stuck in a
reconnect loop — raising faster than the window — would starve the flush **indefinitely** and the
UI would never update. A dirty flag plus a fixed-interval timer has a hard 250 ms ceiling and
always fires. Cost is up to 250 ms of UI lag, which is strictly better than colliding reloads.

## Why `CoalescingRunner` uses a lock, not `SemaphoreSlim.WaitAsync(0)`

The obvious implementation — a volatile `_pending` flag plus `WaitAsync(0)` — has a lost-update
race: a request arriving between the run loop's final `_pending` check and its `Release()` sees
the gate still taken, returns immediately, and is dropped. `CoalescingRunner` takes a short `lock`
around **both** the pending check and the running-state transition, closing that window.

**Do not "simplify" either mechanism back.** Both shapes look like needless ceremony and both are
load-bearing.

## Why not `IDbContextFactory`

Migrating all 9 repositories from injected `AppDbContext` to `IDbContextFactory<AppDbContext>` is
Microsoft's official Blazor Server guidance and would make this class of bug structurally
impossible. It was offered and **not** chosen for this fix — too large and risky a change for a
targeted bug fix. It remains the right long-term direction if this recurs elsewhere.

## Consequences

- UI reflects hardware changes up to 250 ms later. Acceptable and invisible in practice.
- Any **new** subscriber to `HardwareManagerChanged` that touches scoped services must use
  `CoalescingRunner` + `InvokeAsync`. A plain `async void` handler re-introduces the bug.
- Subscribers that only touch `ChannelManager._devices` (a `ConcurrentDictionary`) and call
  `StateHasChanged` need no guard — `DeviceList.razor` and `Calibration.razor` are deliberately
  left as plain `void` handlers.
- Alarm **escalation** stays per-event rather than coalesced: `AlarmPolicy` already rate-limits it
  server-side, so collapsing it client-side would suppress it twice.
