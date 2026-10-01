# T-51 — Docs-vs-Code Audit & Fixes

**Session:** [sessions/2026-09-11_1500_docs-vs-code-audit.md](../sessions/2026-09-11_1500_docs-vs-code-audit.md)
**Status:** Done
**Branch:** `docs/sync-with-code-20260911`

## Ask
Check whether `docs/` matches actual code; fix whatever is stale/wrong.

## What was found
A CMMI Level 3 doc suite (`docs/{ADD,DDD,DBD,ICD,SRS,RTM,PMP,CMP,QAP,RMP,VVP,Calibration}.md` + `CHANGELOG.md`, rewritten `INDEX.md`) had been committed the same day (`bcd87aa`, `dc45d64`) outside this repo's `.claude/` session convention, and contained real inaccuracies rather than mere staleness.

## What was fixed
1. `docs/INDEX.md` — removed 2 dead links (`RELEASE_NOTES.md`, `ARCHITECTURE_CAPACITY.md`, neither ever committed); pointed CM row at real `CHANGELOG.md`.
2. `docs/CHANGELOG.md` — corrected a false claim ("PROTOCOL.md updated") by actually adding the missing content to `PROTOCOL.md` (see #3); fixed a wrong filename (`RSKM.md` → `RMP.md`).
3. `docs/PROTOCOL.md` — added new §14.4 "Battery-Relative Units (ACNx, VNC, §12.3 Tokens)", derived from reading `Utils/BatteryUnitResolver.cs` in full.
4. `docs/DBD.md` — rewritten against real `Models/Entities/*.cs` + `Migrations/*.cs`: fixed a literal duplicate `## 3. Migration History` section with stranded table defs between the copies; updated the ERD + table split from flat `Circuits` to the real `SecondaryBoards`/`Channels` split (T-45, 2026-08-07); corrected `BatterySessions`/`BtsPrograms`/`CalibrationDataPoints`/`UserCircuitAccess`/`AuditLogs`/`ConfigurationEntity`/`CodeMessages`/`DbcFileRecords`/`RegistrationStandards` column lists to match the real entities; added missing `AlarmLog`, `ExportRecords`, and the separate `WorkflowDbContext`/`WorkflowLayouts`; completed the migration history (8 missing migrations + the isolated Workflow migration history).
5. Repo-wide stale class name: `CircuitManager`/`CircuitCommandHandler` (don't exist) → `ChannelManager`/`ChannelCommandHandler` (real) across `ADD.md`, `DDD.md`, `ICD.md`, `RTM.md`, `PROTOCOL.md`, `RMP.md`, `SRS.md`, `PMP.md`. Left `docs/superpowers/{plans,specs}/*` untouched — historical point-in-time records.

## Not done
Full line-by-line verification of the remaining CMMI docs beyond the class-name sweep and DBD.md rewrite — judged out of proportion to the ask. Flag if a deeper pass on a specific document (ADD/DDD/ICD/SRS/RTM/PMP/CMP/QAP/RMP/VVP/Calibration) is wanted.

## Verification
`dotnet build`: 0 errors. `dotnet test`: 771/771 (unchanged — docs-only change).
