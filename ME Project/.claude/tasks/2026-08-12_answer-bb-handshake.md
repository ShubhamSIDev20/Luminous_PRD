# T-37: Answer the 0xBB handshake
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #7)
> File: `tasks/2026-08-12_answer-bb-handshake.md`

---

## Description
New pure `proto/program_frame.[ch]` (7-byte ack, QueryID echoed), Q1
is-ready → `0x01`, Q3 packet count → `0x01` with the count logged and
discarded, **Q4 → per-packet ack** (`0x01` queued / `0x00` bad CRC or queue
full). Gate first: an unregistered circuit gets no reply.
`me_frame_expected_len()` gained Q1=6/Q3=8. Test-first, 13 new checks
(123→136). ADR-18.

## Progress Log
- **2026-08-12** (Session #7): Implemented. **NOT hardware-verified.**

## Related
- Relates to: ADR-18, T-38, T-39, T-40
