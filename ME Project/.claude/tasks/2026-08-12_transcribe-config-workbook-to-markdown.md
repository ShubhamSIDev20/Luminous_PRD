# T-49: Transcribe Config Data Frame Format V6.0.xlsx into Ref Docs/bm_config_v6.0.md
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #9)
> File: `tasks/2026-08-12_transcribe-config-workbook-to-markdown.md`

---

## Description
All five sheets transcribed, the first `0xAA` specification the project has
had. Settles the 40-byte battery payload (§5.3, confirmed against a captured
frame §5.4), the 7-byte `0xAA` ack shape previously described as "the
weakest claim in this file", and frame lengths for all six circuit-scoped
queries. Six discrepancies recorded rather than corrected (§9). Every
arithmetic claim verified by computation; one hedged total (111) found wrong
and corrected to 131. ADR-20.

## Progress Log
- **2026-08-12** (Session #9): Transcribed and cross-verified.

## Related
- Relates to: ADR-20, T-47, T-48
