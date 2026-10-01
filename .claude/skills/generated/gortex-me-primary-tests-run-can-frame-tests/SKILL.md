---
name: gortex-me-primary-tests-run-can-frame-tests
description: "Work in the me-primary/tests · run_can_frame_tests area — 40 symbols across 6 files (86% cohesion)"
---

# me-primary/tests · run_can_frame_tests

40 symbols | 6 files | 86% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\tests\test_ack_frame.c`
- `ME Project\me-primary\tests\test_can_frame.c`
- `ME Project\me-primary\tests\test_channel_list.c`
- `ME Project\me-primary\tests\test_log.c`
- `ME Project\me-primary\tests\test_main.c`
- `ME Project\me-primary\tests\test_program_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\tests\test_ack_frame.c` | test_ack_echoes_the_start_byte_of_the_group, test_ack_can_report_failure, test_ack_echoes_the_query_it_answers, run_ack_frame_tests, test_ack_matches_both_documented_examples, ... |
| `ME Project\me-primary\tests\test_can_frame.c` | test_channels_five_to_eight_are_block_two, test_pack_set_exact_bytes, test_can_id_composition, test_parse_feedback_round_trips_pack_feedback, test_set_and_read_use_different_functions_same_shape, ... |
| `ME Project\me-primary\tests\test_channel_list.c` | test_rejects_out_of_range_channel, run_channel_list_tests, test_parses_single_channel, test_parses_simple_list, test_rejects_too_many_entries, ... |
| `ME Project\me-primary\tests\test_log.c` | test_partial_line_stays_column_aligned, test_full_line_of_a_registration_frame, test_offset_is_rendered_in_hex, test_non_printable_bytes_become_dots, test_printable_ascii_is_preserved, ... |
| `ME Project\me-primary\tests\test_main.c` | main |
| `ME Project\me-primary\tests\test_program_frame.c` | test_packet_count_rejects_what_is_not_a_q3, test_only_q1_and_q3_are_handshake_queries, test_packet_count_is_big_endian, run_program_frame_tests, test_packet_count_parses_the_documented_example |

## Entry Points

- `ME Project\me-primary\tests\test_main.c::main`

## Connected Communities

- **me-primary/tests · run_frame_router_tests** (1 cross-edges)
- **me-primary/tests · put_wrapped** (1 cross-edges)
- **me-primary/tests · build_reference_program** (1 cross-edges)
- **me-primary/tests · run_circuit_registry_tests** (1 cross-edges)
- **me-primary/tests · run_control_frame_tests** (1 cross-edges)
- **me-primary/tests · run_msgq_tests** (1 cross-edges)
- **me-primary/tests · put_step** (1 cross-edges)
- **me-primary/tests · run_realtime_frame_tests** (1 cross-edges)
- **me-primary/tests · run_reg_frame_tests** (1 cross-edges)
- **me-primary/tests · run_rpmsg_frame_tests** (1 cross-edges)
- **me-primary/tests · run_battery_frame_tests** (1 cross-edges)
- **me-primary/tests · run_circuit_store_tests** (1 cross-edges)
- **me-primary/tests · run_crc16_tests** (1 cross-edges)

## How to Explore

```
analyze(operation:"communities", id:"community-27")
explore(operation:"context", task:"understand me-primary/tests · run_can_frame_tests", format:"gcx")
relations(operation:"usages", target:{symbol:"ME Project\me-primary\tests\test_main.c::main"}, format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
