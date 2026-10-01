# Implementation Plan — Battery-Derived Program Values

**Covers features 8, 9, 10, 11** from `Docs/Protocol_Compatible_Features_Analysis.md`:

| # | Feature |
|---|---|
| 8 | Battery-parameter-derived nominal values (ACn5, ACn1, ACn2, ACn4, ACn10, ACn20, VnC) |
| 9 | Battery parameters usable in Limit/Registration columns (CNom, INom, UGas, UMax, ICrank, ChargeF, Rin, CutOff, UNom, EDensity, NoCell) |
| 10 | Percentage-of-nominal-capacity limits (PERCCN_C confirmed protocol-free; PERCCN_P flagged — see Risks) |
| 11 | Special customer limit units (ABATT, VBATT, VNC, WATT) — OHM excluded, see Risks |

All four share **one underlying mechanism**: resolve a named token or percentage expression to a literal numeric value using data already on the `Batteries` entity, then hand that literal value to the existing byte-serialization code completely unchanged. This plan designs that one mechanism once and reuses it across all four features.

---

## 1. Grounding: how the current pipeline works today

Confirmed directly from source (not assumed):

- **`Models/Entities/Batteries.cs`** already has every field needed: `NominalCapacity` (CNom), `NumberOfCells` (NoCell), `GassingVoltage` (UGas), `MaximumVoltage` (UMax), `NominalCurrent` (INom), `ColdCrankingCurrent` (ICrank), `ChargeFactor` (ChargeF), `Impedance` (Rin), `BreakVoltage` (CutOff), `NominalVoltage` (UNom), `EnergyDensity` (EDensity). **No new battery fields required.**
- **`CircuitCommandHandler`** already carries `public BatteryDTO Battery { get; set; } = new();` as a live property on every connected circuit. The battery context we need already exists at the exact point programs are sent to a device — no new plumbing to "find the battery" is needed.
- **`CircuitCommandHandler.SetProgramAsync`** (the real device-transfer path) calls:
  ```csharp
  List<byte[]> steps = DecoderService.ConvertProgramIntoBytesPackets(ProgramDto.ProgramStepModel, resolvedDict);
  ```
  `resolvedDict` is the PRODUCER-program lookup built by `ResolveProducerProgramsAsync`. This is the single integration point for all four features.
- **`DecoderService.ConvertProgramIntoBytesPackets`** already has a documented precedent for exactly this kind of extension — a second overload added for the PRODUCER feature:
  ```csharp
  public static List<byte[]> ConvertProgramIntoBytesPackets(
      List<StepModel> programStepsDTO,
      Dictionary<string, List<StepModel>>? resolvedPrograms)
  {
      var expanded = ProgramBuilder.ExpandProducerSteps(programStepsDTO, resolvedPrograms);
      return ConvertProgramIntoBytesPackets(expanded);   // calls the original, untouched method
  }
  ```
  This repo already has a written plan for that exact pattern (`PRODUCER_FEATURE_PLAN.md`). This plan follows the same shape for consistency: **add a pre-processing pass that rewrites the step list, then hand off to the existing, unmodified serialization code.**
- **`Services/ProgramBuilder.cs`** writes every nominal value, limit, and registration as a plain float/int byte sequence via `ExtractFloatAsByteArraySafe`. It has no idea whether a number came from a literal ("20 A"), a SET variable, or (after this change) a resolved battery parameter — **and it doesn't need to**. Critically: `SET` variables are **never transmitted to the device at all** — `ExtractGlobalVariables` builds a client-side lookup, and `ProcessNominalValues`/`ProcessStandardLimit`/`AddRegistrations` already substitute a variable's value before writing bytes. Resolving a token to a literal value *before* serialization is not a new pattern here — it's the app's existing design, just applied to a new source of the number.
- **`Components/UI/Program/ValidationHelper.cs`** (`ValidateNominal`, `ValidateLimit`, `ValidateRegistration`) runs client-side in `ProgramEditor.razor` for live-typing feedback. It's a separate concern from serialization and needs its own small additive change so the editor doesn't show a red "invalid" error on a token it doesn't yet recognize.

---

## 2. Architecture overview

```
                     ┌───────────────────────────────┐
                     │   BatteryValueResolver (NEW)   │
                     │   static class, pure functions │
                     └───────────────────────────────┘
                        ▲                         ▲
                        │ (preview, live-typing)   │ (authoritative, pre-serialize)
        ┌───────────────┴────────────┐   ┌─────────┴──────────────────────────┐
        │ ValidationHelper.cs         │   │ DecoderService.ConvertProgramInto- │
        │ (ADD optional battery param)│   │ BytesPackets(steps, resolvedPrograms,│
        │ used by ProgramEditor.razor │   │ battery)  ← NEW 3rd overload        │
        └─────────────────────────────┘   └─────────┬──────────────────────────┘
                                                       │ calls (untouched)
                                             ┌─────────▼─────────────┐
                                             │ ProgramBuilder.cs      │
                                             │ (ZERO changes)         │
                                             └────────────────────────┘
```

**Design decision — resolve-then-rewrite, not thread-a-parameter-through:** rather than adding a `BatteryDTO` parameter to `ExtractGlobalVariables`/`ProcessNominalValues`/`ProcessStandardLimit`/`AddRegistrations` (invasive — four signature changes plus every call site), `BatteryValueResolver` does **one pass** over `List<StepModel>` before it ever reaches `ProgramBuilder`, rewriting recognized tokens (`"ACn5"` → `"4.00 A"`, `"80% CNom"` → `"163.20 AhStep"`) directly in the `NominalValues`/`Limits`/`Registrations` string lists. `ProgramBuilder.cs` requires **zero code changes** — it just serializes different-but-still-plain strings, exactly as it does today for a literal value the user typed by hand. This mirrors `ProgramBuilder.ExpandProducerSteps`, which does the same kind of "rewrite the step list, then hand off to unmodified downstream code" transform for PRODUCER steps.

**Where resolution happens — authoritative vs. preview:** a program is authored once but can be assigned to *different* circuits (and therefore different batteries) over its life. `ProgramDTO` has no `BatteryId` of its own — battery context only exists at the circuit/transfer level (`CircuitCommandHandler.Battery`). So:
- **Authoritative resolution** happens at `SetProgramAsync` time, against whichever battery is actually assigned to that circuit — same timing as the existing PRODUCER resolution (`ResolveProducerProgramsAsync`), which also only happens at transfer time, not at edit time.
- **Editor-time validation** is a *preview* only: the editor lets the user optionally pick a battery from a dropdown to see live-resolved values and confirm the syntax looks right, but the real numbers sent to a device are always computed fresh at `SetProgramAsync` against the circuit's actual battery. This is a deliberate, spec-consistent behavior (the same program legitimately produces different device bytes depending on which battery it's run against) — not a bug, but it must be communicated to users and testers (see Risks).

---

## 3. Feature 8 — Battery-parameter-derived nominal values

**User's perspective:** In the Nominal Value column of a CC_CHG/CC_DCHG/etc. step, instead of typing a literal current like `20 A`, the user types a battery-relative token: `ACn5` (meaning "1/5 of the battery's nominal capacity, expressed as a current"). The editor shows it as valid and — if a preview battery is selected — displays the resolved value inline (e.g. "ACn5 → 4.00 A"). At Transfer time, the device receives the actual resolved amperage for whichever battery is assigned to that circuit; the device never sees the word "ACn5."

**Technical design:**
- New static class `Services/BatteryValueResolver.cs`:
  ```csharp
  public static class BatteryValueResolver
  {
      // ACn5 = CNom/5, ACn1 = CNom/1, ACn2 = CNom/2, ACn4 = CNom/4, ACn10 = CNom/10, ACn20 = CNom/20 (all → Amps)
      // VnC  = UNom / NoCell (→ Volts)
      public static bool TryResolveBatteryToken(string token, BatteryDTO battery, out float value, out string unit)
  }
  ```
- `token` matching is case-insensitive, exact-name lookup against a small static dictionary — no regex ambiguity with existing "value + unit" patterns, since these tokens carry **no numeric prefix** (unlike `20 A`), making them trivially distinguishable from existing nominal-value syntax.
- Division-by-zero / missing-data guard: if `battery.NominalCapacity <= 0` (unset), `TryResolveBatteryToken` returns `false` with a specific reason string — the caller surfaces a clear error rather than silently sending `0 A`.

**Non-disruptive integration:**
- `ProgramBuilder.cs` — **no changes**. The resolved `"4.00 A"` string flows through `ProcessNominalValues` exactly like a hand-typed value.
- `ValidationHelper.ValidateNominal` — add one new branch, tried *after* the existing patterns fail to match: if the raw token matches a known battery-token name, call `BatteryValueResolver.TryResolveBatteryToken`. If a preview battery is available, return the resolved value as the "corrected" display string (reusing the existing `(bool IsValid, string? Error, string? Corrected)` return shape — no signature-shape change, only a new optional parameter).
- `ProgramEditor.razor` — add an optional preview-battery selector (dropdown, reuses `IBatteryServices` already injected elsewhere in the app, e.g. `BatteriesList.razor`/`TransferDialog.razor` patterns) and pass it into the existing `ValidateNominal` calls.

**Reused modules:** `Models/Entities/Batteries.cs` (data), `Services/Interfaces/IBatteryServices` (lookup), `ValidationHelper`'s existing return-tuple pattern, `ProgramBuilder`'s existing float-encoding path (untouched).

**New components:** `Services/BatteryValueResolver.cs` (new static class); a small preview-battery `<select>` in `ProgramEditor.razor`'s toolbar (reuses existing dropdown styling from the codebase, no new UI library).

**DB changes:** None. All source fields already exist on `Batteries`.

**Protocol impact:** None. The device receives a plain float in the existing nominal-value byte slot, indistinguishable from a hand-typed number.

**Complexity:** Medium (mostly in wiring the optional battery parameter through two call sites cleanly; the resolution math itself is trivial).

---

## 4. Feature 9 — Battery parameters usable in Limit/Registration columns

**User's perspective:** In a step's Limit column, the user types `INom` (or `UGas`, `UMax`, `CNom`, etc.) instead of a literal threshold like `> 20 A`. The step now terminates when the physical channel crosses that battery's own nominal value — e.g. "interrupt whenever current exceeds this battery's rated current," without hardcoding the number.

**Technical design:**
- Extends the same `BatteryValueResolver.TryResolveBatteryToken` from Feature 8 — the dictionary simply also maps `INom→(NominalCurrent, "A")`, `UGas→(GassingVoltage, "V")`, `UMax→(MaximumVoltage, "V")`, `CNom→(NominalCapacity, "AhStep")`, `NoCell→(NumberOfCells, "")` (dimensionless, only meaningful combined with VnC), `ICrank→(ColdCrankingCurrent, "A")`, `ChargeF→(ChargeFactor, "")`, `Rin→(Impedance, "Ohm")` *(see Risks — Ohm has no existing unit byte, so Rin cannot be sent as a *limit* yet; still resolvable as a raw number for display/registration-adjacent uses only)*, `CutOff→(BreakVoltage, "V")`, `UNom→(NominalVoltage, "V")`, `EDensity→(EnergyDensity, "")`.
- A limit string like `INom` (bare token, no comparison operator) needs a default comparison operator assumed — reuse the existing default in `ProcessStandardLimit` (`op = "="` when omitted) or require the user to type `> INom` explicitly. Recommend requiring the operator explicitly (`> INom`, `< UMax`) to avoid ambiguity, consistent with how the existing channel-threshold limit syntax already works.
- Resolution again happens as a **step-list rewrite** before `ProgramBuilder.ProcessStandardLimit` runs: `> INom` → `> 20.50 A`, byte-identical in shape to what a user would type by hand today.

**Non-disruptive integration:**
- `ProgramBuilder.cs` — no changes (same reasoning as Feature 8: the rewrite happens one layer above it).
- `ValidationHelper.ValidateLimit` — same additive fallback branch pattern as `ValidateNominal` in Feature 8, checking the *value* portion of the limit string against the battery-token dictionary after the existing comparison-operator/unit parsing runs its course.

**Reused modules:** Same `BatteryValueResolver`, same `Batteries` entity fields, same `ProcessStandardLimit` byte path (untouched).

**New components:** None beyond what Feature 8 already introduces — this is the same resolver, applied to the Limit column instead of the Nominal Value column.

**DB changes:** None.

**Protocol impact:** None, for every parameter except **Rin (internal resistance)** — sending it as an actual limit would need an "Ohm" `CutoffCondition` byte, which does not exist today (confirmed: `Models/Enums/ProgramEnums.cs`'s `CutoffCondition` enum has no resistance entry). Recommend shipping Rin as *resolvable/displayable* now, and deferring "usable as an actual device-side Ohm limit" until Resistance nominal-mode support is added (a previously identified protocol-change item).

**Complexity:** Medium (shares almost all of its plumbing with Feature 8 — implement together).

---

## 5. Feature 10 — Percentage-of-nominal-capacity limits (PERCCN_C)

**User's perspective:** In a step's Limit column, the user types something like `80% CNom` — the step ends once the accumulated capacity for *this step* reaches 80% of the battery's nominal capacity, instead of the user having to calculate and type an absolute Ah value themselves.

**Technical design:**
- `BatteryValueResolver.TryResolvePercentOfCapacity(string input, BatteryDTO battery, out float ahValue)` — parses a leading percentage (`80%`) plus a trailing reference token (`CNom`), computes `ahValue = (percent / 100f) * battery.NominalCapacity`, and returns it paired with the **existing** `AhStep` unit (`CutoffCondition.StepCapacity = 0x3B`, already defined and already wired through `TryParseUnit`).
- The rewrite turns `80% CNom` into `163.20 AhStep` (or whatever the resolved absolute value is) — again a plain, already-supported limit string, byte-identical in shape to a hand-typed `AhStep` threshold.
- **Only PERCCN_C (current step's capacity) is included in this pass.** PERCCN_P ("previous step's ending capacity") has no equivalent existing limit-channel byte — see Risks below; it is explicitly **out of scope** for this protocol-free implementation and should not be attempted until a firmware/device-team conversation confirms whether a "previous step capacity" channel code already exists on the device side.

**Non-disruptive integration:**
- `ProgramBuilder.cs` — no changes; the resolved value flows through the exact same `AhStep`/`StepCapacity` limit-encoding path (`ProcessStandardLimit` → `TryParseUnit("ahstep", ...)`) already used for a manually-typed AhStep limit today.
- `ValidationHelper.ValidateLimit` — one more fallback branch: if the limit string matches a `N% <token>` pattern, delegate to `TryResolvePercentOfCapacity`.

**Reused modules:** Same `BatteryValueResolver`, existing `AhStep`/`StepCapacity` limit-encoding path (fully reused, unmodified).

**New components:** None beyond the one new method on the already-introduced `BatteryValueResolver`.

**DB changes:** None.

**Protocol impact:** None for PERCCN_C — it resolves to an absolute value in an existing, already-transmitted unit. **PERCCN_P is a protocol-change item, not included here.**

**Complexity:** Medium (percentage-parsing regex plus reference-value resolution; slightly more parsing work than Features 8/9 but the same resolve-then-rewrite mechanism).

---

## 6. Feature 11 — Special customer limit units (ABATT, VBATT, VNC, WATT)

**User's perspective:** The user can type `ABATT`, `VBATT`, `WATT`, or `VNC` as limit-channel unit names — customer-familiar synonyms for channels/values the app already understands under different names.

**Technical design — these split into two very different cases:**

1. **Pure aliases (ABATT, VBATT, WATT) — zero computation needed.** These are just alternate spellings of channels the app already fully supports: `ABATT` → `Current` (0x31), `VBATT` → `Voltage` (0x32), `WATT` → `Power` (0x33). This is a **one-line addition** to the existing unit-alias switch expressions in `ProgramBuilder.TryParseUnit` and `OperatorConstants`'s unit-recognition path (e.g. `"abatt" => (byte)CutoffCondition.Current` alongside the existing `"a" or "amp" or "amps" => (byte)CutoffCondition.Current` case). No resolver, no battery lookup, no new class — this is pure vocabulary/synonym expansion of code that already exists.
2. **VNC — a real derived value, uses the Feature-8/9 resolver.** `VNC` = "voltage per cell" = `NominalVoltage / NumberOfCells`. This reuses `BatteryValueResolver.TryResolveBatteryToken` exactly like `VnC` in Feature 8 (in fact, `VnC` and `VNC` may be the same spec item under two capitalizations — implement as one dictionary entry, case-insensitive).

**Non-disruptive integration:**
- `ProgramBuilder.TryParseUnit` — add the three alias cases (ABATT/VBATT/WATT) directly to the existing `switch` expression. This is the **only place in this whole plan where an existing method gets a direct edit** rather than being wrapped by a pre-processing pass — and it's a pure additive `case` addition, not a change to any existing case's behavior.
- `VNC` — no changes needed beyond what Feature 8 already implements.

**Reused modules:** `ProgramBuilder.TryParseUnit`'s existing switch expression (additive cases only); `BatteryValueResolver` for VNC.

**New components:** None.

**DB changes:** None.

**Protocol impact:** None for ABATT/VBATT/WATT/VNC. **OHM is explicitly excluded** from this feature — it requires a Resistance nominal-control mode and a corresponding `CutoffCondition` byte that don't exist today (confirmed: no resistance entry anywhere in `Models/Enums/ProgramEnums.cs`). This was already flagged as a protocol-change item in the prior analysis and remains out of scope here.

**Complexity:** Low (ABATT/VBATT/WATT are trivial; VNC rides on Feature 8/9's work).

---

## 7. Shared risks and dependencies

| Risk / Dependency | Detail | Mitigation |
|---|---|---|
| **Reserved-name collisions with user-defined SET variables** | A user could `SET INom = 5 A`, then use `INom` elsewhere expecting *their* variable, while the app resolves it as the reserved battery parameter instead (or vice versa) — silent ambiguity. | Reserved battery-token names must take precedence and be **blocked from redefinition** in `ValidationHelper.ParseSetVariable`/`ExtractGlobalVariables` — this is the same reserved-name validation already recommended as a standalone Low-complexity item (Sheet 9, "Reserved/forbidden label names") in the prior gap analysis. Recommend implementing that guard *alongside* this work, extended to also cover the new battery-token vocabulary. |
| **PERCCN_P has no existing device-side channel code** | Confirmed by reading `CutoffCondition` — no "previous step capacity" entry exists. Implementing P the same way as C would require guessing at an unassigned byte value, which risks colliding with a value the firmware already uses for something else. | Ship PERCCN_C only in this pass. Track PERCCN_P as a protocol-change item pending firmware-team confirmation. |
| **Rin (internal resistance) as an actual device-side limit** | No Ohm unit byte exists in `CutoffCondition` today. | Resolve Rin as a plain number for display/registration-adjacent purposes now; defer "usable as a live device limit" until Resistance mode is added. |
| **Missing/zero battery data produces a wrong resolved value, not an error** | If `Batteries.NominalCapacity` is `0` or unset, `ACn5`/`80% CNom` naively resolve to `0 A`/`0 AhStep` — a program that silently sends a zero-current or zero-capacity limit is a safety concern, not just a bug. | `BatteryValueResolver` must return an explicit failure (not a zero) when a required source field is missing or ≤ 0, and `SetProgramAsync`/the editor must surface that as a hard validation error, not silently continue. |
| **Same program now yields different device bytes depending on which battery is attached** | This is spec-intended behavior (that's the whole point of a battery-relative value), but it's a *behavioral change* from today, where two runs of "the same program" always produced byte-identical packets. | Communicate clearly in release notes / to QA. Confirm `Session.ProgramHash` (computed from the *authored* step text, not resolved bytes) is the correct identity to keep using for change-detection — it already is, since it doesn't need to change for this feature to be correct. |
| **Backward compatibility for existing programs** | Programs with zero battery tokens must serialize to *exactly* the same bytes as before this change. | `BatteryValueResolver`'s rewrite pass is a no-op for any string that doesn't match a recognized battery-token pattern — regression-test by diffing `ConvertProgramIntoBytesPackets` output before/after for a representative set of existing production programs containing no battery tokens. |
| **Editor-time preview vs. transfer-time authoritative value can visibly differ** | If a user previews against Battery A in the editor, then the program is actually run against Battery B, the sent values differ from what was last shown on screen. | Label the editor's preview clearly ("Preview only — actual values resolved against the circuit's assigned battery at Transfer time") to avoid operator confusion. |

---

## 8. Recommended implementation order

1. **`Services/BatteryValueResolver.cs`** — new static class, pure functions, no dependencies on the rest of the pipeline. Fully unit-testable in isolation before touching anything else. Implement `TryResolveBatteryToken` (covers Features 8, 9, and VNC in 11) and `TryResolvePercentOfCapacity` (Feature 10 / PERCCN_C only) together, since they share the same dictionary and float-formatting helpers.
2. **Reserved-name guard** in `ValidationHelper.ParseSetVariable`/`ExtractGlobalVariables` — extend the existing (or newly-added) reserved-name blocklist to include the battery-token vocabulary, closing the collision risk *before* the tokens become usable anywhere else.
3. **`ProgramBuilder.TryParseUnit`** — add the three pure-alias cases (ABATT, VBATT, WATT) from Feature 11. Trivial, isolated, ships independently of everything else in this plan.
4. **`ValidationHelper.ValidateNominal` / `ValidateLimit`** — add the additive fallback branches that call into `BatteryValueResolver`, with an optional preview-battery parameter. This makes the editor accept and preview the new syntax, without yet affecting what gets sent to a device.
5. **`DecoderService.ConvertProgramIntoBytesPackets`** — add the new 3-parameter overload (`steps, resolvedPrograms, battery`) that runs `BatteryValueResolver`'s step-list rewrite pass after PRODUCER expansion, then calls the existing untouched base method.
6. **`CircuitCommandHandler.SetProgramAsync`** — pass `this.Battery` into the new overload. This is the point where the feature becomes "live" for real device transfers.
7. **`ProgramEditor.razor`** — add the optional preview-battery dropdown (reusing existing battery-lookup services) wired into step 4's validation calls; update `DownloadHexPackets`'s existing `ConvertProgramIntoBytesPackets` call to use the same new overload so the debug/preview hex export matches what Transfer actually sends.
8. **Regression pass** — diff serialized bytes for a set of existing, token-free production programs before/after, confirming zero change. Then test each new token/percentage syntax end-to-end against at least two different battery records to confirm per-battery resolution is correct and that missing/zero battery data fails loudly rather than silently.

Steps 1–3 touch no existing call sites and can be built and code-reviewed in isolation. Steps 4–7 are the integration points, done in dependency order (editor validation before serialization, serialization before the live-transfer wiring). Step 8 is the safety gate before this ships to anyone testing against real hardware.
