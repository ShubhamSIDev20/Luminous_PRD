# Session #7: The board answers 0xBB Q1/Q3/Q4
> Date: 2026-08-12T12:42+05:30 | Agent: Claude Code (Opus 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-12_1046_comm-thread-relocation-registration-gate.md](2026-08-12_1046_comm-thread-relocation-registration-gate.md) — comm_thread relocation + per-circuit registration gate (ADR-17).

---

## 🎯 Goal This Session
Driven by **the first hardware test of the 4-thread base**. The developer
observed the Web Application sending a `0xBB` **Q1 is-ready** query *before* any
program data, and asked for it to be answered `0x01` per
`Ref Docs/bm_program_v3.0.md`; also to ignore a `0xBB` **Q3** (packet count)
payload while still acknowledging it.

## ✅ Done This Session

### T-37 — the board answers `0xBB` (ADR-18)

**Diagnosis first.** `frame_router.c:92-99` classified every non-Q4 `0xBB` query
as `ME_MSG_NONE`; `comm_thread.c` logged *"recognised but not handled in this
milestone"* and returned. The deeper finding: **`me_tcp_send_all()` was called in
exactly one place in the whole program** — inside `do_registration()`. The board
had no outbound TCP path at all beyond its own registration request, so
`route_frame()` had neither the socket fd nor `me_system_t`.

Built test-first:

| File | Change |
|---|---|
| `src/proto/program_frame.h` | **New.** `me_prg_pack_ack()`, `me_prg_query_is_handshake()`, `me_prg_parse_packet_count()` |
| `src/proto/program_frame.c` | **New.** 45 lines, pure |
| `tests/test_program_frame.c` | **New.** 11 cases |
| `src/proto/proto_defs.h` | `ME_PRG_ACK_LEN`, `ME_PRG_OFF_ACK_VALUE`, `ME_PRG_VALUE_OK/FAIL`, `ME_PRG_Q1_LEN`, `ME_PRG_Q3_LEN`, `ME_PRG_OFF_PKT_COUNT`; corrected the stale "Q3 not sent in ME" comment |
| `src/proto/frame_router.c` | `me_frame_expected_len()` now returns 6 for Q1 and 8 for Q3 |
| `tests/test_frame_router.c` | +2 cases |
| `src/threads/comm_thread.c` | `route_frame()`/`consume_rx_buffer()` take `sys` + `tcp_fd`; new `frame_crc_ok()`, `send_prg_ack()`, `handle_program_handshake()`; routing resequenced |
| both build scripts, `test_util.h`, `test_main.c` | wired up |

**The order inside `route_frame()` is the design.** Seven steps: classify →
filter `0xDD` (the one valid frame with no CircuitNumber — gating it would report
a nonexistent "circuit 0x00") → **admission control** → answer Q1/Q3 → drop
`ME_MSG_NONE` → verify Q4's CRC → queue, then ack. The gate sits **ahead of** the
reply on the developer's decision: an unregistered circuit gets no answer at all,
not a truthful `0x00`.

**Scope grew by one item, with the developer's explicit yes:** the per-step Q4
ack from the same document (`BB 01 01 04 01`). It costs one call because the
packer is generic over QueryID — and a dropped program step is otherwise
invisible until the chain fails to terminate.

## 💡 Discoveries / Gotchas
- **A doc correction from hardware:** `proto_defs.h` said Q3 was "legacy; not
  sent in ME" and `TASKS.md` T-26 said "Q3 is confirmed gone". The test shows
  otherwise. Both corrected.
- **`ME_PRG_Q1_LEN = 6` is the one unverified constant** and it is now used to
  split the TCP stream. It rests on the layout, not on a captured frame. A wrong
  value fails *non-locally*: CRC mismatch, then a byte-offset desync producing
  "unrecognised start byte" on every following frame. → **T-38**, and the answer
  is already in the developer's 2026-08-12 log.
- **`0xBB` Q4 frames were never CRC-checked before.** Adding the ack forced the
  question, since an ack is a claim about the bytes. This is a real behaviour
  change on the path T-24 depends on → **T-40**.
- **TDD gap caught and closed mid-session.** The RED run failed at *link* on the
  three missing `program_frame` functions, which meant the two new
  `test_frame_router.c` cases never executed. Reverted just the router edit, re-ran,
  watched them fail properly (`expected 0x6, got 0x0` / `expected 0x8, got 0x0`),
  then restored. A link-stage RED does not prove anything about tests that never
  ran.
- **Self-review found a latent underflow.** `frame_crc_ok()` computes
  `len - ME_REG_CRC_LEN` as a `size_t` for its diagnostic. Unreachable today
  (every caller runs after `me_frame_classify()`, which rejects `len < 6`), but on
  a short frame it would underflow to a near-`SIZE_MAX` read length — a logging
  path turned into an out-of-bounds read. Guarded.
- PowerShell 5.1 wrapped the passing test run's stderr in `NativeCommandError`
  again (documented behaviour). Run the `.exe` directly rather than piping the
  script's output.

## 📁 Files Changed This Session
| File | What Changed |
|------|-------------|
| `me-primary/src/proto/program_frame.h` | Created |
| `me-primary/src/proto/program_frame.c` | Created |
| `me-primary/tests/test_program_frame.c` | Created |
| `me-primary/src/proto/proto_defs.h` | Modified — `0xBB` ack/Q1/Q3 constants; stale Q3 comment corrected |
| `me-primary/src/proto/frame_router.c` | Modified — Q1/Q3 expected lengths |
| `me-primary/src/threads/comm_thread.c` | Modified — the reply path; routing resequenced |
| `me-primary/tests/test_frame_router.c` | Modified — +2 cases |
| `me-primary/tests/test_util.h`, `test_main.c` | Modified — runner wiring |
| `me-primary/build.ps1`, `build-native.ps1` | Modified — new sources |
| `Docs/ME_Primary_Implementation_Reference.md` | Modified — new §7 entry, `route_frame()` step table, reply table, counts, open items 8–10 |
| `.claude/*` | ADR-18, T-37/38/39/40, this entry, counts |

## 🔄 In Progress
Nothing — task landed this session.

## 🚫 Blocked
Nothing. Everything below the hardware line is done; only the hardware run
remains, and only the developer can do it.

## 🔜 Next Agent Should Do

1. **Read the developer's 2026-08-12 hardware log for the Q1 frame length**
   (T-38). It is the only unverified constant in this change and its failure mode
   is non-local.
2. **T-24 hardware run**, watching for the three new log lines and for any Q4 CRC
   rejection (T-40).
3. **T-33 is CLOSED, T-35 is won't-do.** The developer decided the Web
   Application team conforms to the current ME code, so ADR-17's 1-of-64 limit is
   an accepted constraint rather than a run risk. Do not re-raise it as a blocker
   and do not build the `--register-circuits` escape hatch. T-34 downgraded to 🟢
   for the same reason.
4. Do **not** start T-39 (Q2 metadata) without knowing whether the Web App waits
   for that reply — the same trap as Q1, one query later.
