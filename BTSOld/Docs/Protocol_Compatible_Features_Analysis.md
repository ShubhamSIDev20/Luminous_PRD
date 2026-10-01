# Protocol-Compatible Feature Analysis — Program Editor

**Question:** How many additional features can be added to the Program Editor entirely at the application/web UI level — where any required calculations or logic are handled by the app before sending data — without changing the existing device communication protocol?

**Scope note:** This intentionally excludes desktop-only workflow chrome that doesn't fit a web app (Pascal Editor, Decompose/Recompose, full Print Preview with page zoom, 20-level folder nesting). Where a desktop feature has a more practical web-native equivalent (e.g., search/tags instead of a folder tree), that equivalent is proposed instead.

---

## How the protocol actually works (why this matters)

Every step is serialized to fixed byte fields, defined in `Services/ProgramBuilder.cs` and `Components/UI/Program/OperatorConstants.cs`:

- 1-byte operator code (1–20, e.g. `CC_CHG`, `PAU`, `GOTO`, `SET`, `TABLE`, `PRODUCER`)
- Nominal values as raw big-endian floats/ints (`ExtractFloatAsByteArraySafe`)
- Limits as `[unit-byte][condition-byte][4-byte value]`
- Actions as `[1-byte opcode][optional extra bytes]` (blank=0x00, INT=0x0E, STO=0x0B, ERR=0x10, MSG=0x11, GOTO=0x09+2-byte step number)
- Registrations as `[1-byte type][4-byte value]`
- Packets framed as `0xAA 0x55 [4-byte next-step-offset] [step bytes] 0x55 0xAA`

**Critical finding:** `SET` variables are never transmitted to the device at all. `ProgramBuilder.ExtractGlobalVariables` builds a client-side lookup table, and `ProcessNominalValues` / `ProcessStandardLimit` / `AddRegistrations` already resolve any variable name to its literal value *before* writing bytes. "Compute a number on the server, bake it into the existing byte slot" isn't a new pattern — **it's the app's existing design**. That's why most of the gaps below turn out to be protocol-free: they're the same resolve-then-serialize mechanism already used for generic SET variables, just applied to a new source of the number (a battery record, a percentage, a scaled table row) instead of a user-typed literal.

---

## Features addable without protocol changes

| # | Feature | Description | Why no protocol change | How it works on current protocol | Assumptions / Limitations | Complexity |
|---|---|---|---|---|---|---|
| 1 | Copy / Paste steps | Copy one or more selected steps, paste elsewhere in the same or another program. | Pure editor/session-state operation on `List<StepModel>`; nothing is sent to a device until Transfer. | Clone the selected `StepModel` objects into session storage (same mechanism `ServerSessionStorageService` already uses for autosave), insert on paste. | None significant. | Low |
| 2 | Undo / Redo | Step back through edits (Ctrl+Z). | Client-side history stack over the in-memory step list. | Snapshot `steps` before each mutating action; existing `Ctrl+S`/`Ctrl+Delete` JS keydown handler already shows the pattern for adding a bound key. | Memory cost scales with undo depth (cap it, e.g. 50 steps). | Low |
| 3 | Insert step(s) with count | "Insert N steps" instead of one at a time. | `AddStep()` already loops-inserts one step; looping it N times is not a protocol concern. | Small dialog captures N, calls existing `AddStep` N times. | None. | Low |
| 4 | Disable/skip a step (no delete) | Flag a step inactive; it's kept in the editor but never reaches the device. | The step just never gets handed to `ProgramBuilder`. | Add `IsDisabled` to `StepModel`; filter `steps.Where(s => !s.IsDisabled)` before calling `ProgramBuilder`/`ExpandProducerSteps`. | GOTO labels *inside* a disabled step become unreachable — validation must catch that, not silently break. | Low–Medium |
| 5 | Reserved/forbidden label names | Block `V`, `A`, `Ah`, `Wh`, etc. as user-defined GOTO labels. | Pure client-side validation before any byte is built. | Add a constant blocklist to `OperatorConstants.cs`, check it wherever `step.Label` is set. | None. | Low |
| 6 | Enforce GOTO can't jump into/out of a PRODUCER block | Prevent a GOTO label match landing inside another program's inlined steps. | Validation-only fix inside `ExtractLables`/`ExpandProducerSteps` — no new bytes, arguably a **correctness bug fix**, not just a feature. | Track which labels originated from an inlined PRODUCER block; reject a GOTO target that crosses that boundary. | None. | Low–Medium |
| 7 | CYC repeat count via a SET variable ("N\*" style) | `CYC` accepts a variable name, not just a literal integer. | `ProcessStandardLimit`/`ProcessNominalValues` already resolve named variables to values for other operators — CYC's `ValidateNominal` branch just never got the same lookup. | Extend the CYC branch to check `globalVariables` before falling back to `int.TryParse`, exactly like the existing GOTO/limit branches do. | None. | Low |
| 8 | Battery-parameter-derived nominal values (ACn5, ACn1, ACn2, ACn4, ACn10, ACn20, VnC) | Enter `ACn5` instead of a literal current value; app resolves it from the battery's capacity. | Resolves to a plain float, written through the *existing* `ExtractFloatAsByteArraySafe` path — identical byte shape to typing a number directly. | `ACn5 = Batteries.NominalCapacity / 5`, computed in `ProgramBuilder` before nominal-value serialization (needs the Battery record passed into the build call, which it currently isn't). | Only correct if `NominalCapacity`/`NumberOfCells` are populated and accurate for that battery record. | Medium |
| 9 | Battery parameters usable in Limit/Registration columns (CNom, INom, UGas, UMax, etc.) | Reference `INom`/`UGas`/etc. directly as a limit threshold. | Same resolve-then-write pattern as #8 — writes into the existing `A`/`V`/`Ah` unit-byte slots. | Extend `ValidUnits` lookup to also check battery-parameter names, resolving against the `Batteries` entity. | Same battery-data-accuracy caveat as #8. | Medium |
| 10 | Percentage-of-nominal-capacity limits (PERCCN_P / PERCCN_C) | "End step at 80% of nominal capacity." | App computes `X% × NominalCapacity` into an absolute Ah value, sent via the existing Ah limit encoding. | Resolve percentage to Ah, write through `ProcessStandardLimit`'s existing Ah-unit path. | Needs a defined reference point for "previous step" vs "current step" capacity, which the app tracks already via `AhStep`/`AhPrev`-style counters. | Medium |
| 11 | Special customer limit units (ABATT, VBATT, VNC, WATT, OHM\*) | Recognize these as valid limit-channel tokens. | Same lookup-table extension as #9 — pure validation/mapping change, no new byte structure. | Add to `ValidUnits`, map to the existing corresponding `CutoffCondition` byte where one already exists (A→ABATT, V→VBATT, etc.). | \*OHM specifically needs a Resistance nominal mode first (see "requires protocol changes" below) — the unit token alone is free, but it's meaningless without that mode. | Low |
| 12 | TABLE built-in drive-cycle profiles (FUDS, SFUDS, DST) | Ship ready-made `.txt` profiles the user can pick instead of hand-authoring one. | They become ordinary TABLE operator files — same `BuildTableOPTxtToBinary` path as any user-uploaded table. | Bundle the three profile files with the app; surface them in the existing TABLE file-picker (`FileManagerService`). | None. | Low |
| 13 | TABLE scaling (Factor_I/Factor_P/Factor_U) | Scale a loaded table's current/power/voltage for reuse across battery ratings. | The scale factor is applied to each row's A/W/V value *in memory*, before `BuildTableOPTxtToBinary` writes the bytes — the device still just sees plain floats. | Multiply parsed `A`/`W`/`V` by the factor inside `BuildTableOPTxtToBinary`. | None. | Low–Medium |
| 14 | TABLE value limiting (Ap/An/Vp/Vn/Wp/Wn clamp) | Clamp table-driven values to a safe range. | Same in-memory pre-processing point as #13. | `Math.Clamp` each parsed value against user-supplied bounds before writing. | None. | Low–Medium |
| 15 | Ramp functions, via auto-generated TABLE | "1000→5000 Watt over 300 sec" authored as a ramp UI, but compiled into a fine-grained TABLE file under the hood. | No new operator — it becomes a normal TABLE step using the existing TABLE byte format. | UI takes start/end/duration, generates N time-sliced rows (e.g., 1 row/sec) interpolating the value, feeds them through the existing `BuildTableOPTxtToBinary`. | It's a **stepped approximation of a ramp**, not a true continuous linear ramp — fidelity depends on how many rows you generate (packet size grows with resolution). Not equivalent to true device-side ramping. | Medium–High |
| 16 | Standalone Program Print/Export (PDF or CSV of the step grid) | Practical modern substitute for the desktop's full Print/Print Setup/Print Preview suite. | Purely a read-out of already-loaded `List<StepModel>` — never touches the device protocol. | Reuse the Excel/PDF export pattern already present in the generic `DataTable` component, applied to the step grid. | None. | Low–Medium |
| 17 | Program locking ("Checked" / read-only) | Role-gated flag that makes a saved program non-editable. | Purely a DB flag + UI/service guard on `SaveProgramAsync` — never reaches the wire protocol. | Add `IsLocked` to the Program entity, add a role check (or reuse existing Administrator/Supervisor roles) gating the editor's save/write paths. | None. | Low |
| 18 | Search/tag-based program organization (in place of deep folder nesting) | Modern equivalent to the desktop's 20-level folder tree, better suited to a searchable web list. | Purely a DB/UI feature on the Program entity — no protocol involvement. | Add a `Tags`/`Category` field and a filter UI to the existing `ProgramList.razor` DataTable, rather than building a full folder tree to match the desktop 1:1. | Deliberate web-native substitute for the desktop's literal folder tree, not a 1:1 port. | Low–Medium |

---

## Borderline — likely protocol-compatible, but need firmware confirmation before building

| Feature | Why it's uncertain |
|---|---|
| **Global Nominal Values/Limits** | `ProcessSetOperator` already writes a `0x00` placeholder byte labeled "No of Global Limit Parameters" — the protocol slot for this *already exists and is simply never populated*. Implementing it is likely protocol-compatible, but the exact per-parameter byte layout that should follow that count isn't visible anywhere in this codebase — needs confirmation from whoever defined the original device protocol before writing real bytes into that field. |
| **Timer1–Timer3** | `ProcessStandardLimit` already resolves *any* named SET variable generically when used as a limit — so `SET MyTimer = 30 min` used later as a limit may already work today under a different name. Whether the desktop's "Timer1-3" needs true independent device-side elapsed-time tracking (vs. just a named duration value) determines whether this is a documentation/naming task (Low) or a real gap. |
| **RLevel variable** | The existing SET-operator "RType" 2-byte field is already a bitmask of active registration channels. If "RLevel" maps to specific bit patterns within that *same* field, it's free; if the device expects a distinct dedicated field, it isn't. Can't tell without firmware documentation. |

---

## Features that require protocol changes

| Feature | Why it needs a protocol change |
|---|---|
| Resistance nominal mode (CR_CHG/CR_DCHG) | Needs a brand-new operator byte code — Current/Voltage/Power each got their own code (1–7, 19); Resistance has none. |
| TASK (OUW, CGRE, CLESS, TCONTR) | No operator code, and "parallel background process" implies a runtime concept (concurrent task execution) the current per-step linear packet model has no representation for. |
| SAVE / REST | No operator code; requires the device to persist/restore named counter state — not something the app can fake by pre-computing a number. |
| PARALLEL, SYNCLine/SYNCProgram | No operator code; requires the device firmware to coordinate multiple circuits — inherently a device-side runtime behavior, not resolvable client-side. |
| RANGE / IRANGE | No operator code for current-range mode switching. |
| CLEAR, FILTER | No operator codes; FILTER is also hardware-gated (Digatron-only) regardless. |
| FILE (test-section naming, incl. TSZ auto-naming) | No `FILE` operator code exists at all today. |
| EIS | No operator code — and already confirmed as an intentional scope exclusion in this repo's own `Docs/comparison.html` ("BTS does not support EIS hardware"). |
| RCH, SETMUX, ADD, SUB, BATT, PROT, PAUA/PAUO/URANGE/IRANGE/OUTA/OUTB/ISOEXT/ISOINT | Each needs its own new operator code; several (PROT, SETMUX) also imply new device-side capabilities (launching an external process, switching acquisition hardware) that a web app can't compute around. |
| Registration frequency/trigger types (delta, threshold `<`/`>`, delayed-range `>>`/`<<` with `&`, max-count `*`) | The current registration byte format is `[channel-type byte][4-byte value]` — there's no field for "trigger mode." The device presumably interprets that value one fixed way today; distinguishing "log every 2V change" from "log every 5 minutes" from "log max 15 times" needs a new mode byte the firmware must also understand. |
| ONERROR / ONEXIT special labels | Labels are **never sent to the device at all** — `ExtractLables` is purely client-side, used only to resolve a GOTO target into a step-number byte. There's no wire mechanism for "the device should auto-jump here on error," so unless firmware already has an undocumented reserved-step convention, this needs new protocol support. |
| GradT/GradTm, GradU(m)/GradI(m), deltaV | These require the device to continuously evaluate a rate-of-change (not a fixed threshold) during the step — the current limit format only encodes a static value comparison, so evaluating a derivative needs new device-side logic and likely a new limit-type byte. |

---

## Bottom line

**18 features** can be added purely at the app layer today (mostly Low/Medium complexity), plus **3 borderline items** that are likely free but need one conversation with whoever owns the device firmware spec to confirm exact byte layout before building them. Everything else in the desktop spec's remaining gap genuinely needs new operator codes, new wire-format fields, or new device-side runtime behavior.

**Suggested starting point:** #4 (disable/skip step), #6 (GOTO scope enforcement), and #7 (CYC repeat via variable) — all Low complexity, and #6 is arguably a correctness fix rather than a new feature.
