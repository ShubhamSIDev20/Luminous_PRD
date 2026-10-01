# MCP (Model Context Protocol) Tools API
> Updated: 2026-09-05T00:00:00Z
> Source: `MCP/DeviceMcpTools.cs`
> Endpoint: `/mcp` (HTTP transport, stateless)
> Cross Reference: [api.md](../api.md) | [device.md](device.md)

---

## Overview
The Battery Testing System exposes MCP tools allowing LLMs and Claude Desktop clients to directly interact with hardware, test configurations, and database entities.

MCP tools reuse the exact same static core methods (`DeviceController.Core*`) as the REST API endpoints, ensuring zero duplicated logic and identical validation and error handling across REST and MCP.

### Registration in `Program.cs`
```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<DeviceMcpTools>();

app.MapMcp("/mcp");   // no .RequireAuthorization() in the actual code — see Auth note below
```

> **Auth note (corrected 2026-09-05):** this doc previously showed `.MapMcp("/mcp").RequireAuthorization()`, but the real `Program.cs` maps it with **no** `.RequireAuthorization()` call. The `/mcp` endpoint is anonymous-accessible today, same gap already tracked for `DeviceController` in `CONTEXT/api.md`/`.claude/TASKS.md` T-2 — no bearer token or header is needed to connect in the client examples below. Locking this down means adding `.RequireAuthorization()` back (or a scheme-specific policy) once the wider auth gap is addressed.

---

## Connecting an MCP Client

The server speaks **Streamable HTTP** at `http://<host>:5066/mcp` (port from `Properties/launchSettings.json`'s `http` profile in dev; swap in the real host/port for a deployed instance). No auth header is required today (see note above). Examples below use `battery-testing-system` as the server name — pick any name you like, it's just a local label.

### Claude Code

```bash
claude mcp add --transport http battery-testing-system http://localhost:5066/mcp
```

This writes to `.mcp.json` (project scope) or your user-level config, depending on the scope flag (`--scope local|project|user`):

```json
{
  "mcpServers": {
    "battery-testing-system": {
      "type": "http",
      "url": "http://localhost:5066/mcp"
    }
  }
}
```

Run `/mcp` inside a Claude Code session afterward to confirm the connection and see the tool list.

### Claude Desktop

`claude_desktop_config.json` only validates **stdio** servers directly — a bare `url`/`type: "http"` entry there is ignored. Two working options:

1. **Custom Connector (recommended, no file edit)** — Settings → Connectors → Add custom connector → paste `http://localhost:5066/mcp`. Desktop manages this in its own app state, not `claude_desktop_config.json`.
2. **`mcp-remote` stdio bridge** — if you need it in the config file (e.g. for scripted setup), bridge through `npx mcp-remote`:
   ```json
   {
     "mcpServers": {
       "battery-testing-system": {
         "command": "npx",
         "args": ["mcp-remote", "http://localhost:5066/mcp", "--transport", "http-only"]
       }
     }
   }
   ```

### VS Code (MCP support / Copilot Chat)

Add to `.vscode/mcp.json` (workspace) or your user MCP config:

```json
{
  "servers": {
    "battery-testing-system": {
      "type": "http",
      "url": "http://localhost:5066/mcp"
    }
  }
}
```

`"type": "http"` is required for remote servers — VS Code tries Streamable HTTP first and falls back to SSE.

### Codex CLI

Add to `~/.codex/config.toml` (global) or `.codex/config.toml` (project, trusted projects only):

```toml
[mcp_servers.battery-testing-system]
url = "http://localhost:5066/mcp"
```

Or via the CLI: `codex mcp add battery-testing-system --url http://localhost:5066/mcp`. Codex recognizes a bare `url` as `streamable_http` transport automatically; add `bearer_token_env_var` only if `.RequireAuthorization()` is reinstated later.

---

## Tool Catalog

### 1. Control Tools

| Tool Name | Parameters | Description | REST Equivalent |
|-----------|------------|-------------|-----------------|
| `SendProgram` | `deviceId` (int), `circuitId` (int), `programId` (int), `batteryId` (int?), `dbcId` (int?) | Configure program, battery, and DBC file on a circuit prior to test start. | `POST /api/device/SendProgram` (ref: [device.md](device.md)) |
| `StartProgram` | `deviceId` (int), `circuitId` (int) | Start battery test execution on a circuit. | `POST /api/device/Start` (ref: [device.md](device.md)) |
| `StopProgram` | `deviceId` (int), `circuitId` (int) | Stop a running battery test on a circuit. | `POST /api/device/Stop` (ref: [device.md](device.md)) |
| `PauseProgram` | `deviceId` (int), `circuitId` (int) | Pause a running test on a circuit. | `POST /api/device/Pause` (ref: [device.md](device.md)) |
| `ContinueProgram` | `deviceId` (int), `circuitId` (int) | Resume a paused test on a circuit. | `POST /api/device/Continue` (ref: [device.md](device.md)) |

### 2. Status & Monitoring Tools

| Tool Name | Parameters | Description | REST Equivalent |
|-----------|------------|-------------|-----------------|
| `GetSession` | `deviceId` (int), `circuitId` (int) | Returns active session metadata (SessionID, Name, Start/EndTime, Battery, Program, DBC, record counts). | `GET /api/device/GetSessions` / `POST /api/device/GetSessionId` |
| `GetDeviceStatus` | `deviceId` (int), `circuitId` (int) | Real-time voltage, current, temperature, step status from circuit handler. | In-memory `RealTimeRecord` |
| `GetLiveData` | `deviceId` (int), `circuitId` (int) | Query live telemetry records from `ChannelManager`. | `POST /api/device/GetLiveData` (ref: [device.md](device.md)) |

### 3. Registry & Lookup Tools

| Tool Name | Parameters | Description | REST Equivalent |
|-----------|------------|-------------|-----------------|
| `GetPrograms` | None | Lists all test programs with ID, name, description, author, creation timestamp. | `POST /api/device/GetPrograms` (ref: [device.md](device.md)) |
| `GetBatteries` | None | Lists all registered batteries in the system. | `POST /api/device/GetBatteries` (ref: [device.md](device.md)) |
| `GetDbcFiles` | None | Lists all CAN DBC file records registered in the system. | `POST /api/device/GetDbcFiles` (ref: [device.md](device.md)) |

---

## Architectural Notes
- **Core Delegation**: MCP tools call `DeviceController.CoreSendProgram`, `CoreStart`, `CoreStop`, `CorePause`, `CoreContinue`, and `CoreLiveData`.
- **Channel Addressing**: Uses `deviceId` + `circuitId` (maps to `SecondaryBoardNumber = 1`, `ChannelList = [circuitId]`). `CommonRequest.ChannelNumber` was removed 2026-09-04 — every MCP tool builds its `CommonRequest` with a single-element `ChannelList` instead. (This was a build break: `DeviceMcpTools.cs` still set `ChannelNumber = circuitId` after the `CommonRequest` field was dropped, and every tool below failed to compile until fixed.)
- **No bulk channels via MCP**: MCP tool signatures still take one `circuitId` per call — `CommonRequest.ChannelList` bulk support (multiple channels in one request) is REST-only for now; see [device.md](device.md#bulk-channel-operations).
- **Tests**: `BatteryTestingSystem.Tests/Controllers/DeviceMcpToolsTests.cs` covers each tool's single-channel key construction against a fake `ChannelManager`/`IChannelCommandHandler`, exercised via `[InternalsVisibleTo("BatteryTestingSystem.Tests")]` on `BatteryTestingSystem.csproj` (needed because `DeviceController.Core*` are `internal`).
