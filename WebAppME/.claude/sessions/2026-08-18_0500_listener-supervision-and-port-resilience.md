# Session #17 — Listener supervision & port resilience (TCP 9999 / UDP 10000+10001)

> Date: 2026-08-18T05:30:00Z
> Agent: Claude (Opus 5)
> Status: ✅ Build-verified (`dotnet build` 0 errors, 387 pre-existing warnings) — **not yet live-verified**

---

## Goal

User asked what happens if `RunCommandListenerAsync` or `StartInternal` throws, given the
application depends on **TCP 9999** and **UDP 10000 / 10001** always being bound. Answered with a
failure analysis, then (user: *"you pick which is best"*) implemented the full hardening set and a
retry policy.

## The answer (failure analysis of the pre-change code)

`StartInternal()` fires four `Task.Run(...)` calls and stores the Tasks in fields that are **never
awaited** except at shutdown. In .NET an unobserved faulted Task does not crash the process, and
`ExecuteAsync` parks on `Task.Delay(Timeout.Infinite)` inside a catch-all. Net effect of any listener
exception: **port silently dead, app still reports healthy.**

Specific defects found:

1. **TCP bind failure was fatal + silent.** `new TcpListener` / `SetSocketOption` / `Start()` sat
   *outside* the `try`. `SocketException(AddressAlreadyInUse)` — stale process, second instance —
   escaped into `Task.Run` and vanished. No log, no retry, 9999 never listened again.
2. **Broken accept socket = 100% CPU hot loop.** `catch (Exception) { _log.Error(...) }` re-looped
   with no delay and no validity check. A permanently-bad socket throws instantly every iteration →
   tight spin + log flood.
3. **`finally { HardwareManagerChanged?.Invoke(); }`** fired on every accept *and* every failure
   (UI event storm under #2), and being in `finally` a throwing subscriber bypassed both catches,
   exited the `while`, and killed the listener.
4. **UDP listeners were weaker still** — the `while` loop was *inside* the `try`, so **one**
   exception on **one** datagram permanently ended the listener. Real trigger on Windows: sending a
   datagram to a powered-off device makes the ICMP port-unreachable surface as
   `SocketException(ConnectionReset)` on the **next** `ReceiveAsync`. One offline device could kill
   the whole data pipe.
5. **`RestartService()` was a landmine.** `_udpChannel` was a `readonly` field created once;
   `StopInternalAsync` called `Writer.TryComplete()` (irreversible) and `StartInternal` did not
   recreate it. After one restart the processor exited immediately and
   `_ = _udpChannel.Writer.WriteAsync(result)` returned a discarded faulted `ValueTask` → **silent
   total data loss, no log**. Mitigated only by the fact that nothing calls `RestartService()`.
6. `StartInternal` overwrote `_lifecycleCts` without disposing the old one; handlers hold the old
   token via `handler._cts = _lifecycleCts`.

## Decision made (ADR-4)

**Retry forever, exponential backoff 1s → 30s cap, never stop the host; escalate to `Fatal` +
event-bus publish after 5 consecutive failures.** Rationale: at a customer site the hardware ports
matter more than this process's liveness, and stopping the host would take the web UI — and any
chance of remote diagnosis — down with the ports. See
`DECISIONS/2026-08-18_listener-retry-forever-policy.md`.

## Files changed

| File | Change |
|------|--------|
| `Services/ChannelManager.cs` | All six fixes below |
| `Services/ListenerHealthCheck.cs` | **New** — `IHealthCheck` reporting per-listener state |
| `Program.cs` | `.AddCheck<ListenerHealthCheck>("hardware-listeners")` on the existing `AddHealthChecks()` |

### `ChannelManager.cs` in detail

- **New supervision region** (after `_log`): `ListenerBackoffFloorMs` (1000), `ListenerBackoffCeilingMs`
  (30000), `ListenerEscalateAfter` (5), `ListenerHealthTopic` (`"listener-health"`), a
  `ListenerState` record, `_listenerStates` dictionary, and public `ListenerStates` /
  `AllListenersHealthy`.
- **`SuperviseAsync(name, port, run, token)`** — wraps each listener; retries on throw *and* on an
  unexpected clean return; `NextAttempt` resets the counter to 1 if the listener had successfully
  bound (so a long-lived listener that dies gets a fast retry, while repeated bind failures climb to
  the ceiling and the Fatal escalation). `BackoffMs` = `1000 * (1 << min(attempt-1, 5))` capped at 30s.
- **`RunCommandListenerAsync`** — binds *inside* the supervised body; `MarkListenerListening`;
  per-accept try/catch with a `maxAcceptFailures = 5` counter + 500 ms delay that `throw`s the broken
  socket back to the supervisor for a rebind; `RaiseHardwareManagerChanged()` moved out of `finally`
  onto the success path; `finally` stops the listener and nulls `_commandListener` if it still points
  at this instance.
- **Both UDP listeners** — `while` moved **outside** the `try`; per-iteration catch;
  `SocketError.ConnectionReset` swallowed at Debug; `ReceiveAsync(token)` (cancellable overload);
  `maxReceiveFailures = 5` then rethrow to the supervisor; `DisableUdpConnReset(udp)` sets
  `SIO_UDP_CONNRESET(false)` on Windows so the ICMP case never throws at all; view listener also
  wraps `OnUdpViewDataReceived?.Invoke` in its own try.
- **Channel lifecycle** — `readonly` field replaced by `static CreateUdpChannel()` + mutable
  `_udpChannel`; `StartInternal` creates a fresh channel per lifecycle and **passes the instance** to
  both `RunUdpStoreListenerAsync(channel, t)` and `StartUdpProcessorAsync(channel, token)` (parameter,
  not field read — removes the restart race); writer now uses `TryWrite` with an error log instead of
  a discarded `WriteAsync`.
- **`StartUdpProcessorAsync`** — wrapped in try/catch; logs `Fatal` if it exits while not cancelled.
- **`StartInternal`** — disposes the previous `_lifecycleCts`; captures `token` locally instead of
  re-reading the field in the `_udpViewHandler` closure.

## Verification

- `dotnet build -v q` → **0 errors**, 387 warnings (all pre-existing; `ChannelManager.cs:629`
  CS1998 and `:820` CS8602 are in the untouched `ViewUdpData` / tail region).
- ⚠️ **Not live-verified.** Nothing has been run against the simulator or real hardware yet — see
  the follow-up task.

## Discoveries / gotchas

- 🪤 **`Program.cs` edit failed once** with *"being used by another process"* (atomic temp+rename
  blocked by the running app / IDE); **an immediate retry of the identical edit succeeded** — same
  Windows behaviour recorded in session #16 for `simulator.py`. Retry before assuming failure.
- `ChannelManager` is registered **both** `AddSingleton<ChannelManager>()` and
  `AddHostedService(provider => provider.GetRequiredService<ChannelManager>())`
  (`Extensions/ServiceCollectionExtensions.cs:80,82`) — which is why `ListenerHealthCheck` can take
  it as a constructor dependency and observe the live instance.
- `EventBusService` has **no topic constants** — the only existing publish uses a *file path* as the
  topic (`ChannelManager.cs`, `decoded.Data.filePath`). `ListenerHealthTopic` is therefore a new
  const on `ChannelManager`; **nothing subscribes to it yet**, it is a hook for a UI banner.
- The health endpoint previously covered only `AddDbContextCheck<AppDbContext>()` — the three ports
  the whole product depends on were invisible to it.
