# BTS-600 Program Editor Feature Gap Analysis

**Source spec:** `Docs/BTS-600_Program_Editor_Feature_List.xlsx` (10 sheets, extracted from the BTS-600/Battery Manager desktop user manual, chapters 11.1, 11.2, 12).
**Compared against:** this repo's web app (Blazor Server, .NET) — primarily `Components/Pages/Programs/ProgramEditor.razor`, `Components/Pages/Programs/ProgramList.razor`, `Components/UI/Program/OperatorConstants.cs`, `Components/UI/Program/ValidationHelper.cs`, `Services/ProgramBuilder.cs`, `Services/DecoderService.cs`, `Repositories/Implementations/ProgramRepository.cs`, `Models/Entities/Batteries.cs`, `Components/Pages/Programs/Registrations.razor`, `Services/FileManagerService.cs`.

**Architectural framing (applies to every "Missing" verdict below):** the web app's Program Editor is a step-grid *authoring, validation, and binary-packet serialization* tool. It builds a `List<StepModel>` and serializes it to command packets consumed by the physical BTS device firmware — it does not execute operators itself. "Missing" means *not authorable, validatable, or serializable by the web editor*, not "physically impossible for the device." A handful of spec claims describe pure device/firmware runtime behavior (e.g., "STO does not stop a queued dispo-list") that cannot be confirmed or denied from this codebase alone — those are marked **Unable to Verify**.

**Corroborating evidence found in-repo:** `Docs/comparison.html` is a pre-existing BM4-desktop-vs-BTS-web comparison document already checked into this repository. It independently confirms the web app (BTS) does not support EIS hardware, corroborating this report's Sheet 8 finding.

---

## Summary

| Metric | Count |
|---|---|
| Total feature rows assessed (across 10 sheets) | 134 |
| Fully Implemented | 28 |
| Partially Implemented | 16 |
| Different Implementation | 19 (11 cosmetic renames + 8 materially different mechanisms) |
| Missing (raw Excel rows) | 64 |
| Missing (distinct capabilities, after merging Sheet 3 ↔ Sheet 8 operator duplicates and Sheet 3/9 label duplicates) | 53 |
| Unable to Verify | 7 |

### Per-sheet breakdown

| Sheet | Rows | Fully | Partial | Different | Missing | Unable to Verify |
|---|---|---|---|---|---|---|
| 1. Program Management | 12 | 2 | 2 | 3 | 4 | 1 |
| 2. Editor Grid & Editing | 12 | 2 | 2 | 1 | 4 | 3 |
| 3. Operators | 30 | 8 | 1 | 2 | 19 | 0 |
| 4. Nominal Values | 6 | 0 | 2 | 0 | 4 | 0 |
| 5. Limits | 13 | 1 | 1 | 0 | 11 | 0 |
| 6. Actions | 7 | 4 | 1 | 0 | 1 | 1 |
| 7. Registration & Formats | 9 | 4 | 2 | 1 | 2 | 0 |
| 8. Special Operators (deep-dive) | 20 | 4 | 2 | 0 | 14 | 0 |
| 9. Cycles, Procedures, Counters | 13 | 3 | 3 | 1 | 4 | 2 |
| 10. Battery Parameters | 12 | 0 | 0 | 11 | 1 | 0 |
| **Total** | **134** | **28** | **16** | **19** | **64** | **7** |

> Note: Sheet 3 (Operators) lists 30 individual rows, not the ~27 estimated in the Excel overview tab, because several rows bundle multiple operator tokens (e.g., "PAUA, PAUO, URANGE, IRANGE, OUTA, OUTB, ISOEXT, ISOINT" is one row). Sheet 8 similarly has 20 rows, and Sheet 9 has 13. Counts above were tallied directly from each sheet's feature rows, not estimated.

### Estimated completion percentage

Two figures, since a single number hides more than it reveals for a spec this broad:

- **Strict (fully-implemented / total, excluding nothing):** 28 / 134 ≈ **21%**
- **Weighted (recommended figure):** full credit for Fully Implemented and the 11 cosmetic battery-parameter renames (equivalent functionality, different names); half credit for Partially Implemented and the 8 materially-different mechanisms (they deliver *some* of the spec's value via a different design); no credit for Missing; the 7 Unable-to-Verify rows are excluded from both numerator and denominator (no evidence either way).
  - Full credit: 28 + 11 = 39
  - Half credit: (16 + 8) × 0.5 = 12
  - Denominator: 134 − 7 = 127
  - **(39 + 12) / 127 ≈ 40%**

Either way, the web app currently covers a **reduced core subset** of the desktop BTS-600 Program Editor spec: solid on basic charge/discharge/pause/cycle/GOTO/registration/battery-metadata functionality, but missing most of the "advanced operator" catalog (TASK, SAVE/REST, PARALLEL, EIS, SYNCLine/SYNCProgram, RANGE/IRANGE, CLEAR, FILTER, TABLE's advanced modes), most limit types beyond a basic threshold/time check, program-level workflow features (folders, print, locking, copy/paste, undo, Pascal editor), and safety-oriented constructs (global limits, ONERROR/ONEXIT, gradient-based limits).

---

## Missing Features

64 raw Excel rows describe an absence; several are the same underlying gap referenced from two sheets (Sheet 3's operator list vs. Sheet 8's deep-dive; Sheet 3's ONERROR/ONEXIT vs. Sheet 9's). The table below lists **53 distinct missing capabilities** with sheet cross-references, merging those duplicates so each row represents one real gap. **Current Status for every row is Not Implemented.**

### Sheet 1 — Program Management

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Folder management | Right-click to create folder/subfolder; drag-and-drop programs into folders; up to 20 nested levels. | Tree browser with folder CRUD and drag-drop reordering. | Add a `ProgramFolder` entity (self-referencing `ParentFolderId`) or a `ParentFolderId` FK on the Program entity; replace/augment `ProgramList.razor`'s flat `DataTable` with a tree component supporting drag-drop. |
| Calculation / graph view | Button plots the program's planned function (current on Y, time on X) before running. | Pre-run visualization of the step sequence's control profile. | New component reusing the existing Highcharts infrastructure (`Components/UI/DataViewer/BmsChart.razor` pattern) to render a synthetic timeline from each step's Nominal Value + Limit duration, without needing live device data. |
| Print program | File menu Print / Print Setup / Print Preview with zoom and two-page view. | Print or export the step grid to a physical/PDF document. | Add a print-friendly CSS view + `window.print()` trigger, or a PDF export following the existing Excel-export pattern already used in the generic `DataTable` component. |
| Program locking ("Checked") | Certificator role + BM.ini flag; adds a "Checked" checkbox that makes the program view-only once set. | Per-program lock enforced by role-based permission. | Add `IsLocked`/`IsChecked` boolean to the Program entity; add a "Certificator" role to ASP.NET Identity roles; guard `SaveProgramAsync` and the editor's write actions when the flag is set. |

### Sheet 2 — Editor Grid & Editing

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Copy program steps | Drag-select contiguous steps in the Step column, right-click → Copy. | Steps copied to an in-memory/session clipboard. | Add a selection-to-clipboard action in `ProgramEditor.razor`, storing a serialized `List<StepModel>` slice in component state or `ServerSessionStorageService`. |
| Paste steps | Right-click destination step → Paste; inserts copied steps above it, same or different program. | Steps inserted from clipboard at the chosen position. | Companion to Copy above — deserialize the clipboard slice and `Insert` at the target index; reuse existing `RenumberSteps()`/`ValidateProgram()` calls. |
| Deactivate / disable step | Enter "!" in Label column to disable a step without deleting it (shown yellow); removing "!" restores it. | Step retained but skipped during execution/serialization, visually flagged. | Add `IsDisabled` bool to `StepModel`; skip disabled steps in `ProgramBuilder`'s packet serialization; add a toggle control + yellow row styling in `StepRow.razor`. |
| Undo (step editing) | Esc restores a field's previous value; Undo steps back through changes. | Multi-level undo across edits. | Introduce a command-history stack in `ProgramEditor.razor` (snapshot `steps` list on each mutating action) with Ctrl+Z binding, mirroring the existing `Ctrl+S`/`Ctrl+Delete` JS keydown handler. |
| Pascal Editor | Author/view Pascal-based nominal-value functions (S_USER editable, S_DIGA read-only) from Nominal Value/Limit columns. | Dialog to Replace/Add/Show/Edit/Delete/New a function, applied to the field. | Large feature — would need a function-definition storage model, a scripting/expression evaluator (or a curated subset), and a dedicated editor dialog. Likely a separate spec/plan of its own; flag as a candidate for a future phase rather than a quick add. |

### Sheet 3 & 8 — Operators (merged; distinct capabilities not in either operator list)

| Feature | Excel Sheet(s) | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|---|
| RCH (Recharge) | 3 | Same as CHA but also sets the recharge relay; charge LED blinks. | A charge variant distinguishable from CHA at the relay/LED level. | Add an `RCH` counterpart to the existing `CC_CHG`/`CV_CHG`/etc. set in `OperatorConstants.cs`, or a "recharge" flag alongside the existing charge modes if the device protocol supports it. |
| RET (Return) | 3 | Returns from a procedure to the calling program/level. | Explicit procedure-return step. | Not clearly needed under this app's PRODUCER model (whole-program inlining rather than nested procedure calls) — see Sheet 9 Procedures notes below before implementing. |
| SETMUX (Switch) | 3 | Activates an additional multiplexer channel for serial data acquisition. | New operator code + hardware-specific handling. | Add `SETMUX` operator constant + `ProgramBuilder`/`DecoderService` packet encoding once the multiplexer hardware protocol is documented for this system. |
| ADD | 3 | Adds together channel values entered in Nominal Value. | Arithmetic combination operator. | Add `ADD` operator constant; extend `ValidationHelper.ValidateNominal` to parse a channel-list-plus-operator expression; extend `ProgramBuilder` encoding. |
| SUB | 3 | Subtracts second channel value from first; respects sign for A/mA limits. | Arithmetic combination operator (inverse of ADD). | Same approach as ADD above. |
| BATT (Battery Simulator preset) | 3 | Constant voltage + upper/lower current limits (units V, A, AG). | New operator for simulator mode. | Add `BATT` operator constant with a 3-field `NominalConfig` entry (Voltage, Current-high, Current-low); wire into `ProgramBuilder`. |
| PROT | 3 | Starts an external protocol/report program, passing circuit/battery/session-ID as arguments. | Program-triggered external process launch. | Requires a host-side (not device) integration — likely a server-side process-launch hook keyed off a `PROT` step being reached, reported via the live-data UDP stream; needs its own design. |
| PAUA, PAUO, URANGE, IRANGE, OUTA, OUTB, ISOEXT, ISOINT | 3 | Legacy/specialized operators only detailed in a separate "New Operators" reference doc. | Device-specific behavior per that doc. | Defer until the "BM_PM_BTS600_New_Operators" reference document is available; add as individual `OperatorConstants` entries once behavior is specified. |
| TASK (parallel background process; OUW/CGRE/CLESS/TCONTR) | 3, 8 | Activates a parallel background process, up to 12 concurrent; built-ins OUW (resistance/power calc), CGRE/CLESS (temperature relay on/off), TCONTR (temperature-based relay switching). | New operator + 4 built-in task types, pairable with PARALLEL for Safetask. | Add `TASK` operator constant with a `NominalConfig` entry offering a task-type dropdown (OUW/CGRE/CLESS/TCONTR); extend `ProgramBuilder` to encode the task selector byte. |
| SAVE | 3, 8 | Persists battery's counters (per current registration format) into a named test section; multiple sections per battery. | Named counter-state checkpoint. | Add `SAVE` operator constant + a "section name" nominal-value field; extend the session/registration-format storage layer to persist named counter snapshots. |
| REST | 3, 8 | Restores counters previously stored via SAVE (matching section name); requires matching registration format pre-declared; waits/interrupts if unavailable. | Resume-from-checkpoint capability — directly relevant to this app's existing "session restart" concerns. | Pairs with SAVE above; add `REST` operator constant + lookup against saved sections; integrate with the existing session-restore logic already used for DBC/session recovery on app restart. |
| RANGE / IRANGE | 3, 8 | Automatic vs. manual current-range switching (1–4 manual, 1–2 for IGBT; 0 = automatic); takes effect at start of next step. | Range-mode control operator. | Add `RANGE`/`IRANGE` operator constants with an integer `NominalConfig` field (0–4), validated range in `ValidationHelper`. |
| PARALLEL | 3, 8 | Links 2+ circuits for synchronized parallel test operation (up to 7 slaves on MBT master, 15 on ME master); requires all linked circuits idle at start. | Multi-circuit synchronization operator + pre-flight idle check. | Add `PARALLEL` operator constant; the "requires idle" pre-flight check would need to query `CircuitManager`'s tracked circuit states before allowing the linked run to start. |
| EIS | 3, 8 | Runs an EIS-Meter impedance-spectroscopy step with a rich parameter set (ADC, VDC, mHz/Hz/kHz, AAcMax, VAcMax, VAcMin, mVideal, EIStime, EISperi, AACstart). | New operator with ~9 sub-parameters. | Confirmed as an intentional scope exclusion — `Docs/comparison.html` already states BTS does not support EIS hardware. No action recommended unless EIS hardware support is added to this product line. |
| SYNCLine / SYNCProgram | 3, 8 | Synchronizes circuits sharing a SyncGroup at a given step; SYNCProgram requires identical program+version, SYNCLine does not check. | Standalone, parameter-less synchronization step. | Add both operator constants; SYNCProgram's version-match check would need a program-version field (currently absent — see also Sheet 1 gaps) before it can be enforced correctly. |
| CLEAR | 3, 8 | Deletes SET registrations/limits/global setpoints; does not clear the active registration format itself. | Reset-scoped-state operator. | Add `CLEAR` operator constant; in `ProgramBuilder`, clear the accumulated SET-variable/global-limit tables built by `ExtractGlobalVariables`/`ProcessSetOperator` without touching the registration-format selection. |
| FILTER | 3, 8 | Sets digital filters per channel (Digatron-configured hardware only). | Hardware-gated operator. | Add `FILTER` operator constant, gated behind a hardware-capability flag (only relevant if Digatron-equipped devices are in scope for this product). |
| FILE (test-section naming, incl. per-cycle auto-naming `TSZ00001`, …) | 3, 8 | Names the test section used for registration storage; supports per-cycle-iteration auto-naming. | New operator + auto-increment naming inside cycle blocks. | Add `FILE` operator constant; auto-naming would hook into the existing cycle (`BEG`/`CYC`) processing in `ProgramBuilder` to increment a section-name counter per iteration. |
| Global Nominal Values / Limits | 8 | Program-wide limit no step may exceed, checked throughout the run; action restricted to ERR/MSG; no action + limit reached terminates the whole program. | Safety-oriented, always-active limit distinct from per-step limits. | The wire format already reserves a byte for this (`ProcessSetOperator` writes a hardcoded `0x00` "No of Global Limit Parameters" placeholder) — add UI to author global limits and populate that count/parameter block instead of always writing zero. |
| TABLE — scaling (Factor_I/Factor_P/Factor_U) | 8 | Scale a loaded table's current/power/voltage values by a factor, for reuse across capacities/ratings. | Special nominal-value tokens applied alongside a TABLE step. | Add `Factor_I`/`Factor_P`/`Factor_U` as recognized special nominal values in `ValidationHelper`; apply the scale factor when `ProgramBuilder.ProcessTableOperator` reads table rows. |
| TABLE — built-in drive cycles (FUDS, SFUDS, DST) | 8 | Predefined industry drive-cycle profiles usable directly, typically with TASK OUW for power calc. | Bundled `.TXT` profiles selectable without manual authoring. | Ship the three profile files with the app (e.g., under a `Table/` seed folder) and surface them in the TABLE file-picker used by `FileManagerService`. |
| TABLE — value limiting (Ap, An, Vp, Vn, Wp, Wn) | 8 | Optional parameters clamp table-driven values to a safe range. | Min/max clamp applied when a table step is processed. | Add these as recognized nominal-value modifier tokens, applied as a clamp in `ProgramBuilder.ProcessTableOperator` after reading each table row. |

### Sheet 4 — Nominal Values

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Battery-parameter-based functions (ACn5, ACn1, ACn2, ACn4, ACn10, ACn20, VnC) | Nominal values derived from battery Nom. Capacity/Cells rather than fixed numbers, e.g. a C5 discharge-rate current. Not usable in ramps. | These tokens accepted directly in Nominal Value, resolved against the battery's stored `NominalCapacity`/`NumberOfCells`. | Extend `ValidationHelper.ValidateNominal` to recognize these tokens and resolve them against the `Batteries` entity fields already present (`NominalCapacity`, `NumberOfCells`); resolve to an actual value at packet-build time in `ProgramBuilder`. |
| Ramp functions | Start value, end value, duration → linear ramp of a control value over time (e.g., "1000-5000 Watt 300 sec"). | New nominal-value syntax parsed and encoded as a ramp. | Extend the regex-based parsing in `ValidationHelper`/`OperatorConstants` (`FullPattern`, `ValueUnitPattern`) to accept a `start-end unit duration` form; extend `ProgramBuilder`'s per-operator byte encoding to carry ramp parameters (likely needs a new wire-format field). |
| Special nominal values (Factor_I/P/U, OUW, CGRE/CLESS, TCONTR) | Purpose-built values for TABLE scaling and TASK-paired relay/power control. | See TABLE-scaling and TASK entries above — this is the nominal-value side of those two operator gaps. | Implement together with TASK and TABLE-scaling above; this is the same underlying gap viewed from the Nominal Value column. |
| Reserved nominal values (File Name, Filter Name, TIMER1–3, MaxCHAI, MaxDCHI, MaxChaU, MaxDchU, MaxChaW, MaxDchW, Number) | System-reserved names with specific meanings (SET-started timers, current/voltage/power limit ceilings, message IDs). | These names recognized specially by SET/validation rather than treated as arbitrary user variables. | Extend `ParseSetVariable`/`ExtractGlobalVariables` with a reserved-name lookup table that gives these tokens special serialization treatment (e.g., TIMER1–3 map to timer-start wire fields) instead of the current generic named-variable path. |

### Sheet 5 — Limits

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Linked (AND) limits | Chain 2+ limit conditions across successive rows via `&`; action fires only once all are met, attached to the last row. | Multi-condition limit chaining. | Extend `ValidationHelper.ValidateLimit` to parse a leading/trailing `&` continuation marker, and `ProgramBuilder`'s limit encoding to link consecutive limit rows into one condition set. |
| Timer1–Timer3 limit | Ends the step when a SET-started timer elapses. | New limit type referencing a running timer. | Depends on TIMER1–3 reserved nominal values above being implemented first; then add `Timer1`/`Timer2`/`Timer3` as recognized limit-condition tokens. |
| GradT / GradTm | Ends the step when temperature rise exceeds a threshold per hour (GradT) or per minute (GradTm). | Gradient-based (rate-of-change) thermal limit — safety-relevant. | Add as recognized limit tokens in `ValidationHelper.ValidateLimit`; requires the device/firmware to already support rate-of-change limit evaluation — verify device protocol capability before committing to a UI-only fix. |
| GradU(m) / GradI(m) | Ends the step when voltage/current rise exceeds a threshold per hour (or per minute with 'm' suffix); limited to 1 channel; negative sign detects a decrease. | Gradient-based voltage/current limit. | Same approach as GradT/GradTm above. |
| deltaV | dChannel/dt gradient algorithm on voltage or GREAL channels; enhanced mode adds multi-channel time-base analysis via GREAL[100]-[119]. | Advanced gradient limit with channel-array indexing. | Significant scope — likely needs its own design pass given the GREAL[100]-[119] indexing concept doesn't exist anywhere in this codebase yet. |
| AhDef | Marks current Ah value as the 100% reference point; usable only with PAU. | Reference-point marker limit, PAU-gated. | Add as a recognized limit token restricted to steps where `OperatorCode == PAU`, validated in `ValidateLimit`. |
| PercAh | Ends step once Ah reaches X% of the AhDef reference. | Percentage-of-reference limit. | Depends on AhDef above; add as a follow-on limit token once a reference point exists to compute against. |
| PERCCN_P / PERCCN_C | Ends step when previous/current step's capacity reaches X% of nominal capacity (CNom); sign ignored. | Percentage-of-nominal-capacity limits. | Extend `ValidateLimit` to recognize these tokens and resolve against `Batteries.NominalCapacity` (already present as a field, per Sheet 10 findings) — same resolution mechanism needed for the Sheet 4 battery-parameter-based nominal values. |
| A_notAbs / mA_notAbs | Sign-aware current limits triggering only on positive current, avoiding false trips from discharge current. | Sign-qualified variant of the existing threshold limit. | Extend `ValidateLimit`'s existing threshold-parsing branch with a sign-aware token variant. |
| Special customer limit options (ABATT, ACN5, OHM, VBATT, VNC, WATT) | Derived/customer-specific channel-value limit units. | Recognized as valid limit-channel units. | Extend `OperatorConstants.ValidUnits` with these tokens; note "OHM" also requires the Resistance nominal-value type (Sheet 4/Sheet 3 finding) to exist first for the underlying channel to be meaningful. |

### Sheet 6 — Actions

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Procedure name (as action) | Entering a procedure's name as the Action loads and executes it, then continues at the next step. | Action-column procedure invocation. | Under this app's PRODUCER-as-whole-program model (see Sheet 9), extend `ProgramBuilder.TryParseOpcodeFull`'s action-parsing fallback to recognize a valid program name and encode a PRODUCER-style jump instead of silently treating it as a no-op. |

### Sheet 7 — Registration & Formats

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| RLevel variable | `SET RLevel = value` dynamically turns registration recording on/off (0.0 all off, 1.0 step-changes only, 2.0 all, 3.0/4.0 suppress step-change variants). | Runtime registration-verbosity control. | Add `RLevel` as a reserved SET-variable name (see Sheet 4 reserved-nominal-values gap) with a dedicated wire-format field consumed by `ProcessSetOperator`. |
| EIS registration format | Special-purpose format for EIS-Meter channels. | New registration format tied to EIS channels. | Depends entirely on EIS operator support (intentionally excluded per `Docs/comparison.html`) — no action needed unless EIS is added. |

### Sheet 9 — Cycles, Procedures, Counters

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Procedures — Decompose / Recompose | Expand a procedure call into inline steps for direct editing ("Decompose"), or collapse inline steps back into a reference ("Recompose"). | Right-click context-menu actions in the grid. | Under this app's PRODUCER model, "Decompose" would mean loading the referenced program's steps inline into the current editor for one-off editing, and "Recompose" would replace an inline block with a PRODUCER reference — a genuinely new authoring mode, not a small tweak. |
| Reserved/forbidden label names | A fixed list of names (V, A, mA, Ah, AhCha, Wh, WhCha, …) reserved for system counters/channels; cannot be used as GOTO labels. | Label input validation rejects these names. | Add a reserved-name constant list to `OperatorConstants.cs` and check it in the Label-entry/`ExtractLables` path in `ProgramBuilder`/`ProgramEditor.razor`. |
| Special Labels — ONERROR / ONEXIT | Jump targets auto-executed on any program error (ONERROR) or after normal completion (ONEXIT). | Two reserved Label values with automatic-jump semantics. | Add `ONERROR`/`ONEXIT` as recognized special values for `StepModel.Label`; requires device/firmware support to actually invoke the jump on error/exit conditions — verify protocol capability before UI-only implementation. |

### Sheet 10 — Battery Parameters

| Feature | Requirement/Description | Expected Behavior | Suggested Implementation |
|---|---|---|---|
| Battery parameters usable as global limits | CNom, NoCell, UGas, UMax, INom, ICrank, ChargeF, Rin, CutOff, UNom, EDensity referenceable directly as channel units in Nominal Value/Limit/Registration columns (e.g., interrupt whenever current exceeds INom). | These parameter names accepted as valid tokens in the same places physical channel units are accepted. | Extend `OperatorConstants.ValidUnits` (or add a parallel battery-parameter lookup) consulted by `ValidateNominal`/`ValidateLimit`/`ValidateRegistration`, resolving against the current program's associated `Batteries` record fields (already present under different names — see Different Implementation section). |

---

## Partially Implemented Features

| Feature | Sheet | What's Implemented | What's Missing | Suggested Improvements |
|---|---|---|---|---|
| Create new program | 1 | `ProgramList.razor::OpenCreateDialog()`/`SaveProgramAsync()` capture Name, Description, MaxAh, Duration; `IProgramServices.CreateProgramAsync` persists it. | No program-number concept (manual or "Next available Program Number" auto-assign); no explicit battery-type scoping at creation time. | Add a `ProgramNumber` field with a "next available" lookup, and consider scoping program creation under a battery type if that hierarchy is added alongside the Sheet 1 folder-management gap. |
| Save program | 1 | Edit-save via `SaveProgramAsync()`; "Save As" achieved via `DuplicateProgram()` (copies with a `_Copy` suffix). | No free-program-number selection or "next available" auto-assign (same root cause as Create above). | Once a program-number field exists, extend `DuplicateProgram` to prompt for/auto-assign a new number. |
| Insert step(s) | 2 | `ProgramEditor.razor::AddStep(afterStepId)` inserts exactly one step, bound to `Alt+N`. | No "how many steps" count dialog; the `Insert` key itself isn't bound (only `Alt+N` is). | Add a small dialog capturing a step count, loop `AddStep` that many times; bind the browser `Insert` key alongside the existing `Alt+N` in the JS keydown handler. |
| Edit field (input assist) | 2 | Operator column uses a searchable dropdown (`OperatorSelect.razor`); Nominal Value/Limit are validated text inputs. | No unified "assist button opens a helper dialog" pattern for Nominal Value/Registration columns — they're plain validated text fields, not dialog-driven pickers. | Add a consistent assist-button affordance to Nominal Value/Registration inputs that opens a filtered value-list dialog, mirroring the Operator column's pattern. |
| Cycles (BEG/CYC) | 9 | `OperatorConstants.BEG`/`CYC`; `ProgramEditor.razor::ValidateProgram` uses a stack to validate BEG/CYC pairing and nesting (no hard depth cap — more permissive than the desktop's 16-level limit). | CYC's repeat count only accepts a plain integer (`int.TryParse`) — the desktop's `"N*"` format and "repeat count referencing a SET-defined variable" are not supported. | Extend the CYC branch of `ValidationHelper.ValidateNominal` to also accept a variable-name lookup against `GlobalVariable`s, matching how the GOTO/BEG branches already resolve labels/variables. |
| Procedures — edit | 9 | `ProgramEditor.razor::HandleViewProducer` opens a read-only `ProgramViewer` dialog to inspect a referenced PRODUCER program's steps while editing the caller. | One-directional only — no equivalent "show calling program" view from inside the referenced program's own edit context (moot under this app's model, since PRODUCER references an independent, separately-editable Program rather than a nested procedure scope). | Low priority given the architectural difference is by design; if desired, add a breadcrumb/back-link when a program is opened via a PRODUCER reference. |
| GOTO — scope restriction | 9 | Multiple independently-targeted GOTO actions across different steps/limits work correctly; label resolution via `ExtractLables`. | "Cannot jump into or out of a procedure" is not enforced — labels are extracted from the fully flattened/PRODUCER-expanded step list with no procedure-boundary awareness. | Add a boundary check in `ExtractLables`/`TryParseOpcodeFull` that rejects a GOTO target whose label lives inside a different program's inlined PRODUCER block. |
| GOTO (as Action) | 6 | `TryParseOpcodeFull` case `0x09` resolves an Action-column GOTO against extracted labels or a literal step number. | Same procedure-boundary gap as above (shared root cause). | Same fix as the GOTO — scope restriction row above; this is the same underlying gap viewed from the Action-column angle. |
| Standard nominal functions (Current/Voltage/Power/Resistance) | 4 | `NominalConfig` maps `CC_CHG`→Current, `CV_CHG`→Voltage, `CP_CHG`→Power. | **Resistance is entirely absent** — no resistance-mode operator or "Ohm" unit anywhere in the codebase. | Add a `CR_CHG`/`CR_DCHG`-style resistance-control operator pair (mirroring the existing 4-mode charge/discharge split) and add "Ohm" to `OperatorConstants.ValidUnits`. |
| Parallel function (combine two nominal values) | 4 | `CCCV_CHG`/`CCCV_DCHG` combine exactly Current+Voltage in one operator. | This is a fixed, pre-named combo — not the spec's generic "combine any two nominal values" mechanism (e.g., Power+Resistance can't be combined once Resistance exists). | Once Resistance is added, consider a generic dual-nominal-value syntax rather than one hardcoded combo per pair, if more combinations become needed. |
| Time limit | 5 | `ValidationHelper.ValidateLimit`/`AutoCorrectUnit` support `N unit` duration format (sec/min/h/hr). | No `hh:mm:ss` clock-format support. | Extend the time-limit branch of `ValidateLimit` to also accept and normalize a colon-delimited duration string. |
| Logical channel counters | 7 | `RStandards.unitValueMap` covers `Ah, AhCha, AhDch, AhStep, Wh, WhCha, WhDch, WhStep` (total/cumulative/per-step counters). | `AhStat`, `AhBal` (charge-factor-adjusted), `AhPrev`, `WhPrev` (previous-step) counters are absent. | Extend `unitValueMap` and `ProgramBuilder.TryParseRegistration`'s unit-mapping switch with these four additional counter tokens. |
| Registration frequency/trigger types | 7 | `ValidationHelper.ValidateRegistration` accepts a plain `value unit` pair (e.g., "5 min", "2.0 A") — covers the spec's "Time interval" style. | No `<`/`>` threshold-prefix parsing, no `>>`/`<<` delayed-start range syntax with `&`, no trailing `*` max-count suffix. | Extend `ValidateRegistration`/`TryParseRegistration` to recognize these additional prefix/suffix syntaxes, each mapped to a distinct trigger-type byte in the registration wire format. |
| ERR (default message codes 1–6) | 8 | `ERR`/`MSG` opcodes fully wired; an `ErrorMessages`/`CodeMessageService` system exists for editable message text. | Whether the specific default code catalog (1 Limit reached, 2 Voltage limit, … 6 External control signal) is pre-seeded could not be confirmed from the visible portion of the data seeder. | Verify `InitializeDataSeeder.cs`'s message-code seed data against the desktop's 6 default codes; add any missing defaults. |
| TABLE — loading (by filename) | 8 | `ValidationHelper.ValidateNominal`'s TABLE branch + `FileManagerService.ValidateFile` load a table by filename. | No 8-character filename length constraint enforced (the desktop limits table filenames to 8 characters). | Add a length check to the TABLE branch of `ValidateNominal`, or document that the constraint no longer applies if the storage layer here doesn't need it (worth confirming with the device protocol before enforcing an artificial limit). |

---

## Different Implementation

### Materially different (8) — same intent, different mechanism

| Feature | Sheet | Desktop Spec Behavior | Web App Behavior | Impact |
|---|---|---|---|---|
| Program listing / tree view | 1 | Folder-organized tree with name/number filtering (yellow-highlight matches) and a "Preview" table toggle. | `ProgramList.razor` uses a single flat, sortable/searchable `DataTable<ProgramDTO>` — no tree, no folders, no toggle (the table is the only view). | Usable at small-to-medium program counts; will not scale to the desktop's expectation of deep folder hierarchies for large program libraries. |
| Open Program Editor | 1 | Double-click a tree node, or "Edit Program" from a Program Information window. | Click a "Code" icon button in the list row's Actions column, opening `ProgramEditor` in a new tab. | Functionally equivalent outcome, different trigger — low impact. |
| Cancel / unsaved-changes prompt | 1 | Cancel prompts "save before closing?" if there are unsaved changes. | `ProgramEditor.razor` auto-persists unsaved edits to server-side session storage (`ServerSessionStorageService`) on every dirty change and silently restores them on next open; `HandleSaveDialogDiscard()` discards rather than prompting. | Arguably safer (no risk of losing work by navigating away) but behaves differently than a user familiar with the desktop app would expect — no "you have unsaved changes" confirmation dialog exists. |
| Comment lines | 2 | Enter "!" in the Label column to insert a comment line; comment text via the field's edit button. | `StepModel` has a dedicated, separate `Comment` field — no "!" marker convention. | Functionally similar end result (steps carry comments) via a cleaner structured field; loses the specific desktop nuance of comments being confirmed via arrow keys or requiring separate rows for multi-line comments. |
| CHA (Charge) / DCH (Discharge) | 3 | Single `CHA`/`DCH` operator whose Nominal Value determines the control mode (current/voltage/power). | Split into 4 explicit operators each for charge and discharge: `CC_CHG`/`CV_CHG`/`CP_CHG`/`CCCV_CHG` and `CC_DCHG`/`CP_DCHG`/`CCCV_DCHG`/`CV_DCHG`. | Arguably clearer/more explicit in the UI (the control mode is visible in the operator name rather than inferred from the nominal value's unit), but a direct 1:1 mapping to the desktop's single-operator model doesn't exist. |
| Preset registration formats | 7 | Named built-in formats with documented per-registration byte costs: CHANREG/CHAREG (42B), CYCLE/CYCLEC/DCHREG family (90B), GSM, SIMPLE (12B), STANDARD (42B). | `InitializeDataSeeder.cs` seeds only two formats — `STANDARD` (A,V,C,W,Ah) and `SIMPLE` (A,V,C,W) — but formats are fully user-editable via `Components/Pages/Programs/Registrations.razor`, arguably more flexible than the desktop's fixed preset list. | None of the desktop's specific preset names/channel-sets/byte-cost accounting carry over; users must recreate any desired preset manually. |
| Procedures — create/invoke | 9 | Select existing steps → right-click "Create procedure" → name it; stored under a "Procedures" section; invoked by entering its name in the Operator column. | `PRODUCER` operator's Nominal Value references the name of a **whole separate, already-saved Program** (not an ad-hoc step selection), inlined at build time via `ProgramBuilder.ExpandProducerSteps`. | A materially different authoring model: whole-program reference vs. step-selection-to-procedure. Invocation itself works equivalently once the "procedure" (i.e., program) exists; only the creation workflow differs. |

### Cosmetic — battery parameter naming only (11)

All 11 of Sheet 10's battery parameters exist as data on `Models/Entities/Batteries.cs`, just under plain-English names instead of the desktop's abbreviations. Functionally equivalent as stored values; only the naming convention differs.

| Desktop Name | Web App Field (`Models/Entities/Batteries.cs`) |
|---|---|
| CNom | `NominalCapacity` |
| NoCell | `NumberOfCells` |
| UGas | `GassingVoltage` |
| UMax | `MaximumVoltage` |
| INom | `NominalCurrent` |
| ICrank | `ColdCrankingCurrent` |
| ChargeF | `ChargeFactor` |
| Rin | `Impedance` |
| CutOff | `BreakVoltage` |
| UNom | `NominalVoltage` |
| EDensity | `EnergyDensity` |

*(The one genuine functional gap for this sheet — these values not being referenceable inside a program step's Nominal Value/Limit/Registration column — is listed under Missing Features → Sheet 10 above.)*

Two additional **naming/convention** differences worth flagging even though the underlying row was classified Partial/Missing above, not Different:

- **Watt vs. "W" convention reversed:** the desktop spec requires power to be entered as `"Watt"` (not `"W"`); this app's `ValidationHelper.AutoCorrectUnit` normalizes `"watt"`/`"watts"` input **to** `"W"` — the exact opposite convention. Anyone porting programs by hand from desktop documentation needs to know this reverses.
- **TABLE field order:** the desktop's file-authoring pattern is `duration;current;power;voltage`; this app's `FileManagerService.ValidateFileContent`/`ProgramBuilder.BuildTableOPTxtToBinary` parse the same field *order* (time, then up to 3 floats) — confirmed compatible, not a gap, but worth a one-line note in user-facing docs since the column labels aren't shown anywhere in the UI.

---

## Unable to Verify

These require either live device/firmware behavior or UI interaction that static code reading cannot confirm. Do not treat these as either implemented or missing without hands-on testing.

| Feature | Sheet | Why It Can't Be Verified From Code |
|---|---|---|
| Multi-system program support (Test/Formation/Laboratory variants) | 1 | No `SystemType` field found under the specific names searched, but a differently-named mechanism could exist in device/circuit configuration paths not covered by this investigation's search terms. |
| Channel-unit constraint (units usable, names not referenceable) | 2 | `OperatorConstants.ValidUnits`'s fixed whitelist structurally prevents arbitrary channel-name references, but this is inferred from validator design, not an explicit documented rule or test confirming the exact desktop semantics. |
| Delete field content (Backspace/Del/Spacebar/Esc semantics) | 2 | Standard HTML input elements are used; native browser key behavior would apply by default, but no live UI testing was performed to confirm exact parity with the desktop's specific key-by-key behavior. |
| Help Dialogs (Nominal Value / Registration column helper dialogs) | 2 | The Operator column's searchable dropdown was confirmed, but Nominal Value/Registration columns' exact interaction pattern (plain input vs. dialog-driven) needs live UI verification, not just source inspection. |
| STO — dispo-list (queued test) interaction | 6 | This app has no dispo-list/queue concept in the Program Editor at all, so the desktop's specific claim ("STO does not stop the remainder of a queued dispo-list") cannot be evaluated — the underlying concept may not apply to this system's architecture. |
| Procedures — shared-edit warning | 9 | No matching warning-dialog string was found via text search, but a runtime toast could theoretically exist under different wording not covered by the search terms used. |
| Counter settings (re-preset Cycle/Ah/Wh via SET before resuming) | 9 | `SET` can assign arbitrary named variables, but whether the device firmware specifically recognizes reserved names like `Ah`/`Wh`/`Cyc` as counter re-presets is a device-protocol question outside this codebase's visibility. |

---

## Recommendations

Prioritization reflects: safety/data-integrity impact, how central the capability is to routine battery-testing workflows described in the spec, and implementation cost relative to value. All 53 distinct missing capabilities (see Missing Features tables above) are assigned below; several Sheet 2/9 workflow items and Sheet 5 gradient limits are elevated due to safety or usability-at-scale concerns even though they're not the cheapest to build.

### High Priority

Safety-relevant, resume-from-failure-relevant, or blocking for everyday multi-user program management at scale:

1. **Global Nominal Values / Limits** (Sheet 8) — wire format already reserves the byte; the whole-run safety ceiling this provides is a meaningful gap without it.
2. **GradT / GradTm, GradU(m) / GradI(m)** (Sheet 5) — rate-of-change thermal/voltage/current limits are classic battery-safety mechanisms; their absence removes a category of protection the desktop relies on.
3. **SAVE / REST** (Sheets 3, 8) — resume-a-test-from-saved-counter-state is directly relevant to this app's known session-restart/recovery concerns (see prior DBC-reload work in this codebase's history).
4. **ONERROR / ONEXIT special labels** (Sheet 9) — automatic error/completion handling is a standard safety and host-integration pattern; its absence means every program must handle errors ad hoc.
5. **Reserved/forbidden label names** (Sheet 9) — cheap validation guard preventing a user from accidentally shadowing a system counter name with a GOTO label.
6. **GOTO scope restriction (procedure boundaries)** (Sheets 6, 9) — currently a GOTO can silently jump into another program's inlined PRODUCER block; this is a correctness bug risk, not just a missing feature.
7. **Resistance nominal-value mode** (Sheets 3, 4) — Current/Voltage/Power exist but Resistance is a core control mode entirely absent; likely blocks any resistance-controlled test recipe from being authored at all.
8. **Copy / Paste steps, Undo** (Sheet 2) — baseline editing ergonomics; their absence makes authoring long programs error-prone and slow compared to the desktop tool users are migrating from.
9. **Folder management / tree program browser** (Sheet 1) — the flat list will not scale as the program library grows; this is a foundational information-architecture gap, not a cosmetic one.
10. **Print program** (Sheet 1) — commonly needed for compliance/documentation workflows in regulated battery-testing environments.
11. **Program locking ("Checked")** (Sheet 1) — role-gated edit protection is a compliance/audit control, not just a convenience feature.
12. **PARALLEL, RANGE/IRANGE** (Sheets 3, 8) — multi-circuit synchronization and current-range control are used in real test setups beyond simple single-circuit charge/discharge.

### Medium Priority

Meaningfully expands capability but lower safety/data-loss stakes, or has a smaller user base:

1. TASK (OUW/CGRE/CLESS/TCONTR) and its dependent Special Nominal Values (Factor_I/P/U)
2. TABLE — scaling, built-in drive cycles (FUDS/SFUDS/DST), value limiting
3. Timer1–Timer3 limit + TIMER1–3 reserved nominal values (paired gap)
4. AhDef / PercAh / PERCCN_P / PERCCN_C (percentage-of-capacity limits)
5. Battery-parameter-based nominal functions (ACn5, ACn1, ACn2, ACn4, ACn10, ACn20, VnC)
6. Ramp functions (linear value-over-time)
7. Battery parameters usable as global limits (Sheet 10's one functional gap)
8. RLevel variable (dynamic registration verbosity control)
9. Registration frequency/trigger types (delta, threshold, delayed-range, max-count)
10. Linked (AND) limits
11. Logical channel counters — AhStat/AhBal/AhPrev/WhPrev
12. Calculation / graph view (pre-run profile plot)
13. Pascal Editor
14. Procedures — Decompose/Recompose
15. CYC repeat-count via SET-defined variable ("N*" format)
16. Program-number field + "Next available" auto-assign (Create/Save program)
17. Help-dialog consistency across Nominal Value/Registration columns

### Low Priority

Niche, hardware-gated, or clearly out-of-scope by design:

1. **EIS operator + EIS registration format** — already confirmed intentionally excluded per `Docs/comparison.html` ("BTS does not support EIS hardware"); no action recommended unless product scope changes.
2. FILTER — Digatron-hardware-only; irrelevant unless that hardware is in scope.
3. RCH, SETMUX, ADD, SUB, BATT, PROT — legacy/niche operators with narrow use cases.
4. PAUA, PAUO, URANGE, IRANGE, OUTA, OUTB, ISOEXT, ISOINT — blocked on an external reference document not available to this codebase.
5. SYNCLine / SYNCProgram — niche multi-circuit feature; consider bundling with PARALLEL work if that's picked up.
6. CLEAR — narrow-scope state-reset operator.
7. FILE test-section naming (incl. TSZ auto-naming) — nice-to-have organizational feature.
8. Special customer limit options (ABATT, ACN5, OHM, VBATT, VNC, WATT)
9. A_notAbs / mA_notAbs sign-aware limits
10. deltaV (GREAL[100]-[119] gradient analysis) — largest single scope item in the whole list; defer until there's a concrete customer need.
11. TABLE filename 8-character limit — likely an artificial desktop-era constraint not worth reintroducing unless the device protocol requires it.
12. Watt/"W" convention alignment — documentation fix, not a code gap (see Different Implementation section).
13. Multi-system program support (Test/Formation/Laboratory variants) — Unable to Verify; investigate only if a concrete multi-system requirement emerges.
