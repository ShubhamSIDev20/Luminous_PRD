# Circuit Filter Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the broken CircuitFilter with a two-layer model: DB-driven access enforcement (Layer 1) + user-managed view preference within their access set (Layer 2) + live status chips on the toolbar (Layer 3), and wire circuit assignment into the user creation flow.

**Architecture:** `DashboardView` loads the user's assigned circuits from `UserCircuitAccess` on init (Layer 1); admins get all circuits. `CircuitFilter` manages a persisted hide-list within the access set (Layer 2), stored via `ServerSessionStorageService` with `Permanent: true` so it survives logouts. Status chips on the toolbar (Layer 3) are computed live every render from `CircuitStatus`. `Users.razor` immediately opens the circuit access dialog after a non-admin user is created.

**Tech Stack:** Blazor Server, `IUserCircuitAccessService`, `ServerSessionStorageService`, Popover/Button/Blazicon UI components (existing), `CircuitStatus` / `ProgramRunningStatus` enums.

---

## Files Changed

| File | Change |
|------|--------|
| `Components/UI/Dashboard/CircuitFilter.razor` | Full rewrite — access-scoped list, search, persist hidden-set |
| `Components/Pages/Home/DashboardView.razor` | Layer 1 init, Layer 3 chips, fix grid render, update CircuitFilter binding |
| `Components/Pages/Settings/Users.razor` | Open circuit access dialog immediately after new non-admin user created |

---

## Task 1 — Rewrite CircuitFilter.razor

**File:** `Components/UI/Dashboard/CircuitFilter.razor`

This component now receives only the circuits the user may access (`AccessCircuits`) and manages a `_hidden` set within them. It persists the hidden set via `ServerSessionStorageService`. It emits the visible set upward via `VisibleCircuitsChanged`.

- [ ] **Step 1.1 — Replace the entire file with the new component**

```razor
@namespace BatteryTestingSystem.Components.UI.Dashboard
@inject ServerSessionStorageService sessionStorage
@using BatteryTestingSystem.Models.Enums

<Popover>
    <PopoverTrigger AsChild>
        <Button Variant="ButtonVariant.Outline" size="ButtonSize.Icon" Class="relative">
            <Blazicon Svg="Lucide.ListFilter" class="h-4 w-4" />
            @if (_hiddenCount > 0)
            {
                <span class="absolute -top-1 -right-1 bg-destructive text-destructive-foreground text-[10px] font-mono rounded-full w-4 h-4 flex items-center justify-center">
                    @(_hiddenCount > 9 ? "9+" : _hiddenCount.ToString())
                </span>
            }
        </Button>
    </PopoverTrigger>

    <PopoverContent Class="w-72 bg-popover border border-border z-50" Align="PopoverAlign.End">
        <div class="space-y-3">

            @* Header *@
            <div class="flex items-center justify-between pb-2 border-b border-border">
                <div>
                    <h4 class="font-medium text-sm text-foreground">My Circuits</h4>
                    <span class="text-[10px] text-muted-foreground">
                        @(AccessCircuits.Count - _hiddenCount) / @AccessCircuits.Count visible
                    </span>
                </div>
                <div class="flex gap-1">
                    @if (_hiddenCount > 0)
                    {
                        <Button Variant="ButtonVariant.Ghost" size="ButtonSize.Small"
                                @onclick="ShowAll" class="h-6 px-2 text-xs">
                            Show All
                        </Button>
                    }
                    <Button Variant="ButtonVariant.Ghost" size="ButtonSize.Small"
                            @onclick="HideAll" class="h-6 px-2 text-xs text-muted-foreground">
                        Hide All
                    </Button>
                </div>
            </div>

            @* Search *@
            <input type="search" placeholder="Search circuits…"
                   class="w-full h-7 px-2 text-xs rounded border border-border bg-background focus:outline-none focus:ring-1 focus:ring-primary"
                   @bind="_search" @bind:event="oninput" />

            @* Circuit list *@
            <div class="max-h-56 overflow-y-auto space-y-0.5">
                @foreach (var c in FilteredList)
                {
                    var key = (c.DeviceID, c.CircuitID);
                    var isVisible = !_hidden.Contains(key);
                    <label class="flex items-center gap-2 px-1 py-1 hover:bg-muted/50 cursor-pointer rounded text-xs">
                        <input type="checkbox" checked="@isVisible"
                               @onchange="@(async e => await Toggle(key, (bool)(e.Value ?? true)))"
                               class="w-3.5 h-3.5 accent-primary cursor-pointer" />
                        <span class="text-foreground">@c.Name – C@(c.CircuitID)</span>
                    </label>
                }
                @if (!FilteredList.Any())
                {
                    <p class="text-xs text-muted-foreground py-2 text-center">No circuits match.</p>
                }
            </div>
        </div>
    </PopoverContent>
</Popover>

@code {
    /// <summary>Circuits this user is permitted to see — set by DashboardView from DB access table.</summary>
    [Parameter] public List<(int DeviceID, int CircuitID, string Name)> AccessCircuits { get; set; } = new();

    /// <summary>Current visible subset — two-way bound to DashboardView._visibleCircuits.</summary>
    [Parameter] public HashSet<(int DeviceID, int CircuitID)> VisibleCircuits { get; set; } = new();
    [Parameter] public EventCallback<HashSet<(int DeviceID, int CircuitID)>> VisibleCircuitsChanged { get; set; }

    // Circuits the user has chosen to hide — persisted; new access circuits auto-show.
    private HashSet<(int DeviceID, int CircuitID)> _hidden = new();
    private string _search = "";
    private bool _ready = false;

    private string StorageKey => $"{CurrentUser.UserId}_circuit_view_pref_v2";

    // Count of hidden circuits that are still in the access set (pruned ones don't count).
    private int _hiddenCount => _hidden.Count(h =>
        AccessCircuits.Any(a => a.DeviceID == h.DeviceID && a.CircuitID == h.CircuitID));

    private IEnumerable<(int DeviceID, int CircuitID, string Name)> FilteredList =>
        string.IsNullOrWhiteSpace(_search)
            ? AccessCircuits
            : AccessCircuits.Where(c =>
                c.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                c.CircuitID.ToString().Contains(_search));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _ready) return;
        _ready = true;

        var stored = sessionStorage.GetComponentState<CircuitViewPref>(StorageKey);
        if (stored?.HiddenCircuits?.Count > 0)
            _hidden = stored.HiddenCircuits.ToHashSet();

        await EmitAsync();
        StateHasChanged();
    }

    // When the access set changes (hardware reconnect/disconnect), prune stale hidden entries.
    protected override async Task OnParametersSetAsync()
    {
        if (!_ready) return;
        var accessKeys = AccessCircuits.Select(c => (c.DeviceID, c.CircuitID)).ToHashSet();
        if (_hidden.RemoveWhere(h => !accessKeys.Contains(h)) > 0)
            await EmitAsync();
    }

    private async Task Toggle((int DeviceID, int CircuitID) key, bool isVisible)
    {
        if (isVisible) _hidden.Remove(key);
        else _hidden.Add(key);
        Persist();
        await EmitAsync();
    }

    private async Task ShowAll()
    {
        _hidden.Clear();
        Persist();
        await EmitAsync();
    }

    private async Task HideAll()
    {
        _hidden = AccessCircuits.Select(c => (c.DeviceID, c.CircuitID)).ToHashSet();
        Persist();
        await EmitAsync();
    }

    // Emit the visible set — all access circuits minus hidden ones.
    private async Task EmitAsync()
    {
        var visible = AccessCircuits
            .Where(c => !_hidden.Contains((c.DeviceID, c.CircuitID)))
            .Select(c => (c.DeviceID, c.CircuitID))
            .ToHashSet();
        await VisibleCircuitsChanged.InvokeAsync(visible);
    }

    // Permanent=true writes through to DB via IConfigStorageService — survives logouts.
    private void Persist()
    {
        var pref = new CircuitViewPref { HiddenCircuits = _hidden.ToList() };
        sessionStorage.SetComponentState(StorageKey, pref, Permanent: true);
    }

    private class CircuitViewPref
    {
        public List<(int DeviceID, int CircuitID)> HiddenCircuits { get; set; } = new();
    }
}
```

- [ ] **Step 1.2 — Build to confirm no compile errors**

```powershell
cd "D:\WorkArea\Projects\TESTBTS\BatteryTestingSystem"
dotnet build -c Debug --nologo -clp:ErrorsOnly 2>&1 | Select-String "error CS|error RZ"
```

Expected: no `error CS` or `error RZ` lines (MSB3027 exe-lock is OK if app is running).

---

## Task 2 — Update DashboardView.razor

**File:** `Components/Pages/Home/DashboardView.razor`

Six changes in this file, applied in order:

### 2a — Replace the `filteredCircuits` field + add new state fields

- [ ] **Step 2a.1 — Replace field declarations (around line 216)**

Find:
```csharp
    private HashSet<(int DeviceID, int CircuitID)> filteredCircuits = new();
```

Replace with:
```csharp
    /// <summary>Layer 1 — circuits this user may see (from DB, immutable at runtime).</summary>
    private HashSet<(int DeviceID, int CircuitID)> _accessCircuits = new();

    /// <summary>Layer 2 — circuits the user chose to show (subset of _accessCircuits).</summary>
    private HashSet<(int DeviceID, int CircuitID)> _visibleCircuits = new();

    /// <summary>Layer 3 — live status chip filter. null = All.</summary>
    private CircuitStatus? _statusChip = null;

    private static readonly (string Label, CircuitStatus? Status)[] StatusChips =
    {
        ("All",       null),
        ("Idle",      CircuitStatus.Idle),
        ("Charge",    CircuitStatus.Charge),
        ("Discharge", CircuitStatus.Discharging),
        ("Pause",     CircuitStatus.Pause),
        ("Error",     CircuitStatus.Error),
    };
```

### 2b — Replace commented-out `LoadUserCircuitFilterAsync` with working implementation

- [ ] **Step 2b.1 — Replace the commented block (lines 361-382)**

Find:
```csharp
    // private async Task LoadUserCircuitFilterAsync()
    // {
    //     try
    //     {
    //         if (string.IsNullOrEmpty(CurrentUser.UserId)) return;

    //         var result = await CircuitAccessService.GetByUserIdAsync(CurrentUser.UserId);
           
    //         if (result.Success && result.Data?.Count > 0)
    //         {
    //             filteredCircuits = result.Data
    //                 .Select(x => (x.DeviceId, x.CircuitId))
    //                 .ToHashSet();
    //         }

    //         // if no access list configured → filteredCircuits stays empty = show all
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error loading circuit filter: {ex.Message}");
    //     }
    // }
```

Replace with:
```csharp
    private async Task LoadUserCircuitFilterAsync()
    {
        try
        {
            bool isAdmin = CurrentUser.User?.IsInRole("Administrator") ?? false;
            if (isAdmin)
            {
                // Admins see every circuit registered in the hardware manager.
                _accessCircuits = circuits
                    .Select(c => (c.Circuit.DeviceID, c.Circuit.CircuitID))
                    .ToHashSet();
                return;
            }

            if (string.IsNullOrEmpty(CurrentUser.UserId)) return;

            var result = await CircuitAccessService.GetByUserIdAsync(CurrentUser.UserId);
            if (result.Success && result.Data?.Count > 0)
            {
                // Only include circuits that are actually present in the hardware manager.
                var hwKeys = circuits.Select(c => (c.Circuit.DeviceID, c.Circuit.CircuitID)).ToHashSet();
                _accessCircuits = result.Data
                    .Select(x => (x.DeviceId, x.CircuitId))
                    .Where(k => hwKeys.Contains(k))
                    .ToHashSet();
            }
            // No assignments → _accessCircuits stays empty → nothing visible (correct: user must be assigned).
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading circuit access: {ex.Message}");
        }
    }
```

### 2c — Call `LoadUserCircuitFilterAsync` in `OnInitializedAsync`

- [ ] **Step 2c.1 — Update `OnInitializedAsync` (around line 354)**

Find:
```csharp
    protected override async Task OnInitializedAsync()
    {
        circuits = CM._devices.Values.ToList();
        CM.HardwareManagerChanged += HandleDeviceChange;
        await SyncDevicesWithDatabase();
    }
```

Replace with:
```csharp
    protected override async Task OnInitializedAsync()
    {
        circuits = CM._devices.Values.ToList();
        CM.HardwareManagerChanged += HandleDeviceChange;
        await SyncDevicesWithDatabase();
        await LoadUserCircuitFilterAsync();
        // _visibleCircuits starts as the full access set; CircuitFilter's OnAfterRenderAsync
        // will emit the persisted preference which may be a subset.
        _visibleCircuits = new HashSet<(int DeviceID, int CircuitID)>(_accessCircuits);
    }
```

### 2d — Fix the grid render rule (line 82)

- [ ] **Step 2d.1 — Replace the broken if-condition**

Find:
```razor
        if (filteredCircuits.Any() && filteredCircuits.Contains((dev.Circuit.DeviceID, dev.Circuit.CircuitID)))
```

Replace with:
```razor
        var _devKey = (dev.Circuit.DeviceID, dev.Circuit.CircuitID);
        bool _inAccess  = _accessCircuits.Contains(_devKey);
        bool _inVisible = _visibleCircuits.Count == 0 || _visibleCircuits.Contains(_devKey);
        bool _inChip    = _statusChip == null || dev.RealTime?.RealTimeRecord?.CircuitStatus == _statusChip.Value;
        if (_inAccess && _inVisible && _inChip)
```

### 2e — Fix `SyncDevicesWithDatabase` (line 455) and `SelectAll` (lines 313, 325)

- [ ] **Step 2e.1 — Fix `SyncDevicesWithDatabase`**

Find:
```csharp
            filteredCircuits = filteredCircuits
                .Where(fc => circuits.Any(c => c.Circuit.DeviceID == fc.DeviceID && c.Circuit.CircuitID == fc.CircuitID))
                .ToHashSet();
```

Replace with:
```csharp
            _accessCircuits = _accessCircuits
                .Where(fc => circuits.Any(c => c.Circuit.DeviceID == fc.DeviceID && c.Circuit.CircuitID == fc.CircuitID))
                .ToHashSet();
            _visibleCircuits = _visibleCircuits
                .Where(fc => _accessCircuits.Contains(fc))
                .ToHashSet();
```

- [ ] **Step 2e.2 — Fix `SelectAll` first filter (line 313)**

Find:
```csharp
            !filteredCircuits.Any() || filteredCircuits.Contains((c.Circuit.DeviceID, c.Circuit.CircuitID)));
```

Replace with:
```csharp
            _accessCircuits.Contains((c.Circuit.DeviceID, c.Circuit.CircuitID)) &&
                (_visibleCircuits.Count == 0 || _visibleCircuits.Contains((c.Circuit.DeviceID, c.Circuit.CircuitID))));
```

- [ ] **Step 2e.3 — Fix `SelectAll` skip-check (line 325)**

Find:
```csharp
            if (filteredCircuits.Any() && !filteredCircuits.Contains((dev.Circuit.DeviceID, dev.Circuit.CircuitID)))
```

Replace with:
```csharp
            if (!_accessCircuits.Contains((dev.Circuit.DeviceID, dev.Circuit.CircuitID)) ||
                (_visibleCircuits.Count > 0 && !_visibleCircuits.Contains((dev.Circuit.DeviceID, dev.Circuit.CircuitID))))
```

### 2f — Update toolbar: fix `className` typo, add status chips, wire new CircuitFilter

- [ ] **Step 2f.1 — Replace the toolbar div and CircuitFilter binding**

Find:
```razor
        <div className="flex items-center gap-2">
            <CircuitFilter
                AllCircuits="@circuits?.Select(c => (c.Circuit.DeviceID, c.Circuit.CircuitID, c.Circuit.DeviceName ?? $"Dev {c.Circuit.DeviceID}")).ToList()"
                           ConnectionStatus="@circuits?.ToDictionary(c => (c.Circuit.DeviceID, c.Circuit.CircuitID), c => c.IsConnected)"
                CircuitStatusMap="@circuits?.ToDictionary(c => (c.Circuit.DeviceID, c.Circuit.CircuitID), c => c.RealTime?.RealTimeRecord?.CircuitStatus ?? CircuitStatus.Idle)"
                ProgramStatusMap="@circuits?.ToDictionary(c => (c.Circuit.DeviceID, c.Circuit.CircuitID), c => c.RealTime?.RealTimeRecord?.ProgramStatus ?? ProgramRunningStatus.Stop)"
                @bind-FilteredCircuits="filteredCircuits" />

            <CardSettings @bind-Config=@CConfig />
            <ColorsSettings/>

            @* Detail panel toggle *@
            <Button variant="ButtonVariant.Outline" size="ButtonSize.Icon" Class="gap-2" OnClick="@(() => _panelOpen = !_panelOpen)"
                    title="@(_panelOpen ? "Close detail DBC" : "Open detail DBC")">
                <Blazicon Svg="@(_panelOpen ? Lucide.Minimize2 : Lucide.Layers)" class="h-4 w-4" />
            </Button>
           
        </div>
```

Replace with:
```razor
        <div class="flex items-center gap-2 flex-wrap">

            @* Layer 3 — live status chips *@
            @foreach (var chip in StatusChips)
            {
                var chipCount = chip.Status == null
                    ? _visibleCircuits.Count
                    : circuits.Count(c =>
                        _accessCircuits.Contains((c.Circuit.DeviceID, c.Circuit.CircuitID)) &&
                        (_visibleCircuits.Count == 0 || _visibleCircuits.Contains((c.Circuit.DeviceID, c.Circuit.CircuitID))) &&
                        c.RealTime?.RealTimeRecord?.CircuitStatus == chip.Status.Value);
                var isActive = _statusChip == chip.Status;
                <button class="px-2 py-0.5 text-[10px] rounded-full border transition-colors @(isActive ? "bg-primary text-primary-foreground border-primary" : "bg-background text-muted-foreground border-border hover:bg-muted")"
                        @onclick="() => _statusChip = chip.Status">
                    @chip.Label <span class="opacity-70 ml-0.5">@chipCount</span>
                </button>
            }

            @* Layer 2 — view preference filter *@
            <CircuitFilter
                AccessCircuits="@_accessCircuits.Select(k => (k.DeviceID, k.CircuitID, circuits.FirstOrDefault(c => c.Circuit.DeviceID == k.DeviceID && c.Circuit.CircuitID == k.CircuitID)?.Circuit.DeviceName ?? $"Dev {k.DeviceID}")).ToList()"
                @bind-VisibleCircuits="_visibleCircuits" />

            <CardSettings @bind-Config=@CConfig />
            <ColorsSettings/>

            @* Detail panel toggle *@
            <Button variant="ButtonVariant.Outline" size="ButtonSize.Icon" Class="gap-2" OnClick="@(() => _panelOpen = !_panelOpen)"
                    title="@(_panelOpen ? "Close detail DBC" : "Open detail DBC")">
                <Blazicon Svg="@(_panelOpen ? Lucide.Minimize2 : Lucide.Layers)" class="h-4 w-4" />
            </Button>

        </div>
```

- [ ] **Step 2f.2 — Build to confirm no compile errors**

```powershell
cd "D:\WorkArea\Projects\TESTBTS\BatteryTestingSystem"
dotnet build -c Debug --nologo -clp:ErrorsOnly 2>&1 | Select-String "error CS|error RZ"
```

Expected: no `error CS` or `error RZ` lines.

- [ ] **Step 2f.3 — Commit**

```powershell
cd "D:\WorkArea\Projects\TESTBTS\BatteryTestingSystem"
git add Components/UI/Dashboard/CircuitFilter.razor Components/Pages/Home/DashboardView.razor
git commit -m "feat: redesign circuit filter with DB-driven access, view preference, and live status chips"
```

---

## Task 3 — Wire circuit assignment into user creation (`Users.razor`)

**File:** `Components/Pages/Settings/Users.razor`

When a non-Administrator user is created, immediately open the circuit access dialog so the admin can assign circuits before closing.

- [ ] **Step 3.1 — Update `SaveUserAsync` to open circuit access dialog after create**

Find (in `SaveUserAsync`, after `UserManager.CreateAsync` success, around line 665):
```csharp
                Toast.Success("Success", $"User '{userModel.UserName}' created successfully.");
            }
            else
```

Replace with:
```csharp
                Toast.Success("Success", $"User '{userModel.UserName}' created successfully.");

                // For non-admin users, open circuit access dialog immediately so circuits
                // are assigned before the user can first log in.
                var createdUser = await UserManager.FindByNameAsync(newUser.UserName);
                bool isAdminRole = string.Equals(userModel.Role, "Administrator", StringComparison.OrdinalIgnoreCase);
                if (createdUser != null && !isAdminRole)
                {
                    await LoadUsersAsync();
                    CloseDialog();
                    await OpenCircuitAccessDialog(createdUser);
                    return;
                }
            }
            else
```

- [ ] **Step 3.2 — Build to confirm no compile errors**

```powershell
cd "D:\WorkArea\Projects\TESTBTS\BatteryTestingSystem"
dotnet build -c Debug --nologo -clp:ErrorsOnly 2>&1 | Select-String "error CS|error RZ"
```

Expected: no `error CS` or `error RZ` lines.

- [ ] **Step 3.3 — Commit**

```powershell
cd "D:\WorkArea\Projects\TESTBTS\BatteryTestingSystem"
git add Components/Pages/Settings/Users.razor
git commit -m "feat: open circuit access dialog immediately after non-admin user creation"
```

---

## Task 4 — Push

- [ ] **Step 4.1 — Push all commits**

```powershell
cd "D:\WorkArea\Projects\TESTBTS\BatteryTestingSystem"
git push origin main
```

---

## Self-Review

**Spec coverage:**
- ✅ Blank-grid bug — fixed in step 2d (requires `_accessCircuits.Contains` so empty access = nothing shows)
- ✅ Stale live status — chips computed each render, no stored state (step 2f)
- ✅ 100+ circuits scale — CircuitFilter shows only access circuits with search (Task 1)
- ✅ DB-driven access — `LoadUserCircuitFilterAsync` restored + admin bypass (step 2b, 2c)
- ✅ View preference persisted — `Persist()` with `Permanent: true` writes to DB (Task 1)
- ✅ `className` typo fixed — step 2f
- ✅ Circuit assignment at user creation — Task 3
- ✅ `SyncDevicesWithDatabase` updated — step 2e.1
- ✅ `SelectAll` updated — steps 2e.2, 2e.3

**Type consistency:**
- `_accessCircuits` and `_visibleCircuits` are both `HashSet<(int DeviceID, int CircuitID)>` — consistent with existing `filteredCircuits` type and all usages
- `CircuitFilter` parameters use `(int DeviceID, int CircuitID, string Name)` — matches the projection in step 2f
- `_hidden` in CircuitFilter is `HashSet<(int DeviceID, int CircuitID)>` — matches what `EmitAsync` reads and what `Toggle`/`HideAll`/`ShowAll` write
- `StorageKey` suffix `_v2` avoids collision with old `_C_Filter_Adv` key from previous component