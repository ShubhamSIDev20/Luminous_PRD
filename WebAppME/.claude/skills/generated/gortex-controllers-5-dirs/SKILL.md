---
name: gortex-controllers-5-dirs
description: "Work in the Controllers +5 dirs area — 136 symbols across 8 files (88% cohesion)"
---

# Controllers +5 dirs

136 symbols | 8 files | 88% cohesion

## When to Use

Use this skill when working on files in:
- `Controllers\AuthController.cs`
- `Controllers\DeviceController.cs`
- `Controllers\ExportController.cs`
- `MCP\DeviceMcpTools.cs`
- `Models\DTOs\RealTimeRecordDto.cs`
- `Models\ViewModels\CommonRequest.cs`
- `Services\Interfaces\ICircuitCommandHandler.cs`
- `Utils\TimingHelper.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Controllers\AuthController.cs` | HttpPost |
| `Controllers\DeviceController.cs` | Continue, GetBatteries, request, request, CoreStart, ... |
| `Controllers\ExportController.cs` | HttpGet |
| `MCP\DeviceMcpTools.cs` | circuitId, circuitId, GetSession, deviceId, circuitId, ... |
| `Models\DTOs\RealTimeRecordDto.cs` | ProgramStatus, SystemErrorId, CycleRunIteration, CircuitStatus, BatteryID, ... |
| `Models\ViewModels\CommonRequest.cs` | ProgramId, CommonRequest, CircuitID, BatteryId, dbcId, ... |
| `Services\Interfaces\ICircuitCommandHandler.cs` | HWReadyToReadWriteAsync, StartProgram |
| `Utils\TimingHelper.cs` | TimingHelper, name, MeasureAsync, action |

## Connected Communities

- **Repositories\Implementations +9 dirs** (2 cross-edges)

## How to Explore

```
get_communities with id: "community-196"
smart_context with task: "understand Controllers +5 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
