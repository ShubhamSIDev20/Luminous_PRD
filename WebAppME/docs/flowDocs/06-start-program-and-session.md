# 6. Start Program & Session Creation

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

Start is the most consequential command in the system: it creates the session
identity that every stored data packet will later be filed under.

```
  OPERATOR: Dashboard -> [Start]   (or REST /api/Device/Start, or MCP, or Scheduler)
        |
        v
  +--------------------------------------------------------------+
  | GUARD CHAIN -- all four must pass                             |
  |                                                               |
  |  1. Program loaded?                                           |
  |     Program == null || ProgramSteps == 0                      |
  |       -> "No program loaded to start."                        |
  |                                                               |
  |  2. Program unchanged since transfer?                         |
  |     Program.ProgramHash != Session.ProgramHash                |
  |       -> "The loaded program does not match ... modified."    |
  |                                                               |
  |  3. Not already running?                                      |
  |     RealTime.ProgramStatus == Running                         |
  |       -> "Program is already running."                        |
  |                                                               |
  |  4. Circuit idle?                                             |
  |     RealTime.CircuitStatus != Idle                            |
  |       -> "The circuit is in an <X> state and cannot start."   |
  +--------------------------------+------------------------------+
                                   | all pass
                                   v
  +--------------------------------------------------------------+
  | BUILD SESSION IDENTITY                                        |
  |                                                               |
  | DecoderService.GetSessionIdBytes(DeviceID, addressByte,       |
  |                     out epochSeconds, out sessionDateTime)    |
  |                                                               |
  |  Bit: 31      24 23      16 15                     0          |
  |      +---------+----------+------------------------+          |
  |      | DeviceID| Addr byte| Epoch seconds (low 16) |          |
  |      | (8 bit) | (8 bit)  |       (16 bit)         |          |
  |      +---------+----------+------------------------+          |
  |                                                               |
  |  Why packed and not a GUID:                                   |
  |   - fits the 4-byte wire budget of the Start command          |
  |   - device+channel in the high bytes => no UNIQUE collision   |
  |     when 64 channels are started in the same second           |
  |   - reversible back to a DateTime (SessionIdToDateTime),      |
  |     which is how the UDP store path finds the date folder     |
  |     for a packet that carries no timestamp                    |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | STAMP THE IN-MEMORY SESSION                                   |
  |                                                               |
  |  Session.SessionID           = epochSeconds                   |
  |  Session.DeviceID            = Channel.DeviceID               |
  |  Session.SecondaryBoardNumber= Channel.SecondaryBoardNumber   |
  |  Session.ChannelNumber       = Channel.ChannelNumber          |
  |  Session.StartTime           = DateTime.Now                   |
  |  Session.EndTime             = null                           |
  |  Session.SessionName         = "<ProgramName>_yyyyMMddHHmmssfff"|
  |  Session.SessionFilePath     =                                |
  |      "<dd-MM-yyyy>/<SessionID>_<Dev>_<Board>_<Ch>.db"         |
  |  Session.Unstorerecordcount  = 0                              |
  |  Session.Storerecordcount    = 0                              |
  |                                                               |
  |  NOTE: the board number MUST be in the file name. Without it, |
  |  two channels with the same ChannelNumber on different boards |
  |  of one device collide onto the same session file.            |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | CREATE THE SESSION .db FILE  (before the wire command!)       |
  |                                                               |
  |  producerPrograms = ResolveProducerProgramsAsync(steps)       |
  |  tableFileData    = CollectTableFileData(steps)               |
  |                                                               |
  |  SqliteBulkDatabaseManager.InsertSessionAsync(new SessionRequest{|
  |      filePath             = Session.SessionFilePath,          |
  |      Program              = Program,                          |
  |      Battery              = Battery,                          |
  |      dbcDatabase          = Session.dbcDatabse,               |
  |      ProducerPrograms     = producerPrograms,                 |
  |      TableFileData        = tableFileData,                    |
  |      ExpandedProgramSteps = ExpandedProgramSteps })           |
  |                                                               |
  |  -> GetContext(filePath) -> EnsureCreated() -> schema sync    |
  |  -> everything written inside ONE transaction                 |
  |                                                               |
  |  RESULT: the session file is SELF-CONTAINED. It carries its   |
  |  own copy of the program, battery, DBC map, sub-programs and  |
  |  table data, so a report generated a year later does not      |
  |  depend on the main DB still holding those rows unchanged.    |
  +--------------------------------+------------------------------+
                                   |
                                   v
  +--------------------------------------------------------------+
  | SEND THE START COMMAND                                        |
  |                                                               |
  |  0xEE / Query 0x01 (Control / Start)                          |
  |  Data = the 4-byte packed SessionID                           |
  |                                                               |
  |  -> SendAndWaitForResponseAsync<bool>(payload)                |
  |     15-second timeout via DeviceConnection                    |
  |                                                               |
  |  The hardware now stamps THIS SessionID into every store      |
  |  packet it emits on UDP 10001 -- that is the join key.        |
  +--------------------------------+------------------------------+
                                   |
                    +--------------+--------------+
                    |                             |
              response FAIL                 response OK
                    |                             |
                    v                             v
        return the failure          IProgramServices.CreateSession(Session)
        (the .db file already          -> row in main DB BatterySession
         exists but stays empty                    |
         -- harmless)                              v
                                    ErrorMessages.GetErrors(true)
                                    ErrorMessages.GetMessages(true)
                                    (refresh code-message cache)
                                              |
                                              v
                                    TEST IS RUNNING
                                    -> data starts arriving (Chapter 7)
```

**Files involved:** `Services/Implementations/ChannelCommandHandler.cs:340-452`,
`Services/Implementations/SqliteBulkDatabaseManager.cs:211-358`,
`Services/DecoderService.cs` (`GetSessionIdBytes`), `docs/PROTOCOL.md` §3, §7

---


---

[⬅ Previous](05-transfer-flow.md) · [⬅ Index](README.md) · [Next ➡](07-udp-10001-session-storage.md)
