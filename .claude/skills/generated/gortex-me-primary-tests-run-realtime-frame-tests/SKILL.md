---
name: gortex-me-primary-tests-run-realtime-frame-tests
description: "Work in the me-primary/tests · run_realtime_frame_tests area — 10 symbols across 1 files (96% cohesion)"
---

# me-primary/tests · run_realtime_frame_tests

10 symbols | 1 files | 96% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_realtime_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_realtime_frame.c` | test_every_payload_offset, run_realtime_frame_tests, test_crc_is_big_endian_over_the_body, test_post_registration_frame_matches_the_developer_bytes, test_post_registration_frame_follows_the_circuit, ... |

## Entry Points

- `ME Project\me-primary\tests\test_realtime_frame.c::test_every_payload_offset`
- `ME Project\me-primary\tests\test_realtime_frame.c::test_post_registration_frame_matches_the_developer_bytes`

## How to Explore

```
analyze(operation:"communities", id:"community-28")
explore(operation:"context", task:"understand me-primary/tests · run_realtime_frame_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_realtime_frame.c::test_every_payload_offset"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
