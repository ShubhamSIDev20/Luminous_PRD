# Session #8: 0xEE Session ID bug + CRC-derived frame boundaries
> Date: 2026-08-12T14:34+05:30 | Agent: Claude Code (Opus 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-12_1242_bb-handshake-reply.md](2026-08-12_1242_bb-handshake-reply.md) — the board began answering `0xBB` Q1/Q3/Q4 (ADR-18).

---

## 🎯 Goal This Session
A hardware bug report. The developer sent a Start command from the Web
Application and the board rejected it:

```
[08:59:28] DEBUG  inbound TCP data (10 bytes)
        0000  EE 01 11 01 01 11 35 F3  62 E7
[08:59:28] ERROR  core: control frame rejected - BAD_CRC
```

*"But the CRC is correct."* They were right. Also asked: acknowledge the `0xAA`
battery packet, and amend the control document with the Session ID.

## ✅ Done This Session

### T-41 — the `0xEE` Start `BAD_CRC` (the reported bug)

**Diagnosed by computing the CRC over every possible boundary** rather than
reading code — a throwaway probe in the scratchpad, linked against the project's
own `crc16.c`:

```
  total= 6  body= 4  computed=0x5CA9  bytes[4..5]=01 11  -> no
  total=10  body= 8  computed=0x62E7  bytes[8..9]=62 E7  -> *** MATCH (BE) ***
```

**Q1 Start carries a 4-byte Session ID** (`0x011135F3`) that
`bm_control_v3.0.md` recorded as *"Payload: None"*. `me_frame_expected_len()`
returned 6, so `consume_rx_buffer()` handed `route_frame()` only
`EE 01 11 01 01 11`, and the CRC was computed over `EE 01 11 01` and compared
against `01 11` — the first half of the Session ID.

**The second bug in the same event, which the developer had not seen yet:**
`off += 6` left `35 F3 62 E7` in the reassembly buffer, below
`ME_FRAME_MIN_LEN`. The *next* frame would have been appended behind that debris
and parsed at a 4-byte offset — "unrecognised start byte" for the life of the
connection. **A wrong length entry does not fail locally.**

### T-42 — `me_frame_resolve_len()` (ADR-19)

Root cause is structural: the protocol has **no length field**, so
`me_frame_expected_len()` is not parsing, it is *recalling documented layout* —
and ADR-4 already says the documents are wrong sometimes. There was no recovery.

The CRC is the only delimiter the protocol actually provides, so it is now the
backstop: layout length if it verifies → whole read if the table has no entry →
shortest-first CRC scan → layout unchanged. A scan that disagrees with a real
table entry logs **WARN naming both lengths**, because silent self-healing would
leave the table wrong forever.

### T-43 — `0xAA` Q5 and all six `0xEE` commands now answered

The ack packer moved out of `program_frame.c` into a shared
`proto/ack_frame.[ch]`; `me_ack_pack()` echoes the start byte, so one packer
serves `0xAA`/`0xBB`/`0xEE`. `ME_PRG_ACK_*`/`ME_PRG_VALUE_*` → `ME_ACK_*`.
**`STORE_CONFIG` is deliberately not acked** — the `0xAA` read queries owe real
data back, not a yes/no.

## 💡 Discoveries / Gotchas

- **Code review caught a Critical defect I had introduced, and my own test was
  asserting the symptom.** `test_resolve_len_splits_a_coalesced_read` asserted
  `scanned == true` for a *perfectly ordinary* `0xAA` frame and I read that as a
  pass. Cause: `layout == 0` ("no entry, by design") and a wrong non-zero layout
  were treated as one state, so **the scan became the primary path for every
  `0xAA` frame** — where a coincidental short CRC match inside its own payload
  would truncate it and orphan the remainder, recreating the exact desync this
  work exists to prevent. Fixed by trying the whole read first when
  `layout == 0`, and by `*out_scanned = (layout != 0 && total != layout)`.
  The RED run reproduced it concretely: **`expected 12, got 6`** on a
  deliberately booby-trapped frame.
- **ADR-18's `0xEE` CRC exemption was also a defect** (review, Important). I had
  reasoned that Core Logic re-verifies so checking twice "could disagree". But the
  ack is sent *synchronously from `route_frame()`* while Core Logic parses later on
  another thread with **no path back to the socket** — so a corrupt `0xEE` frame
  was told OK and then silently dropped. Two CRCs over the same bytes cannot
  disagree; that concern was unfounded. Now checked like every other acked frame.
- **The `0xAA` ack shape is inferred, not documented.** `Ref Docs/` has no config
  document at all. `bm_control_v3.0.md` and `bm_program_v3.0.md` agree on
  `Start|dev|ckt|qid|Value|CRC[2]`, so the family agrees — but this is the weakest
  claim in the change and the first thing to question if the Web App rejects it.
- Session IDs are **logged, not stored.** The `0xCC` live frame has no field for
  one; the UDP 10001 session variant does, which is where it belongs once session
  records exist (T-29).

## 📁 Files Changed This Session

| File | What Changed |
|------|-------------|
| `me-primary/src/proto/ack_frame.h/.c` | Created — the shared 7-byte ack |
| `me-primary/tests/test_ack_frame.c` | Created — 8 cases |
| `me-primary/src/proto/proto_defs.h` | `ME_CTRL_SESSION_ID_LEN`/`_OFF_`/`_BARE_`/`_START_`/`_SYNC_LEN`; `ME_PRG_ACK_*`→`ME_ACK_*` |
| `me-primary/src/proto/control_frame.h/.c` | `session_id` + `has_session_id`; parsed for Q1 only |
| `me-primary/src/proto/frame_router.h/.c` | Q1 = 10 bytes; new `me_frame_resolve_len()` |
| `me-primary/src/proto/program_frame.h/.c` | Packer removed (moved to `ack_frame`) |
| `me-primary/src/threads/comm_thread.c` | `send_ack()` echoes the start byte; `wants_ack`; CRC before every ack; resolve-len + WARN |
| `me-primary/src/threads/core_logic.c` | Logs the Session ID |
| `me-primary/tests/test_frame_router.c` | +10 cases, incl. the planted-short-CRC trap. **One pre-existing assertion corrected** — it used `ME_CTRL_START` and expected 6, encoding the bug |
| `me-primary/tests/test_control_frame.c` | +3 cases, incl. the literal captured wire bytes |
| `me-primary/tests/test_program_frame.c` | Rewritten — only `0xBB`-specific cases remain |
| `Ref Docs/bm_control_v3.0.md` | **Amended** with the Session ID, marked observed-from-hardware not from the source Excel |
| both build scripts, `test_util.h`, `test_main.c` | Wiring |
| `.claude/*`, `Docs/ME_Primary_Implementation_Reference.md` | ADR-19, ADR-18 amendment, T-41→T-45, counts |

## 🔄 In Progress
Nothing — task landed this session.

## 🚫 Blocked
Nothing. Only the hardware run remains, and only the developer can do it.

## 🔜 Next Agent Should Do

1. **T-24 hardware run.** The reported bug is fixed but **nothing here is
   hardware-verified.**
2. **Grep that run's log for `RECOVERED, but the length table … needs fixing`.**
   Each occurrence is another wrong layout entry naming itself (**T-44**) — the
   `0xEE` Q2/Q3/Q4/Q6, `0xBB` Q1/Q3 and all `0xAA` entries are still unconfirmed.
3. Watch for `0xBB` Q4 CRC rejections (T-40) — Q4 is CRC-checked now where it
   never was.
4. T-39 (does `0xBB` Q2 metadata need a reply?) and T-45 (unrecognised `0xEE`
   QueryID acked before rejection) remain open by choice, not oversight.
