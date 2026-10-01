---
name: gortex-me-primary-tests-build-reference-program
description: "Work in the me-primary/tests · build_reference_program area — 10 symbols across 1 files (97% cohesion)"
---

# me-primary/tests · build_reference_program

10 symbols | 1 files | 97% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_step_engine.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_step_engine.c` | put_step, test_time_does_not_accrue_while_a_response_is_outstanding, test_a_response_clears_the_missed_counter, test_cutoff_fires_at_exactly_the_configured_ms, test_poll_and_realtime_coincide_every_tenth_poll, ... |

## How to Explore

```
analyze(operation:"communities", id:"community-32")
explore(operation:"context", task:"understand me-primary/tests · build_reference_program", format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
