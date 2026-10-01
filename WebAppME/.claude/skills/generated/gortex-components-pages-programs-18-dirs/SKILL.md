---
name: gortex-components-pages-programs-18-dirs
description: "Work in the Components\Pages\Programs +18 dirs area — 207 symbols across 28 files (76% cohesion)"
---

# Components\Pages\Programs +18 dirs

207 symbols | 28 files | 76% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Pages\Batteries\BatteriesList.razor`
- `Components\Pages\Devices\DeviceList.razor`
- `Components\Pages\Home\DashboardView.razor`
- `Components\Pages\Programs\ProgramEditor.razor`
- `Components\Pages\Programs\ProgramList.razor`
- `Components\Pages\Programs\Registrations.razor`
- `Components\Pages\Programs\SchedulerPage.razor`
- `Components\Pages\Programs\TableFileManager.razor`
- `Components\Pages\Reports\Reports.razor`
- `Components\Pages\Settings\Users.razor`
- `Components\UI\Calibration\Calibration.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Components\UI\Dashboard\TransferResultsDialog.razor`
- `Components\UI\Toast\Toast.razor`
- `Controllers\DeviceController.cs`
- `DbSecurity\IColumnEncryptionServiceWithSalt.cs`
- `Extensions\ServiceCollectionExtensions.cs`
- `MCP\DeviceMcpTools.cs`
- `Migrations\20260120091054_SessionStorage.cs`
- `Repositories\Interfaces\IExportRepository.cs`
- `Repositories\Interfaces\ISpecificRepositories.cs`
- `Services\BROADCAST\BroadcastUdpService.cs`
- `Services\CircuitManager.cs`
- `Services\FileManagerService.cs`
- `Services\Implementations\ProgramServices.cs`
- `Services\Interfaces\IServices.cs`
- `Services\ServerSessionStorageService.cs`
- `Services\ToastService.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Pages\Batteries\BatteriesList.razor` | ExecuteDelete, dbc, ExecuteDeleteDbc, ClearDbcSelection, database, ... |
| `Components\Pages\Devices\DeviceList.razor` | RegisterAllAsync, HandleDeviceChange, DeleteCircuitAsync, circuit, removeFromCM, ... |
| `Components\Pages\Home\DashboardView.razor` | r, HandleDeviceChange, action, HandleTransferCompleted, results, ... |
| `Components\Pages\Programs\ProgramEditor.razor` | HandleViewProducer, HandleSaveDialogSave, InitializeProgram, programName, HandleSaveDialogDiscard |
| `Components\Pages\Programs\ProgramList.razor` | SaveProgramAsync, ConfirmDelete, programId, OnInitializedAsync, LoadPrograms, ... |
| `Components\Pages\Programs\Registrations.razor` | CreateStandard, ShowDeleteConfirm, SelectStandard, AddNewStandard, ShowEditConfirm, ... |
| `Components\Pages\Programs\SchedulerPage.razor` | schedule, LoadLogsAsync, ToggleActive, LoadFormDataAsync, LoadSchedulesAsync, ... |
| `Components\Pages\Programs\TableFileManager.razor` | ViewFile, DeleteFile, SaveFile, fileName, fileName, ... |
| `Components\Pages\Reports\Reports.razor` | RunExportJobAsync, ConfirmDownloadExistingAsync, LoadSessions, OnInitializedAsync, session, ... |
| `Components\Pages\Settings\Users.razor` | user, SaveUserAsync, ClosePasswordDialog, OpenDeleteConfirmation, user, ... |
| `Components\UI\Calibration\Calibration.razor` | Calibration |
| `Components\UI\Dashboard\TransferDialog.razor` | HandleTransfer, LoadAsync, OnInitializedAsync |
| `Components\UI\Dashboard\TransferResultsDialog.razor` | Success, results, Message, TransferResult, DeviceId, ... |
| `Components\UI\Toast\Toast.razor` | Toast |
| `Controllers\DeviceController.cs` | GetPrograms |
| `DbSecurity\IColumnEncryptionServiceWithSalt.cs` | plainText, Encrypt |
| `Extensions\ServiceCollectionExtensions.cs` | AddJwtAuthentication, services, jwtSettings |
| `MCP\DeviceMcpTools.cs` | GetPrograms |
| `Migrations\20260120091054_SessionStorage.cs` | migrationBuilder, Down, Up, migrationBuilder, SessionStorage |
| `Repositories\Interfaces\IExportRepository.cs` | DeleteAsync, id |
| `Repositories\Interfaces\ISpecificRepositories.cs` | UpdateAsync, entity, GetByUserIdAsync, GetCircuitsAsync, GetProgramsAsync, ... |
| `Services\BROADCAST\BroadcastUdpService.cs` | DisposeAsync |
| `Services\CircuitManager.cs` | Dispose |
| `Services\FileManagerService.cs` | error, UpdateFileAsync, FileHash, content, content, ... |
| `Services\Implementations\ProgramServices.cs` | programId, GetProgramAsync |
| `Services\Interfaces\IServices.cs` | file, UploadAsync, request |
| `Services\ServerSessionStorageService.cs` | storeId, RemoveComponentState |
| `Services\ToastService.cs` | title, Warning, durationMs, description, title, ... |

## Entry Points

- `Components\UI\Dashboard\TransferDialog.razor#code::__RazorCode.HandleTransfer`
- `Components\Pages\Home\DashboardView.razor#code::__RazorCode.DoAction`
- `Components\Pages\Settings\Users.razor#code::__RazorCode.SaveUserAsync`
- `Components\Pages\Programs\SchedulerPage.razor#code::__RazorCode.SaveSchedule`
- `DbSecurity\IColumnEncryptionServiceWithSalt.cs::ColumnEncryptionServiceWithSalt.Encrypt`

## Connected Communities

- **Components\UI\Program +15 dirs** (11 cross-edges)
- **Repositories\Implementations +9 dirs** (11 cross-edges)
- **DbSecurity +2 dirs** (6 cross-edges)
- **Components\UI\Toaster +1 dirs** (5 cross-edges)
- **Services\Implementations +15 dirs** (3 cross-edges)
- **Components\UI\DataViewer +5 dirs** (1 cross-edges)
- **Controllers +3 dirs** (1 cross-edges)
- **Components\UI\Accordion +8 dirs** (1 cross-edges)
- **Components\Layout +6 dirs** (1 cross-edges)
- **Services\Implementations +9 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-298"
smart_context with task: "understand Components\Pages\Programs +18 dirs", format: "gcx"
find_usages with id: "Components\UI\Dashboard\TransferDialog.razor#code::__RazorCode.HandleTransfer", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
