# T-46 — Circuits bulk delete + Transfer UI-freeze fix + label/progress/context-menu follow-ups

> Status: ✅ Done
> Started: 2026-08-21
> Completed: 2026-08-21
> Session: [sessions/2026-08-21_1000_bulk-delete-and-transfer-freeze-fix.md](../sessions/2026-08-21_1000_bulk-delete-and-transfer-freeze-fix.md)

## Request
1. Circuits page (`DeviceList.razor`): "Delete All" with confirmation, plus manual multi-select with "Delete Selected", plus "Select All"/"Clear" helpers.
2. Investigate/fix a reported UI freeze transferring a program to all 640 circuits (`run_sim.py -d 10 -n 64`); verify Start also works after Transfer.
3. Three follow-ups from the same test round: ambiguous `1 - 1` circuit labels in Transfer/action result dialogs, missing progress count on the Start/Stop loading overlay, right-click context menu staying open behind the loading overlay during bulk actions.
4. Commit and push to `main`.

## Outcome
- Bulk select/delete added to `DeviceList.razor`. User-reported "Delete All not working" was root-caused to `ChannelManager.cs`'s intentional soft-delete auto-revive-on-registration colliding with the simulator's ~8s registration retry loop — not a bug in the new code.
- `TransferDialog.HandleTransfer` rewritten from a fully sequential loop to the same parallel-by-device / sequential-per-device pattern already used by `DashboardView.DoAction` (per-`DeviceID` `SemaphoreSlim`), with a `transferCompleted`/`transferTotal` progress counter.
- `TransferResultsDialog`'s `TransferResult` gained `SecondaryBoardNumber`; all 5 construction sites (`TransferDialog.razor` + 4 in `DashboardView.razor`'s `DoAction`) updated; display now shows the full `device-board-channel` label.
- `Processing.razor` gained `Completed`/`Total` parameters (already used by Transfer, now also wired from `DashboardView.razor`'s existing `CompletedSteps`/`TotalSteps` for Start/Stop/Pause/Continue).
- `ContextMenuItem.HandleClick` now closes the menu before awaiting the click handler, instead of after — fixes the menu staying open behind a long-running bulk action's loading overlay.
- Committed as `e59dc86`, pushed to `origin/main`.
- Re-verified live end-to-end (including after an app restart triggered by a context-window boundary killing the previous `dotnet run` process) via chrome-devtools + the 640-circuit simulator: Transfer 640/640 succeeded with correct 3-part labels, Start overlay showed a live climbing count, context menu closed instantly on every action, zero console errors.

## Files touched
`Components/Pages/Devices/DeviceList.razor`, `Components/UI/Dashboard/TransferDialog.razor`, `Components/UI/Dashboard/TransferResultsDialog.razor`, `Components/Pages/Home/DashboardView.razor`, `Components/UI/Loading/Processing.razor`, `Components/UI/ContextMenu/ContextMenuItem.razor`, `.gitignore` (pre-existing unrelated change, included in the same commit).
