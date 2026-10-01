# T-45: An unrecognised 0xEE QueryID is acked before Core Logic rejects it
> Created: 2026-08-12 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-12_unrecognised-ee-queryid-acked-before-rejection.md`

---

## Description
Session #8 closed the CRC half of this: a corrupt `0xEE` frame now gets a
`0x00` NACK from `route_frame()` instead of a false OK. But a **well-formed**
frame whose QueryID is not one of the six still gets `0x01`, then
`me_control_parse()` returns `BAD_QUERY` on the Core Logic thread, which logs
and returns — no path back to the socket. Zero impact while the Web App sends
only the six documented commands.

## Why / Context
Two possible fixes: validate the QueryID in `route_frame()` before acking
(cheap, duplicates a little knowledge), or give Core Logic an outbound NACK
message type through `g_q_comm` (correct, and also the seam for making the
`0xEE` ack mean *"the test actually started"* rather than *"queued"*).

## Progress Log
- **2026-08-12** (Session #8): Identified as residual gap while fixing the
  `0xEE` CRC exemption defect (ADR-18 amendment).

## Related
- Relates to: ADR-18
