---
name: gortex-repositories-implementations-2-dirs
description: "Work in the Repositories\Implementations +2 dirs area — 76 symbols across 10 files (76% cohesion)"
---

# Repositories\Implementations +2 dirs

76 symbols | 10 files | 76% cohesion

## When to Use

Use this skill when working on files in:
- `Data\AppDbContext.cs`
- `Repositories\Implementations\AuditRepository.cs`
- `Repositories\Implementations\BatteryRepository.cs`
- `Repositories\Implementations\CodeMessageRepository.cs`
- `Repositories\Implementations\DbcRepository.cs`
- `Repositories\Implementations\DeviceCircuitRepository.cs`
- `Repositories\Implementations\ProgramRepository.cs`
- `Repositories\Implementations\Repository.cs`
- `Repositories\Interfaces\IRepository.cs`
- `Repositories\Interfaces\ISpecificRepositories.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Data\AppDbContext.cs` | dbcFileRecords, ProgramSchedules, CodeMessages, BatteryTypes, options, ... |
| `Repositories\Implementations\AuditRepository.cs` | _AppDbcontext, AuditRepository.<init>, context, AuditRepository |
| `Repositories\Implementations\BatteryRepository.cs` | BatteryRepository.<init>, dto, _audit, audit, entity, ... |
| `Repositories\Implementations\CodeMessageRepository.cs` | audit, CodeMessageRepository.<init>, context |
| `Repositories\Implementations\DbcRepository.cs` | audit, DbcRepository.<init>, context |
| `Repositories\Implementations\DeviceCircuitRepository.cs` | DeviceCircuitRepository.<init>, context, audit |
| `Repositories\Implementations\ProgramRepository.cs` | context, ProgramRepository.<init> |
| `Repositories\Implementations\Repository.cs` | AddAsync, Repository.<init>, T, GetAllAsync, UpdateAsync, ... |
| `Repositories\Interfaces\IRepository.cs` | id, UpdateAsync, entities, AddAsync, GetAllAsync, ... |
| `Repositories\Interfaces\ISpecificRepositories.cs` | IAuditRepository |

## Connected Communities

- **Repositories\Implementations +9 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-261"
smart_context with task: "understand Repositories\Implementations +2 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
