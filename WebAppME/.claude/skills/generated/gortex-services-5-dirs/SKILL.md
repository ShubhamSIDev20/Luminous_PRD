---
name: gortex-services-5-dirs
description: "Work in the Services +5 dirs area — 84 symbols across 7 files (70% cohesion)"
---

# Services +5 dirs

84 symbols | 7 files | 70% cohesion

## When to Use

Use this skill when working on files in:
- `Components\Pages\Programs\ProgramEditor.razor`
- `Components\Pages\Settings\DeviceDiscovery.razor`
- `Models\Enums\CircuitEnums.cs`
- `Services\BROADCAST\BroadCastModels.cs`
- `Services\CircuitManager.cs`
- `Services\DecoderService.cs`
- `Services\Implementations\CircuitCommandHandler.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Components\Pages\Programs\ProgramEditor.razor` | stepId, value, UpdateRegistration, index |
| `Components\Pages\Settings\DeviceDiscovery.razor` | raw, _srv, OnRawPacket, DisposeAsync, SelectDevice, ... |
| `Models\Enums\CircuitEnums.cs` | Success, Failed, CommandStatus, AlreadyRegistered |
| `Services\BROADCAST\BroadCastModels.cs` | Gateway, BroadcastIpConfig, UdpLivePort, DeviceIp, UniqueId, ... |
| `Services\CircuitManager.cs` | HandleCommandClientAsync, client, token, SendOnly, client, ... |
| `Services\DecoderService.cs` | IpToBytes, BuildBatteryBytes, epochSeconds, EpochSecondsToDateTime, T, ... |
| `Services\Implementations\CircuitCommandHandler.cs` | UnregisterAsync |

## Connected Communities

- **Repositories\Implementations +9 dirs** (7 cross-edges)
- **Components\Pages\Programs +18 dirs** (6 cross-edges)
- **Components\UI\Program +15 dirs** (3 cross-edges)
- **Services +2 dirs** (3 cross-edges)
- **DbSecurity +2 dirs** (1 cross-edges)
- **Models\DTOs +6 dirs** (1 cross-edges)
- **Components\Pages\Settings · DiscoveredDeviceVm** (1 cross-edges)
- **Services +1 dirs · BroadcastDeviceInfo** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-279"
smart_context with task: "understand Services +5 dirs", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
