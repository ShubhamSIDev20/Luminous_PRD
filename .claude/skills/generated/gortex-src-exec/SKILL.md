---
name: gortex-src-exec
description: "Work in the src/exec area — 12 symbols across 1 files (86% cohesion)"
---

# src/exec

12 symbols | 1 files | 86% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\src\exec\step_engine.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\src\exec\step_engine.c` | send_poll, me_exec_tick, send_setpoint, enter_step, dequeue, ... |

## Entry Points

- `ME Project\me-primary\src\exec\step_engine.c::me_exec_tick`

## Connected Communities

- **me-primary/src · me_log** (5 cross-edges)

## How to Explore

```
analyze(operation:"communities", id:"community-3")
explore(operation:"context", task:"understand src/exec", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\src\exec\step_engine.c::me_exec_tick"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
