# 18 — Program Editor: Authoring Guide

> **Scope:** how to build a valid test **Program** in `/program/editor/{id}` — every
> operator's rules, what is mandatory vs optional, the Limit/Action/Label/Registration
> grammar, and worked examples from a one-line program up to a multi-loop, multi-cutoff
> composite program.
>
> Every rule below is read directly from the editor's own validation code, not inferred
> from behaviour. See **Files involved** at the end for exact file:line references.

---

## 1. The three things that decide "Valid"

The header pill (`Valid` / `N errors`) is `errors.Count == 0` from `ValidateProgram()`.
Three passes build that list, in order, for every step:

1. **Nominal Values** — presence (does this operator need any?) then format (per-operator grammar).
2. **Limits → Actions** — presence (does this operator require at least one pair?) then grammar for the Limit and, separately, the Action.
3. **Registrations** — grammar depends on whether the step is `SET`/`REG` (must name an existing Standard) or anything else (must be `value unit`).

Plus two whole-program checks that run once per save: every `CYC` must have a preceding open `BEG` (and vice versa), and every `PRODUCER` step must name a program that actually exists.

*(`Components/Pages/Programs/ProgramEditor.razor:764-979`)*

---

## 2. Structural rules that apply to every program

| Rule | Detail |
|---|---|
| **Step 1 is pinned** | The row with `StepNumber == 1` can never be removed (`RemoveStep`, line 469). By convention (and the built-in default template) it is a `SET` step. |
| **The `STO` step is pinned** | Any step whose operator is `STO` can never be removed or duplicated. `Add Step` always inserts the new row **immediately before** the `STO` row, never after it (`AddStep`, lines 427-464) — so `STO` is effectively always last. |
| **New rows default to `CC Chg`** | Every `+ Add Step` click inserts a fresh `CC_CHG` step (line 429). You then change its Operator dropdown to whatever you actually need. |
| **A brand-new/near-empty program seeds itself** | If a loaded program has fewer than 2 steps, the editor replaces it with `SET (Registrations: [STANDARD]) → CC_CHG → STO` (lines 283-291). |
| **Limit/Action pairs are 1:1 and index-locked** | `Limits[i]` always pairs with `Actions[i]`. Removing pair `i` removes both at once (`RemoveLimitActionPair`, lines 633-643). You cannot have a Limit with no matching Action slot or vice versa. |
| **An Action may legally be blank** | `ValidateAction` returns valid for an empty/whitespace string (`ValidationHelper.cs:315-317`). A Limit still counts as "present" with no Action — see the gotcha in §7. |
| **Labels have no format rule and no uniqueness check** | Any non-empty string is accepted as a Label (trimmed, case-insensitive when matched). Nothing in `ValidateProgram` rejects two steps sharing the same label — see the gotcha in §7. |
| **Duplicate `SET` variable names are rejected** | If the same variable name is declared in more than one `SET` step, every step that declares it gets a `Duplicate variable: <name>` error (lines 708-730). |

---

## 3. Global variables (`SET`)

A `SET` step's Nominal Value field takes `AllowCustom` free-text entries in the form:

```
(varName = number)
(varName = number unit)
```

- `varName` must match `^[a-zA-Z][a-zA-Z0-9_]*$`.
- `unit`, if given, must be one of the **Valid Units** in §5.
- A `SET` step needs **zero** Nominal Values to be valid — it's only mandatory if you're declaring variables. A bare `SET` with just a `Registrations` entry (e.g. `STANDARD`) is perfectly valid — that's exactly what the default template's row 1 is.

Once declared, `varName` can be used **instead of a literal number** in:
- another step's **Limit** (`> myVar` instead of `> 4.2 V` — no unit needed if the variable already carries one),
- another step's **Action** is not variable-aware (Actions are fixed keywords, see §4.9),
- another step's **Registration** entry, or a later `SET`'s own nominal value is *not* variable-aware (each `SET` line defines its own variable).

*(`Components/UI/Program/ValidationHelper.cs:96-140`, `:283-303`)*

---

## 4. Every operator, in full

Legend: **Nominal** = what the Nominal Values column needs · **Limit/Action** = whether a Limit-Action pair is required, and what's allowed in it · **Registrations** = grammar for that column.

### 4.1 `SET` (code 10)
- **Purpose:** declare global variables and/or set the active Registration Standard for everything that follows.
- **Nominal:** optional; free-text `(var = value[ unit])` — see §3.
- **Limit/Action:** not allowed (`LimitActionAllowed[SET] = false`) — the UI won't even show the column controls as required.
- **Registrations:** each entry must exactly match (case-insensitive) an existing Standard name configured under **Program → Registrations** (e.g. `STANDARD`).
- **Example:** `SET` with Registrations = `[STANDARD]`, Nominal = `(cutoffV = 4.2 V)`.

### 4.2 `STO` (code 11)
- **Purpose:** terminate the program. Always the last step; pinned by the editor.
- **Nominal:** disabled (`NominalDisabled[STO] = true`) — no fields shown.
- **Limit/Action:** not allowed.
- **Registrations:** none needed.
- **Wire note:** emits no extra payload at all (`case STO: break;` in `DecoderService.ConvertProgramIntoBytesPackets`).

### 4.3 `CC Chg` — Constant-Current Charge (code 1)
- **Nominal:** exactly 1 field — **Current**, unit `A` (e.g. `10 A`).
- **Limit/Action:** **required**, ≥1 pair. Grammar in §5.
- **Registrations:** `value unit` entries (§6).
- **Example row:** Nominal `10 A` · Limit `> 4.2 V` · Action `STO`.

### 4.4 `CV Chg` — Constant-Voltage Charge (code 2)
- **Nominal:** exactly 1 field — **Voltage**, unit `V` (e.g. `12.6 V`).
- **Limit/Action:** required, ≥1 pair.
- **Example:** Nominal `12.6 V` · Limit `< 0.5 A` · Action `STO` (typical CV "taper current" cutoff).

### 4.5 `CP Chg` — Constant-Power Charge (code 3)
- **Nominal:** exactly 1 field — **Power**, unit `W` (e.g. `100 W`).
- **Limit/Action:** required, ≥1 pair.

### 4.6 `CCCV Chg` — CC-then-CV Charge (code 4)
- **Nominal:** exactly 2 fields — **Current** (`A`) then **Voltage** (`V`), both mandatory.
- **Limit/Action:** required, ≥1 pair.
- **Example:** Nominal `1 A`, `4.2 V` · Limit `< 0.05 A` · Action `STO`.

### 4.7 `CC DChg` — Constant-Current Discharge (code 5)
- **Nominal:** exactly 1 field — **Current** (`A`).
- **Limit/Action:** required, ≥1 pair.
- **Example:** Nominal `20 A` · Limit `< 3.0 V` · Action `STO`.

### 4.8 `CP DChg` — Constant-Power Discharge (code 6)
- **Nominal:** exactly 1 field — **Power** (`W`).
- **Limit/Action:** required, ≥1 pair.

### 4.9 `CCCV DChg` — CC-then-CV Discharge (code 7)
- **Nominal:** exactly 2 fields — **Current** (`A`) then **Voltage** (`V`).
- **Limit/Action:** required, ≥1 pair.

### 4.10 `CV DChg` — Constant-Voltage Discharge (code 19)
- **Nominal:** exactly 1 field — **Voltage** (`V`).
- **Limit/Action:** required, ≥1 pair.

### 4.11 `PAU` — Pause / Rest (code 8)
- **Nominal:** disabled — no nominal fields at all.
- **Limit/Action:** **required**, exactly the "time to wait" condition:
  - **No comparison operator is allowed** — `ValidationHelper.ValidateLimit` explicitly rejects a leading `<`, `>`, `=`, etc. for `PAU` (`"PAU does not allow comparison operators"`).
  - The value must be a number followed by a **time unit only**: `s`, `sec`, `min`, `m`, `hr`, `h`. Example: `30 min`, `1800 s`.
  - A `SET` variable may be used instead of a literal; if that variable has no unit of its own, `PAU` silently treats it as seconds.
  - **Action is optional** even though the Limit is required.
- **Example:** Limit `30 min` · Action *(blank, or `STO` to end the whole program after the rest)*.

### 4.12 `GOTO` — Unconditional Jump (code 9)
- **Nominal:** exactly 1 field, **Target** — either an existing step number or an existing Label (case-insensitive). Non-existent targets are rejected: `"Step {n} does not exist"` / `"Must be step number or valid label"`.
- **Limit/Action:** not allowed — the jump is unconditional, so there is nothing to gate it on.
- **Registrations:** technically accepted by the wire builder but there's no practical reason to set any here.
- **Example:** a step with Label `Retest`; later, a `GOTO` step with Nominal = `Retest` jumps back there every time it's reached.
- Don't confuse this with the **`GOTO <label>` Action** available on other operators (§4.13) — that one is *conditional* (fires only when its paired Limit trips); this operator-level `GOTO` always fires.

### 4.13 The Action vocabulary (applies to every Limit/Action pair, on any operator that allows them)
`ValidationHelper.ValidateAction` accepts exactly these, case-insensitively, and nothing else:

| Action | Meaning |
|---|---|
| *(blank)* | Valid but does nothing when the Limit trips — see gotcha in §7. |
| `INT` | Interrupt/end this step's phase. |
| `STO` | Stop the whole program. |
| `ERR <n>` | Raise error code `n` (integer). |
| `MSG <n>` | Raise message code `n` (integer). |
| `GOTO <label-or-stepnum>` | Jump to that step, conditionally, only when this pair's Limit is met. |

Anything else is rejected with `"Only allowed: INT, STO, ERR <num>, MSG <num>, GOTO <label>"`.

### 4.14 `CYC` — Close a Loop (code 12)
- **Nominal:** exactly 1 field — **Count**, and unlike `BEG`'s field this one *is* format-checked: must parse as an integer (`"Must be a number"` otherwise).
- **Limit/Action:** not allowed.
- **Program-level rule:** every `CYC` must have an earlier, still-open `BEG` — matched with a stack, so `CYC`s close the *most recently opened* `BEG` first (nesting is supported). A `CYC` with no open `BEG` before it errors `"CYC requires a BEG before it"`.

### 4.15 `BEG` — Open a Loop (code 13)
- **Nominal:** exactly 1 field — **Cycle Name**; any non-empty text (no format check, no numeric requirement).
- **Limit/Action:** not allowed.
- **Program-level rule:** every `BEG` must eventually be closed by a `CYC`, or it errors `"BEG requires a matching CYC after it"`.
- Nested loops: `BEG(outer) … BEG(inner) … CYC(inner) … CYC(outer)` is valid; the stack pops the inner `BEG` for the inner `CYC` first.

### 4.16 `INT` (code 14)
- **Purpose:** a standalone interrupt marker.
- **Nominal:** disabled.
- **Limit/Action:** not allowed as an operator.
- Note: `INT` is also one of the five words usable as an **Action** on another step (§4.13) — same keyword, two different contexts (standalone step vs. conditional action).

### 4.17 `REG` — Mid-Program Registration Change (code 15)
- **Purpose:** two independent things bundled into one operator:
  1. **Nominal Value** (field label "Value", optional): a **grouping label only** for logged data, never sent to the device. If provided it must be ≤8 characters, letters/digits/`_`/`-` only (`"REG label must be 8 characters or fewer"` / `"...letters, numbers, '_' and '-'"`). Leaving it blank is explicitly valid (`ValidateNominal` returns valid for empty on `REG`).
  2. **Registrations list**: which Standard(s) apply from this point on. If this `REG` step has its own Registrations entries, they're matched against Standards exactly like a `SET` step. If it has **none**, it inherits from the **nearest preceding `SET` step** in the program (not just the first one — useful in multi-block programs).
- **Limit/Action:** not allowed.
- **Example:** `REG` with Nominal blank, Registrations = `[STANDARD]` — re-asserts the standard mid-sequence after an earlier `SET` changed it.

### 4.18 `ERR` — Raise an Error Code (code 16)
- **Nominal:** exactly 1 field — **Error Code**, must be an integer.
- **Limit/Action:** not allowed as an operator (it *is* usable as an Action elsewhere, §4.13).
- **Example:** standalone `ERR` step with Nominal `7` — unconditionally raises error 7 when the sequence reaches it.

### 4.19 `MSG` — Raise a Message Code (code 17)
- Same shape as `ERR`: **Nominal** = integer Message Code. Also usable as an Action elsewhere.

### 4.20 `TABLE` — Custom Profile Table (code 18)
- **Nominal:** exactly 1 field — must be the name of a **table file that already exists** in **Program → TableFiles**. Validated live against the file manager (`FileManagerService.ValidateFile`); an unknown name is rejected.
- **Limit/Action:** not allowed.
- **Registrations:** `value unit` entries as usual.

### 4.21 `PRODUCER` — Call Another Program (code 20)
- **Nominal:** exactly 1 field — must be the **exact name of another existing program** (validated against every other saved program). Errors: `"PRODUCER: a program must be selected"` (blank) or `"PRODUCER: program '<name>' not found"`.
- **Limit/Action:** not allowed.
- **What happens on export/build:** the `PRODUCER` step is **inlined** — replaced by the referenced program's steps with that program's own first `SET` and last `STO` stripped out, then every step number in the whole (now-longer) program is renumbered from 1 (`ProgramBuilder.ExpandProducerSteps`). This is only resolved when the *outer* program is converted to device packets — inside the editor, the `PRODUCER` step just sits there as one row pointing at the sub-program's name; use the "eye"/view action in the row to preview the referenced steps read-only.
- **Use case:** build a reusable block once (e.g. "Rest_5min": `SET → PAU 5 min → STO`) and reference it from many outer programs instead of copy-pasting.

*(All of §4 sourced from `Components/UI/Program/OperatorConstants.cs:1-120` [operator table, `LimitActionAllowed`, `NominalDisabled`, `NominalConfig`] and `Components/UI/Program/ValidationHelper.cs` [`ValidateNominal`, `ValidateLimit`, `ValidateAction`, `ValidateRegistration`].)*

---

## 5. Limit grammar (the "condition" half of a pair)

```
[ comparison-op ] value unit
[ comparison-op ] value          ← only for a value that is a global variable already carrying a unit
```

- **Comparison operators** (optional, longest-match-first): `>=`  `<=`  `!=`  `==`  `>`  `<`  `=`
  — **except `PAU`, which forbids all of them.**
- **`value`** is either a literal number (max 6 significant digits total, before+after the decimal point combined) or the name of a variable declared in an earlier `SET`.
- **`unit`** must be one of the Valid Units below. If `value` is a variable that already has its own unit, do **not** repeat a unit — that's an explicit error (`"<var> already has unit <u>, don't add unit"`).
- **`PAU`'s unit is restricted further**: only `s, sec, min, m, hr, h` — any other unit (even a normally-valid one like `A` or `V`) is rejected there.

**Valid Units** (`OperatorConstants.ValidUnits`):
`A`, `V`, `W`, `Wh`, `WhCha`, `WhDch`, `WhStep`, `Ah`, `AhCha`, `AhDch`, `AhStep`, `C`, `s`, `sec`, `min`, `m`, `hr`, `h`

Common unit typos are auto-corrected (`AutoCorrectUnit`): `amp/amps→A`, `volt/volts→V`, `watt/watts→W`, `degC/°C→C`, `sec/second/seconds→s`, `min/minute/minutes→min`, `hr/hour/hours→hr`, `ah/amphour(s)→Ah`, `ahcha/ahchg/ahcharge→AhCha`, `ahdch/ahdis/ahdischarge→AhDch`, `wh/watt hour(s)→Wh`, and the `Wh`/`Ah` charge/discharge/step variants similarly. Anything else is compared as a case-insensitive prefix against the list above.

**Valid limit examples:** `> 4.2 V` · `<= 0.05 A` · `> 60 min` (on a non-PAU op, an operator *is* required even for time) · `!= 0 C` · `> myVar` (myVar already has a unit) · `> myVar A` — invalid if `myVar` already has a unit.

*(`Components/UI/Program/ValidationHelper.cs:214-296`, `Components/UI/Program/OperatorConstants.cs:78-83`)*

---

## 6. Registration grammar (the "Registrations" column)

Two completely different rulesets depending on the operator:

| Operator | Rule |
|---|---|
| `SET` | Each entry must exactly (case-insensitive) match an existing Standard name from **Program → Registrations**. |
| `REG` | Same Standard-name match *if you provide any*; if you provide none, it inherits the nearest preceding `SET`'s Registrations at build time. |
| Every other operator | Each entry is `value unit` — a number (or a `SET` variable, whose value/unit substitute automatically) plus a **Valid Unit** from §5. This is a per-step "also record this specific reading" tag, independent of the Standard selected at `SET`. |

*(`Components/UI/Program/ValidationHelper.cs:373-421`, `Services/ProgramBuilder.cs:406-448`)*

---

## 7. Gotchas — things the editor will *let* you do that you probably don't want

1. **A "20 A"-style grey placeholder is not a value.** The Nominal/Limit boxes show an example in light grey when empty (e.g. `20 A`). If you don't actually type into the field, it stays empty and errors as `"Empty value"` — it only *looks* filled. (This is exactly the bug this session hit while building a test program: `20 A` present, but the step showed the "Empty value" error.)
2. **A blank Action is legal.** If you only fill the Limit and leave the Action box empty, the pair validates and the "Add" checkmark commits — but nothing happens when the device hits that limit. Always set `STO`/`INT`/`ERR n`/`MSG n`/`GOTO x` unless you deliberately want a no-op cutoff.
3. **Duplicate Labels are not rejected.** Two steps can share the same Label with no error. Any `GOTO` (operator or action) targeting that Label always resolves to the **first** step in program order that carries it (`FirstOrDefault`), silently ignoring the duplicate.
4. **The pending "+ " row is not counted until you actually add it.** Clicking the pencil/✓ to confirm a new Limit/Action pair is what adds it to `Limits`/`Actions`; an unconfirmed pending row does not show up as its own error — but the *step* still reports `"At least one limit-action pair required"` until one pair is truly added.
5. **`SET` is optional-content, not optional-existence.** You never need to fill `SET`'s Nominal Values, but the *step itself* is pinned at position 1 and cannot be deleted — you can only change what it declares.
6. **A step-number `GOTO` target is checked against real step numbers, which shift.** Adding/removing/moving steps renumbers everything (`RenumberSteps`); a `GOTO 5` typed before a reorder may point at a different step afterward. Prefer Labels over raw step numbers for anything you intend to reorder around.

---

## 8. Worked examples

### 8.1 Minimal valid program (the built-in default template)

| # | Label | Operator | Nominal | Limit → Action | Registrations |
|---|---|---|---|---|---|
| 1 | | SET | | | `STANDARD` |
| 2 | | CC Chg | `10 A` | `> 4.2 V` → `STO` | |
| 3 | | STO | | | |

Charges at 10 A until the pack reaches 4.2 V, then stops. This is exactly what `10Step_TestProgram`'s rows 1–2 and 10 look like — the shape every program starts from.

### 8.2 Multiple safety cutoffs on one step

A single `CC Chg` step can carry several Limit/Action pairs — the first one the device actually reaches wins:

| # | Operator | Nominal | Limits → Actions |
|---|---|---|---|
| 2 | CC Chg | `20 A` | `> 4.2 V` → `STO` |
| | | | `> 45 C` → `ERR 12` |
| | | | `> 90 min` → `MSG 3` |

Normal completion stops on voltage; an over-temperature condition raises error 12 instead; running long raises message 3 (both without necessarily stopping — pair those with `STO` too if you want the program to actually end).

### 8.3 Global variable reused across steps

| # | Operator | Nominal | Limit → Action | Registrations |
|---|---|---|---|---|
| 1 | SET | `(vmax = 4.2 V)` | | `STANDARD` |
| 2 | CC Chg | `10 A` | `> vmax` → `STO` | |
| 3 | CCCV Chg | `5 A`, `vmax` | `< 0.05 A` → `STO` | |
| 4 | STO | | | |

`vmax` is declared once and reused as the cutoff in step 2's Limit **and** as `CCCV Chg`'s second Nominal field in step 3 — change it in one place, both steps follow.

### 8.4 Rest phase between charge and discharge, using `PAU`

| # | Operator | Nominal | Limit → Action |
|---|---|---|---|
| 2 | CC Chg | `10 A` | `> 4.2 V` → `STO` |
| 3 | PAU | *(disabled)* | `30 min` → *(blank)* |
| 4 | CC DChg | `20 A` | `< 3.0 V` → `STO` |

Note `PAU`'s Limit has **no** comparison operator — just `30 min`.

### 8.5 Loop with labels and a conditional skip

| # | Label | Operator | Nominal | Limit → Action |
|---|---|---|---|---|
| 2 | `CycleStart` | BEG | `Endurance` | |
| 3 | | CC Chg | `10 A` | `> 4.2 V` → `STO` |
| 4 | | CC DChg | `20 A` | `< 3.0 V` → `GOTO CycleStart` |
| 4b| | CC DChg limit 2| | `< 2.5 V` → `ERR 9` |
| 5 | | CYC | `10` | |

`BEG` opens a 10-cycle loop (`CYC`'s Nominal = `10`); the discharge step's first Action jumps back to the labelled `BEG` row to repeat, while a second, more severe voltage limit on the same step raises an error instead of looping if the pack drops further than expected.

### 8.6 Composing with `PRODUCER`

Build once, reuse everywhere:

**Sub-program `Rest_5min`:**
| # | Operator | Nominal | Limit → Action |
|---|---|---|---|
| 1 | SET | | |
| 2 | PAU | | `5 min` → *(blank)* |
| 3 | STO | | |

**Main program:**
| # | Operator | Nominal |
|---|---|---|
| 1 | SET | |
| 2 | CC Chg | `10 A` → `> 4.2 V` → `STO` |
| 3 | PRODUCER | `Rest_5min` |
| 4 | CC DChg | `20 A` → `< 3.0 V` → `STO` |
| 5 | STO | |

At export, step 3 is replaced by `Rest_5min`'s middle step only (its own `SET`/`STO` are stripped), so the built program is really `SET → CC Chg → PAU 5min → CC DChg → STO`, renumbered 1–5.

---

## Files involved

- `Components/Pages/Programs/ProgramEditor.razor` — page shell, `AddStep`/`RemoveStep`/`DuplicateStep`/`MoveStep` (structural rules, §2), `ValidateProgram`/`ValidateStep` (the three validation passes, §1, and the `BEG`/`CYC`/`PRODUCER` program-level checks).
- `Components/UI/Program/OperatorConstants.cs` — operator code table, `LimitActionAllowed`, `NominalDisabled`, `NominalConfig` (per-operator nominal field definitions), `ValidUnits`, `ConditionalOperators`.
- `Components/UI/Program/ValidationHelper.cs` — `ValidateNominal`, `ValidateLimit`, `ValidateAction`, `ValidateRegistration`, `AutoCorrectUnit`, `ParseSetVariable` — the exact grammar for every field in §3–§6.
- `Components/UI/Program/StepModel.cs` — the `StepModel` shape (`Label`, `Comment`, `NominalValues`, `Limits`, `Actions`, `Registrations` as independent lists, index-paired for Limits/Actions).
- `Services/ProgramBuilder.cs` — what each operator actually becomes on the wire: `ProcessSetOperator`, `ProcessRegOperator` (§4.17's inheritance rule), `ProcessGotoOperator`, `ProcessTableOperator`, `ProcessDefaultOperator`, `AddRegistrations` (§6's `value unit` encoding), `ExpandProducerSteps` (§4.21's inlining).
- `Services/DecoderService.cs:ConvertProgramIntoBytesPackets` — the per-operator dispatch switch that decides which `ProgramBuilder` processor runs for each step.
