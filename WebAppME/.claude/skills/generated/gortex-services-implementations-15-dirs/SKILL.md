---
name: gortex-services-implementations-15-dirs
description: "Work in the Services\Implementations +15 dirs area — 441 symbols across 32 files (76% cohesion)"
---

# Services\Implementations +15 dirs

441 symbols | 32 files | 76% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Pages\Devices\DeviceList.razor`
- `Components\Pages\Home\DashboardView.razor`
- `Components\Pages\Programs\ProgramList.razor`
- `Components\Pages\Programs\Registrations.razor`
- `Components\Pages\Programs\SchedulerPage.razor`
- `Components\Pages\Settings\Users.razor`
- `Components\UI\Calibration\Calibration.razor`
- `Components\UI\Dashboard\DbcValuePanel.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Components\UI\DataViewer\BmsDashboard.razor`
- `Components\UI\Program\OperatorConstants.cs`
- `Extensions\ServiceCollectionExtensions.cs`
- `Models\DTOs\CircuitDto.cs`
- `Models\DTOs\RegistrationStandardsDTO.cs`
- `Models\DTOs\RequestDTOs.cs`
- `Models\DTOs\ResponseDTOs.cs`
- `Models\Entities\ApplicationRole.cs`
- `Models\Entities\ConfigurationEntity.cs`
- `Models\InitializeDataSeeder.cs`
- `Repositories\Implementations\ConfigStorageRepository.cs`
- `Repositories\Implementations\DeviceCircuitRepository.cs`
- `Repositories\Implementations\ProgramRepository.cs`
- `Repositories\Interfaces\ISpecificRepositories.cs`
- `Services\Implementations\BatteryServices.cs`
- `Services\Implementations\ConfigStorageService.cs`
- `Services\Implementations\DeviceCircuitServices.cs`
- `Services\Implementations\ProgramServices.cs`
- `Services\Implementations\SchedulerService.cs`
- `Services\Implementations\UserCircuitAccessService.cs`
- `Services\Interfaces\ICircuitCommandHandler.cs`
- `Services\Interfaces\ISchedulerService.cs`
- `Services\Interfaces\IServices.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Pages\Devices\DeviceList.razor` | circuit, IsOnline, OpenInfo, context, _infoCircuit, ... |
| `Components\Pages\Home\DashboardView.razor` | AnchorCircuit, dbCircuits, circuits, selectedList, DetailCircuit |
| `Components\Pages\Programs\ProgramList.razor` | OpenEdit, programModel, programId |
| `Components\Pages\Programs\Registrations.razor` | LoadStandards, SelectedStandard, Standards, OnInitializedAsync, EditStandard |
| `Components\Pages\Programs\SchedulerPage.razor` | availableCircuits |
| `Components\Pages\Settings\Users.razor` | allCircuits |
| `Components\UI\Calibration\Calibration.razor` | circuit |
| `Components\UI\Dashboard\DbcValuePanel.razor` | _subscribedCircuit, Circuit |
| `Components\UI\Dashboard\TransferDialog.razor` | selectedCircuits |
| `Components\UI\DataViewer\BmsDashboard.razor` | commandHandler |
| `Components\UI\Program\OperatorConstants.cs` | reloadFromDb, GetStandardsAsync |
| `Extensions\ServiceCollectionExtensions.cs` | services, AddBtsServiceDependencies, configuration |
| `Models\DTOs\CircuitDto.cs` | IsDeleted, CircuitDto, CircuitMaxVoltage, SecondarySwVersion, IsRegistered, ... |
| `Models\DTOs\RegistrationStandardsDTO.cs` | CreatedBy, StandardName, Id, UpdatedBy, CreatedAt, ... |
| `Models\DTOs\RequestDTOs.cs` | Name, ProgramTimeTicks, Description, MaxAh, id, ... |
| `Models\DTOs\ResponseDTOs.cs` | message, T, Ok, CommonResponse, data, ... |
| `Models\Entities\ApplicationRole.cs` | ApplicationRole, rolePriority |
| `Models\Entities\ConfigurationEntity.cs` | ConfigurationEntity, Id, Key, Value |
| `Models\InitializeDataSeeder.cs` | DefaultRegistrationStandard, serviceProvider |
| `Repositories\Implementations\ConfigStorageRepository.cs` | ConfigStorageRepository.<init>, GetConfigurationsAsync, ConfigStorageRepository, context, _AppDbcontext |
| `Repositories\Implementations\DeviceCircuitRepository.cs` | GetCircuitsAsync |
| `Repositories\Implementations\ProgramRepository.cs` | GetRegistrationStandardAsync |
| `Repositories\Interfaces\ISpecificRepositories.cs` | BatteryId, calibration, CreateSession, circuit, GetBattery, ... |
| `Services\Implementations\BatteryServices.cs` | batteryRepository, UpdatePortAssignmentsAsync, port2DbcId, battery, GetBatteries, ... |
| `Services\Implementations\ConfigStorageService.cs` | GetConfigurationAsync, configStorage, config, SaveConfigurationAsync, ConfigStorageService, ... |
| `Services\Implementations\DeviceCircuitServices.cs` | GetFactoryAsync, GetManufacturingAsync, _deviceCircuitRepository, circuit, IsDelete, ... |
| `Services\Implementations\ProgramServices.cs` | AddOrUpdateSession, programId, RecoverProgramAsync, GetSessionsAsync, DeleteProgramAsync, ... |
| `Services\Implementations\SchedulerService.cs` | _repo, GetAllSchedulesAsync, repo, SchedulerService, _batteryService, ... |
| `Services\Implementations\UserCircuitAccessService.cs` | UserCircuitAccessService, _repo, repo, GetByUserIdAsync, userId, ... |
| `Services\Interfaces\ICircuitCommandHandler.cs` | ProgramStepsDTO, Circuit, SendAndWaitForResponseAsync, command, Battery, ... |
| `Services\Interfaces\ISchedulerService.cs` | scheduleId, ISchedulerService, DeleteScheduleAsync, GetLogsAsync, CreateScheduleAsync, ... |
| `Services\Interfaces\IServices.cs` | dto, GetManufacturingAsync, id, GetConfigurationAsync, circuit, ... |

## Entry Points

- `Extensions\ServiceCollectionExtensions.cs::ServiceCollectionExtensions.AddBtsServiceDependencies`

## Connected Communities

- **Repositories\Implementations +9 dirs** (5 cross-edges)
- **Components\Pages\Programs +18 dirs** (2 cross-edges)
- **Services\Implementations +9 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-264"
smart_context with task: "understand Services\Implementations +15 dirs", format: "gcx"
find_usages with id: "Extensions\ServiceCollectionExtensions.cs::ServiceCollectionExtensions.AddBtsServiceDependencies", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
