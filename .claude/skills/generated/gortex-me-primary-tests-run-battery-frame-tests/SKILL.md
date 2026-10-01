---
name: gortex-me-primary-tests-run-battery-frame-tests
description: "Work in the me-primary/tests · run_battery_frame_tests area — 10 symbols across 1 files (97% cohesion)"
---

# me-primary/tests · run_battery_frame_tests

10 symbols | 1 files | 97% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_battery_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_battery_frame.c` | test_short_payload_is_refused, test_longer_payload_is_accepted, test_every_field_offset, test_fields_are_independent, run_battery_frame_tests, ... |

## Entry Points

- `ME Project\me-primary\tests\test_battery_frame.c::test_captured_web_app_frame`
- `ME Project\me-primary\tests\test_battery_frame.c::test_payload_length_is_forty`
- `ME Project\me-primary\tests\test_battery_frame.c::test_every_field_offset`

## How to Explore

```
analyze(operation:"communities", id:"community-19")
explore(operation:"context", task:"understand me-primary/tests · run_battery_frame_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_battery_frame.c::test_captured_web_app_frame"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
