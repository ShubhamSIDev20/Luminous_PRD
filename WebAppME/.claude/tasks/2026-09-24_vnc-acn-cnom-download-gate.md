# T-53 — VNC/ACN/CNom follow-up: VN rename, coefficient'd CNom, download battery gate

**Branch:** `feature/VNC_ACN_CNom`
**Status:** Done (code + tests). Not live-verified in browser. Not committed.

See [sessions/2026-09-24_1150_vnc-acn-cnom-download-gate.md](../sessions/2026-09-24_1150_vnc-acn-cnom-download-gate.md)
for full detail. Summary:

- Confirmed most of the requested feature (ACNx, VNC, bare CNom) was already shipped in `main`
  (T-49, T-50) — this branch was byte-identical to `main` at session start.
- Renamed the voltage-per-cell unit token `VNC` → `VN` everywhere (editor validation, encoder,
  `BatteryUnitResolver`, tests) — clean rename, not an alias, per user decision.
- Added coefficient'd `CNom` support ("0.8 CNom" in a Limit, "0.1 CNom" in a Registration) —
  resolves to Step Capacity (`AhStep`), distinct from the pre-existing bare `CNom` token
  (Accumulated Capacity). Wired into `ProcessStandardLimit`/`TryParseRegistration` +
  `ValidationHelper.ValidateLimit`/`ValidateRegistration`. Deliberately not added to Nominal Value.
- `ProgramEditor.razor`'s hex-packet Download now detects battery-relative tokens
  (`BatteryUnitResolver.ProgramUsesBatteryRelativeTokens`) and prompts for a battery before
  generating packets, matching `TransferDialog`'s existing requirement for the hardware-transfer
  path. `BatteryUnitResolutionException` now caught and toasted instead of crashing.
- 801/801 tests passing (+26 new).

**Follow-up before merge:** live-verify the new download battery-picker dialog in a running app.
