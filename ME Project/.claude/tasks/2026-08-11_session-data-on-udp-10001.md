# T-29: Session data on UDP 10001
> Created: 2026-08-11 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-11_session-data-on-udp-10001.md`

---

## Description
Path is built end to end (Core → Data ring → Comm → UDP). Nothing produces
session records yet.

## Progress Log
- **2026-08-11** (Session #4): Opened — UDP 10001 destination wired in
  `sys_init.c`, but no producer exists.

## Related
- Relates to: T-11
