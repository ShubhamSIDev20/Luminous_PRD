# T-61: Secondary 1 registers all 4 channels (0x11-0x14), independently, over one TCP connection
> Created: 2026-08-19 | Status: Done (2026-08-19, Session #12)
> File: `tasks/2026-08-19_secondary1-registers-4-channels.md`

---

## Description
`--channels` CLI list (replaces `--channel`), `do_registration()` takes an
explicit `circuit_id`, `comm_thread_main()` loops per configured channel with
no ordering dependency between them. Plus the CAN-FD fix this unblocked:
`can_mgr.c` coalesces per-channel SET_VALUES frames into a per-Secondary/
per-block shadow buffer (`me_can_merge_slot()`, pure/host-tested) so one
channel's setpoint frame never zero-stomps another's in the shared 64-byte
block.

## Progress Log
- **2026-08-19** (Session #12): 6-task plan executed, 229 host checks (was
  220), both builds clean. ADR-32. **NOT yet hardware-verified — see T-62.**

## Related
- Relates to: ADR-32, T-62
