# T-34 — Dashboard: filtered Select All + real 3-level My Channels treeview

> Created: 2026-08-18 | Session #20 | Status: ✅ Done (build + live-browser verified)
> Files: `Components/Pages/Home/DashboardView.razor`, `Components/UI/Dashboard/ChannelFilter.razor`

## Request

1. Dashboard required selecting channel cards **one by one**; user wanted a Select All over the
   currently *available* set, with active filters taken into account.
2. The **My Channels** dialog read as a 2-level tree — device and secondary board rendered at the
   same indent — instead of `device → secondary board → channel`.

## Root causes

**(1) Select All** — `SelectAll()` and `DeselectAll()` already existed at `DashboardView.razor:445/485`
but were **never called from any markup**: dead code, hence the one-by-one clicking. Worse, the
"what is visible" predicate was duplicated: `VirtualRows` filtered on
`inAccess && inVisible && inChip && inOnline`, while `SelectAll` checked only `_accessCircuits` /
`_visibleCircuits` and **ignored both header chips** (`_statusChip`, `_onlineOnly` — added in
session #15). Wiring the button up as-is would have selected off-screen cards.

**(2) Treeview** — the markup was **already 3-level** (`DeviceNode → SecondaryNode → ChannelLeaf`).
The board wrapper used `class="pl-5"`, and 🪤 **`pl-5` does not exist in `wwwroot/css/app.min.css`**,
which per **ADR-2** has no rebuild pipeline. It silently resolved to `0px`, so Board rendered flush
with Device while the leaf's `pl-9` (36px) worked — producing the 2-level look.
Verified in the live browser: `pl-5`→0px, `pl-4`→0px, `ml-4`→0px, `pl-6`→24px, `pl-8`→32px, `pl-9`→36px.

## Changes

- `ChannelFilter.razor:73` — `pl-5` → **`pl-6`** (+ comment explaining the ADR-2 trap).
- `DashboardView.razor` — extracted **`FilteredCircuits`**, the single definition of "currently on
  screen". `VirtualRows` chunks it; `SelectAll` selects from it, so the two can never drift again.
- `SelectAll()` rewritten:
  - selects from `FilteredCircuits` (chips now honoured),
  - **reuses the existing `AnchorCircuit`** when a selection exists, instead of resetting to the
    first visible card — otherwise Select All could widen a homogeneous selection into a mixed one,
  - **skips Offline** for both anchor and members (the per-card `UpdateSelectedCircuits` already
    rejects offline, so anchoring on one produced a set no action could run on),
  - reports `"N selected, M skipped"` / success toast.
- New `SelectAllLabel` property + **Select All / Clear** buttons in the header toolbar
  (`ButtonSize.Small` — ⚠️ the enum has `Default/Small/Large/Icon/Auto`, there is **no `Sm`**).

**User decisions:** keep the anchor-state rule and report skips (not select-all-regardless);
fix the indent by swapping to the verified-present `pl-6` rather than inline styles.

## Verification (live, 640 circuits streaming)

- `dotnet build`: **0 errors**.
- Tree indents measured in-browser: Device **4px** → Board **28px** → Channel **60px** — three
  distinct levels (Board was previously 4px, identical to Device). Screenshot: `scratchpad/tree-3level.png`.
- Toolbar: `Select All (640)` enabled, `Clear` disabled with nothing selected.
- Select All → `Clear` becomes enabled (selection applied).
- **Chip integration proven both ways:** `Charge` chip (0 circuits) → `Select All (0)`, **disabled**,
  0 cards rendered; `Stop` chip (640) → `Select All (640)`, enabled.
- 0 `second operation`, 0 ERR/FTL, 0 stderr bytes, 0 browser console messages.

## Open follow-up

`SelectAllLabel` currently shows the **filtered** count, which can overstate when the view holds
mixed states (the anchor rule may select fewer). Marked with a TODO in source; the honest
alternative costs a second pass over the filtered set on every render, which is non-trivial on a
page that re-renders per telemetry tick at 640 circuits. Left for the user to choose.
