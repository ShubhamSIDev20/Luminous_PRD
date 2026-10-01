---
name: gortex-me-primary-tests-run-frame-router-tests
description: "Work in the me-primary/tests · run_frame_router_tests area — 25 symbols across 1 files (98% cohesion)"
---

# me-primary/tests · run_frame_router_tests

25 symbols | 1 files | 98% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_frame_router.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_frame_router.c` | test_program_queries_classify_as_unroutable, test_resolve_len_recovers_a_wrong_layout_length, test_short_frame_is_rejected, test_expected_length, test_resolve_len_does_not_truncate_when_the_layout_overshoots, ... |

## Entry Points

- `ME Project\me-primary\tests\test_frame_router.c::test_expected_length_of_the_config_queries`
- `ME Project\me-primary\tests\test_frame_router.c::run_frame_router_tests`
- `ME Project\me-primary\tests\test_frame_router.c::test_control_start_is_ten_bytes`
- `ME Project\me-primary\tests\test_frame_router.c::test_expected_length_of_the_answered_program_queries`

## How to Explore

```
analyze(operation:"communities", id:"community-24")
explore(operation:"context", task:"understand me-primary/tests · run_frame_router_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_frame_router.c::test_expected_length_of_the_config_queries"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
