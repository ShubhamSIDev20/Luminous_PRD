---
name: gortex-me-primary-tests-run-circuit-store-tests
description: "Work in the me-primary/tests · run_circuit_store_tests area — 11 symbols across 1 files (97% cohesion)"
---

# me-primary/tests · run_circuit_store_tests

11 symbols | 1 files | 97% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_circuit_store.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_circuit_store.c` | test_append_after_completion_resets, test_slot_round_trip, test_battery_round_trip, test_program_append_concatenates, run_circuit_store_tests, ... |

## Entry Points

- `ME Project\me-primary\tests\test_circuit_store.c::test_malformed_circuit_ids_are_rejected`
- `ME Project\me-primary\tests\test_circuit_store.c::test_program_append_concatenates`

## How to Explore

```
analyze(operation:"communities", id:"community-21")
explore(operation:"context", task:"understand me-primary/tests · run_circuit_store_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_circuit_store.c::test_malformed_circuit_ids_are_rejected"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
