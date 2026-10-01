# T-39 — Dashboard card + card-dialog fixes

> Started: 2026-08-19
> Completed: 2026-08-19
> Session: [sessions/2026-08-19_1044_dashboard-card-dialog-fixes.md](../sessions/2026-08-19_1044_dashboard-card-dialog-fixes.md)

## Request
User: "1. Card bottom side spacing getting extra not equal from each side. 2. remove download details from rightclick context 3. and add those download button on dialog where manufactureing, factory info deatils fech 4. there need to improve the card dialog 5. on Manufacturing and factory details default show from db with last sysc date. and if there cards is not running or in idle mode show button for fech from devices that time feach and update those details and show as well."

Clarified item 4 with the user: layout/spacing polish of the `DeviceChannel.razor` dialog only, not new behavior.

## What was done
See the session file for the full write-up. Summary:
1. Fixed the row-spacing CSS bug in `DashboardView.razor` (double-counted `mb-3` + row padding).
2. Removed the "Download Details" context-menu item (and its dead `DoAction`/`CanContextAction` cases) — it was actually a live device-fetch, not a file download.
3. Added a real "Download" button (JSON export via the existing global `downloadFile` JS helper) to the Manufacturing and Factory tabs of the card dialog.
4. Rewrote the Battery tab's markup to match the em-based, label/`bg-muted`-value convention already used by Manufacturing/Factory, for visual consistency across all tabs.
5. Added a `LastSyncedAt` column to `Device`/`Channel` (migration `20260819050949_AddLastSyncedAt`), stamped on every device-fetch write, surfaced in the dialog, plus a "Fetch from device" button gated on Idle/Stop/Connected that re-fetches live data and reloads the dialog from DB afterward.

## Result
`dotnet build` → 0 errors. `dotnet test` → 180/180 passed (unchanged). Migration applied to the dev DB.

## Files changed
- `Components/Pages/Home/DashboardView.razor`
- `Components/UI/Dashboard/DeviceChannel.razor`
- `Models/Entities/Device.cs`
- `Models/Entities/Channel.cs`
- `Models/DTOs/ManufacturingDetailDTO.cs`
- `Models/DTOs/FactoryConfigDetailDTO.cs`
- `Repositories/Implementations/DeviceChannelRepository.cs`
- `Migrations/20260819050949_AddLastSyncedAt.cs` (+ `.Designer.cs`, `AppDbContextModelSnapshot.cs`)

## Follow-up (not done this session)
- **T-40** — Live browser verification of all 5 fixes (spacing, removed menu item, download buttons, Battery tab layout, fetch-from-device + last-synced display) — code/build/test-verified only so far.
