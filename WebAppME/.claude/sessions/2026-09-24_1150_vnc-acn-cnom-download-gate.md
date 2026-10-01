# Session — VNC/ACN/CNom follow-up: VN rename, coefficient'd CNom, download battery gate

**Started:** 2026-09-24T11:50:00Z (IST)
**Branch:** `feature/VNC_ACN_CNom`
**Goal (as given):** "Implement VNC/ACN/CNom battery-relative units in the program editor" — user provided
worked-example formulas and a reference doc (`program_packet_v0.15.md`) plus manual screenshots.

## Key discovery — most of this was already shipped

Before writing any code, verified (via `git log`, `git diff main...HEAD`, and reading the actual
source) that `feature/VNC_ACN_CNom` was created from `main` and was **byte-identical to `main`** at
session start. The VNC/ACN5 feature (T-49, 2026-09-04) and the §12.3 bare-token feature (T-50,
2026-09-08) were already merged. Reported this back to the user before implementing anything, since
building it again would have been wasted/duplicate work.

## What was genuinely new (confirmed by reading `ProcessStandardLimit`/`TryParseRegistration`)

1. **Unit token rename: `VNC` → `VN`.** User's manual screenshots show `VN` (e.g. "> 2.48 VN"), not
   `VNC`. Since the feature is only ~3 weeks old and nothing was reported as already using `VNC` in
   saved programs, user chose a clean rename (not an alias) after being shown the discrepancy.
2. **Coefficient'd `CNom` — genuinely missing.** The already-shipped `CNom` (T-50) is a **bare**
   §12.3 token only (no multiplier), resolving to Accumulated Capacity ("Ah"). The user's spec
   ("0.8 CNom" → 80, per `program_packet_v0.15.md` §14.10) needs a *coefficient* applied to CNom,
   which — confirmed by reading the code — silently mis-parsed before this session: "CNom" was read
   as a literal (unrecognized) unit string, dropping the cutoff-condition byte entirely. Per §14.10's
   own rationale, this must map to **Step Capacity** (`AhStep`/`0x3B`/`0x29`), not Accumulated
   Capacity (`Ah`/`0x3A`) — a per-step counter, not a program-wide one — otherwise the cutoff fires
   at the wrong time depending on what earlier steps already added.
3. **Download/export battery gate — genuinely missing.** `ProgramEditor.razor`'s `DownloadHexPackets`
   (hex-packet export, distinct from `TransferDialog`'s hardware transfer, which already required a
   battery) called `ConvertProgramIntoBytesPackets` with no battery at all — any ACNx/VN/CNom step
   would throw uncaught during export.

One point resolved with the user and *not* changed: the ACN formula (`value × capacity/hours`) —
user's own written example ("100Ah/5h = 0.2A") conflicted with the already-shipped, manual-verified
formula (100/5=20A); the user's own manual screenshot ("nominal capacity is 1 Ah... 1×1Ah/5h=0.2A")
turned out to corroborate the existing 20A formula (same formula, different example capacity: 1Ah vs
their battery's 100Ah) — confirmed no change was needed.

## Changes made

- `Utils/BatteryUnitResolver.cs`: `VncUnit` constant `"VNC"` → `"VN"`; added
  `ResolveCNomCoefficient(coefficient, token, battery)` (CNom-only, maps to `"AhStep"`) and
  `ProgramUsesBatteryRelativeTokens(steps)` (scans Nominal/Limit/Registration for any ACNx/VN/§12.3
  token, used to gate the download prompt).
- `Services/ProgramBuilder.cs`: `"vnc"` → `"vn"` in both `TryParseUnit`/`TryParseRegistration`
  switches; `ProcessStandardLimit` and `TryParseRegistration` now fall back to
  `ResolveCNomCoefficient` when `BatteryUnitResolver.Resolve` returns null (i.e. the unit wasn't
  ACNx/VN). **Deliberately not wired into `ProcessNominalValues`** — CNom is Limit/Registration only,
  per spec and per the reference doc (never shown as a Nominal Value).
- `Components/UI/Program/OperatorConstants.cs`: `ValidUnits` — `"VNC"` → `"VN"`.
- `Components/UI/Program/ValidationHelper.cs`: `ValidateLimit`/`ValidateRegistration` accept
  `"<number> CNom"` (Limit: rejected on PAU, since PAU only carries a plain duration).
- `Components/Pages/Programs/ProgramEditor.razor`: `DownloadHexPackets` split into a gate +
  `PerformDownloadHexPackets(BatteryDTO?)`; when `ProgramUsesBatteryRelativeTokens` is true, shows a
  new battery-picker `Dialog` (same `RadioGroup`/`ScrollArea` pattern as `TransferDialog.razor`)
  before generating packets; `BatteryUnitResolutionException` caught and surfaced via `Toast.Error`
  instead of crashing the circuit. Programs with no battery-relative tokens are unaffected — same
  behavior as before.
- Renamed `VNC`→`VN` in every functional reference across the codebase (doc-file-path mentions like
  `docs/manual-extract/VNC-ACN-battery-parameters.md` left as-is — that's a filename, not the unit
  token).

## Tests

Added 26 tests (775 → **801**, all passing): `BatteryUnitResolverTests` (`ResolveCNomCoefficient` ×4
facts/theories, `ProgramUsesBatteryRelativeTokens` ×2), `ValidationHelperAcnVncTests` (Limit/PAU/
Registration acceptance of coefficient'd CNom), `ProgramToBytePacketTests` (end-to-end byte-encoding:
Limit and Registration resolve to Step Capacity, differ from plain Accumulated Capacity, throw
without a battery). Existing VNC-literal tests updated to VN. `dotnet build`: 0 errors. Full suite
run: 801/801.

## Follow-up fix — `AutoSpace` was truncating "0.8 CNom" to "0.8 C"

User reported after the above: typing `0.8 CNom` into a Limit field and clicking away left only
`C`. Root cause: `OperatorConstants.AutoSpace` (the on-blur formatter, runs *before*
`ValidationHelper.ValidateLimit`) was deliberately kept unaware of `CNom` — `CNom` was NOT added to
`ValidUnits` initially, to avoid it leaking into Nominal Value/`SET` fields. But `AutoSpace`'s fuzzy
fallback (`FindNearestUnit`) searches that same `ValidUnits` list for the *longest registered unit
that is a prefix* of what was typed — and `"C"` (Temperature) is a registered unit and a prefix of
`"CNom"`. So the field silently got rewritten to `0.8 C` before validation ever ran, and then
validated fine as 0.8°C — no error shown anywhere, wrong value stored.

Checked `VN` for the same class of bug (user asked) — **not affected**, because `VN` was already
added as a full `ValidUnits` entry in the earlier rename, so `AutoSpace`'s exact patterns match it
directly and it never reaches the fuzzy prefix-fallback that caused the `CNom` bug.

Fix: added `"CNom"` to `OperatorConstants.ValidUnits` itself (exactly like `ACN5`/`VN` already are),
so `AutoSpace` matches it as a whole token. Confirmed this does NOT open `CNom` up as a Nominal
Value unit — `ValidateNominal`'s per-field check uses `NominalConfig.AcceptedUnits`, a separate,
narrower list, unaffected by `ValidUnits` membership. This also made the `ValidateLimit`/
`ValidateRegistration` special-case branches added earlier in this session redundant (the generic
`AutoCorrectUnit` path now resolves "CNom" itself) — removed them to avoid duplicate logic.

Added 7 regression tests pinning `AutoSpace("0.8 CNom") == "0.8 CNom"` (not `"0.8 C"`), the `> 0.8
CNom` / no-space / lowercase variants, a guard that plain `"0.8 C"` (real Temperature) still works
standalone, and two tests confirming `VN` was never affected. 801 → **808** tests, all passing.

## Live-verified (user, in-browser)

User authored a real "VNC Program" (7 steps: `SET`/STANDARD, `CC_CHG` `> 2.48 VN`, `CC_CHG` `1.90
VN`, `CV_CHG` nominal+limit `7 VN`, `CC_CHG` limit+registration `0.2 CNom`, `CC_CHG` limit `1
ACN5`, `STO`) against a real battery (100 Ah, 1 cell — "Tesla 48KV battery"), hit Download, and
pasted the resulting packet-analysis report. Every value round-tripped correctly:
- `VN` in both Nominal and Limit resolved `value × 1 cell` (2.48→2.48V, 1.90→1.90V, 7→7V) — proves
  the download battery-gate actually fired and passed the right battery through.
- `0.2 CNom` (Limit AND Registration) resolved to **20 AhStep** — `Unit=StepCapacity (0x3B)` /
  `Type=StepCapacity (0x29)`, NOT Accumulated Capacity — exactly the distinction this session's fix
  was for.
- `1 ACN5` resolved to 20 A (`Unit=Current (0x31)`, 100Ah/5h × 1).
- Editor screenshot confirms `0.2 CNom` and `1 ACN5` render intact in the fields (not truncated to
  `0.2 C`) — the `AutoSpace` fix holding up live, not just in unit tests.

**Not done:** not committed/pushed — user has not yet asked for a commit this session.
