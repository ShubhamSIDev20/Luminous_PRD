# T-11: Live data on UDP 10000 + session store on UDP 10001
> Created: 2026-08-07 | Status: Backlog (🟢 Low) — largely done in #4
> File: `tasks/2026-08-07_live-data-udp10000-session-store-udp10001.md`

---

## Description
~~Live data on UDP 10000 + session store on UDP 10001~~ — **largely done in
#4**. UDP 10000 path is built and fed by the demo emitter; both destinations
now configured in `sys_init.c`.

## Why / Context
What remains is session records on 10001 → tracked as **T-29**. Keep this row
only as the pointer.

## Progress Log
- **2026-08-11** (Session #4): UDP 10000 path built; both destinations
  configured. Session-record producer split out as T-29.
- **2026-08-07** (Session #2): Task opened.

## Related
- Relates to: T-29
