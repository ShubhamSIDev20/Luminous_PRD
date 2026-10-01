# Session #18 — Alarm & Notification System

**Date:** 2026-08-18
**Agent:** Claude (Opus 5)
**Goal:** Finish the navbar notification bell (added but never fully implemented) as a real operator alarm inbox — server-persisted alarms with an acknowledge trail, repeat/storm suppression, 30-day retention, toast+sound escalation, and a polished bell popover plus a working `/settings/notifications` page.

## Status: ✅ Complete — build-verified (0 errors), 50/51 tests pass (1 pre-existing unrelated failure), migration applied, HTTP smoke test passed. Live browser/hardware verification NOT done this session (no browser tools loaded, background job session) — see "Still Needed" below.

## What Was Wrong (found via graph exploration)

The bell existed but was half-built:
- `HandleNotificationClick` set `Read = true` but never persisted it — lost on reload.
- The same handler deleted the notification 60s after it was read — silent data loss.
- `NotificationItem` had no severity/source/device fields — no colour, filter, or deep link.
- Dedupe was by `Id`: a recurring fault was dropped forever after the first occurrence, timestamp never refreshed.
- No acknowledge / acknowledge-all / dismiss.
- `/settings/notifications` link existed in two places but the page didn't exist (dead link).
- Storage was per-browser IndexedDB — nothing shared between operators, and an alarm raised with no browser open was lost forever.
- Only 2 producers existed (channel errors, UDP store-failed) — no comms-loss/reconnect alarm at all.

## Design decisions (brainstormed with user, spec written first)

1. **Reuse existing `SeverityLevel` enum** (`Models/Enums/AuditLogEnums.cs` — INFO/WARNING/ERROR/CRITICAL) instead of inventing `AlarmSeverity`. It already has exactly the 4 levels needed.
2. **`AlarmService` bypasses `EventBusService`** — see ADR-5. Own thread-safe `event Action<AlarmChanged>?` instead.
3. **Singleton `AlarmService` resolves `IAlarmRepository` via `IServiceScopeFactory` per operation** — never holds a scoped `AppDbContext` directly (producers like `ChannelManager` are singletons).
4. **Acknowledge is not delete.** Rows persist forever unless pruned by retention (30 days), and an unacknowledged+uncleared row is *never* pruned regardless of age.
5. **Collapse + re-arm:** repeat of same `AlarmKey` while active (unacked+uncleared) bumps `OccurrenceCount`/`LastSeenUtc` on the same row; once acknowledged/cleared, the next occurrence opens a *new* row (acknowledge means "I've seen this state").
6. **Four independent noise-control levels**, deliberately separate (see spec §5): row collapse (never suppressed — the truth), escalation cooldown (60s default, governs toast/beep), storm guard (>20/5min → suppress + one summary alarm), write debounce (repeat bumps batched, first insert always synchronous so persist-before-fan-out holds).
7. **`AlarmChanged.ShouldEscalate`** is a single bool from the server; the client (MainLayout) decides toast vs sound vs both. Server doesn't know per-user prefs.

## Files Created

- `Models/Enums/AlarmEnums.cs` — `AlarmSource` (Comms/ChannelError/DataStore)
- `Models/Entities/AlarmLog.cs` — the alarm row (see schema below)
- `Models/ViewModels/AlarmQueryParameters.cs`
- `Repositories/Interfaces/IAlarmRepository.cs` / `Repositories/Implementations/AlarmRepository.cs`
- `Services/Alarms/AlarmOptions.cs` — bound from `Alarms:*` config section
- `Services/Alarms/AlarmPolicy.cs` — pure, thread-safe cooldown/storm/mute logic (9 unit tests)
- `Services/Interfaces/IAlarmService.cs` — `AlarmRequest`, `AlarmChangeKind`, `AlarmChanged` record, contract
- `Services/Alarms/AlarmService.cs` — the singleton (9 unit tests)
- `Services/Alarms/AlarmRetentionService.cs` — `BackgroundService`, daily prune (4 unit tests)
- `Components/Pages/Settings/NotificationSettings.razor` — new `/settings/notifications` page (escalation toggles, mute, retention info, alarm history table)
- `wwwroot/js/alarm-sound.js` — WebAudio beep, `window.AlarmSound.beep(severity)`, silently swallows failures (autoplay policy)
- `Migrations/20260818113633_AlarmLogTable.cs` (+ `.Designer.cs`) — additive only, `Audit.AlarmLog` table + 3 indexes
- `BatteryTestingSystem.Tests/Services/{AlarmPolicyTests,AlarmServiceTests,AlarmRetentionTests,FakeAlarmRepository}.cs` — 22 new tests, all passing

## Files Modified

- `Data/AppDbContext.cs` — `DbSet<AlarmLog>`, 3 indexes in `OnModelCreating`
- `Extensions/ServiceCollectionExtensions.cs` — `IAlarmRepository` (scoped), `AlarmOptions`/`AlarmPolicy`/`IAlarmService` (singleton), `AlarmRetentionService` (hosted); `ChannelCommandHandler` factory now sets `Alarms`
- `Services/ChannelManager.cs` — ctor takes `IAlarmService`; `TrySendFailureNotification` raises a `DataStore` alarm instead of building `NotificationItem`; disconnect `finally` block raises one `Comms`/CRITICAL alarm per device (not per channel slot — see gotcha below); `ProcessRegistrationPacketAsync` clears the comms-loss alarm on any valid registration packet (independent of whether DB registration itself succeeds)
- `Services/Implementations/ChannelCommandHandler.cs` — `OnNotify`/`SendNotification` removed, replaced by `IAlarmService? Alarms { get; set; }` property + `RaiseAsync` call for channel errors
- `Services/Interfaces/IChannelCommandHandler.cs` — same removal/addition
- `Components/UI/Dashboard/PreviewChannelCommandHandler.cs` — same removal/addition (preview instance, `Alarms` stays null)
- `Components/Layout/MainLayout.razor` — ~120 lines of per-device `OnNotify +=/-=` subscription bookkeeping (`_subscribedDevices`, `SubscribeToDevices`, `HandleDeviceChange`, `AddNotification`, `SaveNotificationsToIndexedDb`) replaced by one `AlarmSvc.OnChanged` subscription + `GetActiveAsync` load; toast+beep fire only when `ShouldEscalate` is true
- `Components/Layout/AppLayout.razor` — `Notifications`/`OnNotificationClick` params replaced by `Alarms`/`OnAlarmAcknowledge`/`OnAlarmAcknowledgeAll`/`OnAlarmActivate`
- `Components/Layout/Navbar.razor` — full bell popover redesign: severity-tinted badge (pulses `BellRing` while an unacked Critical exists), day-grouped rows, All/Unacked/Critical filter tabs, per-row Ack button, occurrence-count chip, "View all alarms" footer link to the new settings page; dead `/settings/notifications` links now bind `ComponentType = typeof(NotificationSettings)`
- `Components/Layout/LayoutModels.cs` — `NotificationItem` class deleted entirely
- `wwwroot/js/indexedDb.js` — `saveNotifications`/`getNotifications`/`deleteNotification` deleted (dead once IndexedDB storage was replaced); `openDB`'s `notifications` object-store creation left as-is (harmless legacy schema, not worth a migration)
- `Components/App.razor` — registered `js/alarm-sound.js`
- `appsettings.json` — new `Alarms` config section (RetentionDays=30, EscalationCooldownSec=60, StormThreshold=20/5min, StormCooloffMinutes=15, FlushIntervalSeconds=5, BellActiveTake=50)

## Gotchas / Non-Obvious Things for Future Agents

1. **`EventBusService` cannot be reused for anything alarm-like or long-lived.** `PublishAsync` only refreshes `Subscription.LastActive` when a message is *published*; a 5-minute `Cleanup` timer removes subscribers past that cutoff. A quiet topic silently loses its subscribers. Also `_handlers` is an unlocked plain `Dictionary` mutated from subscribe/publish/timer threads. See ADR-5.
2. **One comms-loss alarm per device, not per channel slot.** `ChannelManager`'s disconnect `finally` block iterates `deviceConnection.ChannelSlotKeys` (format `"{deviceId}-{board}-{channel}"`) — the device id is extracted from the *first* slot key's `-`-split segment, since raising per-channel would just be noise the storm guard would otherwise have to clean up.
3. **Comms-loss alarm clears on *any* valid registration packet**, not on successful DB registration. The alarm is about the TCP link being back, which a valid packet proves regardless of what the registration logic does with it afterward (already-registered / new / failed / etc.).
4. **`AlarmService` has two constructors** — one taking `Func<IAlarmRepository>` + concrete types (for tests, no timer), one taking `IServiceScopeFactory`/`IOptions<AlarmOptions>` (for DI, starts the flush timer). DI correctly picks the second since `Func<IAlarmRepository>` alone is never registered — verified by an actual `dotnet run` smoke test, not just build success.
5. **Debounce only applies to repeat bumps**, never to the first insert of a new alarm — the first insert is always synchronous, which is what makes "persist before fan-out" actually true (an alarm raised right before a crash/restart with nobody subscribed is still on disk).
6. **`ButtonSize` enum member is `Small`, not `Sm`** — caught by build, not obvious from other component usage patterns.
7. **The dev DB is SQLite** (`D:\MEWebApp\BtsAppdb.db`), not SQL Server as the original design doc assumed — EF logs a (harmless) `SchemaConfiguredWarning` per entity because SQLite doesn't support schemas; `[Table(Schema = "Audit")]` is silently ignored by the SQLite provider. Migration and queries all work fine regardless.
8. **A pre-existing, unrelated test failure** (`ChannelAddressCodecTests.Encode_OutOfRange_Throws`) was present before this session started (confirmed via `git show` diff against `Utils/ChannelAddressCodec.cs` at the pre-session commit — byte-identical) and is NOT something this session touched or should fix under this task's scope.

## Still Needed (flagged to user, not done this session)

- **Live/browser verification** of the actual UI: bell badge pulse, toast, beep-after-first-interaction, day grouping, filter tabs, Ack button, "reload persists acknowledge state", cross-browser shared visibility, mute behavior on `/settings/notifications`. This session verified build/tests/migration/HTTP-200-on-page-load only — no `claude-in-chrome` tools were loaded (background job session).
- **Real hardware/simulator test** of the comms-loss alarm firing on an actual TCP disconnect and clearing on reconnect.
- **Per-user persistence** of the toast/sound checkboxes on the settings page (`_toastEnabled`/`_soundEnabled` are session-only right now, not wired to `ConfigStorageRepository` — deliberately deferred, noted in the plan).

## Commits (10, all on `main`)

```
fd03179 feat(alarms): add AlarmLog entity, AlarmSource enum and migration
92374c2 feat(alarms): add IAlarmRepository with collapse, acknowledge and prune queries
3bf81eb feat(alarms): add escalation policy with cooldown, storm guard and mute
82f97ae feat(alarms): add AlarmService with collapse, re-arm, debounce and storm summary
bf602bb feat(alarms): add 30-day retention service that never prunes unresolved alarms
a757c53 feat(alarms): raise comms-loss, store-failure and channel-error alarms via IAlarmService
10d67ef refactor(alarms): subscribe MainLayout to AlarmService, drop IndexedDB and per-device wiring
413febb feat(alarms): redesign bell popover with severity, grouping, filters and acknowledge
9d3a92d feat(alarms): add alarm settings and history page at /settings/notifications
2fb9cda chore(alarms): remove NotificationItem and IndexedDB notification storage
```

## Docs

- Design spec: `docs/superpowers/specs/2026-08-18-alarm-notification-system-design.md`
- Implementation plan: `docs/superpowers/plans/2026-08-18-alarm-notification-system.md`
