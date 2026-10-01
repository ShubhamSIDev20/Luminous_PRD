---
name: gortex-repositories-implementations-9-dirs
description: "Work in the Repositories\Implementations +9 dirs area — 228 symbols across 28 files (75% cohesion)"
---

# Repositories\Implementations +9 dirs

228 symbols | 28 files | 75% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Layout\MainLayout.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Models\DTOs\FactoryConfigDetailDTO.cs`
- `Models\DTOs\ManufacturingDetailDTO.cs`
- `Models\DTOs\ResponseDTOs.cs`
- `Models\Entities\AuditLog.cs`
- `Models\Entities\DbcFileRecord.cs`
- `Repositories\Implementations\AuditRepository.cs`
- `Repositories\Implementations\BatteryRepository.cs`
- `Repositories\Implementations\CodeMessageRepository.cs`
- `Repositories\Implementations\ConfigStorageRepository.cs`
- `Repositories\Implementations\DbcRepository.cs`
- `Repositories\Implementations\DeviceCircuitRepository.cs`
- `Repositories\Implementations\ExportRepository.cs`
- `Repositories\Implementations\ProgramRepository.cs`
- `Repositories\Implementations\Repository.cs`
- `Repositories\Implementations\SchedulerRepository.cs`
- `Repositories\Implementations\UserCircuitAccessRepository.cs`
- `Repositories\Interfaces\IRepository.cs`
- `Repositories\Interfaces\ISpecificRepositories.cs`
- `Services\DecoderService.cs`
- `Services\FileManagerService.cs`
- `Services\Implementations\CircuitCommandHandler.cs`
- `Services\Implementations\CodeMessageService.cs`
- `Services\Implementations\SchedulerService.cs`
- `Services\Interfaces\ISchedulerService.cs`
- `Services\ProgramBuilder.cs`
- `Utils\CurrentUser.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Layout\MainLayout.razor` | HandleLogout |
| `Components\UI\Dashboard\TransferDialog.razor` | OnPortAssignmentChanged |
| `Models\DTOs\FactoryConfigDetailDTO.cs` | DeviceIPAddress, UdpStoreRemotePort, CircuitType, CircuitMaxDischargeCurrent, DhcpEnabled, ... |
| `Models\DTOs\ManufacturingDetailDTO.cs` | ManufactureDateTime, CommissioningDateTime, SecondarySWVersion, SecondarySerialNumber, SecondaryPCBAssemblyDateTime, ... |
| `Models\DTOs\ResponseDTOs.cs` | message, Fail, data |
| `Models\Entities\AuditLog.cs` | Details, Metadata, EntityId, Timestamp, IPAddress, ... |
| `Models\Entities\DbcFileRecord.cs` | dbcstrJson, Battery, DbcFileRecord, BatteryId, Id, ... |
| `Repositories\Implementations\AuditRepository.cs` | record, GetAuditRecordsAsync, request, LogEventAsync |
| `Repositories\Implementations\BatteryRepository.cs` | port3DbcId, BatteryDelete, CreateOrUpdateBattery, port1DbcId, port2DbcId, ... |
| `Repositories\Implementations\CodeMessageRepository.cs` | id, SaveChangesAsync, type, _audit, GetByIdAsync, ... |
| `Repositories\Implementations\ConfigStorageRepository.cs` | DeleteConfigurationAsync, config, key, GetConfigurationAsync, SaveConfigurationAsync, ... |
| `Repositories\Implementations\DbcRepository.cs` | UpdateAsync, version, DeleteAsync, id, id, ... |
| `Repositories\Implementations\DeviceCircuitRepository.cs` | AllowCicuitAsync, circuit, UpdateManufacturingAsync, UpdateIsDeleteAsync, UpdateFactoryAsync, ... |
| `Repositories\Implementations\ExportRepository.cs` | GetByIdAsync, record, id, CreateAsync |
| `Repositories\Implementations\ProgramRepository.cs` | cuitDto, UpdateProgramAsync, session, GetLastSessionAsync, EndSession, ... |
| `Repositories\Implementations\Repository.cs` | AnyAsync, SaveChangesAsync, FirstOrDefaultAsync, predicate, predicate |
| `Repositories\Implementations\SchedulerRepository.cs` | AddLogAsync, log |
| `Repositories\Implementations\UserCircuitAccessRepository.cs` | userId, circuits, SetUserCircuitAccessAsync |
| `Repositories\Interfaces\IRepository.cs` | SaveChangesAsync, AnyAsync, FirstOrDefaultAsync, predicate, predicate |
| `Repositories\Interfaces\ISpecificRepositories.cs` | LogEventAsync, record, SaveChangesAsync |
| `Services\DecoderService.cs` | payload, DecodeChangeServerConfiguration, DecodeChangeNetConfig, payload |
| `Services\FileManagerService.cs` | fileStream, ReadFileLines, SaveFileAsync, fileName, fileName |
| `Services\Implementations\CircuitCommandHandler.cs` | port2Dbc, T, port3Dbc, port1Dbc, command, ... |
| `Services\Implementations\CodeMessageService.cs` | id, errors, SaveMessagesAsync, DeleteAsync, messages, ... |
| `Services\Implementations\SchedulerService.cs` | request, CreateScheduleAsync |
| `Services\Interfaces\ISchedulerService.cs` | scheduleId, ExecuteScheduleAsync |
| `Services\ProgramBuilder.cs` | LoadFileAndHash, filePath |
| `Utils\CurrentUser.cs` | IpAddress, UserId, UserAgent, CurrentUser, UserName, ... |

## Entry Points

- `Repositories\Implementations\DeviceCircuitRepository.cs::DeviceCircuitRepository.UpdateAsync`
- `Repositories\Implementations\BatteryRepository.cs::BatteryRepository.CreateOrUpdateBattery`
- `Repositories\Implementations\DbcRepository.cs::DbcRepository.UpdateAsync`
- `Services\Implementations\SchedulerService.cs::SchedulerService.CreateScheduleAsync`
- `Repositories\Implementations\DeviceCircuitRepository.cs::DeviceCircuitRepository.InsertAsync`

## Connected Communities

- **Components\Pages\Programs +18 dirs** (10 cross-edges)
- **Components\UI\Program +15 dirs** (8 cross-edges)
- **Services\Implementations +9 dirs** (6 cross-edges)
- **Services\Implementations +6 dirs** (3 cross-edges)
- **Services +1 dirs · DbcDatabase** (2 cross-edges)
- **Services\Implementations +15 dirs** (2 cross-edges)
- **DbSecurity +1 dirs** (2 cross-edges)
- **Repositories\Implementations +2 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-283"
smart_context with task: "understand Repositories\Implementations +9 dirs", format: "gcx"
find_usages with id: "Repositories\Implementations\DeviceCircuitRepository.cs::DeviceCircuitRepository.UpdateAsync", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
