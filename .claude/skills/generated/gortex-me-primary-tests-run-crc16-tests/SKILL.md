---
name: gortex-me-primary-tests-run-crc16-tests
description: "Work in the me-primary/tests · run_crc16_tests area — 14 symbols across 1 files (96% cohesion)"
---

# me-primary/tests · run_crc16_tests

14 symbols | 1 files | 96% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_crc16.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_crc16.c` | test_default_order_is_big_endian, test_verify_rejects_corrupted_payload, test_verify_rejects_wrong_byte_order, test_append_in_default_order_writes_high_byte_first, test_append_little_endian_writes_low_byte_first, ... |

## How to Explore

```
analyze(operation:"communities", id:"community-23")
explore(operation:"context", task:"understand me-primary/tests · run_crc16_tests", format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
