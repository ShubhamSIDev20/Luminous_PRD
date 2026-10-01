# 16. Scheduled Program Execution

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

A schedule is "run this program on these channels at this time" — a delayed Hangfire job
that replays the transfer-then-start sequence unattended.

### 16.1 Creating a schedule

```
  OPERATOR: Programs -> Scheduler -> [New Schedule]
        |
        |  Name, Program, Battery, Port1/2/3 DBC, ScheduledAt,
        |  TargetCircuits (only IsRegistered channels are offered)
        v
  +--------------------------------------------------------------+
  | SchedulerService.CreateScheduleAsync(request)                 |
  |                                                               |
  |   VALIDATION                                                  |
  |     Name blank            -> "Schedule name is required."     |
  |     no TargetCircuits     -> "Select at least one target ..." |
  |     ScheduledAt <= Now    -> "Scheduled time must be in the   |
  |                               future."                        |
  |                                                               |
  |   PERSIST                                                     |
  |     new ProgramSchedule {                                     |
  |         Name, ProgramId, BatteryId,                           |
  |         Port1DbcFileId, Port2DbcFileId, Port3DbcFileId,       |
  |         ScheduledAt,                                          |
  |         TargetCircuitsJson = JSON(request.TargetCircuits),    |
  |         IsActive = true,                                      |
  |         CreatedBy = CurrentUser.UserName }                    |
  |     _repo.AddAsync + SaveChangesAsync                         |
  |                                                               |
  |   SCHEDULE                                                    |
  |     delay = ScheduledAt - Now                                 |
  |     jobId = BackgroundJob.Schedule<ISchedulerService>(        |
  |                 svc => svc.ExecuteScheduleAsync(entity.Id),   |
  |                 delay)                                        |
  |     entity.HangfireJobId = jobId                              |
  |     _repo.UpdateAsync + SaveChangesAsync                      |
  +--------------------------------------------------------------+

  Delete  -> BackgroundJob.Delete(entity.HangfireJobId), then soft-delete the row
  Toggle  -> IsActive flag only; the Hangfire job still fires but exits at the
             IsActive check below
```

### 16.2 Execution — when the job fires

```
  HANGFIRE fires ExecuteScheduleAsync(scheduleId)
        |
        v
  entity = _repo.GetByIdAsync(scheduleId)
        |
        +-- null || !IsActive || IsDeleted
        |     -> log "skipped -- not found / inactive", RETURN
        v
  targets  = JSON -> List<ScheduleCircuitTarget>
  program  = _programService.GetProgramAsync(entity.ProgramId)
  battery  = _batteryService.GetBattery(entity.BatteryId)
  port1/2/3 = ResolveDbcAsync(entity.PortNDbcFileId)   (null-safe, null if unset)
        |
        v
  FOR EACH target:
        |
        |  log = new ScheduleExecutionLog { ScheduleId, DeviceId,
        |                                   CircuitId, ExecutedAt = Now }
        |
        +--> FIND HANDLER
        |      _circuitManager._devices.Values.FirstOrDefault(c =>
        |          c.Channel.DeviceID     == target.DeviceId &&
        |          c.Channel.ChannelNumber == target.CircuitId)
        |
        |      not found -> Status = "Skipped_Offline"
        |                   FailReason = "Channel not found in ChannelManager"
        |                   AddLogAsync, CONTINUE
        |
        +--> STATE GATES
        |      CircuitStatus == Offline
        |          -> "Skipped_Offline", CONTINUE
        |      CircuitStatus != Idle || ProgramStatus != Stop
        |          -> "Skipped_AlreadyRunning"
        |             FailReason = "CircuitStatus=<cs>, ProgramStatus=<ps>"
        |             CONTINUE
        |
        +--> TRANSFER  (same order as the UI: Battery -> DBC -> Program)
        |      HWReadyToReadWriteAsync()
        |          fail -> "Failed", "IsReady failed: <msg>", CONTINUE
        |      SetBatteryParamAsync(battery.Data)        if battery resolved
        |          fail -> "Failed", "Battery transfer failed: <msg>", CONTINUE
        |      TransferDbcFile(port1, port2, port3)      if any port set
        |          fail -> "Failed", "DBC transfer failed: <msg>", CONTINUE
        |      SetProgramAsync(program.Data)             if program resolved
        |          fail -> "Failed", "Program transfer failed: <msg>", CONTINUE
        |
        +--> START
        |      StartProgram()      ==> full Chapter 6 sequence, including
        |          fail -> "Failed", "Start failed: <msg>", CONTINUE
        |
        +--> Status = "Success", AddLogAsync
        |
        v
  NEXT target  (one failure never stops the others)
```

**Every outcome is written to `ScheduleExecutionLog`** — `Success`, `Failed`,
`Skipped_Offline`, `Skipped_AlreadyRunning` — with a `FailReason` string, readable
later via `GetLogsAsync(scheduleId)`. This is the only unattended path in the system,
so its audit trail is the only way to find out what happened overnight.

> **⚠ The scheduler's handler lookup ignores the board number.**
> It matches on `DeviceID` + `ChannelNumber` only — `SecondaryBoardNumber` is not
> compared, and `ScheduleCircuitTarget` carries just `DeviceId` / `CircuitId`. Everywhere
> else in the system addressing is 3-part (`"{Device}-{Board}-{Channel}"`). On a device
> with two boards, channel 1 exists as both `1-0-1` and `1-1-1`, and `FirstOrDefault`
> will pick whichever the dictionary happens to yield first — so a schedule can start a
> program on the wrong physical channel. This matters more since board `0` became a
> valid board (session #11). Not fixed here — documenting only.
> Source: `SchedulerService.cs:291-293`, `ChannelManager.cs:76` (`MakeKey`).

**Files involved:** `Services/Implementations/SchedulerService.cs`,
`Components/Pages/Programs/SchedulerPage.razor`,
`Models/Entities/ProgramSchedule`, `Models/Entities/ScheduleExecutionLog`

---


---

[⬅ Previous](15-broadcast-discovery.md) · [⬅ Index](README.md) · [Next ➡](17-report-and-export.md)
