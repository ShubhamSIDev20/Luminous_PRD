---
name: gortex-models-entities-1-dirs
description: "Work in the Models\Entities +1 dirs area — 99 symbols across 10 files (95% cohesion)"
---

# Models\Entities +1 dirs

99 symbols | 10 files | 95% cohesion

## When to Use

Use this skill when working on files in:
- `Models\Entities\BaseEntity.cs`
- `Models\Entities\Batteries.cs`
- `Models\Entities\BatterySession.cs`
- `Models\Entities\BatteryType.cs`
- `Models\Entities\CodeMessage.cs`
- `Models\Entities\Device.cs`
- `Models\Entities\DeviceSetting.cs`
- `Models\Entities\Program\BtsPrograms.cs`
- `Models\Entities\Program\RegistrationStandard.cs`
- `Models\Entities\TableOperatorFile.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Models\Entities\BaseEntity.cs` | CreatedAt, CreatedBy, UpdatedBy, IsDeleted, UpdatedAt, ... |
| `Models\Entities\Batteries.cs` | Batteries, Producer, Quantity, BreakVoltage, NominalCapacity, ... |
| `Models\Entities\BatterySession.cs` | ProgramHash, BatteryID, Port2DbcFileRecordID, EndTime, SessionFilePath, ... |
| `Models\Entities\BatteryType.cs` | Id, Description, Name, BatteryType |
| `Models\Entities\CodeMessage.cs` | Index, Id, Message, Type, CodeMessage |
| `Models\Entities\Device.cs` | ManufactureDateTime, DeviceName, ClientRemoteIPAddress, ComSwVersion, IPAddress, ... |
| `Models\Entities\DeviceSetting.cs` | SettingName, DeviceSetting, CircuitID, Id, SettingJson, ... |
| `Models\Entities\Program\BtsPrograms.cs` | ProgramJson, ProgramSteps, MaxAh, BtsPrograms, Id, ... |
| `Models\Entities\Program\RegistrationStandard.cs` | UnitList, RegistrationStandard, Description, Id, StandardName |
| `Models\Entities\TableOperatorFile.cs` | Size, FileHash, Description, Name, FilePath, ... |

## How to Explore

```
get_communities with id: "community-243"
smart_context with task: "understand Models\Entities +1 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
