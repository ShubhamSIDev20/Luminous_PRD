---
name: gortex-components-ui-calibration-3-dirs
description: "Work in the Components\UI\Calibration +3 dirs area — 79 symbols across 5 files (87% cohesion)"
---

# Components\UI\Calibration +3 dirs

79 symbols | 5 files | 87% cohesion

## When to Use

Use this skill when working on files in:
- `Components\UI\Calibration\Calibration.razor`
- `Components\UI\Calibration\CalibrationModel.cs`
- `Models\Enums\CircuitEnums.cs`
- `Services\Implementations\CircuitCommandHandler.cs`
- `Services\Interfaces\ICircuitCommandHandler.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\UI\Calibration\Calibration.razor` | HandleTemperatureCalibrationStep, OnHardwareManagerChanged, CalculateTemperatureCalibrationValues, GetMessageClass, Dispose, ... |
| `Components\UI\Calibration\CalibrationModel.cs` | Voltage, User, Success, Current, CalibrationTypeMode, ... |
| `Models\Enums\CircuitEnums.cs` | VoltageDisChargeGainOffset, PreviousCalibration, VoltageChargeLowPoint, StartChargeVerifyCurrentcalibration, CurrentChargeLowPoint, ... |
| `Services\Implementations\CircuitCommandHandler.cs` | range, StopverifyCalibration, offset, queryId, SetGainOffset, ... |
| `Services\Interfaces\ICircuitCommandHandler.cs` | HWReadyToCalibrationAsync, PreviousCalibration, queryId, value, range, ... |

## Entry Points

- `Components\UI\Calibration\Calibration.razor#code::__RazorCode.HandleVerify`
- `Components\UI\Calibration\Calibration.razor#code::__RazorCode.HandleCurrentCalibrationStep`

## Connected Communities

- **Models\DTOs +6 dirs** (3 cross-edges)
- **Services\Implementations +6 dirs** (3 cross-edges)
- **Services +5 dirs** (2 cross-edges)
- **Components\UI\Calibration · CalibrationError** (1 cross-edges)
- **Components\UI\Accordion +8 dirs** (1 cross-edges)
- **Components\UI\Program +15 dirs** (1 cross-edges)
- **Components\Layout +6 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-46"
smart_context with task: "understand Components\UI\Calibration +3 dirs", format: "gcx"
find_usages with id: "Components\UI\Calibration\Calibration.razor#code::__RazorCode.HandleVerify", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
