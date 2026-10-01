---
name: gortex-repositories-implementations-12-dirs
description: "Work in the Repositories\Implementations +12 dirs area — 271 symbols across 32 files (76% cohesion)"
---

# Repositories\Implementations +12 dirs

271 symbols | 32 files | 76% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Layout\MainLayout.razor`
- `Components\Pages\Devices\DeviceList.razor`
- `Components\Pages\Home\DashboardView.razor`
- `Components\Pages\Programs\SchedulerPage.razor`
- `Components\Pages\Programs\TableFileManager.razor`
- `Components\Pages\Settings\Users.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Models\DTOs\CircuitDto.cs`
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
- `Services\ProgramBuilder.cs`
- `Utils\CurrentUser.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Layout\MainLayout.razor` | HandleLogout |
| `Components\Pages\Devices\DeviceList.razor` | OpenInfo, IsOnline, _infoCircuit, circuit, context, ... |
| `Components\Pages\Home\DashboardView.razor` | dbCircuits |
| `Components\Pages\Programs\SchedulerPage.razor` | availableCircuits |
| `Components\Pages\Programs\TableFileManager.razor` | EditFile, fileName, ViewFile, fileName |
| `Components\Pages\Settings\Users.razor` | allCircuits |
| `Components\UI\Dashboard\TransferDialog.razor` | OnPortAssignmentChanged |
| `Models\DTOs\CircuitDto.cs` | CircuitMinVoltage, CreatedAt, CircuitDto, AssemblyDate, SecondaryAssemblyDate, ... |
| `Models\DTOs\FactoryConfigDetailDTO.cs` | CircuitNumber, MacID, CircuitMaxDischargeCurrent, ZntMaxVoltage, DeviceIPAddress, ... |
| `Models\DTOs\ManufacturingDetailDTO.cs` | ComSWVersion, SecondarySWVersion, ManufactureDateTime, MasterSWVersion, PrimaryPCBAssemblyDateTime, ... |
| `Models\DTOs\ResponseDTOs.cs` | Fail, data, message |
| `Models\Entities\AuditLog.cs` | LogId, Metadata, Timestamp, Action, User, ... |
| `Models\Entities\DbcFileRecord.cs` | FileSizeBytes, Version, Description, dbcstrJson, FilePath, ... |
| `Repositories\Implementations\AuditRepository.cs` | LogEventAsync, GetAuditRecordsAsync, record, request |
| `Repositories\Implementations\BatteryRepository.cs` | batteryId, port2DbcId, battery, BatteryDelete, port3DbcId, ... |
| `Repositories\Implementations\CodeMessageRepository.cs` | SaveChangesAsync, index, _audit, CodeMessageRepository, CreateAsync, ... |
| `Repositories\Implementations\ConfigStorageRepository.cs` | GetConfigurationAsync, key, key, SaveConfigurationAsync, DeleteConfigurationAsync, ... |
| `Repositories\Implementations\DbcRepository.cs` | _audit, id, UpdateAsync, name, AddAsync, ... |
| `Repositories\Implementations\DeviceCircuitRepository.cs` | circuit, DeleteAsync, manufacturing, IsDelete, circuit, ... |
| `Repositories\Implementations\ExportRepository.cs` | CreateAsync, id, record, GetByIdAsync |
| `Repositories\Implementations\ProgramRepository.cs` | dto, cuitDto, GetProgramByNameAsync, EndSession, DeleteProgramAsync, ... |
| `Repositories\Implementations\Repository.cs` | SaveChangesAsync, AnyAsync, FirstOrDefaultAsync, predicate, predicate |
| `Repositories\Implementations\SchedulerRepository.cs` | log, AddLogAsync |
| `Repositories\Implementations\UserCircuitAccessRepository.cs` | circuits, userId, SetUserCircuitAccessAsync |
| `Repositories\Interfaces\IRepository.cs` | SaveChangesAsync, predicate, FirstOrDefaultAsync, predicate, AnyAsync |
| `Repositories\Interfaces\ISpecificRepositories.cs` | SaveChangesAsync, LogEventAsync, record |
| `Services\DecoderService.cs` | payload, DecodeChangeServerConfiguration, DecodeChangeNetConfig, payload |
| `Services\FileManagerService.cs` | fileName, ReadFileLines, fileStream, fileName, SaveFileAsync, ... |
| `Services\Implementations\CircuitCommandHandler.cs` | port2Dbc, command, port1Dbc, SendAndWaitForResponseAsync, TransferDbcFile, ... |
| `Services\Implementations\CodeMessageService.cs` | id, SaveErrorsAsync, DeleteAsync, errors, SaveMessagesAsync, ... |
| `Services\ProgramBuilder.cs` | LoadFileAndHash, filePath |
| `Utils\CurrentUser.cs` | UserName, IpAddress, httpContextAccessor, CurrentUser, Configure, ... |

## Entry Points

- `Repositories\Implementations\DeviceCircuitRepository.cs::DeviceCircuitRepository.UpdateAsync`
- `Repositories\Implementations\BatteryRepository.cs::BatteryRepository.CreateOrUpdateBattery`
- `Repositories\Implementations\DbcRepository.cs::DbcRepository.UpdateAsync`
- `Repositories\Implementations\DeviceCircuitRepository.cs::DeviceCircuitRepository.InsertAsync`

## Connected Communities

- **Components\Pages\Programs +18 dirs** (10 cross-edges)
- **Components\UI\Program +15 dirs** (8 cross-edges)
- **Services\Implementations +9 dirs** (4 cross-edges)
- **Services\Implementations +15 dirs** (2 cross-edges)
- **Services\Implementations +6 dirs** (2 cross-edges)
- **DbSecurity +1 dirs** (2 cross-edges)
- **Services +1 dirs · DbcDatabase** (2 cross-edges)
- **Repositories\Implementations +2 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-282"
smart_context with task: "understand Repositories\Implementations +12 dirs", format: "gcx"
find_usages with id: "Repositories\Implementations\DeviceCircuitRepository.cs::DeviceCircuitRepository.UpdateAsync", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
