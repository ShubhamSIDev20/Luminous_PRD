# ADR-4 — Hardware listeners retry forever and never stop the host

> Date: 2026-08-18 | Session #17 | Status: Accepted
> Scope: `Services/ChannelManager.cs` (`SuperviseAsync`), `Services/ListenerHealthCheck.cs`

## Context

TCP 9999 (commands), UDP 10000 (live view) and UDP 10001 (storage) must be bound for the product to
function at all. Before this change every listener ran as a bare `Task.Run` whose Task was never
awaited, so any exception — a failed bind because a stale instance still held the port, a dead socket,
an ICMP-induced `ConnectionReset` — silently killed the port while the process kept serving the web UI
and reporting healthy. See session #17 for the full defect list.

Fixing that required choosing a failure policy for the new supervisor.

## Options considered

1. **Bounded retry, then rethrow to stop the host.** `BackgroundService`'s default
   `BackgroundServiceExceptionBehavior.StopHost` would tear the process down, letting the Windows
   Service recovery action / container orchestrator restart everything. Loud and unambiguous.
2. **Retry forever, quiet.** Self-heals, but an operator has no signal that a port has been down for
   an hour.
3. **Retry forever + escalating signal.** (Chosen.)

## Decision

Retry **forever** with exponential backoff from **1s to a 30s ceiling**. Never rethrow, never stop the
host. After **5 consecutive failures** the log escalates from `Error` to `Fatal` and a
`ChannelManager.ListenerHealthTopic` (`"listener-health"`) event is published carrying every
`ListenerState`, so the UI can raise a banner. A new `ListenerHealthCheck` reports each port's state
through the existing `AddHealthChecks()` endpoint (`Unhealthy` if any listener is not bound).

A listener that bound successfully and later died resets the attempt counter to 1 (fast retry);
repeated *bind* failures keep climbing toward the ceiling and the Fatal escalation
(`ChannelManager.NextAttempt`).

## Why not option 1

These are customer-site installations. Stopping the host takes the web UI down **with** the ports and
removes the operator's only remote diagnostic surface — a strictly worse outcome than a port that is
down but loudly retrying. It also turns the common transient case (a stale instance holding 9999 for a
few seconds during a redeploy) into a restart loop instead of a self-heal.

## Consequences

- A port permanently owned by a *different* application will retry every 30s forever. That is
  intentional; the `Fatal` log line + `Unhealthy` health check + event-bus publish are the escalation
  path, not a process exit.
- The process can be alive with ports down. **Any future liveness/monitoring work must read the health
  check, not process presence.**
- Nothing subscribes to `ListenerHealthTopic` yet — the UI banner is unimplemented (see T-24).
