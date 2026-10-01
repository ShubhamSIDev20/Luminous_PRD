---
name: gortex-components-ui-program-15-dirs
description: "Work in the Components\UI\Program +15 dirs area — 505 symbols across 33 files (90% cohesion)"
---

# Components\UI\Program +15 dirs

505 symbols | 33 files | 90% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Layout\MainLayout.razor`
- `Components\Pages\Home\DashboardView.razor`
- `Components\Pages\Programs\ProgramEditor.razor`
- `Components\Pages\Programs\Registrations.razor`
- `Components\Pages\Programs\SchedulerPage.razor`
- `Components\Pages\Settings\Users.razor`
- `Components\Pages\Test\DialogExample.razor`
- `Components\UI\Accordion\Accordion.razor`
- `Components\UI\Dashboard\HighStockChartRegister.razor`
- `Components\UI\DataViewer\BmsDashboard.razor`
- `Components\UI\DataViewer\BmsDummyData.cs`
- `Components\UI\DataViewer\BmsProgramTable.razor`
- `Components\UI\Datatable\DataTable.razor`
- `Components\UI\Program\LimitActionPairEditor.razor`
- `Components\UI\Program\NominalValuesEditor.razor`
- `Components\UI\Program\OperatorBadge.razor`
- `Components\UI\Program\OperatorConstants.cs`
- `Components\UI\Program\ProgramViewer.razor`
- `Components\UI\Program\RegistrationsEditor.razor`
- `Components\UI\Program\StepModel.cs`
- `Components\UI\Program\StepRow.razor`
- `Components\UI\Program\ValidationHelper.cs`
- `Middleware\RequestLoggingMiddleware.cs`
- `Models\DTOs\ResponseDTOs.cs`
- `Models\Enums\ProgramEnums.cs`
- `Services\CircuitManager.cs`
- `Services\DbcParser.cs`
- `Services\DecoderService.cs`
- `Services\Implementations\SqliteBulkDatabaseManager.cs`
- `Services\Interfaces\ICircuitCommandHandler.cs`
- `Services\PacketAnalyzer.cs`
- `Services\PacketAnalyzerNoReverse.cs`
- `Services\ProgramBuilder.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Layout\MainLayout.razor` | HandleDeviceChange, SubscribeToDevices, OnInitializedAsync |
| `Components\Pages\Home\DashboardView.razor` | tuple, UpdateSelectedCircuits |
| `Components\Pages\Programs\ProgramEditor.razor` | step, limit, afterStepId, stepId, stepId, ... |
| `Components\Pages\Programs\Registrations.razor` | MoveToRight, MoveToLeft |
| `Components\Pages\Programs\SchedulerPage.razor` | key, ToggleCircuitSelection |
| `Components\Pages\Settings\Users.razor` | key, ToggleCircuitAccess, isChecked |
| `Components\Pages\Test\DialogExample.razor` | Label, Comment, Limits, NominalValues, StepNumber, ... |
| `Components\UI\Accordion\Accordion.razor` | OnInitialized |
| `Components\UI\Dashboard\HighStockChartRegister.razor` | HandleLiveDataAsync, realTime |
| `Components\UI\DataViewer\BmsDashboard.razor` | Steps, _expandedSteps |
| `Components\UI\DataViewer\BmsDummyData.cs` | GenerateSteps |
| `Components\UI\DataViewer\BmsProgramTable.razor` | Steps, _producerDialogSteps |
| `Components\UI\Datatable\DataTable.razor` | SeedGroupExpand |
| `Components\UI\Program\LimitActionPairEditor.razor` | index, inputHide, index, index, GlobalVariables, ... |
| `Components\UI\Program\NominalValuesEditor.razor` | GlobalVariables, UpdateFieldValue, value, index, Config |
| `Components\UI\Program\OperatorBadge.razor` | GetOperatorName, GetColorClass |
| `Components\UI\Program\OperatorConstants.cs` | NominalConfig, CC_CHG, _standards, OperatorPattern, ERR, ... |
| `Components\UI\Program\ProgramViewer.razor` | InitialSteps, ValidateStep, HandleFileUpload, e, OnInitialized, ... |
| `Components\UI\Program\RegistrationsEditor.razor` | OnInitialized, HandleAdd, AutoCorrectInput, GlobalVariables, ConfirmEdit, ... |
| `Components\UI\Program\StepModel.cs` | Limits, NominalValues, Comment, Name, OperatorCode, ... |
| `Components\UI\Program\StepRow.razor` | GetInlineErrors, HasErrors, IsLimitActionAllowed, Step, Errors, ... |
| `Components\UI\Program\ValidationHelper.cs` | ValidateAction, ValidateRegistration, AutoCorrectUnit, operatorCode, operatorCode, ... |
| `Middleware\RequestLoggingMiddleware.cs` | InvokeAsync, context |
| `Models\DTOs\ResponseDTOs.cs` | IOStatus, Type, Value, Id, Board |
| `Models\Enums\ProgramEnums.cs` | DischargeCapacity, RegistrationType, StepEnergy, Time, Current, ... |
| `Services\CircuitManager.cs` | circuit, tcpClient, CreateNewHandler, Add |
| `Services\DbcParser.cs` | line, db, comments, index, line, ... |
| `Services\DecoderService.cs` | ConvertProgramIntoBytesPackets, programStepsDTO, ConvertProgramIntoBytesPackets, resolvedPrograms, programStepsDTO, ... |
| `Services\Implementations\SqliteBulkDatabaseManager.cs` | session, InsertSessionAsync |
| `Services\Interfaces\ICircuitCommandHandler.cs` | InitializeAsync |
| `Services\PacketAnalyzer.cs` | Operator, PacketAnalysis, GetActionName, originalSteps, analysis, ... |
| `Services\PacketAnalyzerNoReverse.cs` | GenerateAnalysisReport, originalStep, GetRegistrationTypeName, opByte, GetActionName, ... |
| `Services\ProgramBuilder.cs` | s, stepBytes, stepBytes, ProcessGotoOperator, ms, ... |

## Entry Points

- `Services\DecoderService.cs::DecoderService.ConvertProgramBackup`
- `Components\UI\Program\ProgramViewer.razor#code::__RazorCode.HandleFileUpload`
- `Services\PacketAnalyzer.cs::PacketAnalyzer.GenerateAnalysisReport`
- `Components\Pages\Programs\ProgramEditor.razor#code::__RazorCode.DownloadHexPackets`
- `Components\Pages\Home\DashboardView.razor#code::__RazorCode.UpdateSelectedCircuits`

## Connected Communities

- **Components\Pages\Programs +18 dirs** (9 cross-edges)
- **Repositories\Implementations +9 dirs** (9 cross-edges)
- **Components\UI\Accordion +8 dirs** (8 cross-edges)
- **Models\DTOs +6 dirs** (4 cross-edges)
- **Services +1 dirs · CircuitManager** (2 cross-edges)
- **Models\Enums +1 dirs** (2 cross-edges)
- **Components\UI\Datatable +1 dirs** (1 cross-edges)
- **Components\UI\DataViewer +13 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-272"
smart_context with task: "understand Components\UI\Program +15 dirs", format: "gcx"
find_usages with id: "Services\DecoderService.cs::DecoderService.ConvertProgramBackup", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
