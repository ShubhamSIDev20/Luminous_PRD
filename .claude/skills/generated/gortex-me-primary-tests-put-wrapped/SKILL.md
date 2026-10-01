---
name: gortex-me-primary-tests-put-wrapped
description: "Work in the me-primary/tests · put_wrapped area — 21 symbols across 1 files (99% cohesion)"
---

# me-primary/tests · put_wrapped

21 symbols | 1 files | 99% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_step_decode.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_step_decode.c` | test_ccchg_accepts_eq_comparator_as_inclusive, put_wrapped, test_unknown_operator_is_rejected, test_ccchg_rejects_neq_on_time, test_ccchg_rejects_non_time_cutoff, ... |

## Entry Points

- `ME Project\me-primary\tests\test_step_decode.c::run_step_decode_tests`

## How to Explore

```
analyze(operation:"communities", id:"community-31")
explore(operation:"context", task:"understand me-primary/tests · put_wrapped", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_step_decode.c::run_step_decode_tests"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
