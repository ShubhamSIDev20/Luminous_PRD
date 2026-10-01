# Dashboard Filter, Card Configuration & Card Layout Fixes — Design

Date: 2026-08-11
Status: Approved

## Context

Four related UX issues on the main dashboard (`Components/Pages/Home/DashboardView.razor` and its child components under `Components/UI/Dashboard/`):

1. The "My Channels" filter (`ChannelFilter.razor`) is a flat checkbox list; the user wants a collapsible Device → Secondary Board → Channel hierarchy with cascading selection.
2. Card Configuration (`CardSettings.razor`) is a cramped `Popover` with no visual feedback; the user wants a proper dialog with a live preview, plus a full settings page for the complete option set.
3. The card grid (hand-rolled flex rows in `DashboardView.razor`) leaves leftover row width as dead space after the last card instead of using it as breathing room between cards.
4. In Mini/Compact display modes, hovering a card reveals a detail popover that overlaps neighboring cards/rows, reading as a visual glitch ("extra card").

## 1. Channel filter → collapsible Device › Secondary › Channel tree

**Component:** `Components/UI/Dashboard/ChannelFilter.razor`

- Group `AccessChannels` by `DeviceID`, then by `SecondaryBoardNumber`, then list `ChannelNumber` leaves.
- Each Device and Secondary node is a collapsible header (expand/collapse chevron, default state: collapsed, or expanded if it contains the active search match).
- Tri-state checkbox at Device and Secondary level:
  - **Checked** — no descendant channels hidden
  - **Unchecked** — all descendant channels hidden
  - **Indeterminate** — some hidden, some visible
- Interaction:
  - Clicking a Device/Secondary checkbox forces all descendant channel checkboxes to the same state (cascade down), updating `_hidden` for every affected channel key.
  - Clicking a leaf Channel checkbox toggles only that channel, then recomputes the tri-state of its Secondary parent and Device grandparent (bubble up).
- Search (`_search`) filters at the channel-name/number level; a Device/Secondary node auto-expands when it contains a match, and collapses back if the search is cleared and it wasn't manually expanded (simplest correct behavior: search always drives expansion while a query is active; manual expand/collapse state applies when search is empty).
- Persistence contract is unchanged: `_hidden` (the existing `HashSet<(DeviceID, SecondaryBoardNumber, ChannelNumber)>`) is still what's written via `ChannelViewPref`/`IConfigStorageService`, and `VisibleChannelsChanged` still emits the same shape to `DashboardView`. Only rendering and interaction logic change — no changes to the data contract with the parent.
- "Show All" / "Hide All" buttons keep working exactly as today (clear/fill `_hidden` for all access channels).

## 2. Card Configuration → Dialog (quick) + Settings page (full)

**Components:** `Components/UI/Dashboard/CardSettings.razor` (rework), new `Components/Pages/Settings/CardConfiguration.razor` (new page)

Both surfaces bind to the same `CardConfig` model and both persist through the existing `IConfigStorageService` / `ServerSessionStorageService` path (`{CurrentUser.UserName}_card_config` key) — no change to the storage contract, only to presentation.

### Dashboard quick dialog
- Replace the `Popover` trigger/content with a centered `Dialog` (existing `Modal`/`Dialog` UI primitives).
- Layout: settings controls on one side, a **live preview card** on the other, rendered using the actual `DeviceChannel` card markup (or an equivalent lightweight preview) with sample data, re-rendering on every control change exactly like the real cards would.
- Exactly 5 controls, matching what's most useful to tweak in-context:
  1. Card Size (slider, 150–500px, existing range)
  2. Display Mode (dropdown: Mini/Compact/Normal/Chart/List)
  3. Font Size (slider, existing range)
  4. Decimal Places (button group, existing 0–6)
  5. Quick property toggles — a condensed single row of on/off chips for the top properties (Voltage, Current, Power, Temperature, plus 1-2 more high-value ones), instead of the full 3-section scrollable checklist.
- "Reset" button keeps resetting to `DefaultConfig` as today.
- Refresh Rate and the full property checklist (RealTime/Config/Program sections, Select All/Clear All) are **not** in the quick dialog — they move to the full settings page only.

### Full settings page
- New page under Settings navigation (alongside existing `Components/Pages/Settings/*.razor` pages), e.g. `Components/Pages/Settings/CardConfiguration.razor`.
- Contains the complete current `CardSettings.razor` option set: Card Size, Font Size, Refresh Rate, Decimal Places, Display Mode, and the full 3-section (RealTime/Config/Program) property checklist with Select All/Clear All.
- Includes the same live preview card as the quick dialog.
- The dashboard toolbar's settings icon opens the quick Dialog (not this page); the full page is reached via Settings nav, matching how other admin/config pages in this app are organized.

## 3. Card grid: absorb leftover row width as gap

**Component:** `Components/Pages/Home/DashboardView.razor` (row-building/layout logic)

- Cards remain fixed-width (`CardSize`px, from `CConfig`), laid out in `flex` rows as today — no change to `Virtualize`/row-chunking logic that determines how many cards fit per row.
- Change the row's gap handling from a fixed `gap-3` to a **computed gap** per row: `extraGap = (containerWidth - (cardsInRow * cardWidth)) / (cardsInRow - 1)` (falling back to the base gap when a row has only one card, or when the computed gap would be smaller than the base gap), so leftover width becomes evenly-distributed breathing room between cards (space-evenly behavior) instead of trailing dead space.
- No horizontal scrollbar today and none introduced by this change — cards never grow past `CardSize`, only the space between them grows.
- Vertical scrolling of the card area (`overflow-y-auto`, `max-height: calc(100vh - 160px)`) is unchanged — only cards, never scroll containers, are the layout target of this fix.

## 4. Mini/Compact hover popover: fix overlap instead of removing it

**Component:** `Components/UI/Dashboard/DeviceChannel.razor` (Mini ~line 912, Compact ~lines 951/1027)

- Keep the existing hover-to-reveal detail popover (`group-hover:block`) for Mini/Compact modes — it's the only way to see full detail on those compact cards, per the user's own accepted trade-off.
- Fix the overlap: detect (via a JS interop bounding-rect check, or a simpler CSS-only approach using `bottom-full` as an alternative placement class toggled by row position) whether the card is in the last 1-2 visible rows of the scroll viewport, and flip the popover to open **upward** (`bottom-full mb-1` instead of `top-full mt-1`) in that case, so it never renders into/behind the next virtualized row.
- Ensure stacking is correct: popover keeps `z-50`, and confirm no ancestor in the row/card wrapper chain clips it with `overflow-hidden` (the row and grid containers must allow overflow for the hover layer specifically, even though the outer scroll container clips normally — achieved by keeping the popover's positioning context at the card level, not nesting it under any `overflow-hidden` ancestor different from today).
- Visual distinction: give the popover a small pointer/arrow (pointing at the card) and a slightly more pronounced border/shadow than a card, so on the rare occasion two are near each other it's unambiguous which is the hover detail and which is a real card.
- Scoped strictly to Mini/Compact — Normal/Chart/List modes are untouched.

## Out of scope

- No changes to the underlying data model, `IConfigStorageService`/`ChannelViewPref` storage formats, or the `VisibleChannelsChanged`/`ConfigChanged` event contracts consumed by `DashboardView`.
- No changes to `GridLayout.razor` (confirmed unused by the channel-card grid — `DashboardView.razor` hand-rolls its own flex-row layout).
- No changes to Normal/Chart/List display modes.
- No changes to virtualization/row-chunking logic (`Virtualize`, `ItemSize`) beyond the per-row gap computation described in section 3.

## Testing

- Manual verification in-browser (per project convention) for all four items: tree filter cascade + persistence, quick dialog + settings page both writing/reading the same config, grid gap behavior at various window widths and channel counts (4-channel and 64-channel configs), and hover popover placement near the bottom of the scroll viewport in Mini and Compact modes.
