# ME_Primary_SRS_V0.1 — Read Me First

**Deliverable:** `ME_Primary_SRS_V0.1.xlsx`
**Status:** first draft, 2026-08-26. Not reviewed, not approved.

---

## What this is

The Phase 1 Software Requirements Specification for the **ME Battery Test System**,
covering **only** the Primary Board software and the Battery Manager (BM) PC
application. Secondary Board internal requirements are deliberately excluded.

It was derived from
`ME Workspace/Reference Documents/BTS_Primary_SW_Requirement_Analysis V1.7.xlsx`
and grounded in the current ME design documents under
`ME Project/Docs/`.

## The workbook

| Sheet | What it is | Who needs it |
|---|---|---|
| `0. Read Me` | Scope, how to read a row, colour key, and an auto-computed summary | Everyone — start here |
| `1. Revision History` | Who changed what | Reviewers |
| `2. Topology and Terms` | The 1 : 8 : 8 model, the scope boundary, the vocabulary | Read before sheet 3 |
| `3. ME SW Requirements` | **340 rows.** Filter `Applicability` to get your work list | Developers |
| `4. Traceability BTS to ME` | All **282** BTS rows and what became of each | Reviewers — this is the audit sheet |
| `5. Open Issues and TBD` | **67** unanswered questions with owners and priority | Project lead, architect, client |
| `6. References and Legend` | Sources, order of authority, column definitions | Everyone |

## The one column to understand

**`Cardinality`** — does this requirement exist once for the system, once per
Secondary Board, or once per channel? In a 1-channel product this question did not
arise. In a 64-channel product it decides every data structure. Read it before
writing code.

## Regenerating

Never hand-edit the `.xlsx` — it is generated and will be overwritten.

```
cd tools
python build_me_srs.py
```

Requires Python 3 and `openpyxl`. The build **fails** if any BTS requirement is
neither traced by an ME requirement nor listed as deliberately dropped, so a
requirement cannot be lost by being forgotten.

| File | Role |
|---|---|
| `tools/me_srs_reqs.py` | The requirement rows. Edit here to add or change a requirement. |
| `tools/me_srs_data.py` | Front matter, traceability generator, open-issue register, legend. |
| `tools/build_me_srs.py` | Formatting and Excel generation only. No content. |
| `tools/bts_index.json` | Snapshot of the BTS V1.7 sheet, used to build the traceability sheet. |

Requirement IDs are stable. If a requirement dies, mark it withdrawn in `Status`
and leave the number alone — never renumber, never reuse.

## Before circulating a revision

1. Re-run `build_me_srs.py` and confirm it reports `none unaccounted for`.
2. Check the **At a glance** block on sheet 0 — its numbers are recomputed, so they
   cannot silently disagree with the sheets.
3. Review sheet 5. The **High** priority rows block real work.

## Honest status

Three things a reader should know:

1. **31 requirement statements contain an unanswered `<TBD-nn>`** and cannot be
   implemented as written. They are highlighted pink on sheet 3.
2. **The control-loop rate is unknown (OI-31 / TBD-28).** It decides whether a Linux
   application processor can host the control loop for 64 channels at all. Answer
   this before committing to the software architecture.
3. **The current ME implementation registers exactly one of the 64 channels**
   (OI-26). That is the largest gap between what is built and what this document
   specifies.
