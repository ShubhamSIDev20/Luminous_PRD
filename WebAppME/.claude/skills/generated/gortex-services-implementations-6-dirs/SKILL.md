---
name: gortex-services-implementations-6-dirs
description: "Work in the Services\Implementations +6 dirs area — 154 symbols across 14 files (68% cohesion)"
---

# Services\Implementations +6 dirs

154 symbols | 14 files | 68% cohesion

## When to Use

Use this skill when working on files in:
- `Models\DTOs\RequestDTOs.cs`
- `Models\Entities\Circuit.cs`
- `Models\Enums\CircuitEnums.cs`
- `Program.cs`
- `Services\CommandTracker.cs`
- `Services\DecoderService.cs`
- `Services\Implementations\BatteryServices.cs`
- `Services\Implementations\CircuitCommandHandler.cs`
- `Services\Implementations\DeviceCircuitServices.cs`
- `Services\Implementations\ProgramServices.cs`
- `Services\Implementations\SchedulerService.cs`
- `Services\Implementations\ServiceLocator.cs`
- `Services\Interfaces\IServices.cs`
- `Services\Interfaces\ISqliteBulkDatabaseManager.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Models\DTOs\RequestDTOs.cs` | QueryId, CircuitId, Data, DeviceId, CommandRequest, ... |
| `Models\Entities\Circuit.cs` | CircuitMaxVoltage, SwVersion, Circuit, ZntMaxVoltage, SecondarySerialNumber, ... |
| `Models\Enums\CircuitEnums.cs` | Continue, SyncTime, SystemReset, ProgramControlQuery, Stop, ... |
| `Program.cs` | Program |
| `Services\CommandTracker.cs` | state, _idLock, _currentId, CommandTracker.<init>, CommandInfo, ... |
| `Services\DecoderService.cs` | circuitId, GetEpochTimeBytes, dt, BuildCommand, EpochSeconds, ... |
| `Services\Implementations\BatteryServices.cs` | BatteryId, GetBattery |
| `Services\Implementations\CircuitCommandHandler.cs` | IsConnected, force, dto, GetManufacturingDetails, ResolveProducerProgramsAsync, ... |
| `Services\Implementations\DeviceCircuitServices.cs` | circuit, Factory, UpdateFactoryAsync, UpdateManufacturingAsync, circuit, ... |
| `Services\Implementations\ProgramServices.cs` | session, programName, EndSession, GetProgramByNameAsync, CreateSession, ... |
| `Services\Implementations\SchedulerService.cs` | ExecuteScheduleAsync, scheduleId |
| `Services\Implementations\ServiceLocator.cs` | scope, ScopedService.<init>, SetProvider, _provider, Service, ... |
| `Services\Interfaces\IServices.cs` | BatteryId, session, circuit, UpdateManufacturingAsync, EndSession, ... |
| `Services\Interfaces\ISqliteBulkDatabaseManager.cs` | InsertRecordAsync, recordDto, InsertSessionAsync, session |

## Connected Communities

- **Repositories\Implementations +9 dirs** (16 cross-edges)
- **Components\Pages\Programs +18 dirs** (7 cross-edges)
- **Services +5 dirs** (4 cross-edges)
- **Services\Implementations +9 dirs** (3 cross-edges)
- **Services\Implementations +15 dirs** (3 cross-edges)
- **Components\UI\Program +8 dirs** (2 cross-edges)
- **Controllers +5 dirs** (2 cross-edges)
- **Components\UI\Program +15 dirs** (2 cross-edges)
- **Components\UI\DataViewer +13 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-284"
smart_context with task: "understand Services\Implementations +6 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
