# Alarm & Notification System — Design

Date: 2026-08-18
Status: Approved (design); implementation plan to follow
Area: `Components/Layout`, `Services`, `Models/Entities`, `Repositories`, `Components/Pages/Settings`

## 1. Context

The navbar has a notification bell (`Components/Layout/Navbar.razor:69-119`) backed by a
minimal `NotificationItem` model (`Components/Layout/LayoutModels.cs:25`) and per-device
`event Action<NotificationItem>? OnNotify` subscriptions managed inside
`MainLayout.razor:38-192`. It was never finished.

### Current defects

1. `HandleNotificationClick` (`MainLayout.razor:294`) sets `Read = true` but never persists —
   read state is lost on reload.
2. The same handler deletes the notification 60 seconds after it is read
   (`RemoveNotificationAfterDelay`, `MainLayout.razor:308`) — silent data loss.
3. `NotificationItem` has no severity, source, or device/channel fields, so there is no
   colour, filtering, escalation, or deep link.
4. Dedupe is by `Id` (`MainLayout.razor:156`): a recurring fault is dropped forever and its
   timestamp is never refreshed, so "it is still happening" is invisible.
5. No acknowledge, acknowledge-all, or dismiss.
6. `Navbar.razor:129` and `:350` link to `/settings/notifications`, which does not exist.
7. Storage is per-browser IndexedDB (`wwwroot/js/indexedDb.js:27`): nothing is shared between
   operators, and an alarm raised while no browser is open is lost.
8. Only two producers exist — `ChannelCommandHandler.cs:248` and `ChannelManager.cs:844`.

## 2. Purpose and scope

The bell is the **operator's alarm inbox** for BMS faults. An operator must not miss a
critical fault, and who acknowledged what is recorded.

### In scope (agreed)

- Server-side persisted alarm store with an acknowledge trail.
- Alarm sources: device comms loss/reconnect; channel errors from the BMS (existing, enriched);
  data-store failures (existing, enriched).
- Escalation: bell badge + toast + audible sound for high severity, with muting.
- Duplicate/storm suppression, because volume can get large.
- 1-month retention.
- A real `/settings/notifications` page.

### Out of scope

- Test/program lifecycle and calibration alarms (explicitly deferred).
- Email/SMS/external notification transports.
- Per-role alarm routing.

## 3. Architecture

Chosen approach: a singleton `AlarmService` owning raise/collapse/persist/policy/fan-out,
with its own thread-safe event for UI updates.

```
producers                     AlarmService (singleton)                consumers
-----------                   ------------------------                ---------
ChannelManager       ──┐      1. collapse or insert                   MainLayout (bell)
  comms loss/reconnect │      2. persist  (IAlarmRepository)     ┌──▶  ToastService (scoped)
  UDP store failure    ├─────▶3. mute / cooldown / storm policy  ├──▶  beep() JS interop
ChannelCommandHandler ─┘      4. fire OnChanged ─────────────────┘     Alarm history page
  channel errors                                                       (queries repository)
```

### Why not `EventBusService`

`Services/EventBusService.cs` is unsuitable for alarms as written:

- `PublishAsync` refreshes `Subscription.LastActive` **only when a message is published**, and
  a 5-minute `Cleanup` timer removes subscribers past that cutoff. A topic quiet for five
  minutes has its subscribers garbage-collected — the bell would silently stop receiving
  alarms after five quiet minutes.
- `_handlers` is a plain `Dictionary` mutated from subscribe/publish/timer threads with no lock.

`AlarmService` therefore exposes its own event. Fixing `EventBusService` is not in scope.

### Lifetime constraint

Producers (`ChannelManager`) are singletons, so `AlarmService` must be a singleton; but
`Repository<T>` depends on the scoped `AppDbContext`. `AlarmService` resolves
`IAlarmRepository` through `IServiceScopeFactory` per operation and never holds a context.

## 4. Data model

New entity `Models/Entities/AlarmLog.cs`, new `DbSet<AlarmLog> AlarmLogs` on `AppDbContext`,
new EF migration — following the existing `AuditLog` / `AuditRepository` pattern.

| Field | Type | Purpose |
|---|---|---|
| `Id` | `long` PK | surrogate key |
| `AlarmKey` | `string`, indexed | stable identity of the fault, e.g. `dev12/b0/ch3/comms-loss` |
| `Severity` | `AlarmSeverity` enum | `Info`, `Warning`, `Error`, `Critical` |
| `Source` | `AlarmSource` enum | `Comms`, `ChannelError`, `DataStore` |
| `DeviceId` | `string?` | structured, for deep link and per-device mute |
| `BoardNumber` | `int?` | structured |
| `ChannelNumber` | `int?` | structured |
| `Title` | `string` | short line for the bell |
| `Message` | `string` | detail for the row / history |
| `FirstSeenUtc` | `DateTime` | first occurrence of this active row |
| `LastSeenUtc` | `DateTime`, indexed | most recent occurrence; drives retention |
| `OccurrenceCount` | `int` | collapsed repeat count |
| `AcknowledgedAtUtc` | `DateTime?` | null = unacknowledged |
| `AcknowledgedBy` | `string?` | user id/name that acknowledged |
| `ClearedAtUtc` | `DateTime?` | fault resolved (e.g. link restored); distinct from acknowledged |

Indexes: `AlarmKey`, `LastSeenUtc`, and a composite index supporting the "active rows" query
(`AcknowledgedAtUtc IS NULL AND ClearedAtUtc IS NULL`).

### Rules

- **Acknowledge is not delete.** Rows are never removed on acknowledge. The 60-second
  auto-delete is removed entirely.
- **Active row** = `AcknowledgedAtUtc IS NULL AND ClearedAtUtc IS NULL`.
- **Collapse.** `RaiseAsync` finds the active row for `AlarmKey`: if present, increment
  `OccurrenceCount` and set `LastSeenUtc`; no new row.
- **Re-arm.** If the only rows for that key are acknowledged or cleared, insert a **new** row.
  Acknowledge means "I have seen this state"; a later occurrence is new information.
- **Clear.** `ClearAsync(alarmKey)` stamps `ClearedAtUtc` on the active row, so a restored link
  does not leave a stale fault in the bell.

`NotificationItem` is deleted once all references are migrated. The IndexedDB
`saveNotifications` / `getNotifications` functions and their call sites are removed.

## 5. Repeat and storm handling

Four independent levels. Levels 1 and 2 are deliberately separate: level 1 preserves the truth
of what the hardware did, level 2 governs how often the operator is interrupted.

1. **Row collapse** (section 4) — 500 identical events become one row reading
   `x500, last seen 2s ago`. Never suppressed; this is the evidence.
2. **Escalation cooldown** — per `AlarmKey`, toast + sound fire at most once per cooldown
   window (default 60s, configurable). Repeats inside the window still update the row and the
   badge, silently.
3. **Storm guard** — if a key exceeds `StormThreshold` occurrences within `StormWindow`
   (default 20 in 5 minutes), escalation for that key is suppressed for `StormCooloff`
   (default 15 minutes) and one summary alarm is raised
   (`"<key> flapping — N events in 5 min"`). The row keeps counting.
4. **Write debounce** — repeats update `OccurrenceCount` / `LastSeenUtc` in memory and flush
   per key at most every `FlushIntervalSeconds` (default 5), so a flapping link cannot cause a
   database write per packet.
   **The debounce applies only to repeat bumps of an already-persisted row.** The first
   occurrence of a key (the insert) is always written synchronously before fan-out, per §7 —
   otherwise a fault raised and then lost to a restart would never have existed. A pending
   bump is also flushed on shutdown and on acknowledge.

## 6. Retention (1 month)

`Services/AlarmRetentionService.cs` (`BackgroundService`): runs once at startup, then daily.

- Deletes rows with `LastSeenUtc < UtcNow - Alarms:RetentionDays` (default 30).
- Batched deletes (`ExecuteDeleteAsync` over a bounded `Take`) so the first run on a large
  table does not hold a long write lock.
- **Never deletes a row that is still unacknowledged and uncleared**, regardless of age; an
  unresolved fault must not vanish because it is 31 days old. Such survivors are logged.
- Hard row-count cap as a backstop if the date prune ever fails.

## 7. Service contract

`Services/Interfaces/IAlarmService.cs` / `Services/AlarmService.cs`, registered
`AddSingleton` in `Extensions/ServiceCollectionExtensions.cs` alongside `ChannelManager`.

```csharp
Task RaiseAsync(AlarmRequest req);
Task AcknowledgeAsync(long id, string user);
Task AcknowledgeAllAsync(string user);
Task ClearAsync(string alarmKey);
Task<IReadOnlyList<AlarmLog>> GetActiveAsync(int take);
Task<int> GetUnacknowledgedCountAsync();
event Action<AlarmChanged>? OnChanged;
```

`RaiseAsync` order is fixed: **collapse-or-insert → persist → policy → fire `OnChanged`.**
Persisting before fan-out is what guarantees an alarm raised with no browser connected is
present when an operator next logs in.

`AlarmChanged` carries the affected alarm plus flags for whether the consumer should toast and
whether it should beep, so policy lives in one place and the UI does not re-decide it.

## 8. Producers

| Site | Change |
|---|---|
| `ChannelManager` device connect/disconnect path | new: raise `Comms` alarm on disconnect / failed reconnect; `ClearAsync` on successful reconnect |
| `ChannelManager.cs:844` (UDP store failed) | replace `NotificationItem` construction with `RaiseAsync`, `Source = DataStore`, severity `Error` |
| `ChannelCommandHandler.cs:248` (channel errors) | replace `NotificationItem` construction with `RaiseAsync`; map the BMS code through `CodeMessageRepository` to a human title/message and a real severity instead of a concatenated string |

`event Action<NotificationItem>? OnNotify` (`ChannelCommandHandler.cs:91`) and
`SendNotification` are removed once producers call `IAlarmService` directly.

## 9. Consumer / UI

### `MainLayout.razor` cleanup

Removed: `_subscribedDevices`, the per-device `OnNotify += / -=` re-sync loops
(`:88-149`, `:330-345`), `AddNotification`, `SaveNotificationsToIndexedDb`,
`HandleNotificationClick`'s delete-after-60s, and the IndexedDB load (~120 lines).
Replaced by: one `OnChanged` subscription in `OnInitializedAsync`, one `GetActiveAsync` load,
`IDisposable` unsubscribe, and — on a change flagged for escalation — `ToastService.Error(...)`
plus a `beep()` JS interop call.

### Bell (`Navbar.razor`)

- `Blazicon Svg="Lucide.Bell"` replaces the inlined `<svg>`; `Lucide.BellRing` with a soft
  `animate-pulse` while an unacknowledged `Critical` exists.
- Badge severity-tinted (`Destructive` for Critical/Error, amber for Warning) with
  `ring-2 ring-background` so it reads crisply over the icon.
- Popover `w-96 rounded-xl shadow-lg p-0` in three bands: header (count + mute /
  acknowledge-all / gear actions), a `Tabs` segmented filter (**All / Unacked / Critical**),
  then a `max-h-[420px]` `ScrollArea`.
- Rows grouped under sticky muted day headers ("Today", "Yesterday").
- Row: 2px left severity accent, severity icon chip, bold title over a two-line-clamped
  message, right-aligned relative time (`2m`, `1h`) and an `xN` chip when collapsed.
  Unacknowledged rows get `bg-accent/40` and a dot; hover reveals **Ack** and a chevron that
  deep-links to the device/channel.
- Empty state: centred muted `BellOff` with "No active alarms".
- Footer: "View all alarms →" to the history page.

### `/settings/notifications` (new page)

`Card` sections: **Escalation** (toast on/off, sound on/off, cooldown seconds),
**Mute** (global mute with 15m / 1h / until-restart chips, plus per-device mutes),
**Retention** (shows the 30-day policy; admin-only purge), and **Alarm History** — a
`Datatable` with severity / source / device / date filters and the acknowledge columns,
modelled on `Components/Pages/Settings/ApplicationErrorLogs.razor`.

Per-user preferences (toast, sound, cooldown) persist via the existing
`ConfigStorageRepository`; server-side suppression state (storm guard, global mute) is global
and lives in `AlarmService`.

## 10. Configuration (`appsettings.json`)

```
Alarms:RetentionDays          30
Alarms:EscalationCooldownSec  60
Alarms:StormThreshold         20
Alarms:StormWindowMinutes     5
Alarms:StormCooloffMinutes    15
Alarms:FlushIntervalSeconds   5
Alarms:BellActiveTake         50
```

## 11. Testing

Unit tests in `BatteryTestingSystem.Tests` (alongside `ChannelManagerMultiplexingTests`):

- Collapse: two raises of one key produce one row with `OccurrenceCount == 2`.
- Re-arm: raise → acknowledge → raise produces a second row.
- Clear: `ClearAsync` stamps `ClearedAtUtc` and drops the row out of `GetActiveAsync`.
- Cooldown: N raises inside the window flag escalation exactly once.
- Storm guard: exceeding the threshold suppresses escalation and emits one summary alarm.
- Retention: old rows pruned; an old **unacknowledged and uncleared** row survives.
- Persist-before-fan-out: a raise with no subscriber still leaves a readable row.
- Acknowledge records user and timestamp.

## 12. Risks

1. **Migration on a live customer database** — additive table only, no changes to existing
   tables; safe, but must be applied with the standard migration procedure.
2. **Singleton touching EF** — must use `IServiceScopeFactory`; injecting a scoped repository
   into the singleton would throw at startup.
3. **Browser autoplay policy** blocks audio before a user gesture. The beep is best-effort:
   a failed play is caught and never breaks rendering, and the settings page states that sound
   starts working after the first interaction with the page.
4. **Toast volume** — escalation is gated by cooldown and storm guard; a bug there would be
   felt immediately by operators, so those two paths carry the most test weight.
5. **Removing `NotificationItem`** touches the navbar, the layout, and both producers; the
   compiler catches all of it, but it must be done in one commit to avoid a broken build.
