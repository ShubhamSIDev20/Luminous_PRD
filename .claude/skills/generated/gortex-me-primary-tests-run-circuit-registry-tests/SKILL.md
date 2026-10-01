---
name: gortex-me-primary-tests-run-circuit-registry-tests
description: "Work in the me-primary/tests · run_circuit_registry_tests area — 13 symbols across 1 files (96% cohesion)"
---

# me-primary/tests · run_circuit_registry_tests

13 symbols | 1 files | 96% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_circuit_registry.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_circuit_registry.c` | test_row_boundary_circuits_are_distinct, test_multiple_circuits_can_be_registered, test_init_clears_every_slot, run_circuit_registry_tests, test_nibble_swap_is_not_aliased, ... |

## How to Explore

```
analyze(operation:"communities", id:"community-20")
explore(operation:"context", task:"understand me-primary/tests · run_circuit_registry_tests", format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
