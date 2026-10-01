---
name: gortex-me-primary-tests-run-control-frame-tests
description: "Work in the me-primary/tests · run_control_frame_tests area — 13 symbols across 1 files (97% cohesion)"
---

# me-primary/tests · run_control_frame_tests

13 symbols | 1 files | 97% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_control_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_control_frame.c` | build, test_bad_crc, test_unknown_query_id, test_only_start_carries_a_session_id, test_bad_start_byte, ... |

## Entry Points

- `ME Project\me-primary\tests\test_control_frame.c::test_command_names`
- `ME Project\me-primary\tests\test_control_frame.c::test_only_start_carries_a_session_id`

## How to Explore

```
analyze(operation:"communities", id:"community-22")
explore(operation:"context", task:"understand me-primary/tests · run_control_frame_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_control_frame.c::test_command_names"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
