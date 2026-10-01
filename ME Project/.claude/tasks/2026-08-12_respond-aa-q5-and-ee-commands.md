# T-43: Respond to 0xAA Q5 battery write and to all six 0xEE control commands
> Created: 2026-08-12 | Status: Done (2026-08-12, Session #8)
> File: `tasks/2026-08-12_respond-aa-q5-and-ee-commands.md`

---

## Description
The ack packer moved from `program_frame.c` to a shared `proto/ack_frame.[ch]`
(`me_ack_pack()` echoes the start byte), `ME_PRG_ACK_*`/`ME_PRG_VALUE_*`
renamed `ME_ACK_*`. `STORE_CONFIG` deliberately NOT acked — the `0xAA` read
queries owe real data back, not a yes/no. 8 new checks.

## Progress Log
- **2026-08-12** (Session #8): Implemented.

## Related
- Relates to: ADR-18
