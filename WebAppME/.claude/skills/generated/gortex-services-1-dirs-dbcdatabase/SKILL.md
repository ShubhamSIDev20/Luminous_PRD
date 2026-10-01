---
name: gortex-services-1-dirs-dbcdatabase
description: "Work in the Services +1 dirs · DbcDatabase area — 89 symbols across 2 files (91% cohesion)"
---

# Services +1 dirs · DbcDatabase

89 symbols | 2 files | 91% cohesion

## When to Use

Use this skill when working on files in:
- `Services\DbcParser.cs`
- `Services\Implementations\CircuitCommandHandler.cs`

## Key Files

| File | Symbols |
|------|---------|
| `Services\DbcParser.cs` | ByteOrder, ValueType, WritePort, PortData, Comment, ... |
| `Services\Implementations\CircuitCommandHandler.cs` | p2, p1, p3, MergeDbcDatabases |

## Connected Communities

- **Components\UI\Program +15 dirs** (1 cross-edges)

## How to Explore

```
get_communities with id: "community-277"
smart_context with task: "understand Services +1 dirs · DbcDatabase", format: "gcx"
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
