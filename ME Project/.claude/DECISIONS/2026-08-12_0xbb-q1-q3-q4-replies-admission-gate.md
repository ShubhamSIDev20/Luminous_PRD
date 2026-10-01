# ADR-18: The board answers `0xBB` Q1/Q3/Q4, behind the admission gate
> Date: 2026-08-12 | Session: #7 | Status: Accepted — **extended by #8** to `0xAA` Q5 and all of `0xEE`; the packer moved to `proto/ack_frame.c` and `ME_PRG_ACK_*`/`ME_PRG_VALUE_*` were renamed `ME_ACK_*`. Not hardware-verified.
> File: `DECISIONS/2026-08-12_0xbb-q1-q3-q4-replies-admission-gate.md`

---

**Context:** The first hardware test of the 4-thread base showed the Web
Application sending a `0xBB` **Q1 is-ready** query before any program data. The
board classified it, logged "recognised but not handled in this milestone", and
replied nothing — because until now the registration request was the **only**
frame this program ever wrote to TCP. The same run implies **Q3** (packet count)
is also sent, which corrects the earlier note in `proto_defs.h` that Q3 was
"legacy; not sent in ME".

**Decision:** Four parts.

1. **A new pure module, `src/proto/program_frame.[ch]`**, owns the reply layout:
   `BB | dev | ckt | QueryID | Value | CRC[2]`, 7 bytes,
   `0x01` = OK/Yes, `0x00` = Fail/No. One shape serves Q1, Q3 and Q4 because the
   QueryID is echoed from the query being answered.
2. **The gate comes first.** An unregistered circuit gets **no reply at all**,
   not a negative one. Answering "ready" for a circuit this board never
   registered would be a false claim about hardware it has no record of. The
   alternative — a truthful `0x00` — was considered and rejected by the developer.
3. **Q4 program packets are acknowledged per packet**, and therefore CRC-verified
   before storage. `0x01` = queued to the Data Manager; `0x00` = bad CRC or
   destination queue full.
4. **`me_frame_expected_len()` gains real lengths for Q1 (6) and Q3 (8).** These
   two now arrive mid-conversation and can share a `read()` with the frame behind
   them; without a boundary they would be handed to `route_frame()` as one
   oversized blob, fail CRC, and take the following frame down with them.

**Why a separate module rather than extending `frame_router.c`:** the router
answers "who owns this frame". This answers "what do we send back". Mixing them
would put the only outbound-frame builder inside the file whose whole job is
classifying inbound ones.

**AMENDED BY SESSION #8 — the `0xEE` CRC exemption was a defect.** This ADR
originally exempted `0xEE` from the pre-ack CRC check on the grounds that Core
Logic re-verifies with `me_control_parse()`, so checking here "could disagree with
it". **Code review caught that as wrong.** The ack is sent from `route_frame()`
*synchronously*, while Core Logic parses later on its own thread and has **no path
back to the socket** — `handle_control()` logs a rejection and returns. So a
corrupt `0xEE` frame was told `0x01` OK and then silently dropped: the exact
opposite of this ADR's own rule that an acknowledgement is a claim about the bytes.
`0xEE` is now checked like every other acked frame. Two CRCs over the same bytes
cannot disagree — that concern was unfounded. **Residual gap:** a well-formed frame
carrying an unrecognised QueryID is still acked here before Core Logic rejects it
(**T-45**).

**Why `me_prg_query_is_handshake()` is a pure predicate and not an `if`:** the
*decision* of which queries the comm thread answers is exactly the part worth
testing, and `comm_thread.c` is LINUX-ONLY and excluded from the host build.
Hoisting it leaves only the socket call untested. Q4 is deliberately excluded
from the predicate — its ack means "queued", which only the routing path can
honestly assert.

**Why the Q1 answer is unconditional:** readiness genuinely is unconditional
today. The Data Manager accepts a program at any time and a new one replaces the
old (ADR-14). A circuit already running a test is the case that will need
`ME_PRG_VALUE_FAIL`, and that case does not exist until step execution lands.

**Consequences:**
- `route_frame()` and `consume_rx_buffer()` now take the socket fd and
  `me_system_t`. The inbound path is bidirectional for the first time.
- **`0xBB` Q4 frames are CRC-verified where they never were before.** If the Web
  Application computes that CRC over a different span, every program packet is
  refused with a `0x00` ack. The log prints computed-vs-carried in both byte
  orders, so this is a one-look diagnosis — but it is a behaviour change on the
  path their next hardware run depends on.
- **`ME_PRG_Q1_LEN = 6` is the one unverified constant.** It rests on the layout
  (no payload, same shape as a payload-less `0xEE`), not on a captured frame. If
  wrong, the symptom is a CRC-mismatch hex dump followed by a byte-offset stream
  desync — not a local failure. Confirm against the `inbound TCP data` dump.
- A frame for an unregistered circuit now logs start+query instead of a message
  type name, since Q1/Q3 have no message type. `0xA0` and `0xBB` Q2/Q5/Q6 for an
  unregistered circuit moved from an INFO "not handled" to a WARN "not
  registered" — more accurate, and the gate now precedes that check.
- **Q2 (program metadata) is still unanswered.** If the Web Application waits for
  a reply to it, the handshake stalls there instead.

**Alternatives rejected:**
- *Reply `0x00` for unregistered circuits* — truthful and keeps the Web App's
  state machine moving, but the developer chose silence as the stricter reading
  of ADR-17.
- *Skip the Q4 ack until the run proves it necessary* — the packer is generic, so
  the marginal cost was one call plus tests, and a dropped program step is
  otherwise invisible until the chain fails to terminate.
- *Leave `me_frame_expected_len()` returning 0 for Q1/Q3* — no new assumption,
  but a coalesced read would then destroy two frames instead of splitting them.

**Related**
- Supersedes: none (extended in place by session #8, see amendment above)
- Relates to: ADR-14, ADR-17, ADR-22, T-38, T-39, T-40, T-45
