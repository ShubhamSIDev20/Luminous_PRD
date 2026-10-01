---
name: gortex-me-primary-tests-put-step
description: "Work in the me-primary/tests · put_step area — 14 symbols across 1 files (98% cohesion)"
---

# me-primary/tests · put_step

14 symbols | 1 files | 98% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_program_chain.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_program_chain.c` | build_chain, test_next_index_past_buffer_is_rejected, run_program_chain_tests, test_fetch_each_step, test_result_names, ... |

## Entry Points

- `ME Project\me-primary\tests\test_program_chain.c::test_fetch_each_step`

## How to Explore

```
analyze(operation:"communities", id:"community-26")
explore(operation:"context", task:"understand me-primary/tests · put_step", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_program_chain.c::test_fetch_each_step"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
