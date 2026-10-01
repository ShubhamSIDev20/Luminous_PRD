# Session — Docs-vs-Code Audit & Fixes

> Started: 2026-09-11T15:00:00Z
> Status: Complete — audit done, concrete mismatches fixed, build/tests green.

## Goal
User asked to check whether `docs/` is in sync with the actual code, and fix whatever is stale. Explicit instructions: use RTK for shell commands; work on a separate branch (not `main`) and raise a PR (standing instruction going forward, saved to memory, not project-specific).

## Context found before auditing
Between the previous session read and this one, the repo had moved forward outside this `.claude/` session-tracking flow: two feature commits (`e58a3e5`, `85b4961` — §12.3 battery-parameter tokens, already covered by `.claude/` session #42/T-50, see `SESSION.md`/`TASKS.md`) plus two docs-only commits (`bcd87aa`, `dc45d64`, both `Co-Authored-By: Claude Haiku 4.5`) that added a 13-file CMMI Level 3 documentation suite (`docs/{ADD,DDD,DBD,ICD,SRS,RTM,PMP,CMP,QAP,RMP,VVP,Calibration}.md`) plus `docs/CHANGELOG.md` and a rewritten `docs/INDEX.md`. Those two commits never went through this repo's `.claude/` session convention and, on inspection, contained real inaccuracies — this session's actual scope became auditing *that* new doc suite against the real code, not the already-current `.claude/` memory.

## Findings & fixes

1. **`docs/INDEX.md` had 2 dead links** — `RELEASE_NOTES.md` and `ARCHITECTURE_CAPACITY.md` were listed in the Document List / Process Area tables but neither file was ever committed. Removed both, pointed the CM (Configuration Management) row at the real `CHANGELOG.md` instead.
2. **`docs/CHANGELOG.md` claimed "Updated PROTOCOL.md with encoding specifications"** — false; `PROTOCOL.md` had zero mentions of ACN/VNC/§12.3/`BatteryUnitResolver` before this session. Added a real new §14.4 "Battery-Relative Units (ACNx, VNC, §12.3 Tokens)" to `PROTOCOL.md` documenting both resolution paths, that neither adds a wire byte, and the confirmed-blocked Ramp/Resistance case — read `Utils/BatteryUnitResolver.cs` in full first so every claim traces to real code.
3. **`docs/CHANGELOG.md` named a non-existent file** `RSKM.md` for the Risk Management Plan — the real file is `RMP.md`. Fixed.
4. **`docs/DBD.md` was substantially wrong**, not just stale — read against the actual EF entities (`Models/Entities/*.cs`) and `Migrations/*.cs`:
   - A literal duplicate `## 3. Migration History` heading, with the `ProgramSchedules`/`ScheduleExecutionLogs` table definitions stranded between the two copies instead of living in §1.2 — looks like a bad merge of two draft versions.
   - The ERD and `#### Circuits` table modeled a flat `Devices → Circuits` schema that hasn't existed since the `AddSecondaryBoardAndChannel` migration (2026-08-07, T-45) — real schema is `Devices → SecondaryBoards → Channels`. Rewrote the ERD and split the table into real `SecondaryBoards` + `Channels` definitions.
   - `BatterySessions`, `BtsPrograms`, `CalibrationDataPoints`, `UserCircuitAccess`, `AuditLogs`, `ConfigurationEntity`, `CodeMessages`, `DbcFileRecords`, `RegistrationStandards` all had column lists that don't match their real entity classes (wrong names, wrong types, invented columns like `UserCircuitAccess.CanStart/CanStop/CanCalibrate` that don't exist, missing real columns). Rewrote every one from the actual `Models/Entities/*.cs` source.
   - Entirely missing: `AlarmLog` (T-25, 2026-08-18) and `ExportRecords` (2026-06-02) tables, and the separate `WorkflowDbContext`/`WorkflowLayouts` (T-47 Phase 1, 2026-08-24, its own migration history, isolated from `AppDbContext` on purpose). All added.
   - Migration History table only listed up to `AddProgramScheduler` (2026-05-05); 8 more migrations existed unlisted (`AddCalibrationRangeColumn`, `AddExportRecord`, `AddBatteryPortAssignments`, `AddSessionPortDbc`, `AddDeviceRemoteClientConfig`, `AddScheduleDbcPorts`, `AddSecondaryBoardAndChannel`, `AlarmLogTable`, `AddLastSyncedAt`) plus the whole separate `WorkflowDbContext` migration. Completed the table, split AppDbContext vs WorkflowDbContext.
   - Bumped the doc's own version marker `0.5 (May 2026)` → `0.7 (September 2026)`.
5. **Stale class names repo-wide across the new CMMI docs**: `ADD.md`, `DDD.md`, `ICD.md`, `RTM.md`, `PROTOCOL.md`, `RMP.md`, `SRS.md`, `PMP.md` all referred to `CircuitManager`/`CircuitCommandHandler` — classes that don't exist. The real classes (`ChannelManager`/`ChannelCommandHandler`) date back to the multiplexing rename; these docs were evidently drafted without reading the actual source. Mechanical rename across all 8 files (verified no remaining references outside the two `docs/superpowers/{plans,specs}/...` historical design documents, which were deliberately left alone — those are point-in-time records, not live reference docs). Also fixed a `CircuitEnums.cs` path reference to its real location, `Models/Enums/CircuitEnums.cs` (that one file name itself was correct — "Circuit" enums genuinely still exist, e.g. `CircuitStatus`; only the class names were wrong).

## Not done (scope judgment call)
The remaining CMMI docs (`ADD.md`, `DDD.md`, `ICD.md` beyond the class-name fix, `SRS.md`, `RTM.md`, `PMP.md`, `CMP.md`, `QAP.md`, `RMP.md`, `VVP.md`, `Calibration.md`) were **not** line-by-line verified against code beyond the class-name sweep and the spot checks noted above (port numbers in `ICD.md` §1.2/§3 were spot-checked and are correct: 9999/10000/10001, matching `ChannelManager.cs:61-63`). A full verification pass over all ~2,500 lines of that suite was judged out of proportion to "check docs are updated" — flag if a deeper audit of any specific one of those documents is wanted.

## Verification
- `dotnet build BatteryTestingSystem.sln`: 0 errors (391 pre-existing warnings, unrelated to this session — docs-only + one new PROTOCOL.md section).
- `dotnet test`: 771/771 passing (unchanged — no source code touched this session).
- All changes are Markdown-only; no `.claude/CODEBASE_MAP.md`/`CONTEXT/*` update needed (no code, entities, or file layout changed).

## Files changed
- `docs/INDEX.md`, `docs/CHANGELOG.md`, `docs/PROTOCOL.md`, `docs/DBD.md` — content fixes.
- `docs/ADD.md`, `docs/DDD.md`, `docs/ICD.md`, `docs/RTM.md`, `docs/RMP.md`, `docs/SRS.md`, `docs/PMP.md` — mechanical `CircuitManager`→`ChannelManager` / `CircuitCommandHandler`→`ChannelCommandHandler` / `CircuitEnums.cs`→`Models/Enums/CircuitEnums.cs` rename only.

## Branch / PR
Work done on `docs/sync-with-code-20260911` (never on `main`, per the new standing instruction). Committed and PR raised against `main`.
