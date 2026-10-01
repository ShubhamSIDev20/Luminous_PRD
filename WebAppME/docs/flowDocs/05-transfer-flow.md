# 5. Transfer Flow — Battery → DBC → Program

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Before a test can run, the channel must be loaded with three things. **The order
implemented in code is Battery first, then DBC (optional), then Program** — this
matters because the program steps may reference DBC signal IDs assigned during
the DBC step.

```
  OPERATOR: Dashboard -> select channel(s) -> [Transfer] -> TransferDialog
        |
        |  picks: Program (required), Battery (required),
        |         Port1 DBC / Port2 DBC / Port3 DBC (each optional)
        v
  +--------------------------------------------------------------+
  | STEP 0 -- HWReadyToReadWriteAsync()                           |
  |   0xBB / Query 0x01  (HWReadyForProgram)                      |
  +--------------------------------+------------------------------+
                                   |
                    +--------------+--------------+
                    |                             |
              response FAIL                 response OK
                    |                             |
                    v                             v
       Battery step  = "Skipped:          continue to STEP 1
                        Device not ready"
       Program step  = "Skipped:
                        Device not ready"
       (DBC step never created)
       -> dialog shows the failure, nothing sent

                                   |
                                   v
  +--------------------------------------------------------------+
  | STEP 1 -- SetBatteryParamAsync(battery)                       |
  |                                                               |
  |   guard: ProgramStatus must NOT be Running                    |
  |   0xAA / Query 0x05 (WriteBatteryParams)                      |
  |   Data = DecoderService.BuildBatteryBytes(battery)            |
  |                                                               |
  |   on send, in-memory session is stamped:                      |
  |     Session.BatteryID / BatteryName / battery                 |
  |     handler.Battery = battery                                 |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STEP 2 -- TransferDbcFile(p1, p2, p3)     [OPTIONAL]          |
  |                                                               |
  |   Skipped entirely if all three port selections are empty.    |
  |                                                               |
  |   2a. Assign signal IDs across ALL 3 ports:                   |
  |       sigId starts at 31, increments per SELECTED signal      |
  |       unselected signal          -> SingalId = 0              |
  |       sigId > 255                -> SingalId = 0 + warning    |
  |                                    (signal name collected and |
  |                                     reported back to the UI)  |
  |                                                               |
  |   2b. Stamp session:                                          |
  |       Session.DbcFileRecordID / DbcName          (port 1)     |
  |       Session.Port2DbcFileRecordID / Port2DbcName             |
  |       Session.Port3DbcFileRecordID / Port3DbcName             |
  |       Session.dbcDatabse = MergeDbcDatabases(p1,p2,p3)        |
  |                                                               |
  |   2c. Build wire payload:                                     |
  |       DbcDatabase.BuildMultiPortPayload(p1,p2,p3)             |
  |                                                               |
  |   2d. Send count:  0xBB / Query 0x07 (SendDbcStepsCount)      |
  |            Data = ceil(len / 1400) as int16 BE                |
  |            FAIL HERE -> abort, nothing else sent              |
  |                                                               |
  |   2e. Send chunks: 0xBB / Query 0x08 (SendDbcFile)            |
  |            for offset = 0; offset < len; offset += 1400       |
  |              Data = [len 2B BE][chunk bytes]                  |
  |            each chunk awaits its own ACK                      |
  |            FAIL on any chunk -> abort with the offset         |
  +--------------------------------+------------------------------+
                                   |
                       dev.dbcData.DbcValues = null
                       (clear stale live DBC cache)
                                   |
                                   v
  +--------------------------------------------------------------+
  | STEP 3 -- SetProgramAsync(program)                            |
  |                                                               |
  |   guard: ProgramStatus must NOT be Running                    |
  |                                                               |
  |   3a. ResolveProducerProgramsAsync(steps)                     |
  |       -> loads every sub-program referenced by a PRODUCER step|
  |   3b. ProgramBuilder.ExpandProducerSteps(...)                 |
  |       -> ExpandedProgramSteps kept in memory so the UI can    |
  |          map hardware step numbers back to user-visible steps |
  |   3c. DecoderService.ConvertProgramIntoBytesPackets(...)      |
  |       -> List<byte[]>, one entry per program step             |
  |   3d. Re-pack steps into <= 1400-byte payloads (a step may    |
  |       be SPLIT across two payloads -- the hardware reassembles)|
  |                                                               |
  |   3e. Send count:  0xBB / Query 0x03 (SendProgramStepsCount)  |
  |            Data = payloads.Count as int16 BE                  |
  |            FAIL HERE -> abort                                 |
  |                                                               |
  |   3f. Send each:   0xBB / Query 0x04 (SendProgram)            |
  |            Data = [len 2B BE][payload bytes]                  |
  |            FAIL on any -> abort and return that error         |
  |                                                               |
  |   on success, stamp session:                                  |
  |     Session.ProgramHash / ProgramID / ProgramName / programs  |
  |     handler.Program = program                                 |
  +--------------------------------+------------------------------+
                                   |
                                   v
              TransferDialog renders a StepResult list:
              IsReady | Program Transfer | Battery Transfer | DBC Transfer
              (each with Success flag + message)
```

**Key point about `ProgramHash`:** `SetProgramAsync` writes
`Session.ProgramHash = ProgramDto.ProgramHash`. Chapter 6 shows `StartProgram`
refusing to run if `Program.ProgramHash != Session.ProgramHash` — that is the
guard against "program edited after transfer, hardware still holds the old one".

**Files involved:** `Components/UI/Dashboard/TransferDialog.razor:370-459`,
`Services/Implementations/ChannelCommandHandler.cs:593-781, 1111-1234`,
`Services/DbcParser.cs`, `Services/ProgramBuilder.cs`, `docs/PROTOCOL.md` §6, §14

---


---

[⬅ Previous](04-connection-multiplexing.md) · [⬅ Index](README.md) · [Next ➡](06-start-program-and-session.md)
