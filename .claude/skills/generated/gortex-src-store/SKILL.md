---
name: gortex-src-store
description: "Work in the src/store area — 12 symbols across 2 files (88% cohesion)"
---

# src/store

12 symbols | 2 files | 88% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\src\store\circuit_registry.c`
- `ME Project\me-primary\src\store\circuit_store.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\src\store\circuit_registry.c` | me_registry_mark_registered, me_registry_is_registered |
| `ME Project\me-primary\src\store\circuit_store.c` | me_store_program_ptr, me_store_program_len, me_store_config_get, me_store_battery_set, me_store_battery_get, ... |

## Connected Communities

- **me-primary/src · me_log** (2 cross-edges)

## How to Explore

```
analyze(operation:"communities", id:"community-15")
explore(operation:"context", task:"understand src/store", format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
