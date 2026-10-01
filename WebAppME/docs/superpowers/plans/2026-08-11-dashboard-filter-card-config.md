# Dashboard Filter, Card Configuration & Card Layout Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the flat "My Channels" filter into a collapsible Device›Secondary›Channel tree with cascading selection, replace the cramped Card Configuration popover with a Dialog (5 quick controls + live preview) plus a full Settings page, make the card grid absorb leftover row width as gap instead of dead space, and fix the Mini/Compact hover-detail popover so it no longer overlaps neighboring cards.

**Architecture:** All four changes are scoped to existing Blazor Server components under `Components/UI/Dashboard/`, `Components/Pages/Home/DashboardView.razor`, and one new page under `Components/Pages/Settings/`. No new services, no data-contract changes — `CardConfig`, `ChannelViewPref`, and the `VisibleChannelsChanged`/`ConfigChanged` event shapes are all reused as-is.

**Tech Stack:** Blazor Server (.NET), Razor components, existing in-house UI kit (`Components/UI/Dialog`, `Components/UI/Popover`, `Components/UI/DropdownMenu`, `Blazicon`/Lucide icons), Tailwind utility classes, `ServerSessionStorageService`/`IConfigStorageService` for persistence.

## Global Constraints

- No unit-test framework exists for Razor components (no bUnit in `BatteryTestingSystem.Tests.csproj`) — verification for every task is `dotnet build` (must succeed with no new warnings/errors) plus manual browser verification per project's CLAUDE.md convention ("start the dev server and use the feature in a browser before reporting complete").
- Do not change the data contracts: `ChannelFilter`'s `AccessChannels`/`VisibleChannels`/`VisibleChannelsChanged` parameter shapes, `CardSettings`'/new page's `Config`/`ConfigChanged` shapes, and the `CardConfig`/`DisplayMode` model in `DashboardEnums.cs` are unchanged.
- Persistence keys are unchanged: `{CurrentUser.UserId}_channel_view_pref_v3` (channel filter) and `{CurrentUser.UserName}_card_config` (card config).
- Scope strictly to Mini/Compact display modes for the hover-popover fix; Normal/Chart/List are untouched (per spec, `RenderChart()` has the same hover block but is explicitly out of scope).
- `GridLayout.razor` is confirmed unused by the channel-card grid — do not touch it; the real layout logic lives in `DashboardView.razor`'s hand-rolled flex rows.
- Follow existing code style in each file (Tailwind utility classes, `Lucide`/`Blazicon` icons, existing `Button`/`DropdownMenu`/`Popover`/`Dialog` UI primitives) — do not introduce a new component library or CSS approach.

---

## File Structure

- **Modify** `WebAppME/Components/UI/Dashboard/ChannelFilter.razor` — rewrite the flat list as a collapsible Device›Secondary›Channel tree with tri-state cascade (Task 1).
- **Modify** `WebAppME/Components/UI/Dashboard/CardSettings.razor` — replace `Popover` with `Dialog`, trim to 5 quick controls + live preview card (Task 2).
- **Create** `WebAppME/Components/Pages/Settings/CardConfiguration.razor` — full settings page with the complete config surface (all 6 controls, full 3-section property checklist) + live preview (Task 3).
- **Modify** `WebAppME/Components/Layout/Navbar.razor` — add a "Card Configuration" entry to the Settings dropdown, routed via the existing `Navigate(NavMenuItem)` pattern (Task 3, same task as the page since the page is unreachable without it).
- **Modify** `WebAppME/Components/Pages/Home/DashboardView.razor` — replace the fixed `gap-3` row class with a per-row computed gap so leftover row width becomes even spacing (Task 4).
- **Modify** `WebAppME/Components/UI/Dashboard/DeviceChannel.razor` — fix the Mini/Compact hover-detail popover to flip upward near the viewport bottom and read visually as a tooltip, not a card (Task 5).

Each task is independently buildable and browser-testable; Task 3 depends only on the existing `CardConfig` model (no dependency on Tasks 1/2/4/5). Task 2 and Task 3 share the same live-preview rendering approach but are separate files with no shared new abstraction (per YAGNI — duplicating ~10 lines of preview markup is cheaper than introducing a shared component for two call sites, especially since the "quick" and "full" previews render different states of the same `DeviceChannel`).

---

### Task 1: Collapsible Device › Secondary › Channel tree filter

**Files:**
- Modify: `WebAppME/Components/UI/Dashboard/ChannelFilter.razor` (full rewrite of markup + `@code`)

**Interfaces:**
- Consumes: nothing new — same `[Parameter] List<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber, string Name)> AccessChannels`, `[Parameter] HashSet<(int,int,int)> VisibleChannels`, `[Parameter] EventCallback<HashSet<(int,int,int)>> VisibleChannelsChanged` as today.
- Produces: no new public parameters. `VisibleChannelsChanged` still emits the same `HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber)>` shape consumed by `DashboardView.razor`'s `_visibleCircuits` binding — unchanged from the caller's point of view.

The current implementation (`_hidden` set, `_search`, `RefreshFilteredList()`, `RefreshCachedState()`, `Toggle()`, `ShowAll()`, `HideAll()`, `EmitAsync()`, `Persist()`, `OnAfterRenderAsync`, `OnParametersSetAsync`, `ChannelViewPref`/`ChannelKey` DTOs) stays exactly as-is for state/persistence — only the render tree and the addition of a grouped-tree view model change.

- [ ] **Step 1: Add the grouped tree-node model and grouping logic**

Add these types and a computed property inside the existing `@code` block of `ChannelFilter.razor`, right after `_filteredList`:

```csharp
    private record ChannelLeaf(int DeviceID, int SecondaryBoardNumber, int ChannelNumber, string Name);

    private class SecondaryNode
    {
        public int SecondaryBoardNumber { get; init; }
        public List<ChannelLeaf> Channels { get; init; } = new();
    }

    private class DeviceNode
    {
        public int DeviceID { get; init; }
        public string DeviceLabel { get; init; } = "";
        public List<SecondaryNode> Secondaries { get; init; } = new();
    }

    // Expand state — keyed by DeviceID for device nodes, "{DeviceID}:{SecondaryBoardNumber}" for secondary nodes.
    private HashSet<int> _expandedDevices = new();
    private HashSet<string> _expandedSecondaries = new();

    private List<DeviceNode> BuildTree(List<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber, string Name)> source)
    {
        return source
            .GroupBy(c => c.DeviceID)
            .OrderBy(g => g.Key)
            .Select(deviceGroup => new DeviceNode
            {
                DeviceID = deviceGroup.Key,
                DeviceLabel = $"Device {deviceGroup.Key}",
                Secondaries = deviceGroup
                    .GroupBy(c => c.SecondaryBoardNumber)
                    .OrderBy(g => g.Key)
                    .Select(secGroup => new SecondaryNode
                    {
                        SecondaryBoardNumber = secGroup.Key,
                        Channels = secGroup
                            .OrderBy(c => c.ChannelNumber)
                            .Select(c => new ChannelLeaf(c.DeviceID, c.SecondaryBoardNumber, c.ChannelNumber, c.Name))
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();
    }

    private List<DeviceNode> _tree = new();

    private void RefreshTree()
    {
        _tree = BuildTree(_filteredList);
    }
```

Update `RefreshFilteredList()` to call `RefreshTree()` at its end:

```csharp
    private void RefreshFilteredList()
    {
        _filteredList = string.IsNullOrWhiteSpace(_searchBacking)
            ? AccessChannels
            : AccessChannels.Where(c =>
                c.Name.Contains(_searchBacking, StringComparison.OrdinalIgnoreCase) ||
                c.ChannelNumber.ToString().Contains(_searchBacking)).ToList();
        RefreshTree();
    }
```

- [ ] **Step 2: Add tri-state helpers and cascade/bubble logic**

Add below the tree model:

```csharp
    private enum TriState { Checked, Unchecked, Indeterminate }

    private bool IsVisible((int DeviceID, int SecondaryBoardNumber, int ChannelNumber) key) => !_hidden.Contains(key);

    private TriState GetSecondaryState(SecondaryNode sec)
    {
        var states = sec.Channels.Select(c => IsVisible((c.DeviceID, c.SecondaryBoardNumber, c.ChannelNumber))).ToList();
        if (states.All(v => v)) return TriState.Checked;
        if (states.All(v => !v)) return TriState.Unchecked;
        return TriState.Indeterminate;
    }

    private TriState GetDeviceState(DeviceNode dev)
    {
        var states = dev.Secondaries.Select(GetSecondaryState).ToList();
        if (states.All(s => s == TriState.Checked)) return TriState.Checked;
        if (states.All(s => s == TriState.Unchecked)) return TriState.Unchecked;
        return TriState.Indeterminate;
    }

    private async Task SetDeviceVisible(DeviceNode dev, bool visible)
    {
        foreach (var sec in dev.Secondaries)
            foreach (var c in sec.Channels)
                SetHidden((c.DeviceID, c.SecondaryBoardNumber, c.ChannelNumber), !visible);
        RefreshCachedState();
        Persist();
        await EmitAsync();
    }

    private async Task SetSecondaryVisible(SecondaryNode sec, bool visible)
    {
        foreach (var c in sec.Channels)
            SetHidden((c.DeviceID, c.SecondaryBoardNumber, c.ChannelNumber), !visible);
        RefreshCachedState();
        Persist();
        await EmitAsync();
    }

    private void SetHidden((int DeviceID, int SecondaryBoardNumber, int ChannelNumber) key, bool hidden)
    {
        if (hidden) _hidden.Add(key);
        else _hidden.Remove(key);
    }

    private void ToggleDeviceExpanded(int deviceId)
    {
        if (!_expandedDevices.Remove(deviceId)) _expandedDevices.Add(deviceId);
    }

    private void ToggleSecondaryExpanded(int deviceId, int secondaryBoardNumber)
    {
        var key = $"{deviceId}:{secondaryBoardNumber}";
        if (!_expandedSecondaries.Remove(key)) _expandedSecondaries.Add(key);
    }

    private bool IsDeviceExpanded(int deviceId) =>
        _expandedDevices.Contains(deviceId) || (!string.IsNullOrWhiteSpace(_searchBacking));

    private bool IsSecondaryExpanded(int deviceId, int secondaryBoardNumber) =>
        _expandedSecondaries.Contains($"{deviceId}:{secondaryBoardNumber}") || (!string.IsNullOrWhiteSpace(_searchBacking));
```

Note: `Toggle(key, isVisible)` (the existing leaf-toggle method) stays as-is — it already updates `_hidden` for a single channel, then calls `RefreshCachedState(); Persist(); await EmitAsync();`. No change needed there since parent tri-state is computed live from `_hidden` on every render (`GetSecondaryState`/`GetDeviceState`), not cached — so bubble-up is automatic.

- [ ] **Step 3: Replace the flat list markup with the tree markup**

Replace the existing:
```razor
            @* Channel list — materialise once to avoid double enumeration *@
            @{
                var filteredList = _filteredList;
            }
            <div class="max-h-56 overflow-y-auto space-y-0.5">
                @foreach (var c in filteredList)
                {
                    var key = (c.DeviceID, c.SecondaryBoardNumber, c.ChannelNumber);
                    var isVisible = !_hidden.Contains(key);
                    <label class="flex items-center gap-2 px-1 py-1 hover:bg-muted/50 cursor-pointer rounded text-xs">
                        <input type="checkbox" checked="@isVisible"
                               @onchange="@(async e => await Toggle(key, (bool)(e.Value ?? true)))"
                               class="w-3.5 h-3.5 accent-primary cursor-pointer" />
                        <span class="text-foreground">@c.Name – @(c.SecondaryBoardNumber)-@(c.ChannelNumber)</span>
                    </label>
                }
                @if (!filteredList.Any())
                {
                    <p class="text-xs text-muted-foreground py-2 text-center">No channels match.</p>
                }
            </div>
```

with:

```razor
            @* Device › Secondary › Channel tree *@
            <div class="max-h-64 overflow-y-auto space-y-0.5">
                @foreach (var dev in _tree)
                {
                    var devState = GetDeviceState(dev);
                    var devExpanded = IsDeviceExpanded(dev.DeviceID);
                    <div class="space-y-0.5">
                        <div class="flex items-center gap-1 px-1 py-1 hover:bg-muted/50 rounded text-xs font-medium">
                            <button type="button" @onclick="@(() => ToggleDeviceExpanded(dev.DeviceID))" class="w-4 h-4 flex items-center justify-center text-muted-foreground">
                                <Blazicon Svg="@(devExpanded ? Lucide.ChevronDown : Lucide.ChevronRight)" class="h-3 w-3" />
                            </button>
                            <input type="checkbox"
                                   checked="@(devState == TriState.Checked)"
                                   class="w-3.5 h-3.5 accent-primary cursor-pointer"
                                   @onchange="@(async e => await SetDeviceVisible(dev, (bool)(e.Value ?? true)))" />
                            <span class="text-foreground cursor-pointer flex-1" @onclick="@(() => ToggleDeviceExpanded(dev.DeviceID))">@dev.DeviceLabel</span>
                        </div>

                        @if (devExpanded)
                        {
                            @foreach (var sec in dev.Secondaries)
                            {
                                var secState = GetSecondaryState(sec);
                                var secExpanded = IsSecondaryExpanded(dev.DeviceID, sec.SecondaryBoardNumber);
                                <div class="pl-5 space-y-0.5">
                                    <div class="flex items-center gap-1 px-1 py-1 hover:bg-muted/50 rounded text-xs">
                                        <button type="button" @onclick="@(() => ToggleSecondaryExpanded(dev.DeviceID, sec.SecondaryBoardNumber))" class="w-4 h-4 flex items-center justify-center text-muted-foreground">
                                            <Blazicon Svg="@(secExpanded ? Lucide.ChevronDown : Lucide.ChevronRight)" class="h-3 w-3" />
                                        </button>
                                        <input type="checkbox"
                                               checked="@(secState == TriState.Checked)"
                                               class="w-3.5 h-3.5 accent-primary cursor-pointer"
                                               @onchange="@(async e => await SetSecondaryVisible(sec, (bool)(e.Value ?? true)))" />
                                        <span class="text-foreground cursor-pointer flex-1" @onclick="@(() => ToggleSecondaryExpanded(dev.DeviceID, sec.SecondaryBoardNumber))">Board @sec.SecondaryBoardNumber</span>
                                    </div>

                                    @if (secExpanded)
                                    {
                                        @foreach (var c in sec.Channels)
                                        {
                                            var key = (c.DeviceID, c.SecondaryBoardNumber, c.ChannelNumber);
                                            var isVisible = IsVisible(key);
                                            <label class="flex items-center gap-2 pl-9 pr-1 py-1 hover:bg-muted/50 cursor-pointer rounded text-xs">
                                                <input type="checkbox" checked="@isVisible"
                                                       @onchange="@(async e => await Toggle(key, (bool)(e.Value ?? true)))"
                                                       class="w-3.5 h-3.5 accent-primary cursor-pointer" />
                                                <span class="text-foreground">@c.Name – @(c.SecondaryBoardNumber)-@(c.ChannelNumber)</span>
                                            </label>
                                        }
                                    }
                                </div>
                            }
                        }
                    </div>
                }
                @if (!_tree.Any())
                {
                    <p class="text-xs text-muted-foreground py-2 text-center">No channels match.</p>
                }
            </div>
```

Also add `<input type="checkbox">` indeterminate-state support (native `checked` attribute can't express indeterminate declaratively in Razor) by adding a small `@ref`-based JS-free trick: use a CSS-only indicator instead — replace the device/secondary checkbox's `checked` binding with a 3-way visual using a wrapping `<span>` class instead of relying on the native indeterminate property, to avoid needing JS interop:

Replace both device and secondary checkbox `<input type="checkbox" ...>` elements' `class` attribute with a computed class that visually shows indeterminate via a dash icon overlay. Add this helper near the other tri-state helpers:

```csharp
    private string TriStateCheckboxClass(TriState state) => state switch
    {
        TriState.Indeterminate => "w-3.5 h-3.5 accent-primary cursor-pointer opacity-70",
        _ => "w-3.5 h-3.5 accent-primary cursor-pointer"
    };
```

and set each device/secondary `<input>`'s `checked="@(devState == TriState.Checked)"` combined with `class="@TriStateCheckboxClass(devState)"` (and the secondary equivalent with `secState`) — this gives a visually distinct (dimmed) checkbox for the partial state without JS interop, which is sufficient given the spec's requirement is "tri-state" behavior on click semantics (full cascade) with a distinguishable partial visual, not pixel-perfect native indeterminate rendering.

- [ ] **Step 4: Build and verify no errors**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: Build succeeds with 0 errors (existing warning count unchanged or lower).

- [ ] **Step 5: Manual browser verification**

Start the dev server (use the project's existing run process — e.g. `dotnet run --project WebAppME` or the `run` skill if configured), open the dashboard, open "My Channels":
- Confirm channels are grouped under collapsible Device headers, each containing collapsible Secondary Board headers, each containing channel checkboxes.
- Click a Device checkbox → confirm all its channels toggle visible/hidden together, and the dashboard card grid updates accordingly.
- Click a Secondary checkbox → confirm only that board's channels toggle.
- Uncheck one channel under a Secondary that was fully checked → confirm the Secondary checkbox becomes visually indeterminate (dimmed), and the Device checkbox also reflects indeterminate.
- Type in the search box → confirm matching channels show and their Device/Secondary ancestors auto-expand; clear search → confirm tree returns to prior collapsed/expanded state.
- Reload the page → confirm hidden-channel state persists (per existing `ChannelViewPref` storage).

- [ ] **Step 6: Commit**

```bash
git add WebAppME/Components/UI/Dashboard/ChannelFilter.razor
git commit -m "feat: collapsible Device>Secondary>Channel tree filter with cascading selection"
```

---

### Task 2: Card Configuration quick Dialog (5 controls + live preview)

**Files:**
- Modify: `WebAppME/Components/UI/Dashboard/CardSettings.razor`

**Interfaces:**
- Consumes: existing `CardConfig` (from `WebAppME/Components/UI/Dashboard/DashboardEnums.cs`): `CardSize` (double), `FontSize` (double), `RefreshRate` (int), `ShowDecimal` (int), `DisplayMode` (enum: Mini/Compact/Normal/Chart/List), `VisibleProperties` (List\<string\>). Consumes `DeviceChannel` component (`WebAppME/Components/UI/Dashboard/DeviceChannel.razor`) for the live preview, and `IChannelCommandHandler` — a fake/sample instance is needed (see Step 2 for how to construct one without a real device).
- Produces: no signature change — `[Parameter] CardConfig Config`, `[Parameter] EventCallback<CardConfig> ConfigChanged` stay identical, so `DashboardView.razor`'s `<CardSettings @bind-Config=@CConfig />` usage is untouched.

- [ ] **Step 1: Confirm a sample/preview data source exists for `DeviceChannel`**

`DeviceChannel.razor` requires `[Parameter, EditorRequired] public IChannelCommandHandler Channel`. Search for any existing mock/sample implementation of `IChannelCommandHandler` used for previews or tests before writing a new one:

Run (Gortex): `find_implementations` on `IChannelCommandHandler` to list concrete types. If none is suitable for a static preview (all are live-device-backed), create a minimal preview-only implementation inline in `CardSettings.razor`'s `@code` block:

```csharp
    // Minimal static implementation used only to drive the live preview card — no device I/O.
    private class PreviewChannelHandler : IChannelCommandHandler
    {
        public int DeviceID => 1;
        public int SecondaryBoardNumber => 1;
        public int ChannelNumber => 1;
        public string Name => "Preview Channel";
        // NOTE: implement remaining IChannelCommandHandler members with static sample values
        // matching whatever the interface actually declares (voltage/current/status/etc.) —
        // read the interface definition first via get_symbol_source before filling these in,
        // since its exact member list wasn't captured during design research.
    }

    private readonly PreviewChannelHandler _previewChannel = new();
```

Before writing this class, run `get_symbol_source` on `IChannelCommandHandler` (search_symbols for it first to get its ID) to get its exact member list, then implement every member with a small hardcoded/static sample value (e.g. Voltage=3.7, Current=1.2, Status=Charging) so the preview renders realistic-looking data. This is the one place in the plan where the exact code can't be pre-written because the interface's full member list wasn't captured during research — implement it directly from the interface source, matching types exactly.

- [ ] **Step 2: Replace `Popover`/`PopoverTrigger`/`PopoverContent` wrapper with `Dialog`/`DialogTrigger`-equivalent**

The existing file wraps everything in `<Popover><PopoverTrigger AsChild>...<Button>...</Button></PopoverTrigger><PopoverContent>...</PopoverContent></Popover>`. The `Dialog` primitive (`WebAppME/Components/UI/Dialog/Dialog.razor`) does not have a `DialogTrigger` that opens itself from a child button the same declarative way `Popover` does — it's driven by an `Open`/`OpenChanged` bool, as used in `DeviceDiscovery.razor`:

```razor
<Dialog @bind-open="@_ipDialogOpen">
    <DialogContent class="min-w-[480px] max-w-[560px]">
        ...
    </DialogContent>
</Dialog>
```

Restructure `CardSettings.razor`'s top-level markup to:

```razor
@namespace BatteryTestingSystem.Components.UI.Dashboard
@inject IJSRuntime JS
@inject ServerSessionStorageService sessionStorage
@implements IDisposable
@using BatteryTestingSystem.Components.UI.Dialog

<Button Variant="ButtonVariant.Outline" size="ButtonSize.Icon" Class="gap-2" @onclick="@(() => _dialogOpen = true)">
    <Blazicon Svg="Lucide.Settings" class="h-4 w-4" />
</Button>

<Dialog @bind-Open="_dialogOpen">
    <DialogContent Size="DialogContent.DialogSize.Large" Class="max-w-3xl">
        <div class="flex items-center justify-between mb-2">
            <h4 class="font-medium text-sm text-foreground">Card Configuration</h4>
            <Button variant="ButtonVariant.Ghost" size="ButtonSize.Small"
                    @onclick="ResetConfigAsync"
                    class="h-7 px-2 gap-1">
                <Blazicon Svg="Lucide.RotateCcw" class="h-3 w-3" />
                Reset
            </Button>
        </div>

        @if (!IsReady)
        {
            <div class="text-sm text-muted-foreground">Loading settings...</div>
        }
        else
        {
            <div class="grid grid-cols-2 gap-6">
                <div class="space-y-4">
                    <!-- Card Size, Display Mode, Font Size, Decimal Places, Quick Property toggles go here — see Step 3 -->
                </div>
                <div class="space-y-2">
                    <label class="text-sm font-medium text-foreground">Preview</label>
                    <div class="border border-border rounded-lg p-4 bg-muted/30 flex items-center justify-center" style="min-height: 240px;">
                        <DeviceChannel Channel="_previewChannel"
                                       Mode="Config.DisplayMode"
                                       MaxDigit="Config.ShowDecimal"
                                       FontSize="Config.FontSize"
                                       ChartMaxDataPoints="100"
                                       RefreshRate="Config.RefreshRate"
                                       VisibleProperties="Config.VisibleProperties" />
                    </div>
                </div>
            </div>
        }
    </DialogContent>
</Dialog>
```

Add `private bool _dialogOpen = false;` near the other private fields in `@code`. Remove the now-unused `elementId`, `dropdownOpen` is still used by the Display Mode dropdown (keep it), but the `Popover`/`PopoverTrigger`/`PopoverContent`-specific wiring (e.g. `ElementId="@elementId"` on the old `Popover`, `ParentId="@elementId"` on the `DropdownMenu` inside it) must be removed since `Dialog` doesn't use that popover-anchoring system — drop the `ParentId` attribute from the Display Mode `<DropdownMenu>` (it will anchor to its own trigger button by default).

- [ ] **Step 3: Trim the settings body to exactly 5 controls**

Inside the `<div class="space-y-4">` from Step 2, keep only:
1. The existing **Card Size** slider block (unchanged from current file).
2. The existing **Display Mode** dropdown block (unchanged from current file, minus the `ParentId` attribute per Step 2).
3. The existing **Font Size** slider block (unchanged from current file).
4. The existing **Decimal Places** button-group block (unchanged from current file).
5. A new condensed **Quick Properties** toggle row replacing the current "Visible Properties Selection" 3-section scrollable checklist:

```razor
                    <!-- Quick Properties -->
                    <div class="space-y-2">
                        <label class="text-sm font-medium text-foreground">Quick Properties</label>
                        <div class="flex flex-wrap gap-1.5">
                            @foreach (var key in QuickPropertyKeys)
                            {
                                var meta = RealTimeProperties[key];
                                <button type="button"
                                        @onclick="@(() => ToggleProperty(key, !IsPropertyVisible(key)))"
                                        class="@GetQuickToggleClass(key)">
                                    @meta.Label
                                </button>
                            }
                        </div>
                        <p class="text-xs text-muted-foreground">Full property list available on the Card Configuration settings page.</p>
                    </div>
```

Add the backing field and class helper in `@code`:

```csharp
    private static readonly List<string> QuickPropertyKeys = new() { "Voltage", "Current", "Power", "Temperature" };

    private string GetQuickToggleClass(string key)
    {
        var baseClass = "px-2.5 py-1 text-xs rounded-md border transition-colors";
        return IsPropertyVisible(key)
            ? $"{baseClass} bg-primary text-primary-foreground border-primary"
            : $"{baseClass} bg-muted text-muted-foreground border-border hover:bg-muted/80";
    }
```

Remove the **Refresh Rate** slider block and the entire "Visible Properties Selection" section (the `max-h-64 overflow-y-auto border` div with its three `ToggleSection`/`expandedSection` sub-blocks and the Select All/Clear All buttons) from this file — they move to Task 3's new page. Also remove now-unused members from `@code`: `expandedSection`, `ToggleSection(string)`, `ConfigProperties`, `ProgramProperties` (keep `RealTimeProperties` since `QuickPropertyKeys` reads from it), `SelectAllProperties()`, `ClearAllProperties()`. Keep `UpdateRefreshRate` removed too since its slider is gone — but double check `RefreshRate` is still part of `Config` and still flows to `DeviceChannel`'s `RefreshRate` parameter with whatever value was last persisted (it's just not editable from this quick dialog anymore).

- [ ] **Step 4: Build and verify no errors**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: Build succeeds with 0 errors. If `IChannelCommandHandler`'s member list from Step 1 doesn't compile, fix `PreviewChannelHandler` to match the interface exactly — this is expected to require one iteration since the interface wasn't fully read during design.

- [ ] **Step 5: Manual browser verification**

Start the dev server, open the dashboard, click the Card Configuration (gear) icon:
- Confirm a centered Dialog opens (not a small popover) with settings on the left and a live preview card on the right.
- Confirm exactly 5 controls are present: Card Size, Display Mode, Font Size, Decimal Places, Quick Properties toggle row.
- Adjust each control and confirm the preview card updates immediately (size, mode, font size, decimal precision, and toggled quick properties appearing/disappearing on the preview).
- Confirm "Reset" restores defaults and updates the preview.
- Close the dialog, confirm the real dashboard cards reflect the new settings (same as before this change).

- [ ] **Step 6: Commit**

```bash
git add WebAppME/Components/UI/Dashboard/CardSettings.razor
git commit -m "feat: replace Card Configuration popover with quick Dialog and live preview"
```

---

### Task 3: Full Card Configuration settings page

**Files:**
- Create: `WebAppME/Components/Pages/Settings/CardConfiguration.razor`
- Modify: `WebAppME/Components/Layout/Navbar.razor:~140-160` (Settings dropdown section)

**Interfaces:**
- Consumes: `CardConfig`/`DisplayMode` (`DashboardEnums.cs`), `ServerSessionStorageService` (same `{CurrentUser.UserName}_card_config` key as `CardSettings.razor`), `DeviceChannel` component for preview (same `PreviewChannelHandler` pattern as Task 2 — duplicated here per the file-structure note; do not extract a shared component).
- Produces: a routable page at `/settings/CardConfiguration`, and a new `NavMenuItem` entry in `Navbar.razor`'s Settings `DropdownMenuContent`.

- [ ] **Step 1: Create the page with full config surface**

```razor
@page "/settings/CardConfiguration"
@namespace BatteryTestingSystem.Components.Pages.Settings
@inject ServerSessionStorageService sessionStorage
@using BatteryTestingSystem.Models.Enums
@using BatteryTestingSystem.Components.UI.Dashboard

<div class="p-6 max-w-5xl mx-auto space-y-6">
    <div class="flex items-center justify-between">
        <h2 class="text-xl font-semibold text-foreground">Card Configuration</h2>
        <Button variant="ButtonVariant.Ghost" size="ButtonSize.Small" @onclick="ResetConfigAsync" class="h-8 px-3 gap-1">
            <Blazicon Svg="Lucide.RotateCcw" class="h-3.5 w-3.5" />
            Reset to Defaults
        </Button>
    </div>

    @if (!IsReady)
    {
        <div class="text-sm text-muted-foreground">Loading settings...</div>
    }
    else
    {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
            <div class="space-y-5">
                <!-- Card Size -->
                <div class="space-y-2">
                    <div class="flex items-center justify-between">
                        <label class="text-sm font-medium text-foreground">Card Size</label>
                        <span class="text-xs text-muted-foreground font-mono">@Config.CardSize px</span>
                    </div>
                    <input type="range" min="150" max="500" step="10" value="@Config.CardSize"
                           @onchange="@(e => UpdateCardSize(double.Parse(e.Value?.ToString() ?? "150")))"
                           class="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary" />
                </div>

                <!-- Font Size -->
                <div class="space-y-2">
                    <div class="flex items-center justify-between">
                        <label class="text-sm font-medium text-foreground">Font Size</label>
                        <span class="text-xs text-muted-foreground font-mono">@Config.FontSize px</span>
                    </div>
                    <input type="range" min="8" max="25" step="0.5" value="@Config.FontSize"
                           @onchange="@(e => UpdateFontSize(double.Parse(e.Value?.ToString() ?? "12")))"
                           class="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary" />
                </div>

                <!-- Refresh Rate -->
                <div class="space-y-2">
                    <div class="flex items-center justify-between">
                        <label class="text-sm font-medium text-foreground">Refresh Rate</label>
                        <span class="text-xs text-muted-foreground font-mono">@Config.RefreshRate ms</span>
                    </div>
                    <input type="range" min="0" max="5000" step="100" value="@Config.RefreshRate"
                           @onchange="@(e => UpdateRefreshRate(int.Parse(e.Value?.ToString() ?? "200")))"
                           class="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary" />
                </div>

                <!-- Decimal Places -->
                <div class="space-y-2">
                    <label class="text-sm font-medium text-foreground">Decimal Places</label>
                    <div class="flex gap-2">
                        @for (int i = 0; i <= 6; i++)
                        {
                            var value = i;
                            <button type="button" @onclick="@(() => UpdateShowDecimal(value))" class="@GetDecimalButtonClass(value)">@value</button>
                        }
                    </div>
                </div>

                <!-- Display Mode -->
                <div class="space-y-2">
                    <label class="text-sm font-medium text-foreground">Display Mode</label>
                    <DropdownMenu @bind-Open="dropdownOpen" class="w-full">
                        <DropdownMenuTrigger>
                            <Button Variant="ButtonVariant.Outline" Class="w-full justify-between" OnClick="@(() => dropdownOpen = !dropdownOpen)">
                                <div class="flex items-center gap-2">
                                    <Blazicon Svg="@GetDisplayModeIcon()" class="h-4 w-4" />
                                    <span>@Config.DisplayMode</span>
                                </div>
                                <Blazicon Svg="Lucide.ChevronDown" class="h-3 w-3 ml-2 opacity-50" />
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent Class="w-full">
                            @foreach (DisplayMode mode in Enum.GetValues(typeof(DisplayMode)))
                            {
                                <DropdownMenuItem OnClick="@(() => UpdateDisplayMode(mode))">
                                    <div class="flex items-center gap-2">
                                        <Blazicon Svg="@GetModeIcon(mode)" class="h-4 w-4" />
                                        <span>@mode</span>
                                    </div>
                                </DropdownMenuItem>
                            }
                        </DropdownMenuContent>
                    </DropdownMenu>
                </div>

                <!-- Visible Properties (full 3-section checklist) -->
                <div class="space-y-2">
                    <div class="flex items-center justify-between">
                        <label class="text-sm font-medium text-foreground">Visible Properties</label>
                        <span class="text-xs text-muted-foreground">@Config.VisibleProperties.Count selected</span>
                    </div>
                    <div class="max-h-80 overflow-y-auto border border-border rounded-md">
                        @foreach (var section in new[] { ("realtime", "RealTime Data", RealTimeProperties), ("config", "Configuration", ConfigProperties), ("program", "Program Data", ProgramProperties) })
                        {
                            <div class="border-b border-border last:border-b-0">
                                <button type="button" @onclick="@(() => ToggleSection(section.Item1))" class="w-full flex items-center justify-between px-3 py-2 bg-muted/50 hover:bg-muted transition-colors">
                                    <span class="text-xs font-semibold text-foreground">@section.Item2</span>
                                    <Blazicon Svg="@(expandedSection == section.Item1 ? Lucide.ChevronDown : Lucide.ChevronRight)" class="h-3 w-3" />
                                </button>
                                @if (expandedSection == section.Item1)
                                {
                                    <div class="p-2 space-y-1">
                                        @foreach (var prop in section.Item3)
                                        {
                                            <label class="flex items-center gap-2 px-2 py-1.5 hover:bg-muted/50 rounded cursor-pointer">
                                                <input type="checkbox" checked="@IsPropertyVisible(prop.Key)"
                                                       @onchange="@(e => ToggleProperty(prop.Key, (bool)(e.Value ?? false)))"
                                                       class="w-4 h-4 rounded border-border accent-primary cursor-pointer" />
                                                <div class="flex items-center gap-2 flex-1">
                                                    <Blazicon Svg="@prop.Value.Icon" class="h-3 w-3 text-muted-foreground" />
                                                    <span class="text-sm text-foreground">@prop.Value.Label</span>
                                                </div>
                                            </label>
                                        }
                                    </div>
                                }
                            </div>
                        }
                    </div>
                    <div class="flex gap-2">
                        <Button variant="ButtonVariant.Outline" size="ButtonSize.Small" @onclick="SelectAllProperties" Class="flex-1 h-8 text-xs">Select All</Button>
                        <Button variant="ButtonVariant.Outline" size="ButtonSize.Small" @onclick="ClearAllProperties" Class="flex-1 h-8 text-xs">Clear All</Button>
                    </div>
                </div>
            </div>

            <div class="space-y-2">
                <label class="text-sm font-medium text-foreground">Preview</label>
                <div class="border border-border rounded-lg p-6 bg-muted/30 flex items-center justify-center sticky top-6" style="min-height: 300px;">
                    <DeviceChannel Channel="_previewChannel"
                                   Mode="Config.DisplayMode"
                                   MaxDigit="Config.ShowDecimal"
                                   FontSize="Config.FontSize"
                                   ChartMaxDataPoints="100"
                                   RefreshRate="Config.RefreshRate"
                                   VisibleProperties="Config.VisibleProperties" />
                </div>
            </div>
        </div>
    }
</div>
```

- [ ] **Step 2: Port the `@code` block from the pre-Task-2 `CardSettings.razor`**

Copy the full `@code` block from `CardSettings.razor` as it existed **before** Task 2's trimming (i.e., include `RefreshRate` handling, `ConfigProperties`, `ProgramProperties`, `expandedSection`/`ToggleSection`, `SelectAllProperties`/`ClearAllProperties` — everything Task 2 removed) into this new page's `@code` block, with these changes:
- Remove the `[Parameter] CardConfig Config` / `[Parameter] EventCallback<CardConfig> ConfigChanged` parameters — this page owns its own `Config` field directly (`private CardConfig Config = new();`) since it's a standalone page, not a child component.
- Remove `NotifyConfigChanged()`'s call to `ConfigChanged.InvokeAsync(Config)` (no parent to notify) — keep the `SaveConfigToLocalStorage()` + `StateHasChanged()` calls.
- Add the same `PreviewChannelHandler`/`_previewChannel` field from Task 2, Step 1 (duplicated, not shared, per the file-structure decision).
- Keep `elementId`/`dropdownOpen`/`_isInitialized`/`IsReady` and the `OnAfterRenderAsync` initialization logic as-is (adjust the `DropdownMenu`'s `ParentId="@elementId"` — remove it, matching Task 2's Dialog-compatibility change, since this page has no popover-anchor system either).
- Keep `GetDecimalButtonClass`, `GetDisplayModeIcon`, `GetModeIcon`, `SaveConfigToLocalStorage`/`LoadConfigFromLocalStorage`/`ClearConfigFromLocalStorage`, `Dispose()` unchanged (same storage key `{CurrentUser.UserName}_card_config`, so edits here and in the quick Dialog stay in sync the next time either loads).

- [ ] **Step 3: Wire the Settings nav entry**

In `WebAppME/Components/Layout/Navbar.razor`, inside the existing Settings `<DropdownMenuContent>` block (the one containing "Notification Settings", "Logs", "Discover", "Audit Logs", "User Management"), add a new top-level item (not inside an `AuthorizeView` restriction, since card display config is a personal preference, not an admin action) directly after the "Notification Settings" item:

```razor
                    <DropdownMenuItem OnClick="@(() => Navigate(new NavMenuItem { Url = "/settings/CardConfiguration", Icon = Lucide.LayoutGrid, Title = "Card Configuration", ComponentType = typeof(CardConfiguration), Unique = true }))">
                        Card Configuration
                    </DropdownMenuItem>
```

Add `@using BatteryTestingSystem.Components.Pages.Settings` is already present at the top of `Navbar.razor` (confirmed from existing `@using` list) — no new `@using` needed since `CardConfiguration` lives in that same namespace.

- [ ] **Step 4: Build and verify no errors**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 5: Manual browser verification**

Start the dev server, log in, open the Settings (gear) dropdown in the navbar → confirm "Card Configuration" appears and opens the new page as a tab (consistent with how "Discover"/"Users" open).
- Confirm all 6 controls are present (Card Size, Font Size, Refresh Rate, Decimal Places, Display Mode, full 3-section Visible Properties checklist with Select All/Clear All) plus the live preview.
- Change a setting here, then open the dashboard's quick Card Configuration Dialog (Task 2) — confirm the change persisted and shows there too (same storage key).
- Change a setting in the quick Dialog, reload this full settings page — confirm it reflects the change.

- [ ] **Step 6: Commit**

```bash
git add WebAppME/Components/Pages/Settings/CardConfiguration.razor WebAppME/Components/Layout/Navbar.razor
git commit -m "feat: add full Card Configuration settings page reachable from Settings menu"
```

---

### Task 4: Card grid — distribute leftover row width as gap

**Files:**
- Modify: `WebAppME/Components/Pages/Home/DashboardView.razor` (row template + `@code`)

**Interfaces:**
- Consumes: existing `CConfig.CardSize` (double), existing `VirtualRows` (`List<List<IChannelCommandHandler>>`), existing `ColumnsPerRow` computed property.
- Produces: no new public interface — this is purely internal row-rendering math. Adds one new private computed/method, `RowGapPx(int cardsInRow)`, used only inside this file's row template.

- [ ] **Step 1: Add a container-width tracking field and computed row gap**

`ColumnsPerRow` currently uses a hardcoded `ReferenceContainerWidthPx = 1600` rather than the real measured container width — the plan keeps that existing approximation (changing it to a real measured width via JS interop is out of scope per the spec's "no changes to virtualization/row-chunking logic" constraint) and computes the gap against that same reference width, so the math stays internally consistent with however many columns `ColumnsPerRow` already decided to place per row:

```csharp
    private const double BaseGapPx = 12; // matches existing gap-3 (0.75rem = 12px)

    private double RowGapPx(int cardsInRow)
    {
        if (cardsInRow <= 1) return BaseGapPx;
        var usedWidth = cardsInRow * CConfig.CardSize;
        var leftover = ReferenceContainerWidthPx - usedWidth;
        var computedGap = leftover / (cardsInRow - 1);
        return computedGap > BaseGapPx ? computedGap : BaseGapPx;
    }
```

Add this directly below the existing `ColumnsPerRow` property in the `@code` block.

- [ ] **Step 2: Apply the computed gap to the row template**

Change the row div from the fixed Tailwind class to an inline style using the computed gap, since Tailwind's `gap-3` can't take a dynamic value:

Replace:
```razor
    <div class="flex gap-3 mb-3">
```
with:
```razor
    <div class="flex mb-3" style="gap: @(RowGapPx(row.Count))px;">
```

(`row` is the `Context="row"` variable from the existing `<Virtualize Items="@VirtualRows" Context="row" ...>` — `row.Count` gives the number of cards in that specific row, which may be less than `ColumnsPerRow` for the last row.)

- [ ] **Step 3: Build and verify no errors**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Manual browser verification**

Start the dev server, open the dashboard with a device/channel count that doesn't evenly fill full rows at the current window width (e.g. resize the browser window, or use a channel count that leaves a partial last row):
- Confirm no horizontal scrollbar appears on the card area at any window width tested.
- Confirm rows with fewer cards than a full row (e.g. the last row) show visibly wider, even gaps between cards rather than trailing blank space after the last card.
- Confirm cards never grow/shrink from their configured `CardSize` — only the gaps change.
- Confirm the vertical scroll behavior of the card area (`overflow-y-auto`) is unaffected — scrolling still works normally when there are more rows than fit the viewport.
- Repeat with the 64-channel hardware simulator config (`HardwareSimulator/config.json`) to confirm no layout regression at high channel counts (per this project's existing verified 64-channel testing convention).

- [ ] **Step 5: Commit**

```bash
git add WebAppME/Components/Pages/Home/DashboardView.razor
git commit -m "fix: distribute leftover card-row width as gap instead of trailing dead space"
```

---

### Task 5: Fix Mini/Compact hover-detail popover overlap

**Files:**
- Modify: `WebAppME/Components/UI/Dashboard/DeviceChannel.razor` (Mini ~line 912, Compact ~line 951; leave the Chart-mode copy of this block untouched per spec scope)

**Interfaces:**
- Consumes: nothing new.
- Produces: no new public parameters. Adds one new private field, `_isNearViewportBottom` (bool), set via a small JS interop call, and one new CSS-class helper, `HoverPopoverClasses()`, used by both `RenderMini()` and `RenderCompact()`.

- [ ] **Step 1: Add viewport-edge detection via JS interop**

`DeviceChannel.razor` doesn't currently inject `IJSRuntime` — check via `get_editing_context` on the file first to confirm; if absent, add `@inject IJSRuntime JS` near the top of the file. Add a per-card element reference and a hover-triggered edge check:

```csharp
    private ElementReference _cardRef;
    private bool _flipPopoverUp = false;

    private async Task OnCardMouseEnter()
    {
        try
        {
            var rect = await JS.InvokeAsync<BoundingRect>("eval", $@"
                (function() {{
                    var el = document.querySelectorAll('[data-card-id=""{Channel.DeviceID}-{Channel.SecondaryBoardNumber}-{Channel.ChannelNumber}""]')[0];
                    if (!el) return {{ bottom: 0, viewportHeight: window.innerHeight }};
                    var r = el.getBoundingClientRect();
                    return {{ bottom: r.bottom, viewportHeight: window.innerHeight }};
                }})()
            ");
            _flipPopoverUp = rect.Bottom > rect.ViewportHeight - 260; // 260px ≈ popover height + margin
        }
        catch
        {
            _flipPopoverUp = false; // fail safe: default to opening downward as today
        }
    }

    private class BoundingRect
    {
        public double Bottom { get; set; }
        public double ViewportHeight { get; set; }
    }

    private string HoverPopoverPositionClass => _flipPopoverUp
        ? "absolute left-0 bottom-full mb-1 z-50 hidden group-hover:block"
        : "absolute left-0 top-full mt-1 z-50 hidden group-hover:block";
```

Add a `data-card-id="@($"{Channel.DeviceID}-{Channel.SecondaryBoardNumber}-{Channel.ChannelNumber}")"` attribute and an `@onmouseenter="OnCardMouseEnter"` handler to the outer card `<div>` in both `RenderMini()` and `RenderCompact()` — i.e. change:

```csharp
    <div @onclick="ToggleSelected" class="@GetCardClasses() flex flex-col h-full group"&gt;
```
to:
```csharp
    <div @onclick="ToggleSelected" @onmouseenter="OnCardMouseEnter"
         data-card-id="@($"{Channel.DeviceID}-{Channel.SecondaryBoardNumber}-{Channel.ChannelNumber}")"
         class="@GetCardClasses() flex flex-col h-full group"&gt;
```

in both `RenderMini()` and `RenderCompact()` (two call sites — do not touch `RenderChart()`'s copy of this block).

- [ ] **Step 2: Use the computed position class and add visual distinction**

In both `RenderMini()` and `RenderCompact()`, replace:
```csharp
        <div class="absolute left-0 top-full mt-1 z-50 hidden group-hover:block"&gt;
            <div class="bg-popover border border-border rounded-md shadow-lg p-3 w-64"&gt;
```
with:
```csharp
        <div class="@HoverPopoverPositionClass"&gt;
            <div class="bg-popover border-2 border-primary/40 rounded-md shadow-xl p-3 w-64 relative"&gt;
                <div class="absolute left-4 @(_flipPopoverUp ? "top-full border-t-8 border-t-primary/40" : "bottom-full border-b-8 border-b-primary/40") border-x-8 border-x-transparent w-0 h-0"&gt;&lt;/div&gt;
```
(the small extra `<div>` is the pointer/arrow — `border-t-8`/`border-b-8` with transparent side borders draws a CSS triangle pointing at the card, flipped to match whichever side the popover opens from). Close this extra wrapping `<div>` properly — the existing inner content (`<div class="space-y-3">...` or `space-y-2` for Compact) stays nested exactly as before, just inside one additional `relative` wrapper.

- [ ] **Step 3: Build and verify no errors**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Manual browser verification**

Start the dev server, set Display Mode to Mini via the Card Configuration dialog (Task 2), open the dashboard:
- Hover a card in the top/middle of the visible card area → confirm the detail popover opens downward as before, with a visible arrow pointing up at the card and a distinct border/shadow (reads as a tooltip, not a card).
- Scroll so a row of cards sits near the bottom of the scrollable card area, hover one of those cards → confirm the popover now opens **upward** and does not visually overlap/bleed into the row below or get clipped by the viewport edge.
- Repeat both checks in Compact mode.
- Confirm Normal/Chart/List modes are visually unchanged (no hover popover behavior was ever added/removed there — Chart's identical block is untouched per scope).

- [ ] **Step 5: Commit**

```bash
git add WebAppME/Components/UI/Dashboard/DeviceChannel.razor
git commit -m "fix: flip Mini/Compact hover detail popover upward near viewport bottom, add tooltip styling"
```

---

## Self-Review Notes

- **Spec coverage:** Section 1 (tree filter) → Task 1. Section 2 (Dialog + settings page) → Tasks 2-3. Section 3 (grid gap) → Task 4. Section 4 (hover popover) → Task 5. All four spec sections have a task; the spec's "Out of scope" list (no `GridLayout.razor` changes, no data-contract changes, no Normal/Chart/List changes, no virtualization/row-chunking changes) is respected by every task's Interfaces/Files sections above.
- **Known research gap flagged inline:** `IChannelCommandHandler`'s exact member list wasn't captured during design research (Task 2, Step 1) — the plan explicitly tells the implementer to read the interface source first rather than guessing its members, so this isn't a silent placeholder.
- **Type consistency:** `CardConfig`, `DisplayMode`, `IChannelCommandHandler`, `PreviewChannelHandler` (duplicated intentionally in Tasks 2 and 3, per file-structure rationale), `RowGapPx(int)`, `HoverPopoverPositionClass`, `_flipPopoverUp` are each defined once and referenced consistently within their owning task; no cross-task name drift since Tasks 2/3/4/5 don't share new types.
