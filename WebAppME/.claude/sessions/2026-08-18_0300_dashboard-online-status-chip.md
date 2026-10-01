# Session — Dashboard "Online" status chip + filter

> **Date:** 2026-08-18 (session #15)
> **Agent:** Claude (Opus 5)
> **Status:** ✅ Build-verified (0 errors) — not yet visually confirmed in-browser

---

## Goal
User wants a new **`Online: N`** counter in the dashboard header, after `Offline`, that also acts
as a filter so only online cards are shown.

## Key design point
`Online` is **not** a `CircuitStatus` enum member — `CircuitStatus` is
`Idle/Charge/Discharging/Pause/Countinue/Interrupt/Error/Msg/Offline` (`Models/Enums/CircuitEnums.cs:48`).
Online is the *complement* of `Offline`, so it cannot be expressed as a `_statusChip` value.

Implemented as a separate `bool _onlineOnly` flag, **mutually exclusive** with `_statusChip`:
picking either one always clears the other. (Stacking them would be meaningless — every non-Offline
status is by definition online, so `Online + Charge` is just `Charge`.)

Counting basis: `RealTimeRecord.CircuitStatus != CircuitStatus.Offline`, matching what every other
header chip counts. This keeps the numbers summing to the total (user's report: 72 = 16 Stop + 56 Offline
→ Online: 16).

## What was done
All in `Components/Pages/Home/DashboardView.razor`:

1. **New `Online: N` chip** after the status-chip `@foreach` — same active/dim styling contract.
2. **New state** `_onlineOnly` + three handlers: `SetStatusChip(CircuitStatus?)`,
   `ToggleOnlineOnly()`, `ClearStatusFilter()`.
3. **`circuits: N`** now calls `ClearStatusFilter()` (resets *both* filters) and shows its
   "all" styling only when neither filter is active.
4. **Status chips** now dim when `_onlineOnly` is active, and route through `SetStatusChip`.
5. **`VirtualRows`** filter gained `inOnline` alongside `inAccess`/`inVisible`/`inChip`.

## Discoveries / gotchas
- **No `--status-online` CSS var exists**, and per [ADR-2](DECISIONS/) `wwwroot/css/app.min.css`
  has **no rebuild pipeline** — a newly added var in `app.css` would not reach the browser.
  Reused the already-defined green `text-status-continue` instead. Any future status color must
  either reuse an existing var or accept that ADR-2 has to be resolved first.
- ⚠️ **The header count and the card badge can disagree.** Header chips read
  `RealTime.RealTimeRecord.CircuitStatus`, but `DeviceChannel.razor:1708` derives the *card's*
  displayed status from `Channel.IsConnected` first (`IsConnected ? CircuitStatus : (ProgramStatus
  == Running ? Error : Offline)`). A circuit whose TCP link dropped while its last record still
  says `Charge` counts as Online in the header but renders Offline on the card. Left as-is for
  consistency with the existing chips — flag if the user reports a mismatch.

## Verification
- `dotnet build -p:OutputPath=obj/verify-out/` → **0 errors** (app was running; see build gotcha
  in [session #14](2026-08-18_0200_circuit-id-three-part-display.md)).
- ⏳ Not visually verified in-browser.

## Files changed
| File | Change |
|------|--------|
| `Components/Pages/Home/DashboardView.razor` | `Online: N` header chip, `_onlineOnly` flag + 3 handlers, `VirtualRows` `inOnline` filter |
