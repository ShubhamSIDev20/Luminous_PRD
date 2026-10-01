---
name: gortex-services-implementations-9-dirs
description: "Work in the Services\Implementations +9 dirs area — 112 symbols across 16 files (68% cohesion)"
---

# Services\Implementations +9 dirs

112 symbols | 16 files | 68% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Pages\Batteries\BatteriesList.razor`
- `Components\Pages\Programs\SchedulerPage.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Config\AppSettings.cs`
- `Controllers\DeviceController.cs`
- `Controllers\ExportController.cs`
- `MCP\DeviceMcpTools.cs`
- `Models\DTOs\DbcFileRecordDto.cs`
- `Models\ViewModels\DbcFileRequest.cs`
- `Services\Implementations\CircuitCommandHandler.cs`
- `Services\Implementations\DbcService.cs`
- `Services\Implementations\DeviceCircuitServices.cs`
- `Services\Implementations\SchedulerService.cs`
- `Services\Implementations\SqliteBulkDatabaseManager.cs`
- `Services\Interfaces\ICircuitCommandHandler.cs`
- `Services\Interfaces\IServices.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Pages\Batteries\BatteriesList.razor` | _dbcUploadEditTarget, ConfirmDeleteDbc, _dbcForm, _dbcSelected, dbc, ... |
| `Components\Pages\Programs\SchedulerPage.razor` | battery, OnBatterySelected, availableDbcFiles |
| `Components\UI\Dashboard\TransferDialog.razor` | battery, OnBatterySelected, DbcFiles |
| `Config\AppSettings.cs` | GlobalConfig, DbEncryption, AppSettings, Data, AppSettings |
| `Controllers\DeviceController.cs` | DeviceController.<init>, dbcService, batteryServices, circuitServices, cm, ... |
| `Controllers\ExportController.cs` | deleteFile, Delete, id, HttpDelete |
| `MCP\DeviceMcpTools.cs` | dbcService, cm, programServices, batteryServices, DeviceMcpTools.<init> |
| `Models\DTOs\DbcFileRecordDto.cs` | FileSizeBytes, DbcFileRecordDto, Description, CreatedAt, UpdatedBy, ... |
| `Models\ViewModels\DbcFileRequest.cs` | Version, Id, BatteryId, Description, Name, ... |
| `Services\Implementations\CircuitCommandHandler.cs` | InitializeAsync, ConnectionAlive |
| `Services\Implementations\DbcService.cs` | _repo, id, DbcService, file, GetByIdAsync, ... |
| `Services\Implementations\DeviceCircuitServices.cs` | circuit, UpdateAsync |
| `Services\Implementations\SchedulerService.cs` | DeleteScheduleAsync, scheduleId, isActive, ToggleActiveAsync, scheduleId |
| `Services\Implementations\SqliteBulkDatabaseManager.cs` | SqliteBulkDatabaseManager.<init> |
| `Services\Interfaces\ICircuitCommandHandler.cs` | port2Dbc, port1Dbc, port3Dbc, TransferDbcFile |
| `Services\Interfaces\IServices.cs` | Id, DeleteAsync, GetByIdAsync, deletedBy, GetFilePathAsync, ... |

## Entry Points

- `Services\Implementations\DbcService.cs::DbcService.UploadAsync`

## Connected Communities

- **Repositories\Implementations +9 dirs** (21 cross-edges)
- **Components\Pages\Programs +18 dirs** (9 cross-edges)
- **Services\Implementations +6 dirs** (2 cross-edges)
- **Services\Implementations +15 dirs** (2 cross-edges)
- **Services +1 dirs · DbcDatabase** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-285"
smart_context with task: "understand Services\Implementations +9 dirs", format: "gcx"
find_usages with id: "Services\Implementations\DbcService.cs::DbcService.UploadAsync", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
