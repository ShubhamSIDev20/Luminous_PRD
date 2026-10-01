# T-19: Narrow or drop the Docker-bridge IP warning
> Created: 2026-08-07 | Status: Backlog (🟢 Low)
> File: `tasks/2026-08-07_narrow-drop-docker-bridge-warning.md`

---

## Description
False-positives on this site's `172.16.x.x` LAN. A MAC-prefix (`02:42`) check
was written and **reverted at the developer's request** — ask before changing
again.

## Progress Log
- **2026-08-07** (Session #2): Opened; MAC-prefix fix attempted and reverted
  at developer's request the same session.

## Related
- Relates to: ADR-6
