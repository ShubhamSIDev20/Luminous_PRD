# T-39: Decide whether 0xBB Q2 (program metadata) needs a reply
> Created: 2026-08-12 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-12_decide-bb-q2-metadata-reply.md`

---

## Description
Q1 and Q3 are answered as of #7; Q2 is still logged and dropped. If the Web
Application waits for a reply to it, the handshake stalls at Q2 instead of Q1
and the next hardware run looks like the same bug.

## Why / Context
Answering is one `me_prg_pack_ack()` call — but what the metadata should DO
(store the program name/version/date per circuit) is the real question.

## Progress Log
- **2026-08-12** (Session #7): Opened alongside the `0xBB` handshake work
  (ADR-18). Left open by choice, not oversight.

## Related
- Relates to: ADR-18, T-26
