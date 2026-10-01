# T-46: Watch for a battery payload that is not 40 bytes
> Created: 2026-08-12 | Status: Backlog (🔴 High)
> File: `tasks/2026-08-12_watch-battery-payload-not-40-bytes.md`

---

## Description
Behaviour change in #9 (ADR-21): a payload under 40 is now **refused and
NACKed** where it used to be discarded with a log, and one over 40 WARNs. If
the Web Application sends the legacy 22-byte form, every battery packet fails
— loudly, by design.

## Why / Context
Grep the run for `shorter than the 40 that bm_config_v6.0.md Q5 defines` and
`more than the 40 this build knows`.

## Progress Log
- **2026-08-12** (Session #9): Opened alongside ADR-21 (complete 40-byte
  battery record, refuse short payloads).

## Related
- Relates to: ADR-21, T-24
