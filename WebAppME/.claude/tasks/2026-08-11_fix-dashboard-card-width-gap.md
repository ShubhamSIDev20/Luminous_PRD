# T-8: Fix dashboard channel cards not filling full grid-cell width
> Created: 2026-08-11 | Session: #4 | Status: ✅ Done

## Goal
User reported: on the Dashboard, each channel card's grid cell has a fixed width, but the card content inside doesn't take that full width — leaving a visible gap on the right side of every card.

## What was done
- Traced the render chain for a card: `DashboardView.razor` gives each grid cell a fixed `width: @(CConfig.CardSize)px` wrapper div, which contains `<ContextMenu><ContextMenuTrigger><DeviceChannel/></ContextMenuTrigger></ContextMenu>`.
- Found the root cause in `Components/UI/ContextMenu/ContextMenu.razor`: its wrapping div used `class="relative inline-block"`. `inline-block` shrink-wraps to its content's intrinsic width instead of filling its parent, so the actual card content sized itself to the card's natural content width rather than the fixed `CardSize` slot — producing the gap.
- `ContextMenuTrigger.razor`'s own wrapper div has no width class, so it was already relying on its parent (`ContextMenu`) to establish full width — confirming `ContextMenu` was the one component that needed the fix.
- Checked all usages of `<ContextMenu>` — only one, in `DashboardView.razor` for the channel cards — so the fix is contained with no other call sites to break.

## Fix
`Components/UI/ContextMenu/ContextMenu.razor`: changed the wrapper `class="relative inline-block"` → `class="relative block w-full"`.

## Follow-ups / Not done
- Not yet manually re-verified in the browser (user was asked to rebuild/refresh and confirm cards now fill their grid width with no gap).
