# 0. Master Block Diagram

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

The whole system on one page. Everything below this chapter is a zoom-in on one
of these blocks.

```
                          BTS HARDWARE (physical)
        +--------------------------------------------------------------+
        |  Device 1                Device 2              Device N       |
        |  +--------------+        +--------------+                     |
        |  | Board 0 / 1  |        | Board 0 / 1  |        ...           |
        |  | Ch 1 .. Ch 64|        | Ch 1 .. Ch 64|                      |
        |  +--------------+        +--------------+                     |
        +--------------------------------------------------------------+
             |          |               |                |
   TCP 9999  |  UDP     |  UDP          |  UDP 10002/3   |
   command   |  10000   |  10001        |  broadcast     |
   req/resp  |  live    |  store        |  discovery     |
             |          |               |  (as coded --  |
             |          |               |   see Ch.15)   |
             v          v               v                v
========================================================================
                    ASP.NET CORE 8  --  SERVER PROCESS
========================================================================
  +------------------------------------------------------------------+
  |            ChannelManager  :  BackgroundService (singleton)       |
  |            registered via AddHostedService(...)                   |
  |                                                                   |
  |  +-------------+ +-------------+ +-------------+ +-------------+  |
  |  | TCP Cmd     | | UDP View    | | UDP Store   | | UDP Store   |  |
  |  | Listener    | | Listener    | | Listener    | | Processor   |  |
  |  | :9999       | | :10000      | | :10001      | | (queue rdr) |  |
  |  +------+------+ +------+------+ +------+------+ +------+------+  |
  |         |               |               |               |         |
  |         v               v               +------>--------+         |
  |  Registration      In-memory                     |                |
  |  + response        live update                   v                |
  |  routing                                   Session .db write      |
  +---------|-------------------|--------------------|---------------+
            |                   |                    |
            v                   v                    v
  +---------------------------------------------------------------+
  |                      SERVICE LAYER                             |
  |  DeviceConnection (1 per device, owns the TcpClient)           |
  |  ChannelCommandHandler (1 per channel slot, owns session state)|
  |  DecoderService / ProgramBuilder / DbcParser  (encode-decode)  |
  |  SqliteBulkDatabaseManager (per-session .db writer)            |
  |  IProgramServices / IBatteryServices / IDeviceChannelServices  |
  |  SchedulerService | ExportJobService (Hangfire)                |
  +--------|----------------------------------------|-------------+
           |                                        |
           v                                        v
  +-------------------------+          +---------------------------+
  |   MAIN DATABASE         |          |   SESSION DATA STORE      |
  |   BtsAppdb.db (EF Core) |          |   sessions/<dd-MM-yyyy>/  |
  |   Channels, Devices,    |          |     <SessionID>_<Dev>_    |
  |   BtsPrograms, Batteries|          |     <Board>_<Ch>.db       |
  |   BatterySession, Users |          |   (one SQLite file per    |
  |   DbcFileRecord, Audit  |          |    test session)          |
  +-------------------------+          +---------------------------+
           ^                                        ^
           |                                        |
  +--------+----------------------------------------+-------------+
  |                    PRESENTATION / API LAYER                    |
  |  Blazor Server UI  |  REST DeviceController  |  MCP Tools      |
  |  (Dashboard,       |  (/api/Device/...)      |  (DeviceMcpTools)|
  |   DeviceList,      |                         |                 |
  |   TransferDialog)  |                                           |
  +---------------------------------------------------------------+
                                 ^
                                 |
                          +------+------+
                          |   OPERATOR  |
                          |    (user)   |
                          +-------------+
```

**Files involved:** `Services/ChannelManager.cs`, `Services/DeviceConnection.cs`,
`Services/Implementations/ChannelCommandHandler.cs`,
`Services/Implementations/SqliteBulkDatabaseManager.cs`,
`Extensions/ServiceCollectionExtensions.cs`, `Controllers/DeviceController.cs`,
`Mcp/DeviceMcpTools.cs`, `Components/Pages/`

---


---

[⬅ Index](README.md) · [Next ➡](01-background-service-startup.md)
