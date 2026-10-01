# Session #6: comm_thread relocation + per-circuit registration gate
> Date: 2026-08-12T10:46+05:30 | Agent: Claude Code (Opus 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-12_1026_memory-sync-only.md](2026-08-12_1026_memory-sync-only.md) — memory sync only, no code touched.

---

## 🎯 Goal This Session
Two things, both developer-directed:
1. Move `comm_thread` into `src/threads/` beside the other thread modules.
2. Answer *"does program/command handling happen only after that channel's
   device registration?"* — then **build that gate**, because the answer was no.

## ✅ Done This Session

### T-31 — `comm_thread` relocation
`comm_thread.c/.h` moved `src/` → `src/threads/`, includes converted to the
`../relative` convention the sibling thread modules already use, `build.ps1` and
`main.c` updated, five reference documents corrected, and a stale state-machine
comment (`REGISTERED`/`MONITORING`, collapsed back in #3) fixed. Both builds
green; code review found nothing blocking.

### T-32 — per-circuit registration gate (ADR-17)
**The question was diagnostic, and the finding was real:** nothing gated on
registration. `route_frame()` checked only that the CircuitID was *structurally*
valid (nibbles 1–8) — a well-formed but never-registered circuit was handled
exactly like a live one. `circuit_store.c` has no registration field, and
`can_mgr.c` is still a stub, so no per-circuit registration signal existed at all.

Built test-first:

| File | Change |
|---|---|
| `src/store/circuit_registry.[ch]` | **New.** 64 bools. `init` / `mark_registered` / `is_registered`. Pure, host-tested. |
| `tests/test_circuit_registry.c` | **New.** 12 checks — 111 → **123**. |
| `src/threads/comm_thread.c` | `do_registration()` marks on `0x01`/`0x02`; `route_frame()` gates before any queue send |
| `src/main.c` | `me_registry_init()`; **range-check `--secondary`/`--channel`**; startup line stating the admission policy |
| `build.ps1`, `build-native.ps1` | New sources wired in |
| `Docs/ME_Primary_Implementation_Reference.md` | New §7 entry, `route_frame()` row, 111→123, Open Items 5 and 6 |

**Code review: no Critical, four Important — all applied.** One was my own error:
I had written a comment claiming the failure branch makes a bad `--secondary 99`
diagnosable. It does not — `ME_CIRCUIT_ID` masks with `0x0F` (`proto_defs.h:247`),
so `99 & 0x0F` = 3 → CircuitID `0x31`, a *valid* Secondary 3. The board would
register and gate on the wrong circuit silently. The real defect was silent
truncation of operator input, now rejected in `parse_args()`.

Also applied: the single-owner threading rule in `circuit_registry.h` (it invited
a `can_mgr` write with no synchronisation rule — now says route it through a
message); five more boundary tests (nibble-swap `0x18`/`0x81`, row-boundary
`0x18`/`0x21`, 64-circuit independence sweep, `0x01`/`0x10`/`0x09`/`0x90`,
init-clears-all); the `s_registered` divergence note in `main.c`.

## 💡 Discoveries / Gotchas
- **The gate belongs in `route_frame()` and nowhere else.** Every inbound frame
  funnels through that one function before reaching a queue, so one check covers
  `0xAA`/`0xBB`/`0xEE` and no future message type can bypass it. Verified by
  tracing every start byte through `me_frame_classify()`; `route_frame()` is the
  only producer into `g_q_core`/`g_q_data`.
- **`ME_CIRCUIT_ID` truncates rather than failing.** Any out-of-range
  `--secondary`/`--channel` silently became a different valid circuit. Cosmetic
  before this session; now it decides which circuit's data the board accepts.
- **The board is functionally single-circuit right now.** The `0xDD` frame
  carries one CircuitID and is sent once per connection, so 1 of 64 circuits
  registers while `g_program[64]`, `g_cl_program[64]`, `g_demo[64]` are all
  built for 64. Deliberate and temporary — see T-33, T-35 and ADR-17.
- **The gate's ordering is not host-testable.** `comm_thread.c` is LINUX-ONLY.
  The table is tested; the placement is proven by reading only (T-36).

## 🔄 In Progress
Nothing — both tasks landed this session.

## 🚫 Blocked
Nothing.

## 🔜 Next Agent Should Do

> ⛔ **This list is SUPERSEDED — do not act on it.** Items 1 and 2 were answered
> on 2026-08-12: the developer closed T-33 with "the Web application team will work
> as compliance with the current ME code", which made T-35 won't-do and downgraded
> T-34. Use session #7's list instead.

1. ~~**T-33 — ask the Web App team which CircuitIDs it sends to this board.**~~
   Closed by developer decision — the Web App conforms to the board.
2. ~~**T-34** — same conversation: does every `0xEE`/`0xAA` frame carry a real
   per-circuit CircuitID?~~ Downgraded to 🟢; still an open design note.
3. **T-24 — the hardware run.** Still the only thing that proves any of this.
   Nothing from sessions #4, #6 or #7 is hardware-verified.
