# Session #9: Battery config spec, 40-byte battery record, 0xAA length table
> Date: 2026-08-12T17:40+05:30 | Agent: Claude Code (Opus 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-12_1434_ee-session-id-bug-crc-frame-delimiter.md](2026-08-12_1434_ee-session-id-bug-crc-frame-delimiter.md) — the `0xEE` Session ID fix and CRC-derived frame boundaries (ADR-19).

> Session #9 | 2026-08-12 17:40–21:25 IST

---

## 🎯 Goal This Session

Three requests, plus one that emerged mid-way:

1. **Store the battery data** per Secondary/Channel in the Data Manager, "the same
   way you store program data" — needed later for program execution.
2. **Sync the documents** with the code.
3. **A temporary dummy `0xCC` frame** after successful registration, because the
   CAN Manager produces nothing. The developer supplied the 86 bytes.
4. *(mid-session)* **Read `Config Data Frame Format V6.0.xlsx` and write it into
   `Ref Docs/` as Markdown** — supplied after I reported that no `0xAA`
   specification existed anywhere.

## ✅ Done This Session

### The finding that reframed request 1

Battery storage **already worked**. The path existed end to end and the
developer's own hardware log had already printed
`data: circuit 0x11 - battery stored: 1.0 Ah, 1 cells, 1.0 V max`.

The real defect was narrower and worse. Decoding the captured frame:

| | |
|---|---|
| Frame on the wire | 46 bytes = 4 header + **40 payload** + 2 CRC (CRC `0x98E8` BE, verified) |
| `ME_BATTERY_PAYLOAD_LEN` | **22** |
| Result | **18 payload bytes silently discarded on every battery packet** |

And silently *by design*: `me_battery_parse()` documented itself as tolerating a
longer payload "so a protocol revision does not break it". **A defensive rule had
become a data-loss rule** the moment the Web Application sent more than the legacy
firmware did. No warning, no log, nothing.

I reported this before writing any code, since it contradicted the premise of the
request. That is what produced request 4.

### T-49 — `Ref Docs/bm_config_v6.0.md` (ADR-20)

All five sheets transcribed. It resolved three things the codebase had flagged as
guesses:

- The **40-byte** battery payload, every field named, and §5.4 confirms all twelve
  boundaries against the captured frame
- The **7-byte `0xAA` ack shape**, which `ack_frame.c` called *"the weakest claim
  in this file"* — the guess was right
- **Frame lengths for all six** circuit-scoped queries

Six discrepancies **recorded rather than corrected** (§9), on the ADR-4 principle
that a clean document hides where the risk is. The two that mattered: Impedance and
Energy Density are the only 4-byte battery fields the sheet does *not* annotate
"It will be in float" and its samples decode only as `uint32` — but the wire says
float, and **the developer confirmed float the same day**, so §9.3 is now marked
resolved and the spreadsheet samples are simply wrong (T-47 closed, no code change);
and **Q7–Q10 are broadcast frames with a 2-byte header** that every `0xAA` path here
would mis-parse (T-48, still open).

Every arithmetic claim was verified by computation rather than transcribed on
faith. One hedged number — a 111-byte Factory total — was **wrong**; it is 131, and
the working is now shown in the document.

### T-50 — the complete battery record (ADR-21)

`me_battery_t` 7 → 12 fields. `circuit_store.c` needed **no change** because it
holds the struct by value — the payoff of storing a struct rather than a blob.

A payload under 40 is now **refused, not half-parsed** (the developer's choice when
asked): a record with `valid = true` and a zeroed nominal voltage would let a test
run against nonsense, surfacing as a wrong *result* rather than an error.

### T-51 — `0xAA` length entries, and the regression they caused (ADR-22)

Filling in the `0xAA` table **broke four existing tests, and one of them mattered**:

```
FAIL [router: a coincidental short CRC match must not truncate a frame]
     expected 0xC (12), got 0x6 (6)
```

That is the truncation guard I wrote in session #8, and it used a `0xAA` frame as
its vehicle. ADR-19's whole-read protection was gated on `layout == 0`; once Q5 had
a 46-byte entry, a **short** `0xAA` frame had a layout *larger than what arrived*,
so step 1 couldn't use it, the gate excluded it from the whole-read step, and it
fell into the scan and truncated at the planted CRC.

**Adding a correct table entry made a different frame parse worse.** The gate is
gone; the whole-read step now runs for every start byte. Two tests changed vehicle
to `0xA0`, which is now the genuinely lengthless start byte, and a new test names
the regression directly.

### T-52 — the post-registration `0xCC` frame (ADR-23)

The developer's pasted frame was **85 bytes and matched no CRC** — a zero lost to a
line wrap. Brute-forcing the missing byte against the given CRC gave 53 candidates
that all decode identically, so the intent was unambiguous: 86 bytes,
`step_number = 1`, `temperature = 25.0 °C`, everything else zero, CRC `0xF261` BE
confirmed.

Two structural calls worth recording:

- The byte layout went in **`realtime_frame.c`**, not `demo_realtime.c`, because
  that module is PURE and in the host build — which is the only way the exact 86
  bytes could be proven on the laptop rather than discovered on hardware.
- `me_demo_send_post_registration()` is the **only** function in `demo_realtime.*`
  called from the **Communication** thread. It has its **own** buffer; sharing the
  emitter's `s_tx` would have been a silent data race against `me_demo_service()`.

## 📁 Files Changed This Session

| File | What |
|---|---|
| `Ref Docs/bm_config_v6.0.md` | **NEW** — the project's first `0xAA` specification |
| `src/proto/battery_frame.h/.c` | 22 → 40 bytes, 12 fields, provenance rewritten |
| `src/proto/realtime_frame.h/.c` | `me_realtime_pack_post_registration()` |
| `src/proto/frame_router.c` | `0xAA` length table; **whole-read gate removed** |
| `src/proto/proto_defs.h` | `0xAA` lengths; note on why Q7–Q10 are unnamed |
| `src/threads/demo_realtime.h/.c` | post-registration frame + its own buffer |
| `src/threads/comm_thread.c` | one call at the end of `do_registration()` |
| `src/threads/data_mgr.c` | reject < 40, WARN > 40, log all 12 fields |
| `src/threads/core_logic.c` | log all 12 fields |
| `tests/test_battery_frame.c` | rewritten — 8 cases incl. the captured frame |
| `tests/test_realtime_frame.c` | +2 cases — the exact 86 bytes |
| `tests/test_frame_router.c` | +3 cases; 4 updated (vehicle `0xAA` → `0xA0`) |
| `Docs/ME_Primary_BTS_Block_Diagram.md` | §4.2, §6.6.1, open items 1 + 5–9, decisions 14–21 |
| `Docs/ME_Primary_Implementation_Reference.md` | 5 module entries, length table, open items 15–19 |
| `.claude/DECISIONS.md` | ADR-20…23; **ADR-19 amended** |
| `.claude/TASKS.md`, `SESSION.md`, `ARCHITECTURE.md`, `CODEBASE_MAP.md` | synced |

## 💡 Discoveries / Gotchas

- **A "tolerant" parser is a lossy parser.** `me_battery_parse()` accepting a
  longer payload was written as forward-compatibility and became silent data loss.
  It now WARNs when the payload exceeds what it knows.
- **Adding a correct entry to a lookup table can break an unrelated code path.**
  See T-51. The general form: a layout that *overshoots* what has arrived tells you
  nothing about where the frame ends, so it must not disable a safety net.
- **The developer's premise was wrong and reporting it was the highest-value act
  of the session.** Had I implemented "store the battery data" as asked, I would
  have re-implemented working code and left the 18-byte loss in place.
- Verify pasted hex before building on it. The `0xCC` frame was one byte short;
  computing the CRC caught it in seconds.
- `me_demo_init()` runs at `main.c:195`, **before any thread starts**, so the
  Communication thread's first registration cannot race it. Checked, not assumed.

## 🔬 Verification

| Layer | Result |
|---|---|
| Host protocol tests | **158 checks run, 0 failed** (was 149) |
| Cross-build | Clean under `-Werror`, static `ELF64 AArch64` |
| Hardware | ❌ **Not run.** Only `deploy.ps1` proves any of this (ADR-3) |

RED was watched properly: 157 checks / **33 failed**, tests *executing* rather than
failing to compile — the router returning `0` where 6/46/10 was expected, the five
new battery fields reading `0`, and the poison byte `0xA5` still in the frame
buffer. One honest caveat: `test_config_write_battery_splits_a_coalesced_read`
**passed** in the RED run, because ADR-19's scan already resolved that frame — it
is a characterization test, not proof of new behaviour.

## 🚫 Blocked
Nothing. Only the hardware run remains (T-24), and only the developer can do it.

## 🔄 In Progress
Nothing — all four requests landed this session.

## 🔜 Next Agent Should Do

1. Nothing until the developer runs `deploy.ps1`. **Sessions #4, #6, #7, #8 and #9
   are all unverified on hardware.**
2. On that log, grep for: `shorter than the 40 that bm_config_v6.0.md Q5 defines`
   (T-46), `RECOVERED, but the length table … needs fixing` (T-44), and the
   post-registration `demo:` line (should appear **before** any program transfer).
3. Sanity-check the logged `impedance` and `energy density` values. Impedance and
   Energy Density are confirmed `float` (T-47 closed, 2026-08-12) — absurd numbers
   (≈1.4e-43 or ≈1e9) now mean the **Web Application** is still sending `uint32`,
   not that the parser guessed wrong.
