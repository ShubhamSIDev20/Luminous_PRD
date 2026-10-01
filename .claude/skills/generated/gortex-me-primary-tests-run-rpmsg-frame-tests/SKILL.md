---
name: gortex-me-primary-tests-run-rpmsg-frame-tests
description: "Work in the me-primary/tests · run_rpmsg_frame_tests area — 17 symbols across 1 files (98% cohesion)"
---

# me-primary/tests · run_rpmsg_frame_tests

17 symbols | 1 files | 98% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_rpmsg_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_rpmsg_frame.c` | test_stream_split_across_reads, test_stream_single_frame, test_stream_resyncs_on_garbage, test_stream_two_frames_one_push, test_pack_can_golden, ... |

## Entry Points

- `ME Project\me-primary\tests\test_rpmsg_frame.c::test_dlc_table`
- `ME Project\me-primary\tests\test_rpmsg_frame.c::test_pack_can_golden`
- `ME Project\me-primary\tests\test_rpmsg_frame.c::test_can_round_trip`
- `ME Project\me-primary\tests\test_rpmsg_frame.c::test_stream_split_across_reads`
- `ME Project\me-primary\tests\test_rpmsg_frame.c::test_stream_ack_then_frame`

## How to Explore

```
analyze(operation:"communities", id:"community-30")
explore(operation:"context", task:"understand me-primary/tests · run_rpmsg_frame_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_rpmsg_frame.c::test_dlc_table"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
