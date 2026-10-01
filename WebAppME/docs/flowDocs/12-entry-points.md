# 12. Entry Points — Four Doors, One Core

> Part of the [BTS System Flow documentation](README.md) · [Document index](README.md)

---

The same `ChannelCommandHandler` methods are reachable four different ways. No
entry point has its own copy of the logic.

```
  +-------------+   +--------------+   +-------------+   +---------------+
  | BLAZOR UI   |   | REST API     |   | MCP TOOLS   |   | SCHEDULER     |
  |             |   |              |   |             |   |               |
  | Dashboard   |   | POST         |   | DeviceMcp   |   | SchedulerService|
  | TransferDlg |   |  /SendProgram|   | Tools.cs    |   | ProgramSchedule|
  | DeviceList  |   |  /Start      |   |             |   | rows -> timed  |
  | Programs    |   |  /Stop       |   | (reuses the |   | Start/Stop     |
  | Calibration |   |  /Pause      |   |  Core*      |   |               |
  |             |   |  /Continue   |   |  methods)   |   |               |
  +------+------+   +-------+------+   +------+------+   +-------+-------+
         |                  |                 |                  |
         |                  |                 |                  |
         +--------+---------+--------+--------+---------+--------+
                           |
                           v
              +---------------------------+
              | ChannelManager._devices   |
              | lookup by key             |
              | "<Dev>-<Board>-<Ch>"      |
              +-------------+-------------+
                            |
                            v
              +---------------------------+
              | IChannelCommandHandler    |
              |                           |
              |  SetBatteryParamAsync     |
              |  TransferDbcFile          |
              |  SetProgramAsync          |
              |  StartProgram             |
              |  StopProgram              |
              |  PauseProgram             |
              |  ContinueProgram          |
              |  TimeSyn / ResetSystem    |
              |  GetManufacturingDetails  |
              |  GetFactoryConfigDetails  |
              |  Calibration methods      |
              |  UnregisterAsync          |
              +-------------+-------------+
                            |
                            v
              +---------------------------+
              | DeviceConnection          |
              | .SendAndWaitAsync         |
              +-------------+-------------+
                            |
                            v
                    TCP :9999 -> hardware
```

> **⚠ The doors share the handler, but not the orchestration.** Each entry point
> decides for itself in what order to call the handler's methods. `TransferDialog`
> sends Battery → DBC → Program with all 3 DBC ports; `DeviceController.CoreSendProgram`
> sends Program → Battery → DBC with port 1 only. The single-core diagram above is true
> of the *command* layer, not of the *sequence* layer. Side-by-side comparison in
> [S4](13-code-call-sequences.md#s4-transfer-from-rest--mcp-devicecontrollercoresendprogram).

**Note on the preview path:** `PreviewChannelCommandHandler` implements the same
interface but returns `"Preview only."` for every command. It backs the
dashboard's layout-preview mode so designers can arrange cards without any
hardware attached.

**Files involved:** `Components/Pages/Home/DashboardView.razor`,
`Components/UI/Dashboard/TransferDialog.razor`,
`Components/UI/Dashboard/PreviewChannelCommandHandler.cs`,
`Controllers/DeviceController.cs:71-125`, `Mcp/DeviceMcpTools.cs`,
`Services/Implementations/SchedulerService.cs`

---


---

[⬅ Previous](11-error-disconnect-reconnect.md) · [⬅ Index](README.md) · [Next ➡](13-code-call-sequences.md)
