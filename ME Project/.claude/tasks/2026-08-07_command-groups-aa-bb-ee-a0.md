# T-12: Command groups 0xAA/0xBB/0xEE/0xA0
> Created: 2026-08-07 | Status: Backlog (🟢 Low) — 0xAA/0xBB/0xEE done in #4
> File: `tasks/2026-08-07_command-groups-aa-bb-ee-a0.md`

---

## Description
~~Command groups `0xAA`/`0xBB`/`0xEE`/`0xA0`~~ — **`0xAA`/`0xBB`/`0xEE` done in
#4**. Parsed and routed by `frame_router.c` + `control_frame.c` +
`battery_frame.c`. **Only `0xA0` calibration remains.**

## Why / Context
(Old note said dispatch happens in `MONITORING` — that state no longer
exists; it is `idle_loop()` in `ME_COMM_IDLE`.)

## Progress Log
- **2026-08-11** (Session #4): `0xAA`/`0xBB`/`0xEE` parsed and routed.
  `0xA0` calibration remains unimplemented.
- **2026-08-07** (Session #2): Task opened.

## Related
- none
