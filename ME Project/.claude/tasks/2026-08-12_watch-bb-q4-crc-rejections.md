# T-40: Watch for 0xBB Q4 CRC rejections on the next hardware run
> Created: 2026-08-12 | Status: Backlog (🔴 High)
> File: `tasks/2026-08-12_watch-bb-q4-crc-rejections.md`

---

## Description
New in #7: Q4 frames are CRC-verified before storage, because the per-packet
ack asserts the bytes were good. Previously never checked on this path. If
the Web Application computes that CRC over a different span, **every program
packet is refused with a `0x00` ack**.

## Why / Context
Self-diagnosing — the log prints computed-vs-carried in both byte orders —
but it is a behaviour change on the path T-24 depends on.

## Progress Log
- **2026-08-12** (Session #7): Opened alongside the `0xBB` handshake work
  (ADR-18).

## Related
- Relates to: ADR-18, T-24
