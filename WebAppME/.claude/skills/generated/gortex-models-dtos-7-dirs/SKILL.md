---
name: gortex-models-dtos-7-dirs
description: "Work in the Models\DTOs +7 dirs area — 81 symbols across 10 files (86% cohesion)"
---

# Models\DTOs +7 dirs

81 symbols | 10 files | 86% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Pages\Settings\ApplicationErrorLogs.razor`
- `Models\DTOs\AuditLogDto.cs`
- `Models\DTOs\ResponseDTOs.cs`
- `Models\Enums\AuditLogEnums.cs`
- `Models\ViewModels\AuditLogQueryParameters.cs`
- `Pages\Login.cshtml.cs`
- `Pages\Logout.cshtml.cs`
- `Repositories\Interfaces\ISpecificRepositories.cs`
- `Services\Implementations\AuditService.cs`
- `Services\Interfaces\IServices.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Pages\Settings\ApplicationErrorLogs.razor` | ApplyFiltersAsync, Logs, RefreshAsync, OnInitializedAsync, ClearFiltersAsync, ... |
| `Models\DTOs\AuditLogDto.cs` | Timestamp, Metadata, Details, IPAddress, UserAgent, ... |
| `Models\DTOs\ResponseDTOs.cs` | TotalRecords, Logs, PageSize, AuditResponce, PageNumber |
| `Models\Enums\AuditLogEnums.cs` | EXPORT, UPDATE, STOP, PAUSE, LOGIN, ... |
| `Models\ViewModels\AuditLogQueryParameters.cs` | FromDate, AuditLogQueryParameters, Module, User, Action, ... |
| `Pages\Login.cshtml.cs` | OnPostAsync |
| `Pages\Logout.cshtml.cs` | user, OnGet, id |
| `Repositories\Interfaces\ISpecificRepositories.cs` | request, GetAuditRecordsAsync |
| `Services\Implementations\AuditService.cs` | GetAuditLogsAsync, parameters, entity, ToDto |
| `Services\Interfaces\IServices.cs` | GetAuditLogsAsync, parameters |

## Entry Points

- `Pages\Login.cshtml.cs::LoginModel.OnPostAsync`

## Connected Communities

- **Components\UI\Toaster +1 dirs** (1 cross-edges)
- **Components\UI\Program +15 dirs** (1 cross-edges)
- **Components\Pages\Programs +18 dirs** (1 cross-edges)
- **Repositories\Implementations +9 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-282"
smart_context with task: "understand Models\DTOs +7 dirs", format: "gcx"
find_usages with id: "Pages\Login.cshtml.cs::LoginModel.OnPostAsync", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
