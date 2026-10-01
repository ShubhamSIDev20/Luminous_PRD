---
name: gortex-me-primary-tests-run-reg-frame-tests
description: "Work in the me-primary/tests · run_reg_frame_tests area — 31 symbols across 1 files (99% cohesion)"
---

# me-primary/tests · run_reg_frame_tests

31 symbols | 1 files | 99% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_reg_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_reg_frame.c` | test_pack_writes_ip_in_dotted_quad_order, test_parse_accepts_success_response, test_pack_writes_mac_in_wire_order, build_response, test_parse_rejects_non_success_values, ... |

## Entry Points

- `ME Project\me-primary\tests\test_reg_frame.c::run_reg_frame_tests`

## How to Explore

```
analyze(operation:"communities", id:"community-29")
explore(operation:"context", task:"understand me-primary/tests · run_reg_frame_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_reg_frame.c::run_reg_frame_tests"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
