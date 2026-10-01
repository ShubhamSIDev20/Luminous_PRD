# ADR-5: AlarmService uses its own event, not EventBusService

**Date:** 2026-08-18
**Session:** #18
**Status:** Accepted

## Context

Building the alarm/notification system (session #18), the natural first instinct was to publish alarms through the existing `Services/EventBusService.cs` singleton, which `ChannelManager` already depends on for other pub/sub needs.

## Why it was rejected

Reading `EventBusService.cs` end to end surfaced two disqualifying problems:

1. **Subscribers silently expire.** `PublishAsync` only refreshes a subscription's `LastActive` timestamp when a message is actually published on that topic. A background `Cleanup` timer runs every few minutes and removes any subscription whose `LastActive` is older than `_expiry` (5 minutes). If an alarm topic goes quiet for 5+ minutes — entirely plausible for, say, a `channel-error` topic when nothing is going wrong — the bell's subscription is garbage-collected without warning, and the next alarm on that topic is silently dropped. This is exactly backwards for something whose entire job is "don't miss the next occurrence."
2. **No synchronization.** `_handlers` is a plain `Dictionary<string, List<Subscription>>` mutated from `Subscribe`, `Unsubscribe`, `PublishAsync`, and the `Cleanup` timer callback — all of which can run concurrently from different threads (producers, the timer, and Blazor circuit threads). No lock guards it.

Both are acceptable for `EventBusService`'s existing use cases (presumably short-lived, frequently-active subscriptions) but disqualifying for an alarm bus, where "notify me the instant something is wrong, even if nothing has been wrong for hours" is the whole point.

## Decision

`AlarmService` (`Services/Alarms/AlarmService.cs`) exposes its own plain C# event:

```csharp
public event Action<AlarmChanged>? OnChanged;
```

Fired outside its internal `SemaphoreSlim` lock (after persist, before returning from `RaiseAsync`/`AcknowledgeAsync`/etc.) so a slow UI-side handler can never block a producer. `MainLayout` subscribes once in `OnInitializedAsync` and unsubscribes in `Dispose`.

## Consequences

- No cross-cutting change to `EventBusService` was made or is implied by this decision — it remains exactly as suited (or unsuited) to its existing callers as before.
- Any future code needing a similarly "must never silently stop receiving" pub/sub channel should look at this pattern (plain event, explicit subscribe/dispose lifecycle) rather than `EventBusService`.
- If `EventBusService`'s expiry/locking issues are ever fixed, this decision could be revisited — but that fix is out of scope for session #18 and not currently tracked as a backlog item.
