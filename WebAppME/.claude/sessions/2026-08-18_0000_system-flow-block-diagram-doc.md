# Session #12 — System Flow & Block Diagram Document (`docs/flowDocs/`)

> **Date:** 2026-08-18
> **Agent:** Claude (Opus 5, 1M context)
> **Goal:** Produce a complete college-report-style block/flow diagram document covering the
> full operational lifecycle: device registration → user allow → transfer (battery/DBC/program)
> → start program → UDP 10001 registration/store data → session-wise storage, plus every
> control command, all driven from the background service.
> **Status:** ✅ Complete — documentation only, no code changed

---

## What was asked

User requested a block-diagram document in `docs/flowDocs/` covering "everything": first
device registration, user allow, then registration; then the actions a user can perform —
transfer program / battery / manufacturing / control commands — all from the background
service; explicitly including "after registration we transfer program battery dbc optional,
then we start program, then we get registration data on udp 10001, there we store that
session wise, how we store that also show there".

## Process followed

Used the `superpowers:brainstorming` skill. Classified as **bounded** (documentation of flows
that already exist in this repo), announced the classification, explored the real code,
asked two clarifying questions (diagram style, file layout), presented a 13-chapter outline
for approval, got "do it", then wrote the document.

**User's answers:**
- Diagram style: **ASCII box diagrams** (renders everywhere, matches "college project" look)
- Layout: **one master document**, not split per-flow

## Files created

| File | Notes |
|---|---|
| `docs/flowDocs/00-SYSTEM-FLOW.md` | New. ~13 chapters + appendix, all ASCII block diagrams, every diagram annotated with the source file/line it was derived from |

No source files were modified.

## Document structure

| Ch | Content |
|----|---------|
| 0 | Master block diagram — hardware ↔ 4 background listeners ↔ service layer ↔ 2 databases ↔ UI |
| 1 | Background service startup (`AddHostedService` → `ExecuteAsync` → `LoadDevicesAsync` + `StartInternal` spawning 4 loops) and shutdown/restart |
| 2 | Registration handshake + the full `ProcessRegistrationPacketAsync` decision tree |
| 3 | The user "Allow" gate — `DeviceList.razor` → `AllowCicuitAsync` → `IsRegistered=1` → next hardware retry succeeds; plus what `InitializeAsync` rehydrates |
| 4 | Connection multiplexing (shared socket, `SemaphoreSlim`, `(addr,queryId)` correlation) + the two hard-learned invariants |
| 5 | Transfer flow: HW-Ready → **Battery → DBC (optional) → Program** |
| 6 | Start program: 4-guard chain, packed SessionID, self-contained session `.db` creation, then the `0xEE/0x01` command |
| 7 | UDP 10001 → session-wise storage: the full 5-stage pipeline, `InsertRecordAsync` internals, on-disk layout, session-file tables, and a "why this shape" rationale table |
| 8 | UDP 10000 live-view path (display-only; the DBC cache is the one bridge to storage) |
| 9 | Control commands + the two routes a session can end by |
| 10 | Read-back commands (manufacturing / factory config) |
| 11 | Error, disconnect & reconnect paths + a 12-row failure catalogue |
| 12 | Four entry points (Blazor / REST / MCP / Scheduler) converging on one handler |
| A | Appendix: ports, start bytes, query IDs, status codes, SessionID bit layout, Storage V2 header, file naming, related docs |

## Discoveries / gotchas

1. **User's stated transfer order was wrong; the code's order is Battery → DBC → Program.**
   `TransferDialog.razor:372-445` runs `HWReadyToReadWriteAsync` → `SetBatteryParamAsync` →
   `TransferDbcFile` → `SetProgramAsync`. (The *result list* shown in the dialog is ordered
   IsReady/Program/Battery/DBC, which is probably what created the confusion — display order
   ≠ execution order.) Flagged to the user before writing and documented the real order.

2. **⚠ `TimeSyn()` and `ResetSystem()` build the same wire frame.** `TimeSyn` uses
   `StartByte.Control` (`0xEE`) but takes its query ID from `ConfigurationQuery.SyncTime`
   (`0x06`); `ResetSystem` uses `ProgramControlQuery.SystemReset` — also `0x06`. Both send
   `0xEE` + query `0x06`, differing only in TimeSyn's 4-byte epoch payload. `docs/PROTOCOL.md`
   §7 says SyncTime under the Control family should be `0x05`. Recorded as an observation in
   the doc (Chapter 9) and raised as **T-21**. Not fixed — needs firmware confirmation.
   Source: `ChannelCommandHandler.cs:559-592`, `Models/Enums/CircuitEnums.cs:12-40`.

3. **Registration is deliberately fail-first.** An unknown channel gets `CommandStatus.Failed`
   but its row is still inserted into `Channels`. The protocol has no server→device "provision"
   command, so approval works by letting the hardware's own retry loop land in the success
   branch once `IsRegistered` flips to `1`. Worth knowing before anyone "fixes" the Failed
   response.

4. **Session end has two independent routes.** Operator `StopProgram()` (`0xEE/0x02`), *and*
   `StartStoreWorkerAsync` detecting an `OperatorConstants.STO` record while draining the
   store queue. Both call `IProgramServices.EndSession`. Natural completion is discovered in
   the data stream, not via any command.

5. **Session `.db` files are intentionally self-contained.** `InsertSessionAsync` copies the
   program, battery, merged 3-port DBC map, PRODUCER sub-programs, TABLE file data and
   expanded steps into the session file at start time, so a report generated later does not
   depend on the main DB still holding those rows unchanged.

6. **The date folder comes from the packed SessionID, not the wall clock.** `StoreUdpData`
   calls `SessionIdToDateTime` to reconstruct the start date from the low-16 epoch bits, so a
   test that runs past midnight files all its packets under its *start* date. Falls back to
   `DateTime.Now` (logged) if reconstruction fails.

## Addendum — Chapter 13 (code call sequences)

User followed up asking for the **code sequence**. Added **Chapter 13 — Code Call Sequences**
to the same document: 9 sequence diagrams (S1–S9) in participant-lane form with numbered
calls, real method signatures, and a closing cross-reference table mapping each sequence to
its entry method and `file:line`.

| Seq | Flow |
|---|---|
| S1 | Registration — accept → read → parse → DB branch → response |
| S2 | Operator Allow (+ deregister mirror, `Add` expanded, `InitializeAsync` rehydration as S2b) |
| S3 | Transfer from the UI (`TransferDialog`) |
| S4 | Transfer from REST/MCP (`CoreSendProgram`) — with the divergence table |
| S5 | Start program |
| S6 | UDP 10001 packet → disk (all 3 loops, 16 numbered steps) |
| S7 | Command send / response correlation |
| S8 | Disconnect and reconnect |
| S9 | Cross-reference table |

**7. ⚠ New finding while writing S4 — the UI and REST paths do not agree on transfer.**
`TransferDialog.razor:372-445` sends **Battery → DBC → Program** with all 3 DBC ports.
`DeviceController.CoreSendProgram` (`DeviceController.cs:41-108`, also the path
`DeviceMcpTools` takes) sends **Program → Battery → DBC** and calls
`TransferDbcFile(dbc.Data)` — **port 1 only**, p2/p3 left null. A channel loaded over REST
therefore gets its program before its DBC signal map and can never receive port-2/3 DBC
files. They also differ on failure handling (UI aborts; `CoreSendProgram` logs a message and
continues to the next request item). Documented in S4 with a comparison table, noted in
Chapter 12, raised as **T-23**. Not reconciled — needs firmware confirmation of which order
is valid.

## Verification

Documentation-only change — no build run and none needed. Every diagram was written against
source read this session (`ChannelManager.cs`, `ChannelCommandHandler.cs`,
`SqliteBulkDatabaseManager.cs`, `TransferDialog.razor`, `DeviceList.razor`,
`DeviceChannelServices.cs`, `CircuitEnums.cs`, `SqliteDbContext.cs`, `docs/PROTOCOL.md`).

## Follow-ups

- **T-21** — confirm the `TimeSyn` / `SystemReset` query-ID collision against firmware.
- Consider linking `docs/flowDocs/00-SYSTEM-FLOW.md` from `README` / onboarding material.
