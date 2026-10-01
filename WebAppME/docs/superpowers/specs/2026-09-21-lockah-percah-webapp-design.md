# LOCKAh / PERCAh — WebAppME implementation design

**Date:** 2026-09-21
**Status:** implemented 2026-09-21 (WebAppME). Firmware side out of scope — see §6.
**Scope:** `WebAppME` only (Program Editor UI, step encode/decode, validation). Firmware is out of
scope — `BTS_SEC_FW_V201` is not part of this repo; see §6.
**Branch:** `feature/lockah-percah-operator` (off `feature/program-step-control`)
**UI mockup (interactive):** https://claude.ai/artifact/89PmuFKRzHfXY28mswMy4H
**Upstream sources:**
[`2026-09-17-percah-ahdef-design.md`](2026-09-17-percah-ahdef-design.md) (product/wire design),
[`program_packet_v0.13.md`](../../PROTOCOL.md) §14 (worked bytes) — **this file is normative for
the WebAppME opcode; where it disagrees with those two on the LOCKAh byte value, this file wins**
(see §1).

---

## 1. The opcode conflict — why this document exists

Both upstream sources assume operator byte `0x14` is free in the target firmware (held only by an
unused `IDLE` enum slot). It is **not** free in WebAppME's own operator space
(`Components/UI/Program/OperatorConstants.cs`): `0x14` is `PRODUCER` (a real, shipped operator —
"Select program") and `0x15` is `CC_RECHG` (added 2026-09-15, see
`.claude/tasks/2026-09-15_cc-rechg-opcode.md`). Both are live and referenced throughout
`NominalConfig`, `LimitActionAllowed` and the operator picker.

**Decision, confirmed by the user 2026-09-21: `LOCKAh = 0x16` (decimal 22) in WebAppME.**
`PERCAh` keeps `0x3E` as both upstream docs specify — no conflict, `CutoffCondition` in
`Models/Enums/ProgramEnums.cs` currently ends at `StepEnergy = 0x3D`.

Every worked byte from the upstream docs that shows `14` at the operator-byte offset must be
recomputed with `16` for WebAppME. §5 below redoes the reference program's step-3 packet with the
correct byte so there is one place that's actually right for this codebase.

Whoever owns the Secondary firmware for this product needs to know the byte moved — that repo
isn't here, so this doc can't update it; flagged for the developer to route.

---

## 2. What the feature does (unchanged from the upstream spec)

| Name | Kind | WebAppME byte | Role |
|---|---|---|---|
| `LOCKAh` | operator | `0x16` | Stores the present Ah counter × a multiplier as a 100% reference. Ends its step immediately. No cutoff conditions, no registration block — same minimal shape as `GOTO`. |
| `PERCAh` | cutoff condition | `0x3E` | Ends a regulating step when the Ah counter reaches a percentage of that reference. Never valid on `PAU` or `LOCKAh`. |

A program needs both: `LOCKAh` writes the number, `PERCAh` reads it.

---

## 3. UI design

### 3.1 Reference program in the step grid

The existing Program Editor grid (Step / Label / Operator / Param Name / Nominal Value / Cutoff
Condition / Logic / Limit Value / Action / Registration) needs no new columns — both new pieces
fit existing ones. `LOCKAh`'s multiplier sits in **Nominal Value** like any operator's setpoint;
`PERCAh` sits in **Cutoff Condition** like `Voltage` or `Accumulated Capacity` does today.

```
 Step  Operator     Param Name           Nominal Value   Cutoff Condition   Logic  Limit Value    Registration
 ───── ──────────── ──────────────────── ─────────────── ────────────────── ────── ────────────── ─────────────
   1   SET                                Ah = 0                                                   STANDARD *
   2   CC DChg      Current               2.0 A           Voltage             <     3.2 V
   3   LOCKAh                             1.0              — (no cutoffs, no Action column) —
   4   SET                                Ah = 0                                                   STANDARD *
   5   CCCV Chg     Current / Voltage     1.6 A / 4.1 V   Current             <     1.0 A
   6   CC Chg       Current               1.0 A           PERCAh              >     130 PERCAh
   7   STO

 * auto-set: SET rows whose Nominal Value declares Ah/Wh now default Registration to STANDARD.
```

The full-color version, with per-operator category badges and inline "NEW · 0x16" / "NEW · 0x3E"
markers, is artboard **Main** in the mockup linked above.

### 3.2 Adding a LOCKAh step

Three interaction points, all built on mechanisms the editor already has — nothing here is new
UI infrastructure, only new data:

1. **Operator picker.** `LOCKAh` appears in the existing dropdown (`OperatorConstants.Operators`),
   grouped under "Other" (`Category = "control"`, same group as `PRODUCER`) — the picker's grouping
   is a fixed category list (`OperatorSelect.razor`'s `GetGroupedOperators`), so a new category
   name would have silently excluded the operator from the default browse view; its badge still
   gets a distinct teal color via `Color = "lock"`, which is independent of grouping. Selecting it
   enables a single Nominal Value field labelled "Multiplier" and disables the Cutoff Condition /
   Limit / Action columns for that row (`LimitActionAllowed[LOCKAH] = false` — the same mechanism
   that already disables those columns for `STO`/`SET`/`GOTO`).
2. **Nominal Value validation.** New floor: the multiplier must be **≥ 1.0** — stricter than the
   upstream spec's own `> 0` (D5/V9). This is deliberately the authoritative rule for WebAppME per
   the user's instruction; see §7. `0.8` is rejected inline with "Must be 1.0 or greater"; `1.1` is
   accepted with the field's existing inline-correction affordance.
3. **Limit Value unit entry.** Typing `130 PER` and pressing Tab completes to `130 PERCAh` — this
   is the editor's *existing* generic prefix-completion (`ValidationHelper.AutoCorrectUnit`'s
   fallback against `OperatorConstants.ValidUnits`), the same mechanism that already completes
   `V`/`min`/`hr` today. Adding `"PERCAh"` to `ValidUnits` is the entire change needed for this to
   work — no bespoke autocomplete code.

See artboard **NewOperator** in the mockup for the picker, both validation states, and the
before/after of the Tab-completion.

### 3.3 Why PERCAh can't reach PAU or LOCKAh through the UI

No extra validation code needed for either case — both are already structurally impossible:

- **`PAU`**: its Limit field is parsed by a dedicated time-only path (`ProcessPauLimit` /
  `ValidateLimit`'s `isPAU` branch) that only accepts values from `TimeUnits`. `PERCAh` is never
  added to `TimeUnits`, so it's simply not a legal token there.
- **`LOCKAh`**: `LimitActionAllowed[LOCKAH] = false` means the Limit/Cutoff/Action columns don't
  exist for that row at all — there's no field to type `PERCAh` into.

---

## 4. Encoding changes (WebAppME side)

| Change | File | Detail |
|---|---|---|
| New cutoff condition | `Models/Enums/ProgramEnums.cs` | `CutoffCondition.PercAh = 0x3E` |
| New operator | `Components/UI/Program/OperatorConstants.cs` | `LOCKAH = 22` (`0x16`); `Operators` entry; `LimitActionAllowed[LOCKAH] = false`; `NominalConfig.Configs[LOCKAH]` = one unitless "Multiplier" field; `"PERCAh"` added to `ValidUnits` |
| Nominal floor | `Components/UI/Program/ValidationHelper.cs` — `ValidateNominal` | New `LOCKAH` branch: reject float `< 1.0` |
| Limit floor | `Components/UI/Program/ValidationHelper.cs` — `ValidateLimit` | Reject `PERCAh` with value `≤ 0` |
| Cutoff byte | `Services/ProgramBuilder.cs` — `TryParseUnit` | `"percah" => CutoffCondition.PercAh` |
| LOCKAh packet | `Services/ProgramBuilder.cs` | New `ProcessLockAhOperator`: operator byte + float32 multiplier + end — mirrors `ProcessGotoOperator`'s shape (no cutoff-count byte, no registration byte) |
| SET global params | `Services/ProgramBuilder.cs` — `ProcessSetOperator` | Currently hardcodes the "No. of Global Limit Parameters" byte to `0x00` always — this is the real gap. Rewrite to detect `Ah=`/`Wh=`-named SET variables, emit a 5-byte block per one found (`0x26`/`0x2A` + float32), and write the real count. Any other variable name: unchanged, byte-identical to today (`N=0`) |
| Registration inheritance | `Services/ProgramBuilder.cs` — `ProcessSetOperator`, `FindNearestPrecedingSet` (new, shared with `ProcessRegOperator`) | When an `Ah`/`Wh` global param is present and `step.Registrations` is empty, inherit the nearest preceding SET step's Registrations — same rule REG already uses — falling back to `["STANDARD"]` only when there is no earlier SET |
| Dispatch | `Services/DecoderService.cs` — `FormatStepBytes` | New `case OperatorConstants.LOCKAH: ProgramBuilder.ProcessLockAhOperator(...)` |
| Decode parity | `Services/PacketAnalyzer.cs`, `PacketAnalyzerNoReverse.cs` | Mirror decode support so the packet-inspector view doesn't mis-read the new bytes |
| Simulator parity | `HardwareSimulator/program_decoder.py`, `simulator.py` | Register the new opcodes, following the `CC_RECHG` precedent |
| Docs | `docs/PROTOCOL.md` | Opcode table, cutoff-condition table, `SET` layout section |

---

## 5. Worked bytes — step 3 (`LOCKAh`), recomputed for `0x16`

Corrects the upstream doc's `14` at the operator-byte offset. Everything else in the reference
program (§7.2 of the design spec / §14.3–14.10 of the protocol notes) is unaffected — only this
one byte changes, and the packet stays 15 bytes.

```
0xAA 0x55 0x00 0x00 0x00 0x3A 0x00 0x03 0x16 0x3F 0x80 0x00 0x00 0x55 0xAA
   0    1    2    3    4    5    6    7    8    9   10   11   12   13   14
```

| Offset | Field | Hex | Decoded |
|---|---|---|---|
| 8 | Operator | `16` | `LOCKAh` — **0x16, not the upstream doc's 0x14** |
| 9–12 | Nominal Value (Multiplier) | `3F 80 00 00` | 1.0 (= 100%) |

---

## 6. Out of scope

- **Firmware.** The design spec marks the Secondary firmware (`BTS_SEC_FW_V201`) as already
  implemented at `0x14`. That repo is not part of `f:\Quench_Project\ME_PRD` and this document
  makes no claim about it — only that WebAppME's own wire encoding will use `0x16`, and that
  whoever owns that firmware needs to reconcile the byte before the two sides talk to real
  hardware.
- **Persistence across power-fail** (§5 of the design spec) — firmware-side, not applicable here.

---

## 7. Deviations from the upstream design spec — both deliberate, both user-confirmed

| # | Upstream says | WebAppME does | Why |
|---|---|---|---|
| 1 | `LOCKAh = 0x14` (D1, §3.1) | `LOCKAh = 0x16` | `0x14`/`0x15` already taken in this codebase — see §1 |
| 2 | `LOCKAh` nominal value must be `> 0` (D5, V9) | Must be `≥ 1.0` | Explicit user instruction 2026-09-21: a capacity lock should never target less than 100% of what was delivered |

---

## 8. Open items — resolved during implementation

1. **Registration auto-set — revised 2026-09-21, post-implementation.** The first pass hardcoded
   `"STANDARD"` whenever a SET step declared `Ah=`/`Wh=` with no Registrations chosen. That was
   wrong for a multi-block program: the reference program's *second* `SET Ah = 0` should carry
   forward whatever the *first* block's SET actually used, not be forced back to a fixed default
   every time. Fixed to inherit the nearest preceding SET step's Registrations — the identical
   rule `ProcessRegOperator` already applies for REG (`RegStep_WithoutItsOwnRegistrations_...`) —
   falling back to `"STANDARD"` only when there is no earlier SET to inherit from (the reference
   program's first block, which has nothing to inherit and is explicitly `STANDARD`). An explicit
   choice on the step itself is still never overridden.
2. **`Wh` scope** — both shipped together (`Ah→0x26`, `Wh→0x2A`), same mechanism, no extra cost.

---

## 9. Testing plan

Mirrors the existing `ProgramToBytePacketTests.cs` pattern:

| Area | Cases |
|---|---|
| `LOCKAh` encode | Byte-for-byte against §5 above; multiplier `1.0` and `1.1`; rejects `< 1.0` at the validation layer |
| `PERCAh` encode | Cutoff byte `0x3E` on a regulating operator; rejects `≤ 0`; confirms it's structurally unreachable on `PAU`/`LOCKAh` |
| `SET` global params | `Ah = 0` → 5-byte block, count byte `01`; `Wh = -640.5` → same for energy; both present → count `02`; unrelated `myVar = 5 A` → byte-identical to today (`N=0`) |
| `SET` registration | `Ah=`/`Wh=` present → `Registrations` defaults to `STANDARD` |
| Unit autocomplete | `PER` + Tab → `PERCAh`, exercised through `AutoCorrectUnit` |
| Regression | Every existing operator's packet bytes unchanged (`PRODUCER`, `CC_RECHG`, `PAU`, etc. untouched by the `0x16` assignment) |

---

## 10. Implementation record

`dotnet build`: 0 errors. `dotnet test`: 850/850, including the new cases in
`ProgramToBytePacketTests.cs` and the new `ValidationHelperLockAhPercAhTests.cs`.
`python -m pytest tests -q` (`HardwareSimulator`): 37/37, including 3 new LOCKAh/SET decode cases.

| Part | State | Where |
|---|---|---|
| `LOCKAh = 0x16` — const, `Operators` entry, `LimitActionAllowed`, `NominalConfig` | done | `OperatorConstants.cs` |
| `PercAh = 0x3E` cutoff enum | done | `Models/Enums/ProgramEnums.cs` |
| `LOCKAh` ≥ 1.0 validation | done | `ValidationHelper.ValidateNominal` |
| `PERCAh` ≤ 0 rejection | done | `ValidationHelper.ValidateLimit` |
| `PERCAh` unit → cutoff byte | done | `ProgramBuilder.TryParseUnit` |
| `LOCKAh` packet encode | done | `ProgramBuilder.ProcessLockAhOperator` (new) |
| `SET` global parameters (`Ah`/`Wh`) + registration auto-set | done | `ProgramBuilder.ProcessSetOperator` (rewritten) |
| Dispatch wiring | done | `DecoderService.FormatStepBytes` |
| Packet-inspector decode parity (SET param blocks, `LOCKAh` name, `PercAh` cutoff name) | done | `PacketAnalyzer.cs`, `PacketAnalyzerNoReverse.cs` |
| Operator badge color for the new `lock` color | done | `OperatorBadge.razor` |
| Simulator opcode parity (`OP_LOCKAH`, `_decode_lockah`, `_decode_set_or_reg` global-param walk) | done | `HardwareSimulator/program_decoder.py`, `simulator.py` |
| `docs/PROTOCOL.md` (§15.1, §15.8, §15.14, §18.4) | done | |
| Firmware (Secondary/Primary) | **out of scope** — see §6 | — |
| HIL | **not applicable** — Web App only | — |
