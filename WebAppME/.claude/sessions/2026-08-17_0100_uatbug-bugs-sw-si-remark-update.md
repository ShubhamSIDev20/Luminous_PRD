# Session — UATBUG.xlsx `Bugs_SW` SI Remark update (SR 78-138)

> Date: 2026-08-17 | Agent: Claude (Sonnet 5)

## Goal
User pasted a 20-item annotated list (their own notes in brackets) covering SR 78-138 of `docs/UATBUG.xlsx` → `Bugs_SW` sheet, and asked to fill in the **SI Remark** column (column C — the dev/software-team status column; column D "ADPL Remark" is the client/SI verification response) for that entire row range, from a UI/WebApp-only point of view. Primary/Secondary hardware/firmware bugs are explicitly Nikhil's scope, not this one.

## Sheet Structure Discovered
`Bugs_SW` sheet: row 1 = title, row 2 = headers (`SR.`, `BTS SW Observations List`, `SI Remark`, `ADPL Remark`), data rows 3-143 map `row = SR + 2`. File is 13.3MB (too large for `desktop-commander read_file`'s 10MB Excel limit) — read/wrote it via a `python -i` REPL + `openpyxl` instead (`start_process`/`interact_with_process`).

## What Was Done
1. Dumped full `Bugs_SW` rows 3-143 to `scratchpad/bugs_sw.json` and read it to map the user's 20 numbered bracket-comments to their SR rows (e.g. item 1 → SR78 install docs, item 20 → SR136 TABLE current-value display, etc).
2. Verified two ambiguous items against actual code/git before writing anything:
   - **SR136** ("TABLE program current value not shown on GUI"): the just-prior uncommitted session (`sessions/2026-08-17_0000_table-upload-toast-and-view-table-dialog-fixes.md`) fixed a *different*-sounding bug (TABLE "View" eye-icon dead stub + upload success-toast-as-error). Asked user to confirm these are the same bug — user confirmed "same bug, mark resolved."
   - Confirmed `docs/deployment/` and `docs/architecture/` already contain the install/DB-backup/DB-restore and DB-architecture docs referenced by SR78 and SR106 ("docs Created").
   - Grepped `TableFileManager.razor` and found the 8-char filename limit (SR134) is **already enforced by design**, not a bug.
3. For the ~30 rows the user left no comment on (SR82, 85-89, 99-102, 104, 109-138 minus already-covered ones), drafted a proposed SI Remark for each — classified as either Nikhil's Primary/Secondary scope, an open WebApp/UI bug needing reproduction, or a Phase-II candidate — and presented the full draft table to the user for review before writing (per this repo's "confirm before shared-document edits" norm).
4. User approved with "yes do it." Wrote all 57 SI Remark cells via a one-shot `openpyxl` script (`scratchpad/update_bugs_sw.py`) and saved the workbook. Rows 91, 107, 108 were left untouched (already had correct/adequate SI Remark text).
5. Verified the save landed correctly by re-reading two updated cells fresh from disk (row 80 = SR78, row 136 = SR134).

## Gotcha
`~$UATBUG.xlsx` lock file meant the workbook was open in Excel at the time — asked the user to close it before writing (Excel file locks silently cause openpyxl saves to fail or produce a conflicting copy). User confirmed it was closed, then the write succeeded.

## Files Changed
- `docs/UATBUG.xlsx` (`Bugs_SW` sheet, SI Remark column, rows for SR 78-138 minus SR91/107/108) — binary xlsx, not diffable via git text diff.

## Next Agent Should Do
- Several drafted remarks were explicitly flagged as "needs reproduction/investigation" (SR118, 120, 125, 127, 128, 130, 133, 135, 137) — these are documentation notes only, not yet actually investigated/fixed in code. If asked to work on Bugs_SW follow-ups, start there.
- SR119 (Program tab: rename Edit→Rename, Duplicate→Save As, Code→Program Edit; move to right-click menu) and SR122 (dock icon labels) are open, scoped UI tasks — good candidates for actual implementation work next.
