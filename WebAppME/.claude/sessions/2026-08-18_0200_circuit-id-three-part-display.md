# Session — Circuit Records: 3-part Circuit ID display

> **Date:** 2026-08-18 (session #14)
> **Agent:** Claude (Opus 5)
> **Status:** ✅ Build-verified (0 errors) — not yet visually confirmed in-browser

---

## Goal
User report: the **Circuit Records** datatable's *Circuit ID* column showed `device-channel`;
it must show `device-secondaryBoard-channel` (e.g. `1-0-1`).

## Root cause
`Components/Pages/Devices/DeviceList.razor`'s `circuitId` `ColumnDef` defines the value **three
times**, and only the display copy was stale:

| Selector | Was | Correct? |
|----------|-----|----------|
| `SearchSelector` | `{DeviceID}-{SecondaryBoardNumber}-{ChannelNumber}` | ✅ already |
| `ExcelValueSelector` | `{DeviceID}-{SecondaryBoardNumber}-{ChannelNumber}` | ✅ already |
| `CellTemplate` | `@c.DeviceID-@c.ChannelNumber` | ❌ **the bug** |

So search-by-`1-0-1` matched and the Excel export was right, while the grid rendered `1-1`.

## What was done
All in `Components/Pages/Devices/DeviceList.razor`:

1. **Line 38** — `CellTemplate` now renders `@($"{c.DeviceID}-{c.SecondaryBoardNumber}-{c.ChannelNumber}")`.
2. **Line 284** — Circuit Detail dialog title, same 2-part → 3-part fix.
3. **Lines 431 / 441 / 447** — Delete / Deregister / Register confirm messages, same fix.

Items 2-5 were not literally in the ask but are the same defect on the same screen, reachable
from these rows. They matter more since session #11: with secondary board `0` **and** `1` both
valid, "Are you sure you want to Delete 1-1?" is genuinely ambiguous about which board.

The internal identity keys (lines 399/455/494/579) were already 3-part — untouched.

## Discovery / gotcha
- **`ColumnDef` splits a column's value across `SearchSelector` / `ExcelValueSelector` /
  `CellTemplate`.** Any *composed* (non-plain-property) column can drift between the three
  silently, exactly as here. Durable fix, not done: a single computed `ChannelDto.CircuitId`
  property that all three call. Worth doing if a third consumer (PDF export) appears.
- **Build gotcha:** `dotnet build` fails with `MSB3021`/`MSB3027` (locked `BatteryTestingSystem.exe`)
  whenever the app is running — this is *not* a compile error. Verify with
  `dotnet build -p:OutputPath=obj/verify-out/` and delete the folder after; `-t:Compile` does
  **not** avoid it (the copy is still scheduled).

## Verification
- `dotnet build -p:OutputPath=obj/verify-out/` → **0 errors**.
- ⏳ Not visually verified in-browser.

## Files changed
| File | Change |
|------|--------|
| `Components/Pages/Devices/DeviceList.razor` | Circuit ID cell template + detail-dialog title + 3 confirm messages now render `device-secondaryBoard-channel` |
