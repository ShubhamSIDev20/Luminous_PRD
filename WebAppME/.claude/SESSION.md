# SESSION.md — Session Index
> Updated: 2026-09-29T18:34:00Z
> Full detail for any session lives in `sessions/{filename}` — this file only points to it.

---

## 📍 Current Session
**File:** [sessions/2026-09-29_1804_acn-arbitrary-divisor.md](sessions/2026-09-29_1804_acn-arbitrary-divisor.md)
**Goal:** User asked to generalize the ACN family (branch `feature/VNC_ACN_CNom`) to accept ANY positive-integer hour divisor in Nominal Values/Limit/Registration (e.g. "1 ACN7", "8 ACN3"), reversing T-54's fixed-set restriction (only bare/1/2/4/5/10/20 previously validated) — still gated to Current-capable fields only, still resolving to plain Amps via the existing `value × (capacity/X)` formula. Immediate follow-up: neither the coefficient nor the divisor in "1 ACN3" may be 0.
**Status:** Done (code + tests). Generalized `BatteryUnitResolver.Resolve` from a fixed dictionary to a regex-parsed divisor; propagated through `OperatorConstants.AutoSpace`'s unit regex, `ValidationHelper.AutoCorrectUnit`/`ValidateNominal`, `NominalValuesEditor.razor`'s own on-blur unit lookup (was about to silently truncate "1 ACN7"→"1 A", same bug class as prior CNom/ACN7 truncation fixes), and `ProgramBuilder.TryParseUnit`/`TryParseRegistration`'s hex-byte mapping (Cutoff Condition / Registration Type). **Follow-up 1:** added `BatteryUnitResolver.IsValidAcnUnit` (rejects a zero divisor, e.g. "ACN0", up front instead of only failing later at transfer time) + explicit zero-coefficient checks in `ValidateNominal`/`ValidateLimit`/`ValidateRegistration`. **Follow-up 2:** bare "ACN" (no divisor typed) now also rejected editor-side — `IsValidAcnUnit` requires an explicit divisor; `Resolve` itself is deliberately left still defaulting a bare "ACN" it's handed to 5h, for backward compat with already-saved programs. Removed bare "ACN" from `AcnUnits`, restructured `AutoCorrectUnit` so it can't leak back in via the generic fallback, fixed the now-broken `Contains("ACN")` "is a Current field" marker to a `StartsWith` shape check in both `ValidateNominal` and `NominalValuesEditor.razor`. Root-caused the "Registration can't type ACN" report to a stale running exe (PID 18616, was blocking the build) rather than a real gap — `ValidateRegistration` already accepted it correctly; stopped the stale process and rebuilt. 857/857 tests (+44 total this session). Build clean. Merged into `Bug_Enhancement_Sept_2026` 2026-09-30 — see that branch's own session log for merge-conflict resolution detail (DecoderService.cs CRC byte order, OperatorConstants.cs/ValidationHelper.cs ACN+PERCAh combination).

## Previous Session
**File:** [sessions/2026-09-24_1150_vnc-acn-cnom-download-gate.md](sessions/2026-09-24_1150_vnc-acn-cnom-download-gate.md)
**Goal:** User asked to implement VNC/ACN/CNom battery-relative units on branch `feature/VNC_ACN_CNom` — found most (ACNx, VNC, bare CNom) was already shipped (T-49/T-50); the branch equaled `main` exactly.
**Status:** Done. Real gaps closed: unit token `VNC`→`VN` (rename, per manual screenshots); coefficient'd `CNom` (e.g. "0.8 CNom") now resolves to Step Capacity in `ProcessStandardLimit`/`TryParseRegistration` + `ValidationHelper` (previously silently mis-parsed — dropped the cutoff-condition byte); `ProgramEditor.razor`'s hex-packet Download now prompts for a battery when the program uses any battery-relative token (previously threw uncaught). **Follow-up fix:** `AutoSpace` (on-blur formatter) was silently truncating "0.8 CNom" → "0.8 C" — its fuzzy fallback found "C" (Temperature) as a prefix match since "CNom" wasn't in `ValidUnits`; fixed by adding "CNom" as a full `ValidUnits` entry. `VN` checked and confirmed unaffected (was already a full entry). 808/808 tests (+33 total). **Live-verified by user**: real 7-step program (VN/CNom/ACN5 mixed) downloaded against a real 100Ah/1-cell battery, packet report confirms every value resolved correctly (CNom→StepCapacity not AccumulatedCapacity, VN×cells, ACN5×capacity/hours). Not yet committed.

## Previous Session
**File:** [sessions/2026-09-17_1000_jump-to-step-simulator-support.md](sessions/2026-09-17_1000_jump-to-step-simulator-support.md)
**Goal:** User live-tested T-53's Jump-to-Step feature and found it silently did nothing — verify why, then add real `HardwareSimulator` support for Q10.
**Status:** Done. Root cause: `simulator.py`'s generic 0xBB fallback ACKed the unrecognized `0x0A` query as `STATUS=Success` without acting on it — a false-positive, not a missing response. Added `CoreEngine.jump_to_step` (moves the step pointer, resets step-elapsed-time, resets a loop's counter only when landing on its tracked `BEG`) and `simulator.py`'s own `0x0A` branch + `_handle_jump_to_step` (gates on `device.paused`, since `CoreEngine.state` itself never reflects pause). `REASON 0x04`/`0x05` have no simulator equivalent (Secondary-only states, in-flight collision) — still covered by T-53's C#-side tests. 26/26 Python tests (+8). Branch `feature/jump-to-program-step` (same as T-53).

## Previous Session
**File:** [sessions/2026-09-16_1100_jump-to-program-step.md](sessions/2026-09-16_1100_jump-to-program-step.md)
**Goal:** Add "Jump to Step..." to the Dashboard's per-circuit right-click menu (BM Program v3.2 Q10, `0x0A`) — plan-first per explicit request (used EnterPlanMode/ExitPlanMode), enabled only while running, client-validated before any hardware round-trip.
**Status:** Done (code + tests). New `JumpToStepReason` enum + `DecoderService.TryDecode` case (STATUS+REASON, unlike other Program-family queries), `Utils/JumpStepValidator` shared by dialog + handler, `ChannelCommandHandler.JumpToStepAsync`, new `JumpToStepDialog.razor`, new context-menu entry + `CanContextAction` gate. `docs/PROTOCOL.md` §6.6 + `docs/manual-extract/bm_program_v3.2.md` (source doc saved). Q9 (live step update) and Workflow Canvas wiring deliberately out of scope. 793/793 tests (+18). ⚠️ No browser tool available this session — code/tests verified, actual click-through not done. Branch `feature/jump-to-program-step`.

## Previous Session
**File:** [sessions/2026-09-15_1000_cc-rechg-opcode.md](sessions/2026-09-15_1000_cc-rechg-opcode.md)
**Goal:** Add a new step operator `CC_ReChg` (opcode 21, `0x15` — user corrected mid-turn from an initially-stated `0x14`, which is already `PRODUCER`), encoded/processed exactly like `CC_Chg`.
**Status:** Done. Registered the new opcode everywhere `CC_CHG` is (`OperatorConstants.cs`, `PacketAnalyzer`/`PacketAnalyzerNoReverse`, `docs/PROTOCOL.md`, both `HardwareSimulator` mirrors). No `DecoderService` dispatch-switch change needed — any opcode not special-cased already falls through to the same generic encoding path `CC_CHG` uses. 775/775 .NET tests (+4), 18/18 Python tests (+1). Branch `feature/cc-rechg-opcode-0x14`.

## Previous Session
**File:** [sessions/2026-09-11_1500_docs-vs-code-audit.md](sessions/2026-09-11_1500_docs-vs-code-audit.md)
**Goal:** Audit `docs/` against actual code and fix mismatches; standing instruction going forward: always work on a separate branch + PR, never commit to `main` directly.
**Status:** Done. Found the CMMI doc suite added 2026-09-11 (`bcd87aa`/`dc45d64`, outside this repo's `.claude/` session convention) contained real inaccuracies, not just staleness: 2 dead links in `docs/INDEX.md`, a false "PROTOCOL.md updated" claim in `docs/CHANGELOG.md` (now actually added, §14.4), a wrong filename (`RSKM.md`→`RMP.md`), `docs/DBD.md` rewritten from the real EF entities/migrations (was missing `SecondaryBoards`/`AlarmLog`/`ExportRecords`/`WorkflowDbContext` entirely and had a duplicated section), and a repo-wide stale-class-name sweep (`CircuitManager`/`CircuitCommandHandler` → `ChannelManager`/`ChannelCommandHandler`) across 8 of the new docs. Build 0 errors, 771/771 tests (unchanged, docs-only). Branch `docs/sync-with-code-20260911`, PR raised.

## Previous Session
**File:** [sessions/2026-09-08_1000_battery-parameters-intern-table.md](sessions/2026-09-08_1000_battery-parameters-intern-table.md)
**Goal:** Explore Ramp/Resistance and §12.3 "Using Battery Parameters" from the manual — check what can be added to the Program Editor without changing `ProgramBuilder`'s wire-encoding protocol, same as VNC/ACN5.
**Status:** Done. Ramp/Resistance confirmed genuinely blocked (no wire-format support exists for either, per `docs/PROTOCOL.md`) — not implemented. §12.3's 10 bare `INTERN[]` tokens (all but `Rin`) implemented per an approved plan — reuses the existing `SET`-variable substitution, no new resolver needed. Also re-audited and fixed a second `AutoCorrect` unit-mangling edge case in `NominalValuesEditor.razor`. Build clean, 770/770 tests passing (up from 751).

## Previous Session
**File:** [sessions/2026-09-04_1200_vnc-acn5-program-editor.md](sessions/2026-09-04_1200_vnc-acn5-program-editor.md)
**Goal:** Implement VNC/ACN5 battery-relative nominal-value/limit/registration units in the Program Editor, resolved against the selected battery at transfer time — per a written, user-approved plan. Also processed `docs/BM_Manual_eng.pdf` into a reusable, searchable text extraction + curated reference doc.
**Status:** Done. Plan approved, implemented exactly as written. Build clean, 751/751 tests passing (up from 696).

## Previous Session
**File:** [sessions/2026-09-03_1100_api-enhancements.md](sessions/2026-09-03_1100_api-enhancements.md)
**Goal:** Implement API enhancements — `POST /api/Device/GetDbcFiles` + ChannelRange/ChannelList bulk support; continuation (2026-09-04): migrate to `ChannelList`-only, fix the resulting MCP build break, verify + fix correctness bugs, add tests, update docs.
**Status:** Done. Build clean, 696/696 tests passing (up from 679). `.claude/agent-memory/` folder removed (was duplicating `CONTEXT/api*.md`).

## Previous Session
**File:** [sessions/2026-09-02_1000_program-to-bytepacket-tests.md](sessions/2026-09-02_1000_program-to-bytepacket-tests.md)
**Goal:** Add tests for `DecoderService.ConvertProgramIntoBytesPackets`. Update task sheet.
**Status:** Done. 25 tests, suite 639 → **664**. Encoder verified correct. 4 findings raised (not fixed).

## Previous Session (pre-previous)
**File:** [sessions/2026-09-01_1000_dashboard-default-workflows-optional.md](sessions/2026-09-01_1000_dashboard-default-workflows-optional.md)
**Goal:** Make `DashboardView` the default landing tab again (as before the Workflows branch), keep "Workflows" as an optional, reachable-but-not-default nav entry — then merge `feat/workflow-canvas-experiment` into `main`.
**Status:** ✅ Done, and MERGED TO MAIN + PUSHED. Flipped `WorkflowDefaultTab`'s priority so `LegacyDashboard=true` always wins the default tab even with `WorkflowCanvas=true`; canvas only becomes default once `LegacyDashboard` is explicitly off. Set `appsettings.Development.json` back to `LegacyDashboard: true` (kept `WorkflowCanvas: true` so the menu entry stays visible). Updated `WorkflowDefaultTabTests.cs` to match. Live-verified in Chrome DevTools. **Merged `feat/workflow-canvas-experiment` → `main`** via `git merge --no-ff` (commit `1bd54f5`, 112 commits / 141 files / +33.7k lines) and pushed to `origin/main`.

## Previous Session
**File:** [sessions/2026-08-27_1100_workflow-canvas-battery-cell-design.md](sessions/2026-08-27_1100_workflow-canvas-battery-cell-design.md)
**Goal:** Turn the battery-cell channel node from description into an approved design, then build it (T-48).
**Status:** ✅ Spec, plan AND full implementation, live-verified. **635 tests** (from 597). 9 feature commits `b76ca26`..`69168ed`, spec `d32c8f8`, plan `4d8203a`. The channel node is now a battery cell: cap-mounted channel number, the dashboard card's `RenderNormal` arrangement inside (Primary Data as two unlabelled columns, Configuration/Program Data labelled), a segment march driven by circuit status with idle showing **STOP**. ⚠️ **SoC had never rendered in live mode** — derived by net coulomb count `(AhCha-AhDch)/NominalCapacity` returning `double?` where null is UNKNOWN. Height was MEASURED at nine selections, all exact: chrome **114**, heading 19, row 15, gap 1, all 28 keys **520px**. Edges attach at **0px** deviation.
