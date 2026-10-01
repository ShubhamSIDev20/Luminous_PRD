# BM Program Frame Format V3.2

**Source:** `BM Documents/SW & HW Query-Responce/Excel/Program/Program Frame Format V3.2.xlsx`
**Interface:** Web Application (SW) ↔ BTS Hardware (HW)
**Start byte:** `0xBB`
**Supersedes:** `bm_program_v3.1.md`

## Packet Structure

`Start | Device Number | Circuit Number | Query ID | [Payload] | CRC (2 Bytes)`

Value `0x01` = OK/Yes, `0x00` = Fail/No

## Handshake Sequence

| Q# | Direction | Description | Query ID |
|----|-----------|-------------|----------|
| Q1 | SW→HW | Is HW ready to accept program data? | `0x01` |
| Q2 | SW→HW | Send Program Metadata/Information | `0x02` |
| Q3 | SW→HW | Send number of program data packets | `0x03` |
| Q4 | SW→HW | Send actual program step data | `0x04` |
| Q5 | SW→HW | Read Program metadata saved on HW | `0x05` |
| Q6 | SW→HW | Read Program saved on HW | `0x06` |
| Q7 | SW→HW | Send number of CAN DBC packets | `0x07` |
| Q8 | SW→HW | Send CAN DBC data | `0x08` |
| Q9 | SW→HW | Live step update — amend the currently executing step | `0x09` |
| Q10 | SW→HW | **Jump to an arbitrary program step** ⚠ New in V3.2 | `0x0A` |

## Program Metadata Payload (Q2 / Q5)

| ID | Parameter | Format |
|----|-----------|--------|
| 1 | Program Name | ASCII string, max 15 bytes, length-prefixed (1 byte) |
| 2 | Program Version | ASCII fixed 11 bytes, e.g. `999.999.999` |
| 3 | Host IP Address | 4 bytes hex (e.g. 192.168.100.14 → `C0 A8 64 0E`) |
| 4 | Host MAC Address | 6 bytes hex |
| 5 | Program Creation Date | 4 bytes Epoch time |

## Q3: Number of Packets
```
BB 01 01 03 00 03 -- --   (3 packets follow)
```

## Q4: Send Program Step Data
```
BB 01 01 04 LEN_HI LEN_LO [step bytes...] -- --
```
- Step packet format: refer to "Program Packet V0.10" document
- Response ACK per step: `BB 01 01 04 01 -- --`

## Q6 Response: Send saved program
```
BB 00 01 06 NUM_STEPS -- --   (header, then step data packets follow)
```

## Q9: Live Step Update

Amends the program step that is **currently executing**, without restarting it. The HW does **not**
store the amended step — it is validated, forwarded to the Secondary as `ps_program` **Q8**, and
discarded. `programDataBuffer` and EEPROM are untouched, so the resident program is unchanged.

```
Query:    BB DEV CKT 09 LEN_HI LEN_LO [one step packet: AA 55 ... 55 AA] CRC_LO CRC_HI
Response: BB DEV CKT 09 STATUS REASON CRC_LO CRC_HI
```

| Field | Size | Description |
|-------|------|-------------|
| `LEN_HI` / `LEN_LO` | 2 B | Step payload length, same encoding as Q4 |
| step packet | ≤ 255 B | Exactly **one** step, byte-identical encoding to what Q4 carries ("Program Packet V0.10") |
| `STATUS` | 1 B | `0x01` = applied by the Secondary, `0x00` = rejected |
| `REASON` | 1 B | `0x00` on success; see the table below on rejection |

### Q9 rejection reasons (`REASON`)

| Value | Meaning | Raised by |
|-------|---------|-----------|
| `0x00` | Applied by the Secondary (`STATUS` = `0x01`) | — |
| `0x01` | Program is idle, or awaiting operator action (interrupt / error / message wait) | Primary |
| `0x02` | Step number is not the currently executing step | **Primary only** |
| `0x03` | Operator change not permitted — parameters only | **Primary only** |
| `0x04` | Operator not eligible for live update | Primary |
| `0x05` | Secondary NACK (**any** cause), or Primary↔Secondary link failure | Primary |
| `0x06` | Malformed step packet | **Primary only** |
| `0x07` | A previous live update is still in flight — retry after the pending response | Primary |

> `0x02`, `0x03` and `0x06` can only ever originate on the Primary: the `ps_program` Q8 ACK/NACK
> frame carries a single ACK/NACK byte and **no reason field**, so every Secondary-side rejection
> reaches the Web App as `0x05`.
>
> **For the Web App:** treat `0x05` as "rejected by the Secondary — reason not available on the
> wire", not strictly as a link failure. The most common real cause is that the step advanced
> between the operator pressing apply and the frame arriving.
>
> `0x07` means the request was not evaluated at all. It is safe and correct to re-send the same
> amendment once the outstanding response arrives.

### Q9 eligible operators

A live update may change only the step's **parameters** — nominal value(s), cutoff conditions,
registration parameters. The operator byte (step-data offset 8) must match the resident original.
Eligibility is an **allow-list**, so any operator added in future is rejected until explicitly listed:

| Operator | Opcode | | Operator | Opcode |
|----------|--------|---|----------|--------|
| `CC_Chg` | `0x01` | | `CP_DChg` | `0x06` |
| `CV_Chg` | `0x02` | | `CCCV_DChg` | `0x07` |
| `CP_Chg` | `0x03` | | `CV_DChg` | `0x13` |
| `CCCV_Chg` | `0x04` | | `PAU` | `0x08` |
| `CC_DChg` | `0x05` | | | |

Everything else is rejected with `REASON 0x04` — notably `TABLE` (`0x12`), which carries row-pull
state, and `BEG` (`0x0D`) / `CYC` (`0x0C`) / `GOTO` (`0x09`), which are structural and would desync
the `ps_program` Q6 cycle table.

### Q9 notes

- **No Q1/Q2/Q3 handshake.** No metadata, no packet count, and no `readyForPrgData` gate — that flag
  is `false` while a program runs, by design. Q9 is gated on the opposite condition.
- `LEN` must describe exactly one step. Two or more steps in the payload → `REASON 0x06`.
- The step packet's 4-byte **next-packet offset** at `[2..5]` has no absolute meaning in a
  standalone one-step payload. For Q9 it must be either **the payload length** or
  **`TERMINATOR_INDEX` (`0xFFFFFFFF`)**; anything else is `REASON 0x06`.
- A **paused** program is accepted as well as a running one.
- The response is **deferred**: the Primary answers only after the Secondary has ACKed, NACKed, or
  the retries have been exhausted. Worst-case latency is `RESPONSE_TIME_OUT` × `BTS_SEC_RETRY_COUNT`.
- A power fail **discards** the amendment: resume replays the original step from EEPROM.
- Step elapsed time and accumulated step capacity/energy are **preserved** across the amendment.

---

## Q10: Jump to Program Step ⚠ New in V3.2

Ends the step the hardware is currently executing and continues the program from an **arbitrary step
number**. The resident program is **not modified** — this changes only *where execution is*, never
*what the program contains*.

```
Query:    BB DEV CKT 0A STEP_HI STEP_LO CRC_LO CRC_HI
Response: BB DEV CKT 0A STATUS REASON CRC_LO CRC_HI
```

| Field | Size | Description |
|-------|------|-------------|
| `STEP_HI` / `STEP_LO` | 2 B | Target step number, **big-endian**, 1-based. Same numbering the `0xCC` measured-parameters frame reports at payload offset 0–1. |
| `STATUS` | 1 B | `0x01` = jump performed, `0x00` = rejected |
| `REASON` | 1 B | `0x00` on success; see the table below on rejection |

The frame is 8 bytes in both directions. The response shape is identical to Q9's.

### Q10 rejection reasons (`REASON`)

| Value | Meaning | Raised by |
|-------|---------|-----------|
| `0x00` | Jump performed (`STATUS` = `0x01`) | — |
| `0x01` | No program is running or paused on this circuit | **Primary only** |
| `0x02` | Target step number does not exist in the resident program (`0`, or greater than the number of steps loaded) | **Primary only** |
| `0x03` | Malformed frame (wrong length) | **Primary only** |
| `0x04` | Secondary NACK (**any** cause), or Primary↔Secondary link failure | Primary |
| `0x05` | A previous jump is still in flight — this request was **not evaluated**; retry after the pending response | Primary |

> **Corrected 2026-09-15 (post-implementation review).** `0x01` was previously documented as
> "Primary **or Secondary**" and as covering operator-wait and power-fail-resume. It cannot: the
> `ps_program` Q9 ACK/NACK carries **no reason field**, so *every* Secondary-side rejection reaches
> the Web App as **`0x04`** — including all of these, which only the Secondary can see:
>
> | Secondary-side rejection | Reaches the Web App as |
> |---|---|
> | Channel is paused, or awaiting operator action (interrupt / error / message wait) | `0x04` |
> | A power-fail resume is in progress | `0x04` |
> | A step request is already in flight (the channel is mid step-change) | `0x04` |
>
> **For the Web App:** treat `0x04` as "the hardware refused — try again in a moment", not as a link
> failure. The two most common real causes are a paused channel and a jump that landed exactly on a
> step boundary; both clear on their own and a retry normally succeeds.
>
> `0x05` means the request was not evaluated at all. Re-sending the same jump once the outstanding
> response arrives is safe.

### Target step eligibility — there is no allow-list

**Any step that exists may be a jump target**, including `BEG`, `CYC`, `GOTO`, `TABLE`, `SET`, `REG`
and every regulating operator. This is a deliberate difference from Q9.

The reason: the hardware already supports landing on any step. The `GOTO` operator inside a program
does exactly this, and a jump is the same action triggered from outside the program instead of from
within it. Restricting the target would make the Web App button *less* capable than a `GOTO` step
the operator could have written into the program themselves.

The Primary validates only that the step **exists**.

### Effect on the current step

The currently executing step is **ended**, not suspended:

| State | Effect |
|-------|--------|
| Step elapsed time | reset — the target step starts from zero |
| Step capacity (Ah) / energy (Wh) | reset |
| Cutoff debounce counters | reset |
| End-of-step registration record | **written** for the truncated step, so the data file shows where and when execution left it |
| Program elapsed time | **continues** |
| Program total Ah / Wh | **continues** |
| Registration type / parameters carried from an earlier `SET` / `REG` / `TABLE` step | **continues** |

This is the opposite of Q9, which preserves step accumulators. Q9 amends a step in place; Q10 ends
one step and starts another.

### Effect on cycles (`BEG` … `CYC` loops)

A jump behaves **exactly as a `GOTO` operator does** — the loop rules are not special-cased. The
Secondary derives every loop decision by comparing the current step number against the cycle table,
so it never holds a "path taken" stack that a jump could corrupt.

The resulting iteration-counter behaviour:

| Case | Example | Counter behaviour |
|------|---------|-------------------|
| Jump **within** the loop you are already in | `7 → 5`, loop is steps 3…11 | Unchanged — you are still inside the loop |
| Jump **onto the `BEG` step** of a loop | `7 → 3` | **Reset to zero** — the loop restarts with its full repeat count |
| Jump **out of** a loop | `7 → 13` | The abandoned loop keeps its partial count. Harmless unless the program later re-enters that loop somewhere other than its `BEG` |
| Jump **into the middle** of a loop from outside | `1 → 7` | Runs to `CYC` and loops normally, using whatever count was left behind — so it may complete fewer than the full repeat count |

A loop that completes normally already zeroes its own counter, so a stale non-zero count is only
reachable by deliberately abandoning a loop part-way.

> **Operator-facing rule, for the Web App UI: to restart a loop, jump to its `BEG` step.**

### Q10 notes

- **No Q1/Q2/Q3 handshake, and no `readyForPrgData` gate** — that flag is `false` while a program
  runs. Q10, like Q9, is gated on the opposite condition.
- **Jumping to the step already running is accepted and does nothing** — `STATUS 0x01`,
  `REASON 0x00`, with no frame sent to the Secondary and no step restart. It is *not* a "restart
  this step" command. (To restart the current step, stop and re-start the program, or jump away and
  back.)
- **A paused program REJECTS the jump** (`REASON 0x04`). ⚠ **Changed 2026-09-15** — V3.2 originally
  documented "a paused program accepts the jump and stays paused". That is not implementable on this
  hardware and never worked: `bm_control` Q3 Pause reaches the Secondary as an *interrupt*, which
  puts the channel in the same operator-wait state (`csInt`) that the next bullet rejects. The two
  rules contradicted each other. **The operator must Continue first, then jump.**
- **Operator-wait states reject the jump** — user interrupt, error wait and message wait each hold a
  saved step number that the Continue command restores, and a jump would have to unwind it. The
  operator must Continue or Stop first. This is a deliberate V3.2 limitation. Because only the
  Secondary can see these states, they arrive as `REASON 0x04`, not `0x01`.
- **A jump landing exactly on a step boundary is rejected** (`REASON 0x04`) — if a step request is
  already in flight, accepting would put a second one on the link and start the wrong step. It is
  transient: retrying a moment later succeeds. The Web App should surface this as "try again"
  rather than as an error.
- The response is **deferred**: the Primary answers only after the Secondary has ACKed, NACKed, or
  the retries have been exhausted, so `STATUS` reflects the real outcome rather than "accepted for
  forwarding". Worst-case latency is `RESPONSE_TIME_OUT` × `BTS_SEC_RETRY_COUNT`.
- **Only one jump may be in flight at a time.** A second Q10 arriving while the first is unresolved
  is rejected with `REASON 0x05` and is *not* queued — queuing could land the operator on a step they
  did not choose last.
- **Q9 and Q10 share nothing but their response shape.** They use independent in-flight slots; a
  pending Q9 does not block a Q10 or vice versa. Sending both at once is legal but pointless, since
  the jump ends the step the amendment was aimed at.
- A power fail during a jump **discards** it: resume replays the step recorded in EEPROM, which is
  whichever step the Secondary last requested.
- **Skip / repeat / restart come free.** "Skip this step" is a jump to `current + 1`; "restart the
  program" is a jump to step `1`; "restart this loop" is a jump to the loop's `BEG`. All three are
  Web App-side arithmetic on the step number and need no additional query.

### Q10 examples

Jump to step 20 on device 1, circuit 1:
```
Query:    BB 01 01 0A 00 14 CRC_LO CRC_HI
Response: BB 01 01 0A 01 00 CRC_LO CRC_HI     (jumped)
```

Jump rejected because step 500 is not in the program:
```
Query:    BB 01 01 0A 01 F4 CRC_LO CRC_HI
Response: BB 01 01 0A 00 02 CRC_LO CRC_HI     (no such step)
```

Jump rejected because a previous jump has not yet been answered:
```
Query:    BB 01 01 0A 00 14 CRC_LO CRC_HI
Response: BB 01 01 0A 00 05 CRC_LO CRC_HI     (busy — retry after the pending response)
```

---

## Notes

- Firmware module: PRG in `bts_app.c`; Q9/Q10 gates and deferred responses in `networkDataHandler.c`
- Program packet encoding per BM4 operator semantics (see `project_bm4_bts600_semantics.md`)
- **V3.2 vs V3.1:** added **Q10** (jump to program step, `0x0A`) + Response10 with the
  `STATUS`/`REASON` pair. Q1–Q9 and the step packet format are **unchanged**. Paired with
  `ps_program_v1.8.md` (Q9); Primary and Secondary are flashed **lockstep**.
  Design: `docs/superpowers/specs/2026-09-15-jump-to-program-step-design.md`
- **V3.1 vs V3.0:** added Q9 (live step update, `0x09`) + Response9, and tabulated the pre-existing
  Q7/Q8 CAN DBC queries. Design:
  `BTS_Primary_SOM/docs/2026-09-10-dynamic-step-update-design.md`
- The Web App learns the current step number from the `0xCC` measured-parameters frame
  (`bm_measured_param_v5.2.md`, payload offset 0–1). After a successful jump the reported step
  number changes on the next 1 Hz measured-data tick, not instantly with the Q10 response.

---

> **WebAppME implementation note (2026-09-16, updated 2026-09-21): both Q9 and Q10 are implemented**,
> merged together on `feature/program-step-control`.
>
> **Q9 (Live Step Update)** — `Models/Enums/CircuitEnums.cs` (`ProgramDataQuery.LiveStepUpdate`,
> `LiveStepUpdateReason`), `DecoderService.TryDecode` + `BuildLiveStepUpdatePacket`,
> `ChannelCommandHandler.LiveStepUpdateAsync`, `LiveStepUpdateValidator` (client-side echo of the
> Primary-only REASON checks), exposed via the Dashboard's per-circuit context menu ("Edit Current
> Step" → `LiveStepUpdateDialog.razor`, which reuses the existing program step-row grid in a
> session-only mode — operator locked, nothing persisted). `HardwareSimulator`:
> `CoreEngine.apply_live_step_update` + a dedicated `0x09` branch in `simulator.py`'s
> `handle_program_command`.
>
> **Q10 (Jump to Program Step)** — `Models/Enums/CircuitEnums.cs` (`ProgramDataQuery.JumpToStep`,
> `JumpToStepReason`), `DecoderService.TryDecode`, `ChannelCommandHandler.JumpToStepAsync`, exposed
> via the Dashboard's per-circuit context menu ("Jump to Step..." → `JumpToStepDialog.razor`).
> `HardwareSimulator`: `CoreEngine.jump_to_step` + `simulator.py`'s `_handle_jump_to_step`.
>
> Q1-Q8 already existed for the program download flow. The simulator only models `device.paused`
> (the `0xEE` PAUSE command) as a rejection state for either query — it does not model the real
> hardware's separate interrupt/error/message-wait states, so it can only ever produce the
> "not running/idle" REASON, never the real hardware's Secondary-side-rejection REASON.
>
> See `docs/PROTOCOL.md` §6.6 (Q9) and §6.7 (Q10) for the implemented byte-level detail, and
> `.claude/tasks/2026-09-16_jump-to-program-step.md` /
> `.claude/tasks/2026-09-17_jump-to-step-simulator-support.md` for Q10's implementation record.
