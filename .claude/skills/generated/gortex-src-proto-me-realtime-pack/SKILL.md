---
name: gortex-src-proto-me-realtime-pack
description: "Work in the src/proto · me_realtime_pack area — 18 symbols across 6 files (100% cohesion)"
---

# src/proto · me_realtime_pack

18 symbols | 6 files | 100% cohesion

## When to Use

Use this skill when working on files in:
- `ME Project\me-primary\src\proto\ack_frame.c`
- `ME Project\me-primary\src\proto\control_frame.c`
- `ME Project\me-primary\src\proto\crc16.c`
- `ME Project\me-primary\src\proto\frame_router.c`
- `ME Project\me-primary\src\proto\realtime_frame.c`
- `ME Project\me-primary\src\proto\reg_frame.c`

## Key Files

| File | Symbols |
|------|---------|
| `ME Project\me-primary\src\proto\ack_frame.c` | me_ack_pack |
| `ME Project\me-primary\src\proto\control_frame.c` | query_is_known, me_control_parse |
| `ME Project\me-primary\src\proto\crc16.c` | me_crc16_read, me_crc16_modbus, me_crc16_append, me_crc16_verify |
| `ME Project\me-primary\src\proto\frame_router.c` | me_frame_resolve_len, length_is_declared, me_frame_expected_len |
| `ME Project\me-primary\src\proto\realtime_frame.c` | me_realtime_pack_post_registration, put_u16_be, me_put_f32_be, me_realtime_pack, put_u32_be |
| `ME Project\me-primary\src\proto\reg_frame.c` | me_reg_parse_response, me_reg_pack_request, bounded_len |

## How to Explore

```
analyze(operation:"communities", id:"community-6")
explore(operation:"context", task:"understand src/proto · me_realtime_pack", format:"gcx")
```

_`format: "gcx"` returns the [GCX1 compact wire format](../../docs/wire-format.md) — round-trippable, ~27% fewer tokens than JSON. Drop it for JSON output; agents using `@gortex/wire` or the Go `github.com/gortexhq/gcx-go` package decode either._
