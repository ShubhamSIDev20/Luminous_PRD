# DECISIONS.md — ADR Index
> Updated: 2026-08-18T22:00:00Z
> Full detail for any decision lives in `DECISIONS/{filename}` — this file only indexes and describes.
> Future agents: scan this table before changing architecture or patterns.

---

## 📜 Decisions
<!-- Newest first. One row per decision, ever. -->
| ID | Decision | File | Date | Session | Status |
|----|----------|------|------|---------|--------|
| ADR-6 | Hardware events are coalesced at the producer (250 ms flush timer, **not** a debounce — a reconnect loop would starve one) and single-flighted at the consumer via `CoalescingRunner`; UI DB work runs inside `InvokeAsync` | [DECISIONS/2026-08-18_hardware-event-coalescing-and-single-flight.md](DECISIONS/2026-08-18_hardware-event-coalescing-and-single-flight.md) | 2026-08-18 | #19 | Accepted |
| ADR-5 | `AlarmService` uses its own plain C# event, not `EventBusService` — the latter's subscribers silently expire after 5 quiet minutes and its handler dictionary is unlocked | [DECISIONS/2026-08-18_alarmservice-bypasses-eventbusservice.md](DECISIONS/2026-08-18_alarmservice-bypasses-eventbusservice.md) | 2026-08-18 | #18 | Accepted |
| ADR-4 | Hardware listeners (TCP 9999 / UDP 10000+10001) retry forever with backoff and never stop the host — escalation is `Fatal` log + event bus + `Unhealthy` health check, not a process exit | [DECISIONS/2026-08-18_listener-retry-forever-policy.md](DECISIONS/2026-08-18_listener-retry-forever-policy.md) | 2026-08-18 | #17 | Accepted |
| ADR-3 | Program byte-format decode uses bounded candidate-search (zero-limit steps write no count byte at all) | [DECISIONS/2026-08-11_program-decode-candidate-search.md](DECISIONS/2026-08-11_program-decode-candidate-search.md) | 2026-08-11 | #6 | Accepted |
| ADR-2 | `wwwroot/css/app.min.css` has no Tailwind rebuild pipeline — new utility classes silently don't render | [DECISIONS/2026-08-11_css-build-has-no-pipeline.md](DECISIONS/2026-08-11_css-build-has-no-pipeline.md) | 2026-08-11 | #5 | Documented constraint |
| ADR-1 | One TCP connection per device, multiplexed across channels | [DECISIONS/2026-08-07_device-connection-multiplexing.md](DECISIONS/2026-08-07_device-connection-multiplexing.md) | 2026-08-07 | #1 | Accepted |