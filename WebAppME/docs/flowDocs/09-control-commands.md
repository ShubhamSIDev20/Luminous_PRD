# 9. Control Commands

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

All six control commands share one shape: guard → build frame → send and wait →
update DB. They differ only in the guard and the side-effect.

```
                    +--------------------------------+
                    |  SendAndWaitForResponseAsync<T>|
                    |                                |
                    |  1. Connection.TcpClient null  |
                    |     or !Connected -> Fail      |
                    |  2. commandinterrupt already   |
                    |     true -> "Another command   |
                    |     is in progress."           |
                    |  3. commandinterrupt = true    |
                    |  4. addressByte = Encode(      |
                    |        board, channel)         |
                    |     queryId = command[3]       |
                    |  5. Connection.SendAndWaitAsync|
                    |        (15 s timeout)          |
                    |  6. DecoderService.TryDecode<T>|
                    |  finally:                      |
                    |     commandinterrupt = false   |
                    |     OnChannelChanged?.Invoke() |
                    +----------------+---------------+
                                     ^
             every command below funnels through here
                                     |
  +----------------------------------+--------------------------------+
  |                                                                    |
  |  START      0xEE/0x01   guards: program loaded, hash match,        |
  |                                 not running, circuit Idle          |
  |                         data:   packed 4-byte SessionID            |
  |                         effect: creates session .db + main DB row  |
  |                                 (full detail in Chapter 6)         |
  |                                                                    |
  |  STOP       0xEE/0x02   guards: none                               |
  |                         effect: on success ->                      |
  |                                 Session.EndTime = Now              |
  |                                 IProgramServices.EndSession(...)   |
  |                                                                    |
  |  PAUSE      0xEE/0x03   guards: ProgramStatus MUST be Running      |
  |                                 else "Program is not running,      |
  |                                       cannot Interrupt."           |
  |                                                                    |
  |  CONTINUE   0xEE/0x04   guards: program must be in paused state    |
  |                                                                    |
  |  TIME SYNC  0xEE/0x06   data:   4-byte epoch                       |
  |             (see note)  effect: aligns hardware clock with server  |
  |                                                                    |
  |  RESET      0xEE/0x06   guards: intended for system-error recovery |
  |                                 only ("only if has system error")  |
  |                         effect: soft reboot of the channel;        |
  |                                 hardware will re-register          |
  |                                 (back to Chapter 2)                |
  |                                                                    |
  |  UNREGISTER 0xDD/0x02   effect: tells the channel to forget the    |
  |                                 server; handler removed from       |
  |                                 _devices (Chapter 3 deregister)    |
  +--------------------------------------------------------------------+

  Every command is also recorded for audit:
    CommandTracker.AddCommand("<Command> command executed by <UserName>!")
```

> **⚠ Observation — TimeSyn and SystemReset build the same frame.**
> `TimeSyn()` uses `StartByte.Control` (`0xEE`) but takes its query ID from
> `ConfigurationQuery.SyncTime` (`0x06`), while `ResetSystem()` uses
> `ProgramControlQuery.SystemReset` — which is *also* `0x06`. On the wire both
> therefore send `0xEE` + query `0x06`, differing only in that TimeSyn appends a
> 4-byte epoch payload. Per `docs/PROTOCOL.md` §7, SyncTime under the Control
> family is `0x05`, not `0x06`. This is documented here as observed behaviour,
> not as intended design — it should be confirmed against the firmware before
> anyone relies on either command.
> Source: `ChannelCommandHandler.cs:559-592`.

### Session end — two different routes

```
        OPERATOR STOPS IT                    PROGRAM RUNS TO COMPLETION
               |                                        |
               v                                        v
        StopProgram()                        hardware emits a record with
        0xEE / 0x02                          Operator == OperatorConstants.STO
               |                                        |
               v                                        v
        response OK?                         StartStoreWorkerAsync detects it
               |                             while draining the store queue
               v                                        |
        Session.EndTime = Now                           v
        EndSession(Session)                  Session.EndTime = Now
                                             EndSession(Session)

        Both converge on the same main-DB update. The second route needs
        no command at all -- the end of the test is discovered in the data.
```

**Files involved:** `Services/Implementations/ChannelCommandHandler.cs:298-592,
858-869`, `Services/CommandTracker.cs`, `docs/PROTOCOL.md` §7

---


---

[⬅ Previous](08-udp-10000-live-view.md) · [⬅ Index](README.md) · [Next ➡](10-read-back-commands.md)
