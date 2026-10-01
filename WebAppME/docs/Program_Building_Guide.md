# Program Building Guide

A **Program** is the step-by-step test sequence you build in the Program Editor
(Program → Programs → Create/Edit) and run against a battery. This guide explains
exactly what each step type needs to be accepted as **Valid**, what is mandatory
and what is optional, and walks through example programs from the simplest
possible test up to loops and reusable sub-programs.

The editor tells you your program's status at all times, next to the program name:
a green **Valid** badge, or a red **N errors** count. A program with any errors
cannot be sent to a device — fix every error first.

---

## 1. The shape every program starts from

Every new program begins as three steps, and two of them are permanent:

| # | Step Type | What it does |
|---|---|---|
| 1 | **SET** | Always the first step. Cannot be deleted. Used to pick which measurements get logged, and optionally to define reusable values. |
| 2 | **CC Chg** (Constant-Current Charge) | A placeholder step — change its type to whatever your test actually needs. |
| 3 | **STO** (Stop) | Always the last step. Cannot be deleted. Ends the test. |

Clicking **+ Add Step** always inserts a new step right before the final **STO** —
so **STO** always stays last, no matter how many steps you add.

---

## 2. What makes a step "Valid"

Every step is checked against three things:

1. **Nominal Value(s)** — the main setting for that step type (a current, a
   voltage, a target program, etc.). Some step types need one, some need two,
   some need none.
2. **Limit → Action** — an optional or required condition-and-response pair:
   *"when this measurement crosses this value, do this."* Certain step types
   (all the charge/discharge/rest types) require **at least one** such pair;
   most other step types don't use this at all.
3. **Registrations** — which data points get logged for this part of the test.

Get all three right on every step, and the program shows **Valid**.

---

## 3. Reusable values (the `SET` step)

A **SET** step can define one or more named values you reuse later in the
program, written like this:

```
(myLimit = 4.2 V)
```

- The name (`myLimit`) can be letters, numbers and underscores, but must start
  with a letter.
- The unit is optional but must be a recognized unit if given (see the unit
  list in §6).
- You don't have to define anything in a `SET` step — a bare `SET` step (just
  used to pick a Registration, see §7) is completely valid too.
- Once defined, you can type that name instead of a number anywhere a
  condition or setting expects a value — for example a Limit of `> myLimit`
  instead of `> 4.2 V`. If you change the value in the `SET` step, every place
  that reuses the name follows automatically.
- Don't define the same name twice — the editor will flag every step that
  does.

---

## 4. Every step type, and what it needs

### SET — Start of a test block
- **Nominal Value:** optional (see §3).
- **Limit/Action:** not used.
- **Registration:** required — pick the measurement set (e.g. `STANDARD`)
  that applies from here on. This must match a Registration Standard that's
  already configured for the system.

### STO — Stop
- **Nominal Value:** none — there's nothing to fill in.
- **Limit/Action:** not used.
- Always the final step; the editor won't let you delete or move it away
  from the end.

### CC Chg — Constant-Current Charge
- **Nominal Value:** one field, the charge current (e.g. `10 A`).
- **Limit/Action:** **required** — at least one condition, e.g. "stop when
  voltage passes 4.2 V."
- **Example:** current `10 A`, condition `> 4.2 V`, response `STO`.

### CV Chg — Constant-Voltage Charge
- **Nominal Value:** one field, the charge voltage (e.g. `12.6 V`).
- **Limit/Action:** required — commonly a low-current "taper" cutoff, e.g.
  `< 0.5 A` → `STO`.

### CP Chg — Constant-Power Charge
- **Nominal Value:** one field, the charge power (e.g. `100 W`).
- **Limit/Action:** required.

### CCCV Chg — Constant-Current then Constant-Voltage Charge
- **Nominal Value:** two fields — current, then voltage (both required).
- **Limit/Action:** required.
- **Example:** `1 A`, `4.2 V`; condition `< 0.05 A` → `STO`.

### CC DChg — Constant-Current Discharge
- **Nominal Value:** one field, the discharge current (e.g. `20 A`).
- **Limit/Action:** required, e.g. `< 3.0 V` → `STO`.

### CP DChg — Constant-Power Discharge
- **Nominal Value:** one field, the discharge power (e.g. `100 W`).
- **Limit/Action:** required.

### CCCV DChg — Constant-Current then Constant-Voltage Discharge
- **Nominal Value:** two fields — current, then voltage.
- **Limit/Action:** required.

### CV DChg — Constant-Voltage Discharge
- **Nominal Value:** one field, the discharge voltage.
- **Limit/Action:** required.

### PAU — Pause / Rest
- **Nominal Value:** none.
- **Limit/Action:** **required**, but it's simpler than the charge/discharge
  types — it's always a plain duration, with **no** comparison symbol in
  front of it: just `30 min` or `1800 s`, not `> 30 min`.
- **Response is optional.** You can leave it blank (the test just waits, then
  moves on) or set `STO` to end the whole program after the rest.

### GOTO — Jump to Another Step
- **Nominal Value:** one field — the step number or step **Label** to jump
  to. It must be a step that actually exists in the program, or you'll get
  an error.
- **Limit/Action:** not used — this jump always happens unconditionally,
  every time the sequence reaches it.
- To make a step a jump *target*, give it a Label (the small text field on
  the left of each row) and reference that Label from your `GOTO` step
  instead of a raw step number — that way, if you add or remove steps later
  and the numbering shifts, your jump still points at the right place.

### CYC — Close a Loop
- **Nominal Value:** one field — how many times to repeat, as a whole
  number (e.g. `10`).
- **Limit/Action:** not used.
- Every `CYC` must have a matching **BEG** step earlier in the program, or
  it's flagged as an error. Loops can be nested — an inner loop's `CYC`
  always closes the most recently opened `BEG`.

### BEG — Open a Loop
- **Nominal Value:** one field — a name for this loop/cycle (any text).
- **Limit/Action:** not used.
- Every `BEG` must have a matching `CYC` later in the program, or it's
  flagged as an error.

### INT — Interrupt Marker
- **Nominal Value:** none.
- **Limit/Action:** not used as its own step.
- The word `INT` is also one of the possible **responses** you can attach to
  another step's condition (see §5) — same word, two different jobs.

### REG — Change Registration Mid-Program
- **Nominal Value:** optional. If you give it one, it's just a short
  (8 characters or fewer, letters/numbers/`-`/`_` only) label used to group
  logged data — it has no effect on the device itself.
- **Registration:** if you pick one here, it applies from this point on. If
  you leave it blank, this step automatically continues using whatever
  Registration was set by the nearest `SET` step **before** it in the
  program — useful when a program has several test blocks, each starting
  with its own `SET`.
- **Limit/Action:** not used.

### ERR — Raise an Error Code
- **Nominal Value:** one field — the error code, a whole number.
- **Limit/Action:** not used as its own step.
- Also usable as a **response** on another step's condition (see §5), which
  raises the same error only when that condition is actually met.

### MSG — Raise a Message Code
- Same shape as `ERR`: **Nominal Value** is a whole-number message code.
  Also usable as a response elsewhere.

### TABLE — Run a Custom Profile
- **Nominal Value:** one field — the name of a table/profile file that has
  already been uploaded under Program → Table Files. An unrecognized name
  is rejected.
- **Limit/Action:** not used.
- **Registration:** as usual.

### PRODUCER — Insert Another Program
- **Nominal Value:** one field — the exact name of another **existing**
  program on the system. An unrecognized or blank name is rejected.
- **Limit/Action:** not used.
- **What it does:** when the program is run, this step is replaced by the
  referenced program's own steps (minus that program's own opening `SET`
  and closing `STO`, since this program already has its own). This lets you
  build a small reusable block once — e.g. a standard 5-minute rest — and
  drop it into as many programs as you like instead of retyping it.

---

## 5. Conditions and responses (Limit → Action)

Wherever a Limit/Action pair is required or allowed, it works the same way:

**The condition (Limit):**
```
[ comparison symbol ]  value  unit
```
- Comparison symbols: `>` `<` `>=` `<=` `==` `!=` `=`. You can also write it
  with no symbol at all only for `PAU` (see above) — every other step type
  needs one.
- The value can be a plain number, or the name of a value you defined in an
  earlier `SET` step.
- The unit must be one of the recognized units (see §6). If you're reusing a
  `SET` value that already has its own unit, don't add another unit after
  it — that's flagged as an error.

**The response (Action)** — exactly one of:

| Response | What it means |
|---|---|
| *(left blank)* | Nothing happens when the condition is met — the condition is only recorded, not acted on. Usually not what you want; pick one of the options below unless you're sure. |
| `INT` | Interrupt this step early. |
| `STO` | Stop the whole test. |
| `ERR <number>` | Raise that error code. |
| `MSG <number>` | Raise that message code. |
| `GOTO <label or step number>` | Jump to that step, but only when this particular condition is met (unlike the standalone `GOTO` step type, which always jumps). |

A step can have **more than one** condition/response pair — for example, a
charge step can stop normally on voltage, but also raise an error if it runs
too long or gets too hot. Whichever condition the device reaches first is the
one that takes effect.

---

## 6. Recognized units

| Category | Units |
|---|---|
| Current | `A` |
| Voltage | `V` |
| Power | `W` |
| Temperature | `C` |
| Capacity | `Ah`, `AhCha`, `AhDch`, `AhStep` |
| Energy | `Wh`, `WhCha`, `WhDch`, `WhStep` |
| Time | `s`, `sec`, `min`, `m`, `hr`, `h` |

Common alternate spellings are corrected automatically — `amps`, `volts`,
`watts`, `degC`, `seconds`, `minutes`, `hours`, `amphours` and similar all map
to the units above. If you type something the system doesn't recognize, it
will flag the field rather than guess.

---

## 7. Registrations — what gets logged

There are two different situations:

- **On a `SET` or `REG` step:** you choose which pre-configured Registration
  Standard applies (e.g. `STANDARD`). This determines the whole set of
  measurements the device logs from that point in the test onward.
- **On any other step:** each Registration entry is a specific value-and-unit
  reading you want recorded at that step, separate from the overall standard
  chosen at `SET`/`REG`.

---

## 8. Common mistakes to avoid

1. **Don't mistake the example text for an actual value.** Empty fields show
   light-grey example text (like `20 A`) to hint at the expected format —
   that's not a real value until you type your own number in. A field left
   showing only the grey example is still empty and will be flagged.
2. **Don't leave a response blank unless you mean to.** A condition with no
   response is accepted by the editor, but the device won't do anything when
   that condition is met — no stop, no jump, no error. Set an actual
   response unless that's genuinely what you want.
3. **Don't reuse the same Label on two different steps.** The editor doesn't
   stop you, but any jump aimed at that Label will always go to whichever
   step has it **first** in the program — the second one with the same
   Label is silently unreachable by name.
4. **Prefer Labels over raw step numbers for `GOTO`.** Step numbers shift
   automatically whenever you add, remove, or reorder steps. A jump to a
   Label always follows that step wherever it moves; a jump to a bare number
   can end up pointing at the wrong step after an edit.
5. **A `SET` step doesn't need to define anything to be valid** — it's fine
   to use it purely to choose a Registration Standard.

---

## 9. Example programs

### 9.1 Simplest possible program
| # | Step | Setting | Condition → Response | Registration |
|---|---|---|---|---|
| 1 | SET | | | `STANDARD` |
| 2 | CC Chg | `10 A` | `> 4.2 V` → `STO` | |
| 3 | STO | | | |

Charges at 10 A until the pack reaches 4.2 V, then stops.

### 9.2 Multiple safety conditions on one step
| # | Step | Setting | Conditions → Responses |
|---|---|---|---|
| 2 | CC Chg | `20 A` | `> 4.2 V` → `STO` |
| | | | `> 45 C` → `ERR 12` |
| | | | `> 90 min` → `MSG 3` |

Normal completion stops on voltage; overheating raises an error instead;
running unusually long raises a message. Add `STO` to any of these if you
also want the test to end when they trigger.

### 9.3 A reusable value
| # | Step | Setting | Condition → Response | Registration |
|---|---|---|---|---|
| 1 | SET | `(vmax = 4.2 V)` | | `STANDARD` |
| 2 | CC Chg | `10 A` | `> vmax` → `STO` | |
| 3 | CCCV Chg | `5 A`, `vmax` | `< 0.05 A` → `STO` | |
| 4 | STO | | | |

`vmax` is defined once and reused as the charge cutoff for two different
steps — change it in one place and both steps follow.

### 9.4 Rest in the middle of a test
| # | Step | Setting | Condition → Response |
|---|---|---|---|
| 2 | CC Chg | `10 A` | `> 4.2 V` → `STO` |
| 3 | PAU | | `30 min` → *(blank)* |
| 4 | CC DChg | `20 A` | `< 3.0 V` → `STO` |

Charge, rest 30 minutes with no comparison symbol on the pause condition,
then discharge.

### 9.5 A repeating cycle with labels
| # | Label | Step | Setting | Condition → Response |
|---|---|---|---|---|
| 2 | `CycleStart` | BEG | `Endurance` | |
| 3 | | CC Chg | `10 A` | `> 4.2 V` → `STO` |
| 4 | | CC DChg | `20 A` | `< 3.0 V` → `GOTO CycleStart` |
| | | | | `< 2.5 V` → `ERR 9` |
| 5 | | CYC | `10` | |

Charges and discharges the pack, looping back to the labelled start step up
to 10 times (set on the closing `CYC` step) — unless the pack drops
unexpectedly low, in which case it raises an error instead of continuing.

### 9.6 Building a reusable block

**A small "5-minute rest" program, saved on its own:**
| # | Step | Setting | Condition → Response |
|---|---|---|---|
| 1 | SET | | |
| 2 | PAU | | `5 min` → *(blank)* |
| 3 | STO | | |

**Using it inside a bigger program:**
| # | Step | Setting |
|---|---|---|
| 1 | SET | |
| 2 | CC Chg | `10 A` → `> 4.2 V` → `STO` |
| 3 | PRODUCER | `Rest_5min` |
| 4 | CC DChg | `20 A` → `< 3.0 V` → `STO` |
| 5 | STO | |

When this program runs, step 3 is replaced with the rest program's middle
step, so the effective sequence is: charge, rest 5 minutes, discharge, stop.
Build the rest program once and reuse it in any other program the same way.
