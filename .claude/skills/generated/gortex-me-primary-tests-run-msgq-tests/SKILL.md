---
name: gortex-me-primary-tests-run-msgq-tests
description: "Work in the me-primary/tests · run_msgq_tests area — 13 symbols across 1 files (97% cohesion)"
---

# me-primary/tests · run_msgq_tests

13 symbols | 1 files | 97% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_msgq.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_msgq.c` | test_oversized_payload_is_refused, fill, producer_main, run_msgq_tests, test_send_recv_round_trip, ... |

## Entry Points

- `ME Project\me-primary\tests\test_msgq.c::test_send_recv_round_trip`
- `ME Project\me-primary\tests\test_msgq.c::test_full_queue_drops_and_counts`

## How to Explore

```
analyze(operation:"communities", id:"community-25")
explore(operation:"context", task:"understand me-primary/tests · run_msgq_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_msgq.c::test_send_recv_round_trip"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
