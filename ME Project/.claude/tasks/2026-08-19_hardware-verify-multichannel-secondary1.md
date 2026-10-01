# T-62: Hardware-verify Secondary 1 multi-channel (1-4) registration + CAN-FD SET_VALUES coalescing
> Created: 2026-08-19 | Status: Active
> File: `tasks/2026-08-19_hardware-verify-multichannel-secondary1.md`

---

## Description
Hardware-verify Secondary 1 multi-channel (1-4) registration + CAN-FD
SET_VALUES coalescing on board `172.16.18.167` — both builds clean, host
tests passing, but no channel traffic has run on real hardware yet.

## Why / Context
ADR-32 (T-61) implemented independent per-channel registration and CAN-FD
SET_VALUES shadow-buffer coalescing to prevent one channel's frame from
zero-stomping (CMD_STO-ing) another channel's setpoint on the shared 64-byte
block. Both builds are clean and 229 host checks pass, but nothing here has
been proven against a real Secondary yet.

## Acceptance Criteria
Checklist: `Docs/specs/2026-08-19-multi-channel-secondary1-plan.md` Task 5
Step 3:
- [ ] Register with `--channels 1,2,3,4`
- [ ] Send 4 programs
- [ ] Stagger-start them
- [ ] Confirm one channel's activity never resets another's setpoint via the
      RTT/CAN logs
- [ ] Stop one channel mid-run and confirm only its slot reports STO afterward

## Progress Log
- **2026-08-19**: Task opened as the hardware-verification follow-up to T-61 /
  ADR-32 (PR #7 merged to main).
- **2026-08-20 (session #15)**: First real hardware run with the M7 link
  actually live (`--device /dev/ttyRPMSG30` was missing before). Registered
  `--channels 1,2,3,4`, sent and stagger-started 4 programs — confirmed via
  RTT/CAN logs that the shadow buffer correctly carries all 4 channels'
  distinct setpoints in one merged frame (ADR-32/35), and that CAN-FD
  traffic collapses to one physical frame per block (ADR-35). Found and
  fixed 3 more bugs surfaced by this run: step-timing drift (ADR-36),
  repeated SET_VALUES (ADR-37), and the M7 echoing our own TX as if it were
  a Secondary reply (ADR-38). **Still open from the acceptance criteria:**
  a targeted "stop one channel mid-run, confirm only its slot reports STO"
  check — Stop was issued for two channels near the end of one run but not
  specifically verified against this exact acceptance bullet.

## Related
- Depends on: T-61 (done)
- Relates to: ADR-32, ADR-28, T-24
