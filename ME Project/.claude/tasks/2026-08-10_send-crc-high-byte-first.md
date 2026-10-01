# T-20: Send CRC high byte first (big-endian) — HARDWARE-VERIFIED
> Created: 2026-08-10 | Status: Done (2026-08-10, Session #3)
> File: `tasks/2026-08-10_send-crc-high-byte-first.md`

---

## Description
`ME_CRC_ORDER_DEFAULT` in `src/proto/crc16.h` set to big-endian, single
source of truth for `sys_init.c`, `--crc-order` help text, and `deploy.ps1`.
Supersedes ADR-5.

## Progress Log
- **2026-08-10** (Session #3, 09:38 UTC): Hardware-verified both directions
  — request CRC `0x369E` → `36 9E`; response CRC `0xBE15` → `BE 15`.

## Related
- Relates to: ADR-9, T-7
