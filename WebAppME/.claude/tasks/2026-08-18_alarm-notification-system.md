# T-25: Alarm & Notification System (finish the navbar bell)

**Status:** ✅ Done (code + build/test verified; live browser/hardware verification outstanding — see T-26/T-27)
**Session:** #18
**Started/Completed:** 2026-08-18

## Summary

The navbar notification bell existed but was never finished — see session
`sessions/2026-08-18_1700_alarm-notification-system.md` for the full defect list and design
rationale. Rebuilt it as a server-persisted operator alarm inbox with an acknowledge trail,
repeat/storm suppression, 30-day retention, and toast+sound escalation.

Full brainstorm → design spec → implementation plan → 10-task TDD execution, all on `main`
(no worktree, per explicit user choice).

- Spec: `docs/superpowers/specs/2026-08-18-alarm-notification-system-design.md`
- Plan: `docs/superpowers/plans/2026-08-18-alarm-notification-system.md`

## Verification performed this session

- `dotnet build` — 0 errors after every task
- `dotnet test` — 22 new tests (AlarmPolicy ×9, AlarmService ×9, AlarmRetention ×4), all pass;
  50/51 total (1 pre-existing unrelated failure, confirmed present before this session started)
- `dotnet ef database update` — `Audit.AlarmLog` migration applied cleanly
- `dotnet run` smoke test — app starts, `Database migrations applied successfully`, `BTS
  Service started successfully`, `GET /settings/notifications` → HTTP 200, no exceptions in log

## NOT verified this session (flagged, not silently skipped)

- Live UI behavior (badge pulse, toast, beep, day grouping, filters, Ack button, persistence
  across reload, cross-browser shared visibility, mute) — no browser tooling was loaded this
  session (background job). See new backlog item T-26.
- Real hardware/simulator comms-loss disconnect/reconnect firing — see new backlog item T-27.

## Related decisions

- ADR-5 (`DECISIONS/2026-08-18_alarmservice-bypasses-eventbusservice.md`): `AlarmService` does
  not use `EventBusService` — its 5-minute subscriber expiry would silently drop a quiet
  alarm topic's listener.
