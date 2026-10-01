# T-24: Full battery-test flow, hardware run, against a real Secondary over RPMsg
> Created: 2026-08-11 | Status: Active
> File: `tasks/2026-08-11_full-battery-test-hardware-run.md`

---

## Description
Full battery-test flow, hardware run, against a real Secondary over RPMsg —
partially proven 2026-08-19 (registration + CPU3 pin live), not yet
end-to-end.

## Why / Context
This is the umbrella hardware-verification task for the entire BTS 4-thread
base. Nothing in sessions #4 through #11 is proven end-to-end on real
hardware until this runs.

### What to expect on the hardware run

> ℹ️ **Admission control is live (session #6, ADR-17).** The run below works for
> the circuit passed via `--secondary`/`--channel` — `0x11` by default. Frames for
> any other circuit are dropped with `route: circuit 0xNN … is not registered`,
> and a startup INFO line states the policy. **The Web Application conforms to
> this board's CircuitID** (developer decision, T-33 closed 2026-08-12), so this
> is expected behaviour rather than a risk. If a "not registered" WARN does appear
> on the run, that line names the mismatch precisely.
>
> ⚠️ **Changed by sessions #7, #8 and #9 (ADR-18 → ADR-23).** The board now REPLIES to
> `0xAA` Q5, all six `0xEE` commands and `0xBB` Q1/Q3/Q4. **The `0xEE` Start
> BAD_CRC from the 2026-08-12 run is fixed** (T-41 — Q1 carries a Session ID).
> Watch for the `RECOVERED, but the length table … needs fixing` WARN: each
> occurrence is another wrong layout entry naming itself (T-44).
>
> 🆕 **Session #9 adds two visible changes to this run.** A `0xCC` frame now arrives
> **immediately after registration**, before any program is sent (ADR-23). And the
> battery record is **40 bytes with all 12 fields**, so a payload under 40 is
> refused and NACKed rather than half-stored (ADR-21).

```
  registration banner -> state: IDLE
       -> NEW (#9): "demo: circuit 0x11 - sent one 86-byte 0xCC frame to UDP 10000
                     after registration (step 1, 25.0 C, all else zero)"
          One live frame should appear on the Web App here, before anything else.
  Web App sends 0xBB Q1 is-ready
       -> "route: circuit 0x11 - answering 0xBB Q1 with value 0x01 (OK/Success)"
  (optionally 0xBB Q3) -> "Web Application announces N program packet(s)" + ack
  send 0xBB Q4 program packets, final step nextIndex = FFFFFFFF
       -> one "answering 0xBB Q4 with value 0x01" per packet
       -> "data: circuit 0x11 - program COMPLETE: N step(s), M bytes"
  send 0xAA Q5 battery data (46 bytes: 4 + 40 + 2)
       -> NEW (#9): "data: circuit 0x11 - battery stored (40 bytes): 1.000 Ah,
                     1 cells, gassing 1.00 V, max 1.00 V, nominal 1.00 A, ..."
       -> NEW (#9): "data: circuit 0x11 -   charge factor 1 %, impedance 1.00 Ohm,
                     break 1.00 V, nominal 1.00 V, energy density 2.00 Wh/Kg,
                     battery ID 1"
       -> an 0xAA ack
  send 0xEE Q1 Start (10 bytes, carries the Session ID)
       -> "core: control Start for circuit 0x11 ..."
       -> "core: circuit 0x11 - session ID 0x........ (logged, not stored)"
       -> an 0xEE ack, meaning QUEUED not completed
       -> NEW (#9): "core: circuit 0x11 - battery data received: ..." x2 lines,
                     all 12 fields
       -> "PROGRAM STEP 1 EXTRACTED" banner + hex dump
       -> ⚠️ CHANGED #10: the 1Hz demo ramp is GONE. me_execute_program() now
          starts a REAL step_engine (ADR-27). For a SET->CCChg->STOP program:
          "core: circuit 0x11 - execution started, engine state RUNNING"
          -> a CAN_TX SET_VALUES logged by can_mgr (fabricated feedback echoes
             the commanded current, voltage drifts up slowly)
          -> a 0xCC frame every ~1s carrying that fabricated current/voltage
             (not 1.0->60.0A - depends on the program's own nominal current)
          -> when the TIME cutoff fires: another CAN_TX (CMD_STO) + a final
             Idle 0xCC, circuit -> STOPPED
       -> a genuinely offline/unresponsive channel would show 3x
          "missed CAN response" WARN then "marking channel offline" ERROR
          (should not happen with can_mgr's fabricator - flag if it does)
  Ctrl-C -> "queues:" lines; any non-zero drop count is the thing to report
```

**Three log lines from #9 that mean something went wrong:**

| Line | Meaning |
|---|---|
| `battery payload of NN bytes is shorter than the 40 that bm_config_v6.0.md Q5 defines` | The Web App is sending the legacy short form. Q5 is NACKed and nothing is stored (T-46). |
| `battery payload is NN bytes, M more than the 40 this build knows` | The protocol moved past V6.0 (T-46). |
| `impedance` / `energy density` reading absurdly (≈1.4e-43 or ≈1e9) | The Web App is sending these two as `uint32`. They are **confirmed `float`** as of 2026-08-12 (ADR-20 §9.3, T-47), so this now means the *sender* is wrong, not the parser. |

## Progress Log
- **2026-08-19** (Session #12): Partially proven — registration and CPU3 pin
  confirmed live on hardware `172.16.18.167`. Full battery-test flow over the
  real RPMsg CAN link still not run end-to-end. Reworded from earlier phrasing.
- **2026-08-11**: Task opened as the umbrella hardware-verification item for
  the 4-thread BTS base.

## Related
- Depends on: T-62 (multi-channel hardware verification), T-60 (isolcpus provisioning)
- Relates to: ADR-17, ADR-18, ADR-19, ADR-21, ADR-23, ADR-27, ADR-29, ADR-30
