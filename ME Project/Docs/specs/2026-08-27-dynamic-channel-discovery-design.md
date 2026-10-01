# Dynamic Channel Discovery — Design

Status: **DRAFT — awaiting developer review.** Not approved for implementation.

Author: Claude Code, from developer brief 2026-08-27, revised twice the same day
(retry logic removed; M7 dummy responder and block-select decision added).

---

## 1. Problem

The board today learns which channels exist from the **operator**, not from the
**hardware**. `me_config_t.channels[]` / `channel_count` (`sys_init.h:25-26`)
are filled from a `--channels 1,2,3,4` CLI flag, and `comm_thread_main()`
(`comm_thread.c:824`) loops over exactly that list to send one `0xDD`
registration per entry.

This means:

- A channel that is **not physically present** is still registered with the Web
  Application, which then shows a circuit that can never run a test.
- A channel that **is** present but was left off the command line is invisible.
- Every hardware change needs a `deploy.ps1` flag change to match.

**Goal:** the Primary determines which channels physically exist by asking the
hardware at power-up, and registers exactly those.

---

## 2. The workflow, as specified

Developer brief 2026-08-27, as corrected the same day:

1. On power-up, the Primary sends a `READ_VALUES` CAN-FD frame to
   **Secondary 1, Block 1**. From the response it determines whether channels
   1, 2, 3 and 4 are present.
2. It then sends a `READ_VALUES` frame to **Secondary 1, Block 2**, and from
   that response determines whether channels 5, 6, 7 and 8 are present.
3. The same sequence repeats through **Secondary 8**.
4. Once the Primary knows which channels each Secondary has, it starts `0xDD`
   device registration for every present channel.

**One probe per block. No retries.** Exactly 16 frames, always.

```
Power-up
  |
  +-- S1 Block 1  -> READ_VALUES --> response --> ch 1,2,3,4 present?
  +-- S1 Block 2  -> READ_VALUES --> response --> ch 5,6,7,8 present?
  +-- S2 Block 1  -> ...
  +-- S2 Block 2  -> ...
  |    :          strictly sequential, one outstanding probe at a time
  +-- S8 Block 1
  +-- S8 Block 2
  |
  +--> channel map complete --> register every present channel via 0xDD
```

### 2.1 Decisions taken during review

| Question | Decision |
|---|---|
| How is "present" detected? | **The whole 16-byte slot is zero → absent.** Not voltage alone. See §3. |
| How many Secondaries are swept? | **All 8**, both blocks each. No CLI input. |
| What if discovery finds nothing? | **Register nothing.** No fallback to a CLI list, no optimistic registration, no exit. |
| Is a registered channel ever removed? | **No.** Loss mid-test is already handled by `me_exec_force_stop()`. |
| Retries? | **None.** One probe per block. |
| How is Block 1 told apart from Block 2? | **`Channel #` bytes in the probe payload.** See §4. |
| How is this tested before real Secondaries exist? | **M7 dummy responder.** See §5. |

### 2.2 Interpretation the developer did not specify

The brief says *"it will get response for that packet"*, which holds when the
Secondary is physically present. When a Secondary is not wired, **no response
ever arrives**.

**Interpretation applied:** a probe that times out means that whole block's four
channels are absent; the sweep moves to the next block. With retries removed
this is the only reading that keeps the sweep progressing.

**Consequence to be aware of:** a real Secondary that answers more slowly than
`ME_DISC_RESPONSE_TIMEOUT_MS` has all four of its channels marked absent, with
no second chance. The timeout must therefore be set generously relative to the
documented bus timing (2 ms for 8 channels at 2 Mbps,
`master_slave_can_v1.0.md`), not tightly. The protocol's own polling rule allows
3 resends before declaring a slave offline (`master_slave_can_v1.0.md:120`);
this design deliberately does not use them, per the developer's instruction to
keep it simple.

---

## 3. Why "voltage == 0" is not the presence test

The original brief said a zero voltage value means the channel is absent.
Implemented literally, this **silently refuses to register a working channel
that has no battery connected** — an open-terminal channel genuinely reads
0.000 V.

The per-channel 16-byte slot carries more than voltage
(`master_slave_can_v1.0.md`, "Per-channel 16-byte record"):

```
 offset  size  Slave (READ_VALUES) field
   +0      4   Feedback Voltage (float)
   +4      4   Feedback Current (float)
   +8      1   STATE          <- CMD_STO=0x00 .. CMD_ERR=0x04
   +9      1   Channel #      <- 0x01 .. 0x08, 1-based
  +10      1   INT Para #
  +11      1   Reserved
  +12      4   Data (float)
```

A **present** channel populates `Channel #` at `+9` with its own 1-based number,
which is never zero. An **absent** channel has no responder to populate
anything, so its 16 bytes stay as the Secondary left them — zero.

```
 ch2 slot, PRESENT but no battery:      ch3 slot, ABSENT:
   00 00 00 00   V = 0.000                00 00 00 00
   00 00 00 00   A = 0.000                00 00 00 00
   00            STATE = CMD_STO          00
   02            Channel # = 2  <- KEY    00            Channel # = 0  <- KEY
   00 00                                  00 00
   00 00 00 00                            00 00 00 00
```

**Presence test:** `memcmp(slot, zeros, 16) != 0`.

Testing the whole slot rather than only `+9` is deliberate: transcription note
14 of `master_slave_can_v1.0.md` flags `Channel #` as *"⚠ unclear — confirm
before implementing"*, because the sheet never says whether the Secondary is
required to echo it. Testing all 16 bytes is correct whether the Secondary
populates `Channel #`, `STATE`, a real voltage, or any combination — any one of
them being non-zero proves something answered for that slot.

> ⚠ **This is the one assumption in the design that hardware must confirm.**
> It is cheap to confirm: run the sweep with a known-absent channel and read the
> `disc:` log lines added in §7. If a Secondary zero-fills the slots of channels
> it does not have *and* reports a genuine 0 V for ones it does, no byte
> inspection can tell them apart and the design needs a different signal.

---

## 4. Telling Block 1 apart from Block 2

**This is a protocol-level decision, not an implementation detail.** It binds
the M7 dummy responder *and* real Secondary firmware.

### 4.1 The gap

Both blocks of a Secondary share one CAN identifier. For Secondary 1:

```
CAN_ID = (circuit6 << 5) | function5 = (1 << 5) | 0x02 = 0x022
```

…for a Block 1 probe **and** a Block 2 probe alike. The workbook defines the
identifier as circuit + function only (`master_slave_can_v1.0.md`, "Identifier
layout"); nothing in it encodes the block. The responder therefore cannot tell
from the identifier which four channels are being asked about, and the workbook
never says how it should. This gap is not called out in the source document.

### 4.2 The decision

**The probe payload carries the block, in the `Channel #` field of each slot.**

```
Block 1 probe (CAN ID 0x022):        Block 2 probe (CAN ID 0x022):
  byte  9 = 0x01  (slot 0, +9)         byte  9 = 0x05
  byte 25 = 0x02  (slot 1, +9)         byte 25 = 0x06
  byte 41 = 0x03  (slot 2, +9)         byte 41 = 0x07
  byte 57 = 0x04  (slot 3, +9)         byte 57 = 0x08
  every other byte zero                every other byte zero
```

The responder reads byte 9 and answers with that block's four channels.

**Why this and not something new:** `Channel #` at `+9` is already defined for
the Master direction by the workbook, and `me_can_pack_read()`
(`can_frame.c:63-72`) already writes it — for one channel. Nothing is invented;
an existing field is used for the thing it names. Real Secondary firmware can
implement the same rule with no protocol extension.

Rejected alternatives: replying with both block frames to one probe (the
Primary would then have to attribute two same-ID replies by arrival order —
the exact ambiguity §6.2 exists to avoid); and a stateful alternating toggle in
the responder (one dropped frame desynchronises it permanently, and channels
5-8 get recorded as 1-4 with no way to detect it).

### 4.3 New helper required

`me_can_pack_read()` fills exactly one slot. Discovery needs all four:

```c
/* Builds a 64-byte READ_VALUES probe addressing a whole block: every one of
 * the four slots carries its own Channel # at +9, and nothing else. This is
 * what tells the responder which block is being asked about - the CAN ID
 * cannot (see design doc section 4). block is ME_CAN_BLOCK_1 or _2. */
void me_can_pack_read_block(me_can_block_t block,
                            uint8_t out64[ME_CAN_FRAME_LEN]);
```

Added to `can_frame.c`/`.h`, which is pure logic with no sockets or platform
headers — so the probe layout is host-testable, the same discipline the file
already follows.

---

## 5. The M7 dummy responder

Real Secondaries are not yet available (developer, 2026-08-27). To make
discovery verifiable before they arrive, the **M7 generates a dummy response**
to each `READ_VALUES` probe, so the Primary sees one Secondary with all eight
channels present and registers `0x11`–`0x18`.

**The M7 firmware is not in this repository.** `me-primary` is A53 userspace
only. This section is therefore a **change request to the M7 firmware owner**,
not work this design implements. There is already an open task to reach that
owner: `.claude/tasks/2026-08-20_confirm-m7-echo-root-cause-with-firmware-owner.md`.

### 5.1 What the M7 must do

On receiving a `READ_VALUES` frame (function `0x02`) for Secondary 1
(CAN ID `0x022`):

| Probe carries | M7 replies with |
|---|---|
| byte 9 = `0x01` | Block 1 frame: slots 0-3 populated for channels 1, 2, 3, 4 |
| byte 9 = `0x05` | Block 2 frame: slots 0-3 populated for channels 5, 6, 7, 8 |

Each populated slot carries:

```
 +0..+3  Feedback Voltage  - dummy, NON-ZERO   (see 5.2)
 +4..+7  Feedback Current  - dummy, NON-ZERO   (see 5.2)
 +8      STATE             - 0x00 (CMD_STO), the device is idle
 +9      Channel #         - the channel's own 1-based number
 +10..15 zero
```

The reply's CAN ID is `(1 << 5) | 0x02` = `0x022`, function `0x02` — the
Secondary always answers on function `0x02`, whichever function was requested
(`master_slave_can_v1.0.md`, "Functions"), and `handle_stream_frame()` already
rejects anything else (`can_mgr.c:445`).

Probes addressed to Secondaries 2-8 get **no reply**, so those 56 channels are
correctly discovered as absent.

### 5.2 ⚠ The dummy values MUST be non-zero

This is a correctness requirement on the dummy, not a cosmetic choice.

The M7 loops every frame the Primary transmits back onto the same RX stream
(ADR-38, §6.1). So for each probe the Primary receives **our own echo** as well
as the dummy reply. The echo of a Block 1 probe has four non-zero slots — the
`Channel #` bytes from §4.2 — which under §3's presence test reads as *"all
four channels present."*

If the dummy reply carried zero voltage and current, it would be **byte-
identical to the echo of our own probe**, and the collector could not tell
them apart. Non-zero dummy values are what make the genuine reply
distinguishable.

Suggested values, chosen to be obviously synthetic in a log:

| Channel | Voltage | Current |
|---|---|---|
| 1 | 3.100 | 1.100 |
| 2 | 3.200 | 1.200 |
| … | … | … |
| 8 | 3.800 | 1.800 |

Per-channel distinct values also prove slot mapping end to end: if the Primary
logs channel 6 at 3.600 V, slot 1 of Block 2 was decoded correctly. A single
shared constant would hide an off-by-one slot error.

### 5.3 This is a test fixture, not product behavior

The dummy responder makes the Primary register eight channels that do not
physically exist. Shipped by accident, it would put eight phantom circuits in
front of the Web Application permanently.

It must be removable — a compile-time flag in the M7 firmware, off by default
in any production build — and its removal must be tracked. A task is opened for
that removal at the same time the request goes to the firmware owner, not
afterwards.

---

## 6. Three traps in the existing CAN code

All three are pre-existing behaviors in `can_mgr.c` that the sweep walks
straight into. None is visible from the protocol documents.

**None is an artifact of today's bench rig.** They are properties of the M7
firmware and of `can_mgr.c`, and all three persist once real Secondaries are
connected. Trap 6.2 in fact becomes *certain* rather than occasional, because
the sweep probes Block 1 and Block 2 of the same Secondary back to back by
construction.

### 6.1 Our own echo reads as "all four channels present"

`handle_stream_frame()` (`can_mgr.c:491`) discards any inbound frame that is
byte-identical to `s_set_shadow` for that Secondary/block:

```c
if (memcmp(can.data, s_set_shadow[secondary][block_ix], ME_CAN_FRAME_LEN) == 0) {
    s_rx_echo++;
    return;
}
```

This exists because the M7 hands back every frame we transmit on the same CAN1
RX stream it uses for genuine Secondary replies — `RPMSG_PROTOCOL.md` documents
the length-80 `GET_FRAME` only as "Streamed CAN1 RX frame", with no field
distinguishing origin (ADR-38, `can_mgr.c:107-118`). **This is M7 firmware
behavior, not a bench-rig artifact**; it does not go away when real Secondaries
are wired. There will then be genuine replies *and* echoes interleaved on one
stream.

**Why the sweep breaks it.** The filter compares against `s_set_shadow`, which
is all zeros at power-up. But a discovery probe is **not** all zeros — §4.2 puts
`Channel #` bytes in four slots. So the echo of a probe does not match shadow,
is not filtered, and reaches the presence test with four non-zero slots:
**every block would read as "all four channels present"**, including blocks
with nothing behind them.

**Handling.** During the sweep, `can_mgr` sets `s_discovery_active`. While set,
`handle_stream_frame()` routes inbound frames to the discovery collector
*before* the echo filter, and the collector compares each inbound frame against
**the exact probe it just transmitted**:

| Inbound frame | Verdict |
|---|---|
| Byte-identical to the transmitted probe | Our echo. Discard, keep waiting until timeout. |
| Anything else | Genuine reply. Parse slots, settle the block. |
| Nothing before timeout | Block absent. |

This is why §5.2 requires non-zero dummy values, and it is the same reason a
real Secondary's reply is distinguishable: it carries measured voltage and
current, which the probe does not.

**Residual risk with real hardware, stated plainly.** A *present* channel that
happens to read exactly 0.000 V, 0.000 A, `STATE = CMD_STO` would produce a
reply byte-identical to the probe, and be discarded as an echo. The mitigation
is to consume **one** matching frame as the echo and accept a second matching
frame as the reply — the M7 echoes exactly once. That covers the case, but it
depends on the M7 echoing exactly once per transmission, which is observed
behavior rather than documented behavior. **Confirm with the firmware owner in
the same conversation as §5.**

### 6.2 Block 1 and Block 2 replies are ambiguous by CAN ID

Both blocks of a Secondary share one CAN ID and every reply carries
`function5 = 0x02`. The identifier encodes the *Secondary*, never the *block*.
So `handle_stream_frame()` infers the block from which channels are already
known active (`can_mgr.c:480`):

```c
const uint8_t block_ix = ((mask & 0x0Fu) != 0u) ? 0u : 1u;
```

The file already warns this is fragile (`can_mgr.c:470-474`) and it has been
safe only because, per ADR-28, one block is ever live per Secondary in practice.

**Why the sweep breaks it.** The sweep probes Block 1 then Block 2 of the same
Secondary, in that fixed order. `handle_can_tx()` marks channels active on every
successful transmit (`can_mgr.c:322`):

```
probe S1 Block 1  ->  reply  ->  mask becomes 0x0F (ch1-4 active)
probe S1 Block 2  ->  reply  ->  mask & 0x0F != 0  ->  block_ix = 0
                                  ^^^^^^^^^^^^^^^^ Block 1. Wrong.
```

Channels **5-8 would be recorded as channels 1-4**. With the M7 dummy of §5
this fires immediately: Secondary 1 has channels in both blocks by design, so
this is the first thing bench testing would hit.

This is the most serious of the three: 6.1 and 6.3 give wrong *presence*
answers, this one gives a confidently wrong *identity* answer. A Secondary with
channels 5-8 would be registered as `0x11`-`0x14`, and every later command would
reach the wrong physical channel — with a valid CRC, so nothing would appear
wrong.

**Handling:** the discovery collector does not use the mask inference at all. It
holds the block being probed as explicit state (`s_disc_block`), set before each
transmit and read when the reply arrives. Since exactly one probe is outstanding
at a time (§8.3), there is never an ambiguity to resolve. This sidesteps the bug
during discovery; it does **not** fix it for steady-state operation, which
remains as-is and is called out as follow-up work in §10.

### 6.3 The TX path would erase the probe's block selector

`handle_can_tx()` transmits **`s_set_shadow`'s bytes, not the caller's frame**:

```c
memcpy(can.data, shadow, ME_CAN_PAYLOAD_BYTES);   /* can_mgr.c:285 */
```

This is deliberate and correct for normal operation — it is what stops one
channel's setpoint frame from zero-stomping another's in the shared block
(ADR-32). But it means the caller's payload is discarded.

For a discovery probe that is fatal: the `Channel #` bytes §4.2 depends on would
be overwritten with shadow's zeros, and the M7 would receive **two identical
all-zero probes** for Block 1 and Block 2 — unable to tell them apart, which is
the exact problem §4 exists to solve.

**Handling:** discovery probes bypass the shadow path. The sweep writes its
probe frame directly, and does **not** merge into or read from `s_set_shadow` —
a probe carries no setpoint, so there is nothing to preserve and nothing to
protect. The shadow path is untouched for every non-discovery frame.

---

## 7. Observability

The sweep decides whether the board does anything at all, so it logs its
reasoning, not just its result:

```
disc: sweep starting, 8 secondaries x 2 blocks, 16 probes
disc: S1 B1 probe sent (ch# 01 02 03 04)
disc: S1 B1 <- echo discarded (matches probe)
disc: S1 B1 <- reply in 4 ms
disc:   ch1 PRESENT (V 3.100 A 1.100 state 0x00 ch# 0x01)
disc:   ch2 PRESENT (V 3.200 A 1.200 state 0x00 ch# 0x02)
disc:   ch3 PRESENT (V 3.300 A 1.300 state 0x00 ch# 0x03)
disc:   ch4 PRESENT (V 3.400 A 1.400 state 0x00 ch# 0x04)
disc: S1 B2 probe sent (ch# 05 06 07 08)
disc: S1 B2 <- echo discarded (matches probe)
disc: S1 B2 <- reply in 4 ms
disc:   ch5 PRESENT (V 3.500 A 1.500 state 0x00 ch# 0x05)
disc:   ch6 PRESENT (V 3.600 A 1.600 state 0x00 ch# 0x06)
disc:   ch7 PRESENT (V 3.700 A 1.700 state 0x00 ch# 0x07)
disc:   ch8 PRESENT (V 3.800 A 1.800 state 0x00 ch# 0x08)
disc: S2 B1 -> no reply (timeout 50 ms); ch1-4 marked ABSENT
disc: S2 B2 -> no reply (timeout 50 ms); ch5-8 marked ABSENT
...
disc: sweep complete - 8 of 64 circuits present: 0x11 0x12 0x13 0x14 0x15 0x16 0x17 0x18
comm: registering 8 discovered circuit(s)
```

Logging the raw field values behind each verdict is what makes §3's assumption
falsifiable on hardware, and logging the discarded echo separately is what keeps
§6.1 diagnosable rather than invisible. With the §5.2 per-channel dummy values,
a channel logging the wrong voltage is immediate proof of a slot-mapping error.

---

## 8. Architecture

### 8.1 Where discovery lives

Discovery runs **inside the CAN Manager thread** (`can_mgr.c`), before it enters
its normal `poll()` loop.

Reasons:

- `can_mgr` is the sole owner of the RPMsg fd (`s_fd`). No other thread can
  transmit a CAN frame, and adding a second writer would break the "one owner,
  no locking" property the file is built on (`can_mgr.c:6-8`).
- The per-Secondary/per-block buffers it needs already exist there
  (`can_mgr.c:104,126`).
- The sweep is strictly serial request/response with a timeout. A dedicated
  pre-loop phase keeps the steady-state `poll()` loop unchanged.

### 8.2 The channel map — a new pure module

A new module `src/store/channel_map.c` / `.h` holds the result:

```c
/* PURE LOGIC: no sockets, no platform headers - same rule as
 * circuit_registry.h, so the presence verdict is host-testable. */

void me_chmap_init(void);

/* Record the verdict for one circuit. Rejects a malformed CircuitID. */
bool me_chmap_set_present(uint8_t circuit_id, bool present);

/* True only if circuit_id was proven present by the discovery sweep. */
bool me_chmap_is_present(uint8_t circuit_id);

/* Fill out[] with every present CircuitID; returns how many. */
uint8_t me_chmap_present_list(uint8_t *out, uint8_t max);

/* True once the sweep has run to completion (all 8 secondaries settled). */
bool me_chmap_sweep_complete(void);
```

**Why a new module rather than extending `circuit_registry.c`:** that file
answers *"is this circuit allowed to be handled"* — an access-control question
decided by the Web Application's `0xDD` reply. This answers a different question
decided by the hardware: *"does this circuit physically exist"*. The header of
`circuit_registry.h:8-12` makes exactly this argument for why it is separate
from `circuit_store.c`; the same reasoning applies again here.

Ownership follows the rule `circuit_registry.h:29-34` already lays down for
cross-thread writes: **`can_mgr` is the only writer, and it writes only during
the sweep, before the comm thread is permitted to read.** The handoff is a
one-way flag (`sweep_complete`), not a shared mutable structure.

### 8.3 Sweep sequencing

```
for secondary in 1..8:
    for block in 1..2:
        probe = me_can_pack_read_block(block)          <- section 4.3
        transmit probe directly, CAN ID = (secondary << 5) | 0x02
                                                      <- bypasses shadow, section 6.3
        wait up to ME_DISC_RESPONSE_TIMEOUT_MS:
            inbound identical to probe  -> echo, discard, keep waiting
            inbound differs             -> reply, stop waiting
                                                      <- section 6.1

        if reply received:
            for each of the 4 slots:
                present = (slot is not all-zero)
                me_chmap_set_present(CIRCUIT_ID(secondary, channel), present)
        else:
            mark all 4 of this block's channels ABSENT   (section 2.2)

mark sweep_complete
```

**Cost: exactly 16 probes.** At the documented 2 Mbps data phase, plus one
timeout per absent block, the whole sweep completes in well under a second.

**One outstanding probe at a time.** This is load-bearing, not a style choice —
it is what makes the reply unambiguous in §6.2 and what makes the echo
identifiable in §6.1.

**The RPMsg link must exist before the sweep can mean anything.** If the fd
could not be opened (`s_fd < 0`), the sweep has not run: nothing was asked, so
nothing was answered, and nothing may be marked absent. The sweep is deferred
until the link opens, then runs once.

This is not a retry of a probe — it is waiting for the transport to exist. The
distinction matters because without it, a board that boots faster than its M7
would permanently conclude all 64 circuits are absent on every cold boot. The
RPMsg device race is a known, documented failure mode on this board
(`Docs/runbooks/2026-08-21-imx8mp-deployment-guide.md`).

Once the sweep completes, the map is **final for the power cycle**. Newly wired
hardware requires a restart — see §10.

### 8.4 Registration consumes the map

`comm_thread_main()` (`comm_thread.c:824`) changes from iterating
`sys->cfg.channels[]` to iterating the discovered list:

```c
uint8_t present[ME_MAX_CIRCUITS];
const uint8_t n = me_chmap_present_list(present, ME_MAX_CIRCUITS);

for (uint8_t i = 0; i < n; i++) {
    const bool ok = do_registration(sys, fd, present[i]);
    ...
}
```

Everything else in the registration path is unchanged. `do_registration()`
already takes an explicit `circuit_id` (`comm_thread.c:168`), and the
independence guarantee from the 2026-08-19 design — no channel's outcome gates
another's — is preserved as-is.

**The comm thread must not race the sweep.** It waits for
`me_chmap_sweep_complete()` before its first registration pass, bounded by
`ME_DISC_WAIT_TIMEOUT_MS`. On timeout it proceeds with whatever is in the map
(possibly nothing) rather than blocking the TCP connection forever — the board
stays connected and diagnosable, per §2.1.

**`--channels` is removed.** Keeping it as a fallback was considered and
rejected: a fallback that fires exactly when discovery fails would re-introduce
the registration of non-existent channels in precisely the case the feature
exists to prevent, and would do it silently. `--secondary` is also removed; the
sweep covers all 8. Both are breaking changes to `deploy.ps1` and the board's
`docker-compose.yml` — see §9.

---

## 9. Impact on existing interfaces

| Surface | Change |
|---|---|
| `--channels 1,2,3,4` | **Removed.** No replacement. |
| `--secondary N` | **Removed.** All 8 are swept. |
| `me_config_t.channels[]`, `.channel_count`, `.secondary` | Removed from the struct. |
| `src/util/channel_list.c/.h` | Becomes dead code — it exists only to parse `--channels`. Delete with the flag. |
| `src/proto/can_frame.c/.h` | Gains `me_can_pack_read_block()` (§4.3). Existing functions unchanged. |
| `deploy.ps1` | Must drop both flags. |
| Board `docker-compose.yml` | Must drop both flags. **The runbook already warns these have drifted** (`Docs/runbooks/2026-08-21-imx8mp-deployment-guide.md`) — this change requires updating the board's compose file, or the container will fail to start on an unrecognized argument. |
| **M7 firmware (external)** | Dummy responder per §5. **Not in this repo — a change request to the firmware owner.** |

---

## 10. Explicitly out of scope

- **Retries.** Removed at the developer's instruction. The protocol permits 3
  resends before declaring a slave offline (`master_slave_can_v1.0.md:120`); if
  false-absent verdicts show up on real hardware, reinstating them is the first
  thing to try. See §2.2.
- **Hot-plug.** The map is final per power cycle. Adding a Secondary requires a
  restart. Perpetual re-sweeping would put CAN traffic on a bus shared with
  running battery tests, and a transient glitch during a multi-hour test is a
  worse failure than needing a restart to add hardware.
- **Deregistration.** A registered channel is never removed. A channel lost
  mid-test is already handled by `me_exec_force_stop()`
  (`step_engine.c:98-104`), which sends `CMD_STO` and stops that channel's
  context, leaving its siblings running.
- **The steady-state Block 1 / Block 2 RX ambiguity** (`can_mgr.c:466-480`).
  Discovery avoids it (§6.2); it is not fixed for normal operation. The M7 dummy
  of §5 puts channels in **both** blocks of Secondary 1, which makes this
  reachable in steady state for the first time — so it is **follow-up work that
  this change makes urgent**, not a pre-existing issue that can keep waiting.
- **Telling the Web Application which channels were discovered.** The `0xDD`
  registration set implicitly conveys it. No new frame type.

---

## 11. Testing plan

| Layer | Command | Covers |
|---|---|---|
| Host protocol tests | `.\build-native.ps1` | `channel_map.c`: set/get per circuit, malformed CircuitID rejected (never folded to slot 0), `present_list` ordering and count, `sweep_complete` gating. `me_can_pack_read_block()`: Block 1 → bytes 9/25/41/57 = 01/02/03/04 and all 60 other bytes zero; Block 2 → 05/06/07/08. Slot-presence predicate: all-zero slot → absent; `Channel #` set but 0 V → **present**; `STATE` alone → present; voltage alone → present. Sweep sequencer as a pure function over a scripted inbound table: 16 probes in S1B1…S8B2 order; a frame identical to the probe is discarded as echo and does not settle the block; a differing frame settles it; a timed-out block marks exactly its own 4 channels absent. |
| Cross-build | `.\build.ps1` | Compiles clean under `-Werror`, static aarch64 ELF. **Proves it builds, not that it works.** |
| Bench, M7 dummy | `.\deploy.ps1` (developer-run) | Requires §5 in the M7 first. See §11.1. |
| Hardware-in-the-loop | `.\deploy.ps1` with real Secondaries | The final proof. See §11.2. |

### 11.1 What the M7 dummy CAN prove

With §5 implemented, a bench run proves substantially more than host tests:

- (a) All 8 channels of Secondary 1 discovered; Secondaries 2-8 absent; exactly
  `0x11`-`0x18` registered with the Web Application.
- (b) **Trap 6.2 is closed** — channels 5-8 are recorded as 5-8, not as 1-4.
  This is the first time the two-blocks-on-one-Secondary case has ever been
  exercised, and the §5.2 per-channel dummy values make a slot-mapping error
  visible immediately (channel 6 must log 3.600 V).
- (c) **Trap 6.1 is closed** — the `echo discarded` line appears once per probe
  and no block is falsely reported present from an echo.
- (d) **Trap 6.3 is closed** — the M7 can distinguish the two probes at all,
  which it can only do if the `Channel #` bytes survived the TX path.
- (e) Boot with the M7 held down → nothing marked absent, sweep deferred, then
  completes correctly once the link appears (§8.3).

That is every trap in §6 plus the end-to-end registration path. It is a genuine
verification of the mechanism.

### 11.2 What only real Secondaries can prove

The dummy cannot answer these, because it is built from the same assumptions the
code is:

- **§3's presence test.** The dummy populates slots exactly as §3 predicts a
  Secondary will. It confirms the Primary reads that layout correctly; it
  cannot confirm the layout is what real hardware produces. A genuinely absent
  channel on a real Secondary is the only way to test this.
- **Timeout adequacy (§2.2).** The dummy replies at RPMsg speed with no CAN bus
  in the path. Whether `ME_DISC_RESPONSE_TIMEOUT_MS` is generous enough for a
  real Secondary over a real 500 kbps/2 Mbps bus is untested until one exists.
- **Mixed presence.** The dummy reports all 8 present. A real Secondary with
  some channels absent is needed to prove the per-slot verdict, and to
  distinguish "present at 0 V" from "absent".
- **The §6.1 residual risk** — a present channel reading exactly 0 V/0 A/STO,
  whose reply is byte-identical to the probe. The dummy avoids this by
  construction (§5.2 mandates non-zero values), which means it cannot exercise
  it.

Per project convention a passing host build is never reported as "tested".
Per `ME Project/CLAUDE.md`, a traceability verdict may not be promoted to `DONE`
on `build-native.ps1` alone. **A bench run against the M7 dummy is real evidence
and should be recorded as such — but the requirement is not closed until real
Secondaries verify §11.2.**

---

## 12. Hardware-critical notice

`⚠ HUMAN REVIEW REQUIRED — hardware-critical change.`

This design puts new frames on the CAN-FD bus at power-up, changes which
circuits the board will command, and requests a firmware change on the M7. It
touches the real-time polling path (`can_mgr.c`) and the Secondary-facing
protocol. It must not be implemented until the developer approves this document,
and must not be trusted for production until §11.2 is satisfied.

---

## 13. Open questions for the developer

1. **§3's presence test is an inference, not a documented behavior.** The CAN
   workbook does not state what a Secondary puts in the slot of a channel it
   does not have. The design is built to be correct under the most likely
   behavior (untouched slot stays zero) and to make a wrong guess *visible* in
   the logs rather than silent (§7) — but a confirmation from the hardware team
   should replace the inference before implementation. The M7 dummy cannot
   settle this (§11.2).
2. **Does the M7 echo exactly once per transmitted frame?** §6.1's residual-risk
   mitigation depends on it. This is observed behavior, not documented behavior.
   Worth confirming in the same conversation that carries the §5 dummy request —
   the existing task
   `.claude/tasks/2026-08-20_confirm-m7-echo-root-cause-with-firmware-owner.md`
   is already open with that owner.
3. **Who owns the M7 change, and on what timeline?** §5 is a request to another
   team. Discovery can be implemented and host-tested without it, but no bench
   evidence exists until the dummy responder lands.
4. **§4.2 binds real Secondary firmware too.** The `Channel #`-selects-the-block
   rule is not in the CAN workbook. If real Secondaries are already specified to
   work some other way, this decision must change before it is built into the
   M7 dummy — otherwise the dummy trains the Primary on a protocol real hardware
   will not speak.
