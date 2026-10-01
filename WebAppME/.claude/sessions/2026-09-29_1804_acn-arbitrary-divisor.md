# Session 2026-09-29 18:04 — ACN generalized to arbitrary hour divisor

**Branch:** `feature/VNC_ACN_CNom`
**Goal:** User asked (with a screenshot of the ACN Program editor's CCCV CHG row) to let the ACN
family accept ANY positive-integer hour divisor in Nominal Values, Limit and Registration —
previously only the manual's own named C-rates (`ACN`/`ACN1`/`ACN2`/`ACN4`/`ACN5`/`ACN10`/`ACN20`)
validated; a value like `"1 ACN7"` was explicitly rejected (T-54, 2026-09-28) as "not a real
divisor". The user reversed that decision: the coefficient AND the hour-divisor suffix can both be
any number now (e.g. `"1 ACN7"`, `"8 ACN3"`), still resolving to plain Amps via the existing
`value × (battery.NominalCapacity / X)` formula, and still gated to Current-capable fields only
(not exposed on Voltage/Power nominal fields).

**Status:** Done (code + tests), build clean, not committed.

## Root cause / design

`BatteryUnitResolver.AcnHourDivisors` was a fixed `Dictionary<string,int>` keyed on the 7 literal
tokens. Every layer downstream mirrored that fixed set independently, so the fix touched 5 files:

1. `Utils/BatteryUnitResolver.cs` — replaced the dictionary with a regex `^ACN(\d+)?$`; `Resolve`
   now parses the digit suffix as the hour divisor (bare `ACN` still defaults to 5h), throwing on
   `ACN0`/negative. Added `IsAcnUnit(string?)`. `AcnUnits` kept as-is (canonical UI suggestions —
   the resolver itself now accepts more than that array lists).
2. `Components/UI/Program/OperatorConstants.cs` — `UnitPattern` (regex behind `AutoSpace`) gained
   an `ACN\d+` alternative ahead of the escaped literal list, so `AutoSpace` matches a numbered ACN
   unit directly instead of falling to the fuzzy `FindNearestUnit` path.
3. `Components/UI/Program/ValidationHelper.cs` — `AutoCorrectUnit` normalizes any `IsAcnUnit` match
   before the generic prefix-search fallback (fixes both `ValidateLimit` and `ValidateRegistration`
   automatically, since both call `AutoCorrectUnit`). `ValidateNominal`'s Current-field exact-match
   check gained a fallback: accept `IsAcnUnit` only when `field.ExtraUnits` contains `"ACN"` (i.e.
   only on a field that already opts into the ACN family — Current fields; Voltage/Power fields are
   untouched, preserving "only where A/current is calculated").
4. `Components/UI/Program/NominalValuesEditor.razor` — `AutoCorrect` (on-blur handler for the
   Nominal Values column) had its own independent exact-match-only unit lookup that would have
   **silently rewritten** `"1 ACN7"` → `"1 A"` on blur before validation ever ran (same bug class
   as the CNom/ACN7 truncation bugs fixed in earlier sessions) — added the same `IsAcnUnit` +
   `ExtraUnits.Contains("ACN")` guard.
5. `Services/ProgramBuilder.cs` — `TryParseUnit` (Limit → `CutoffCondition` byte) and
   `TryParseRegistration`'s `RGtype` switch (Registration → `RegistrationType` byte) both had a
   literal `"acn" or "acn1" or ... or "acn20"` case. Normally dead by the time it's reached (the
   caller already resolved the unit to plain `"a"` via `BatteryUnitResolver.Resolve`), but
   generalized to an `unit.StartsWith("acn")` prefix check anyway per the user's explicit ask to
   "use proper hex code for Cutoff Conditions, Registration and Limit" — keeps the byte mapping
   correct even if ever called with an unresolved unit.

`RegistrationsEditor.razor`'s own `AutoCorrectInput`/`AutoCorrectEditInput` were checked and found
**not** to need a change — their fallback (`validUnit == null`) already preserves the literally
typed text rather than collapsing it, and the final normalization happens via
`ValidationHelper.ValidateRegistration` → `AutoCorrectUnit` (already fixed in #3).

## Verification

- `dotnet build` — 0 errors (had to `Stop-Process` a stale locked `BatteryTestingSystem.exe`,
  same recurring Windows gotcha as prior sessions).
- `dotnet test` — **832/832** passing (up from 813; +19 new tests, 1 old test intentionally
  flipped — `ValidateLimit_UnsupportedNumberedAcnUnit_RejectedNotSilentlyCorrected` renamed/inverted
  to `ValidateLimit_AcceptsArbitraryNumberedAcnUnit` since the old pinned behavior was the thing
  being reversed).
- New coverage: `BatteryUnitResolverTests.cs` (arbitrary-divisor `Resolve` + `ACN0` throws),
  `ValidationHelperAcnVncTests.cs` (Nominal/Limit/Registration accept arbitrary ACN, Voltage/Power
  fields still reject it, `AutoCorrectUnit` casing), `ProgramToBytePacketTests.cs` (end-to-end
  byte-identical encoding for `ACN7` across Nominal/Limit/Registration — proves the
  CutoffCondition/RegistrationType hex byte is still correct for a non-standard divisor).

## Not done / follow-up

Not yet live-verified in the running app (only build+test verified) and not committed — same
"code done, needs live browser + commit decision" state as this branch's other recent sessions.

## Follow-up (same session, 18:23) — reject zero coefficient and zero divisor

User's immediate follow-up: in `"1 ACN3"`, neither the coefficient (`1`) nor the hour divisor
(`3`) may be `0`. Two real gaps, both silent until now:

- **Zero divisor (`"1 ACN0"`)**: `IsAcnUnit`'s regex `^ACN(\d+)?$` matches `"ACN0"` as shape-valid,
  so it passed every editor validation path untouched and would only throw
  `BatteryUnitResolutionException` later, at transfer time, when `BatteryUnitResolver.Resolve`
  actually divided by the zero hour count.
- **Zero coefficient (`"0 ACN3"`)**: nothing anywhere checked the numeric value against zero —
  it would validate fine and silently encode to `0 A`.

Fix: added `BatteryUnitResolver.IsValidAcnUnit(string?)` — same shape check as `IsAcnUnit`, but
also requires the divisor (when present) to parse as `> 0`. Swapped it in at the two points that
do final unit *acceptance* (`ValidationHelper.AutoCorrectUnit`, and `ValidateNominal`'s dynamic
Current-field match) so `"ACN0"` is rejected as an invalid unit up front — this also fixes
`ValidateLimit`/`ValidateRegistration` for free, since both go through `AutoCorrectUnit`.
Deliberately did **not** touch `NominalValuesEditor.razor`'s on-blur `AutoCorrect` (still uses the
broader `IsAcnUnit`) — that method's job is only to preserve/normalize the literally-typed text
before `ValidateNominal` renders the real error, and switching it to `IsValidAcnUnit` would have
made it fall back to `field.Unit` and silently rewrite `"1 ACN0"` → `"1 A"`, reintroducing the
exact truncation-bug class this branch has fixed multiple times before (CNom, ACN7).

Separately added an explicit zero-coefficient check (`value == 0` when the resolved unit is
ACN-family) in `ValidateNominal`, `ValidateLimit`, and `ValidateRegistration` — each returns
"ACN value must be greater than 0".

Tests: +18 (`BatteryUnitResolverTests.IsValidAcnUnit_*`, 7 new `ValidationHelperAcnVncTests`
covering zero-divisor/zero-coefficient rejection on all three fields plus a "still accepts a
genuinely valid '1 ACN3'" guard). **850/850 passing**, build clean.

## Follow-up 2 (same session, 18:34) — bare "ACN" (no divisor typed) must now error too

User's next report: "1ACN" (no trailing number) validates in Nominal Value today and shouldn't —
"first and last should be valid number". Also asked to confirm Registration accepts ACN at all
(their live testing suggested it couldn't).

**Root cause of the live "Registration can't type ACN" report**: the running `BatteryTestingSystem.exe`
(PID 18616) was a stale build from before this session's earlier fixes — confirmed by the build
step itself failing with `MSB3027`/file-locked until that process was stopped. `ValidateRegistration`
already correctly accepted an arbitrary-divisor ACN value per the earlier fix (pinned by
`ValidateRegistration_AcceptsArbitraryNumberedAcnUnit`, passing since the first follow-up) — no
code change was needed there; stopped the stale process and rebuilt so the user is testing current
code. Added `ValidateRegistration_AcceptsAcnWithExplicitDivisor` as an explicit end-to-end pin.

**Bare "ACN" fix**: previously `IsValidAcnUnit` treated an omitted divisor as "defaults to 5h" (the
manual's own documented convenience) and accepted it. Changed `IsValidAcnUnit` to require an
*explicit* positive-integer divisor — bare "ACN" is now shape-valid (`IsAcnUnit` still true, same
as "ACN0") but not accepted by the editor. Deliberately left `BatteryUnitResolver.Resolve` itself
unchanged (still defaults bare "ACN" it's handed to 5h) — that's a backward-compatibility path for
programs already saved with a bare "ACN" before this change; only the editor's acceptance of
*newly typed* input was tightened.

Ripple effects, since bare "ACN" was previously a literal member of `BatteryUnitResolver.AcnUnits`
(the Current field's `ExtraUnits`) and thus exact-matched directly, bypassing `IsValidAcnUnit`
entirely:
- Removed bare `"ACN"` from `AcnUnits` (now `{ACN1,ACN2,ACN4,ACN5,ACN10,ACN20}`).
- `ValidationHelper.AutoCorrectUnit` restructured: ACN-shaped input (`IsAcnUnit`, the broad check)
  is now handled entirely within one branch — valid → normalize, invalid → return null — and never
  falls through to the generic `ValidUnits.FirstOrDefault(u => u.StartsWith(unit))` fallback below
  it. That fallback would otherwise re-accept bare "ACN" anyway, since the literal `"ACN"` string is
  *still* kept in `OperatorConstants.ValidUnits` (deliberately — needed so `AutoSpace`'s exact-match
  regex still recognizes "ACN" as a real unit shape and round-trips `"1ACN"` → `"1 ACN"` without
  falling into the fuzzy `FindNearestUnit` path, which would otherwise match `"ACN".StartsWith("A")`
  and silently mangle it to `"1 A"` — the exact truncation-bug class this branch keeps re-encountering).
- `ValidateNominal`'s "is this a Current field" marker changed from `ExtraUnits.Contains("ACN")`
  (a literal-equality check, now always false since bare "ACN" is gone from `AcnUnits`) to
  `ExtraUnits.Any(u => u.StartsWith("ACN"))` (a shape check — still true for `ACN5` etc). Same fix
  applied to `NominalValuesEditor.razor`'s on-blur `AutoCorrect`, which kept its broader `IsAcnUnit`
  check (not tightened to `IsValidAcnUnit`) so a bare/zero-divisor ACN is still preserved as typed
  rather than silently falling back to `"A"` — the real rejection surfaces through `ValidateNominal`'s
  red styling, not a silent rewrite at the text-formatting layer.

Tests: +8 (`IsValidAcnUnit` theory renamed/extended, `AutoCorrectUnit_RejectsBareAcn`,
`ValidateNominal/Limit/Registration_RejectsBareAcn`, `AutoSpace_BareAcn_PreservedAsTypedNotMangledToPlainAmps`,
`ValidateRegistration_AcceptsAcnWithExplicitDivisor` ×3); removed the one `[InlineData("1.0 ACN")]`
case that pinned the now-reversed accept behavior. **857/857 passing**, build clean (after stopping
the stale running exe, PID 18616, which had been blocking the build output).
