# T-25: Confirm the 0xAA battery/config field offsets with the Web App team
> Created: 2026-08-07 | Status: Backlog (🟢 Low) — RESOLVED 2026-08-12
> File: `tasks/2026-08-07_confirm-aa-battery-config-field-offsets.md`

---

## Description
~~Confirm the `0xAA` battery/config field offsets with the Web App team~~ —
**RESOLVED 2026-08-12**. `Ref Docs/bm_config_v6.0.md` now exists (ADR-20) and
§5.4 confirms the layout against a captured frame. The offsets were right as
far as they went — the payload was **40 bytes, not 22**, and 18 bytes were
being silently discarded (ADR-21).

## Why / Context
Residual questions split out as **T-47** (float vs int) and **T-48**
(broadcast header). Keep this row as the pointer.

## Progress Log
- **2026-08-12** (Session #9): Resolved by ADR-20/ADR-21; split into T-47 and
  T-48.
- **2026-08-07** (Session #2): Task opened.

## Related
- Relates to: ADR-20, ADR-21, T-47, T-48
