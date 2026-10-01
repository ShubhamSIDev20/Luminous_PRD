---
name: gortex-models-dtos-6-dirs
description: "Work in the Models\DTOs +6 dirs area — 82 symbols across 10 files (81% cohesion)"
---

# Models\DTOs +6 dirs

82 symbols | 10 files | 81% cohesion

## When to Use

Use this skill when working on files in:
- `Components\UI\Calibration\Calibration.razor`
- `Models\DTOs\CalibrationData.cs`
- `Models\DTOs\CalibrationDataPointDto.cs`
- `Models\DTOs\CalibrationDto.cs`
- `Models\Entities\CalibrationDataPoint.cs`
- `Models\Enums\CircuitEnums.cs`
- `Repositories\Implementations\DeviceCircuitRepository.cs`
- `Services\DecoderService.cs`
- `Services\Implementations\CircuitCommandHandler.cs`
- `Services\PacketAnalyzerNoReverse.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\UI\Calibration\Calibration.razor` | GetCurrentRangeBounds, GetRangeButtonsOrdered |
| `Models\DTOs\CalibrationData.cs` | GetCurrentCharge, Temperature, range, CurrentDischarge, GetCurrentDischarge, ... |
| `Models\DTOs\CalibrationDataPointDto.cs` | Offset, DeviceId, Mode, Type, DateTime, ... |
| `Models\DTOs\CalibrationDto.cs` | IsVerifying, IsTemperature, UpsertRangePoint, SystemHighInput, ActualHighInput, ... |
| `Models\Entities\CalibrationDataPoint.cs` | Id, CircuitId, DateTime, Type, Range, ... |
| `Models\Enums\CircuitEnums.cs` | CalibrationType, Range1, CalibrationMode, Full_Range, Discharge, ... |
| `Repositories\Implementations\DeviceCircuitRepository.cs` | GetAllCalibrationDataAsync, circuit |
| `Services\DecoderService.cs` | data, ParseCalibrationPayload |
| `Services\Implementations\CircuitCommandHandler.cs` | PreviousCalibration |
| `Services\PacketAnalyzerNoReverse.cs` | ReadFloat, offset, data |

## Connected Communities

- **Components\UI\Program +15 dirs** (5 cross-edges)
- **Repositories\Implementations +9 dirs** (2 cross-edges)
- **Services +2 dirs** (2 cross-edges)
- **Services\Implementations +6 dirs** (1 cross-edges)
- **Components\Pages\Programs +18 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-293"
smart_context with task: "understand Models\DTOs +6 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
