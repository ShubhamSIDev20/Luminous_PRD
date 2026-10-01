# ADR-20: Transcribe the config workbook into `Ref Docs/bm_config_v6.0.md`
> Date: 2026-08-12 | Session: #9 | Status: Accepted
> File: `DECISIONS/2026-08-12_transcribe-config-workbook-bm-config-v6.md`

---

**Context:** `Ref Docs/` held five protocol documents and **no configuration
document**. Every `0xAA` layout in the codebase was reverse-engineered from legacy
firmware and carried an explicit "NOT specification-backed" warning; the `0xAA` ack
shape was described as *"the weakest claim in this file"*. The developer supplied
`Config Data Frame Format V6.0.xlsx` and asked for it as Markdown in the project.

**Decision:** Transcribe all five sheets into `Ref Docs/bm_config_v6.0.md`, following
the existing `bm_*` naming convention. **Transcribe, not rewrite** — where the
spreadsheet is internally inconsistent or disagrees with observed traffic, record
both readings in a Discrepancies section (§9) rather than silently correcting.

**What it settled:**
- The Q5 battery payload is **40 bytes**, all fields named (§5.3), and §5.4 confirms
  every boundary against a captured frame — the strongest layout evidence in the
  project short of a hardware run
- The 7-byte `0xAA` ack shape, previously inferred, is documented verbatim (§3.5).
  The guess had been right
- Real frame lengths for all six circuit-scoped queries (→ ADR-22)

**Since resolved by the developer, same day:**
- **§9.3 Impedance and Energy Density are `float`.** They were the only two 4-byte
  battery fields the sheet does not annotate "It will be in float"; its samples
  (`00 00 00 64` = "100 Ohm") decode sensibly only as `uint32`. The Web Application
  had sent `3F 80 00 00` for a field set to `1`, so the parser chose float on the
  principle that **observed traffic wins** — and the developer's confirmation
  (2026-08-12, they will instruct the Web App team to send float only) agrees with
  it, so **no code changed**. The spreadsheet samples are simply wrong. Both
  encodings are 4 bytes wide, so the other choice would have yielded a nonsense
  value rather than a parse error — that asymmetry is why it was escalated instead
  of assumed, and it is pinned by
  `test_impedance_and_energy_density_are_floats()` (T-47 closed).

**What it did NOT settle — recorded, not resolved:**
- **§9.1 Q7–Q10 are broadcast with a 2-byte header** (→ ADR-22, T-48)
- **§9.2, §9.4–§9.7** spreadsheet errors carried forward: Battery ID's ID collides
  with Charge Factor; two Factory params share ID `0x05`; a sample IP contradicts
  its decoded value; Response6 prints the wrong Query ID; the referenced sheet
  `BTS_ConfigPacket` does not exist. None affect the Q5 frame this board receives
- **§9.8** Sync Time appears on both `0xAA` Q6 and `0xEE` Q5; only `0xEE` is built

**Why record discrepancies instead of correcting them:** the same reasoning as
ADR-4. This file is evidence of what the source said, and a future reader hitting a
wrong value needs to know the source was ambiguous — not read a clean document that
hides where the risk is. Every arithmetic claim in it (float decodes, the epoch, all
three payload totals) was verified by computation, and the one hedged number found
wrong — a 111-byte Factory total that is actually 131 — was corrected with its
working shown.

**Consequences:**
- ✅ `0xAA` is specification-backed; three "not specification-backed" warnings and
  one "weakest claim in this file" retired from the codebase
- ✅ Q1–Q4 and Q6 request/response layouts are documented for whoever implements them
- ⚠️ It is a transcription of a **V6.0** workbook of unknown currency, not a
  statement from the Web Application team. §9.3 in particular needs their word
- ⚠️ `Ref Docs/` now mixes sources: four documents from the Web App team, plus
  `bm_control_v3.0.md`'s Session ID amendment (from hardware) and this file (from a
  spreadsheet). Each says which it is at the top

**Related**
- Supersedes: none
- Relates to: ADR-4, ADR-21, ADR-22, T-47, T-48
