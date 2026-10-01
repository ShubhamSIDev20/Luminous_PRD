---
name: gortex-components-ui-dataviewer-13-dirs
description: "Work in the Components\UI\DataViewer +13 dirs area — 172 symbols across 26 files (76% cohesion)"
---

# Components\UI\DataViewer +13 dirs

172 symbols | 26 files | 76% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Pages\Programs\ProgramEditor.razor`
- `Components\Pages\Programs\ProgramList.razor`
- `Components\Pages\Programs\SchedulerPage.razor`
- `Components\Pages\Reports\Reports.razor`
- `Components\UI\Dashboard\HighStockChartRegister.razor`
- `Components\UI\Dashboard\TransferDialog.razor`
- `Components\UI\DataViewer\BmsChart.razor`
- `Components\UI\DataViewer\BmsDashboard.razor`
- `Components\UI\DataViewer\BmsDataTable.razor`
- `Components\UI\DataViewer\BmsProgramTable.razor`
- `Components\UI\DataViewer\Models.cs`
- `Components\UI\Program\NominalValuesEditor.razor`
- `Components\UI\Program\StepRow.razor`
- `Data\SqliteDbContext.cs`
- `Migrations\20251223060513_TableOP.Designer.cs`
- `Models\DTOs\ProgramDTO.cs`
- `Models\DTOs\RequestDTOs.cs`
- `Models\SqliteEntities\MeasurementData.cs`
- `Repositories\Implementations\ProgramRepository.cs`
- `Repositories\Implementations\Repository.cs`
- `Repositories\Interfaces\IRepository.cs`
- `Services\Implementations\ExportJobService.cs`
- `Services\Implementations\ServiceLocator.cs`
- `Services\Implementations\SqliteBulkDatabaseManager.cs`
- `Services\Interfaces\ISqliteBulkDatabaseManager.cs`
- `Utils\UseChartAttribute.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Pages\Programs\ProgramEditor.razor` | allPrograms |
| `Components\Pages\Programs\ProgramList.razor` | ViewProgram, programs |
| `Components\Pages\Programs\SchedulerPage.razor` | availablePrograms |
| `Components\Pages\Reports\Reports.razor` | OpenSessionPanel, session, _sessionProducerPrograms |
| `Components\UI\Dashboard\HighStockChartRegister.razor` | _filteredData, Data |
| `Components\UI\Dashboard\TransferDialog.razor` | programs |
| `Components\UI\DataViewer\BmsChart.razor` | Data, SampledData, ChartFields, _offlineData |
| `Components\UI\DataViewer\BmsDashboard.razor` | Pg, _liveBuffer, _sessionProducerPrograms |
| `Components\UI\DataViewer\BmsDataTable.razor` | DisplayRows, LiveRows |
| `Components\UI\DataViewer\BmsProgramTable.razor` | Program, ProducerPrograms |
| `Components\UI\DataViewer\Models.cs` | GetLabelUnit, name, ChartField, GetChartFields |
| `Components\UI\Program\NominalValuesEditor.razor` | AllPrograms |
| `Components\UI\Program\StepRow.razor` | AllPrograms |
| `Data\SqliteDbContext.cs` | Program, Battery, OnConfiguring, SqliteDbContext, Measurements, ... |
| `Migrations\20251223060513_TableOP.Designer.cs` | DbContext |
| `Models\DTOs\ProgramDTO.cs` | ProgramSteps, CreatedAt, UpdatedAt, IsDeleted, ProgramId, ... |
| `Models\DTOs\RequestDTOs.cs` | filePath, ExpandedProgramSteps, Battery, dbcDatabase, Program, ... |
| `Models\SqliteEntities\MeasurementData.cs` | CircuitStatus, ChargeCapacity, MeasurementData, ErrorId, Current, ... |
| `Repositories\Implementations\ProgramRepository.cs` | GetProgramsAsync |
| `Repositories\Implementations\Repository.cs` | CountAsync, predicate |
| `Repositories\Interfaces\IRepository.cs` | predicate, CountAsync |
| `Services\Implementations\ExportJobService.cs` | dbMgr |
| `Services\Implementations\ServiceLocator.cs` | Dispose |
| `Services\Implementations\SqliteBulkDatabaseManager.cs` | ctx, names, FetchDbcDatabaseAsync, FetchBatteryAsync, ctx, ... |
| `Services\Interfaces\ISqliteBulkDatabaseManager.cs` | CountSessionRowsAsync, GetDistinctRegLabelsAsync, filePath, ct, filePath, ... |
| `Utils\UseChartAttribute.cs` | UseChartAttribute |

## Entry Points

- `Services\Implementations\SqliteBulkDatabaseManager.cs::SqliteBulkDatabaseManager.ReadSessionPageAsync`

## Connected Communities

- **Components\Pages\Programs +18 dirs** (11 cross-edges)
- **Data** (1 cross-edges)
- **Components\UI\Program +15 dirs** (1 cross-edges)
- **Components\UI\DataViewer +4 dirs · RegLogRecord** (1 cross-edges)
- **Repositories\Implementations +9 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-289"
smart_context with task: "understand Components\UI\DataViewer +13 dirs", format: "gcx"
find_usages with id: "Services\Implementations\SqliteBulkDatabaseManager.cs::SqliteBulkDatabaseManager.ReadSessionPageAsync", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
