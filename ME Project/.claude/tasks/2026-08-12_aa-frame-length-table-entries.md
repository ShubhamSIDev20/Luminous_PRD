# T-51: 0xAA frame-length table entries (Q1–Q4 = 6, Q5 = 46, Q6 = 10; Q7–Q10 = 0)
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #9)
> File: `tasks/2026-08-12_aa-frame-length-table-entries.md`

---

## Description
Q7–Q10 deliberately 0 for their 2-byte broadcast header, so `0xAA` splits by
layout instead of by CRC scan. **Exposed a regression in my own ADR-19
code:** the whole-read step was gated on `layout == 0`, so a short `0xAA`
frame with a 46-byte entry skipped it and the scan truncated it — a
*correct* table entry made a *different* frame parse worse. Gate removed;
new regression test
`test_resolve_len_does_not_truncate_when_the_layout_overshoots`. ADR-22 +
ADR-19 amendment.

## Progress Log
- **2026-08-12** (Session #9): Implemented; regression found and fixed same
  session.

## Related
- Relates to: ADR-22, ADR-19, T-48
