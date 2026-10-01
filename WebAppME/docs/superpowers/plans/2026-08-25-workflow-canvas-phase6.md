# Workflow Canvas Phase 6 — Home Landing & Cross-Workflow Exclusivity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a channel already placed in one of a user's saved workflows unavailable to place in their other workflows (D22), and make the canvas the page a user lands on after logging in, without touching the Dashboard (D17).

**Architecture:** Cross-workflow exclusivity is a pure data lookup (deserialize every other saved layout's JSON, collect channel ids) consumed by the palette at the point of placement — enforced server-side in the placement handler, not just visually in the picker. The home-landing change is a 2-line fix to the login handler's redirect target, **not** the mechanism originally sketched in the design spec — see the correction note in Task 4.

**Tech Stack:** C# / .NET 8, Blazor Server, ASP.NET Core Razor Pages, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md` (Sections 4-5, decisions D17/D22)

## Global Constraints

- Never modify `DashboardView.razor` or `TransferDialog.razor` (standing constraint since Phase 1).
- Enforcement point for D22 is the palette/placement action, not a post-save conflict check — a
  user must never be able to place a claimed channel in a second workflow to begin with.
- Follow strict TDD for pure-logic pieces; UI/routing pieces are browser-verified, matching this
  branch's established practice.
- Full suite green, then live browser verification, then commit — one commit per task.
- Update the `.claude/` memory files before ending the session, per this repo's mandatory workflow.

## Correction from the design spec — read before starting Task 4

The design spec's Section 5 proposed swapping which `NavMenuItem` owns `Url = "/"` in
`WorkflowMenu.cs`, adding a second `@page "/"` to `WorkflowCanvasPage.razor`, and generalizing
`TabService.AddTab`'s hardcoded `Title == "Display"` check (D24). **Investigation during planning
found this mechanism does not work and is unnecessary:**

- `Components/Pages/Home/TabView.razor` already declares `@page "/"` — a second component
  declaring the same route would be an ambiguous-route conflict, not a valid change.
- Root routing in this app is driven entirely by each page's own `@page` directive
  (`DashboardView.razor` → `/Dashboard`, `WorkflowCanvasPage.razor` → `/workflows`,
  `DeviceList.razor` → `/device/list`, etc.) — `NavMenuItem.Url` only affects nav-highlighting and
  the in-memory "virtual tab" system (`TabService`/`TabViewer`), it does not drive routing at all.
- The actual post-login destination is decided by `Pages/Login.cshtml.cs`, which hardcodes
  `return Redirect("/");` in **two** places (the already-signed-in short-circuit in `OnGet`, and
  the successful-login branch of `OnPostAsync`) — with no `ReturnUrl` handling to interact with.

**The corrected, minimal change is Task 4 below: point both redirects at `/workflows` instead of
`/`.** This achieves the exact outcome already approved (canvas is what a user lands on after
login; Dashboard stays fully reachable, untouched, at `/Dashboard`) with a smaller, lower-risk
diff than the spec's original sketch, and touches no shared tab-system code at all. D24 is
therefore dropped — nothing needs generalizing since `TabService.cs` is never touched.

---

### Task 1: `GetClaimedChannelsAsync` on `IWorkflowLayoutService` (D22 data layer)

**Files:**
- Modify: `Services/Interfaces/IWorkflowLayoutService.cs`
- Modify: `Services/Implementations/Workflow/WorkflowLayoutService.cs`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowLayoutServiceTests.cs`

**Interfaces:**
- Consumes: `IWorkflowLayoutRepository.ListForUserAsync` (existing), `WorkflowGraphJson
  .TryDeserialize` (existing, already used by `LoadAsync`).
- Produces: `IWorkflowLayoutService.GetClaimedChannelsAsync(string userId, long? excludingLayoutId)
  : Task<IReadOnlyDictionary<long, string>>` — maps a channel's `EntityId` to the **name** of the
  other saved layout that already contains it. Only channels in layouts other than
  `excludingLayoutId` (the one currently open, or `null` for a new/unsaved layout) are included.

- [ ] **Step 1: Write the failing tests**

Add to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowLayoutServiceTests.cs`, in a new
section:

```csharp
    // ============================================================ GetClaimedChannelsAsync

    [Fact]
    public async Task GetClaimedChannelsAsync_MapsAChannelToTheLayoutThatHoldsIt()
    {
        var (service, _) = NewService();
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));
        await service.SaveAsync(null, "Alpha", null, graph, User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Equal("Alpha", claimed[100]);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_ExcludesTheCurrentlyOpenLayout()
    {
        // Editing "Alpha" itself must not report its own channels as claimed by someone else.
        var (service, _) = NewService();
        var graph = GraphWith(Node("c", NodeKind.Channel, 100));
        var alphaId = await service.SaveAsync(null, "Alpha", null, graph, User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: alphaId);

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_CoversEveryOtherSavedLayout()
    {
        var (service, _) = NewService();
        await service.SaveAsync(null, "Alpha", null, GraphWith(Node("c1", NodeKind.Channel, 100)), User);
        await service.SaveAsync(null, "Beta", null, GraphWith(Node("c2", NodeKind.Channel, 200)), User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Equal("Alpha", claimed[100]);
        Assert.Equal("Beta", claimed[200]);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_NeverReturnsAnotherUsersLayouts()
    {
        var (service, _) = NewService();
        await service.SaveAsync(
            null, "Theirs", null, GraphWith(Node("c", NodeKind.Channel, 100)), "user-b");

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Empty(claimed);
    }

    [Fact]
    public async Task GetClaimedChannelsAsync_IgnoresNonChannelNodes()
    {
        var (service, _) = NewService();
        await service.SaveAsync(
            null, "Alpha", null,
            GraphWith(Node("d", NodeKind.Device, 1), Node("b", NodeKind.Board, 10)), User);

        var claimed = await service.GetClaimedChannelsAsync(User, excludingLayoutId: null);

        Assert.Empty(claimed);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~GetClaimedChannelsAsync"
```

Expected: FAIL — `GetClaimedChannelsAsync` does not exist.

- [ ] **Step 3: Add the interface member and implement it**

In `Services/Interfaces/IWorkflowLayoutService.cs`, add to the interface:

```csharp
    /// <summary>Every channel EntityId already placed in one of this user's OTHER saved layouts,
    /// mapped to that layout's name (for a disabled-palette-item tooltip). Excludes
    /// excludingLayoutId (the layout currently open, if any) so editing a layout does not report
    /// its own channels as claimed by someone else.</summary>
    Task<IReadOnlyDictionary<long, string>> GetClaimedChannelsAsync(
        string userId, long? excludingLayoutId);
```

In `Services/Implementations/Workflow/WorkflowLayoutService.cs`, add:

```csharp
    public async Task<IReadOnlyDictionary<long, string>> GetClaimedChannelsAsync(
        string userId, long? excludingLayoutId)
    {
        var rows = await _repository.ListForUserAsync(userId);
        var claimed = new Dictionary<long, string>();

        foreach (var row in rows)
        {
            if (row.Id == excludingLayoutId) continue;
            if (!WorkflowGraphJson.TryDeserialize(row.LayoutJson, out var graph, out _)) continue;

            foreach (var node in graph!.Nodes)
            {
                if (node.Kind == NodeKind.Channel && node.EntityId is { } channelId)
                    claimed[channelId] = row.Name;
            }
        }

        return claimed;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj --filter "FullyQualifiedName~GetClaimedChannelsAsync"
```

Expected: PASS.

- [ ] **Step 5: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Services/Interfaces/IWorkflowLayoutService.cs Services/Implementations/Workflow/WorkflowLayoutService.cs BatteryTestingSystem.Tests/Services/Workflow/WorkflowLayoutServiceTests.cs
git commit -m "feat(workflow-canvas): GetClaimedChannelsAsync for cross-workflow exclusivity (D22)

New IWorkflowLayoutService.GetClaimedChannelsAsync maps every channel
already placed in one of a user's OTHER saved layouts to that layout's
name, excluding the currently-open layout. Reuses the same
ListForUserAsync + WorkflowGraphJson.TryDeserialize pattern LoadAsync
already relies on - no new repository method needed."
```

---

### Task 2: Palette shows claimed channels as unavailable (D22 UI)

**Files:**
- Modify: `Components/UI/WorkflowCanvas/PalettePanel.razor`

**Interfaces:**
- Consumes: nothing new from earlier tasks (this is pure rendering, wired to real data in Task 3).
- Produces: `PalettePanel.ClaimedChannels: IReadOnlyDictionary<long, string>` parameter (default
  empty).

No new C#-testable logic — this is a markup/disabled-state change, browser-verified in Task 3
alongside the enforcement it visualizes.

- [ ] **Step 1: Add the parameter and disable claimed channel chips**

In `Components/UI/WorkflowCanvas/PalettePanel.razor`, replace the channel chip loop:

```razor
                                @foreach (var channel in board.Channels)
                                {
                                    var onCanvas = IsChannelPlaced(channel.ChannelId);
                                    <button class="wf-chip @(onCanvas ? "wf-chip--on" : "")"
                                            disabled="@onCanvas"
                                            @onclick="() => PlaceOne(device, channel.ChannelId)">
                                        @channel.ChannelNumber
                                    </button>
                                }
```

with:

```razor
                                @foreach (var channel in board.Channels)
                                {
                                    var onCanvas = IsChannelPlaced(channel.ChannelId);
                                    var claimedBy = ClaimedChannels.TryGetValue(channel.ChannelId, out var owner) ? owner : null;
                                    <button class="wf-chip @(onCanvas || claimedBy is not null ? "wf-chip--on" : "")"
                                            disabled="@(onCanvas || claimedBy is not null)"
                                            title="@(claimedBy is not null ? $"Already placed in workflow '{claimedBy}'" : null)"
                                            @onclick="() => PlaceOne(device, channel.ChannelId)">
                                        @channel.ChannelNumber
                                    </button>
                                }
```

Add the parameter in `@code`:

```csharp
    [Parameter] public IReadOnlyDictionary<long, string> ClaimedChannels { get; set; } =
        new Dictionary<long, string>();
```

- [ ] **Step 2: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS (no test constructs `<PalettePanel>` directly as of Phase 5 — a new default-valued
parameter cannot break an existing call site either way, but confirm this with the run).

- [ ] **Step 3: Commit**

```bash
git add Components/UI/WorkflowCanvas/PalettePanel.razor
git commit -m "feat(workflow-canvas): palette disables channels claimed by another workflow (D22)

PalettePanel gains a ClaimedChannels parameter (channel EntityId ->
owning layout name). A claimed channel's chip renders disabled with a
tooltip naming which workflow holds it, same visual treatment as an
already-on-canvas channel. Not yet wired to real data - Task 3 does
that and adds the actual placement-time enforcement."
```

---

### Task 3: Wire real data in and enforce at placement time (D22 enforcement)

**Files:**
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`

**Interfaces:**
- Consumes: `IWorkflowLayoutService.GetClaimedChannelsAsync` (Task 1), `PalettePanel
  .ClaimedChannels` (Task 2).
- Produces: page field `_claimedChannels: IReadOnlyDictionary<long, string>`; private
  `RefreshClaimedChannelsAsync()`.

This is the task that actually makes exclusivity load-bearing — Tasks 1-2 alone only grey out the
UI, this task both feeds it real data and stops a claimed channel from being placeable at all
(even via the "All" bulk-place button, which does not go through the individual chip's `disabled`
attribute).

- [ ] **Step 1: Add the field and refresh helper**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, add near `_nodeConfig`:

```csharp
    private IReadOnlyDictionary<long, string> _claimedChannels = new Dictionary<long, string>();

    private async Task RefreshClaimedChannelsAsync()
    {
        _claimedChannels = await LayoutService.GetClaimedChannelsAsync(_userId, _currentLayoutId);
    }
```

- [ ] **Step 2: Call it on initial load and whenever the current layout changes**

In `OnInitializedAsync`, after `_userId` is set (and after the existing `_nodeConfig`
load/normalize block from Phase 5), add:

```csharp
        await RefreshClaimedChannelsAsync();
```

`HandleSave` and `HandleSaveAs` both funnel through a shared `PersistAsync(long? id, string name)`
— that is the one place to add the call for both. In `PersistAsync`, after:

```csharp
        _currentLayoutId = savedId;
        _dirty = false;
        _layouts = await LayoutService.ListAsync(_userId);
```

add:

```csharp
        await RefreshClaimedChannelsAsync();
```

In `HandleLoad`, after `_currentLayoutId = id;` is set, add the same call. In `HandleDelete`'s
branch that resets `_currentLayoutId = null;`, add it there too — the excluded-layout set is only
correct for whichever layout is now open.

- [ ] **Step 3: Enforce exclusivity in `HandlePlaceDevice`**

Replace:

```csharp
    private async Task HandlePlaceDevice(
        (TopologyDevice Device, IReadOnlyCollection<long>? Channels) placement)
    {
        var originY = WorkflowPlacement.NextFreeLaneY(_graph);

        _graph = WorkflowPlacement.PlaceDevice(
            _graph, placement.Device, placement.Channels, 0, originY);

        _dirty = true;
        RefreshSubscription();
        await InvokeAsync(StateHasChanged);
    }
```

with:

```csharp
    private async Task HandlePlaceDevice(
        (TopologyDevice Device, IReadOnlyCollection<long>? Channels) placement)
    {
        var requested = placement.Channels
            ?? placement.Device.Boards.SelectMany(b => b.Channels).Select(c => c.ChannelId).ToList();

        var allowed = requested.Where(id => !_claimedChannels.ContainsKey(id)).ToList();

        if (allowed.Count < requested.Count)
        {
            Toast.Warning(
                $"{requested.Count - allowed.Count} channel(s) skipped — already placed in another workflow.");
        }

        if (allowed.Count == 0) return;

        var originY = WorkflowPlacement.NextFreeLaneY(_graph);

        _graph = WorkflowPlacement.PlaceDevice(_graph, placement.Device, allowed, 0, originY);

        _dirty = true;
        RefreshSubscription();
        await InvokeAsync(StateHasChanged);
    }
```

- [ ] **Step 4: Pass the real data to the palette**

In the `<PalettePanel>` element, add:

```razor
                <PalettePanel Topology="_topology" Catalog="_catalog" Graph="_graph"
                              OnPlaceDevice="HandlePlaceDevice"
                              OnPlaceBattery="HandlePlaceBattery"
                              ClaimedChannels="_claimedChannels" />
```

- [ ] **Step 5: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Verify in the browser**

Stop the app, rebuild, restart, hard-reload. Using the same logged-in user:
- Save a layout ("Alpha") containing a couple of channels from `SIM_DEVICE_001_1`.
- Start a new, unsaved layout (or a different saved one) and open the Devices palette tab for the
  same device: the channels that are in Alpha show disabled, with a tooltip naming Alpha.
- Confirm clicking the disabled chip does nothing, and clicking "All" on that device places every
  channel *except* the ones Alpha already holds, with a warning toast reporting how many were
  skipped.
- Re-open Alpha itself: those same channels are NOT shown as claimed (editing a layout must never
  lock you out of your own channels).

- [ ] **Step 7: Commit**

```bash
git add Components/Pages/Workflows/WorkflowCanvasPage.razor
git commit -m "feat(workflow-canvas): enforce cross-workflow channel exclusivity (D22)

Wires GetClaimedChannelsAsync into the page (refreshed on initial
load and whenever the open layout changes via Load/Save/Save as/
Delete) and enforces it in HandlePlaceDevice - a claimed channel is
filtered out before it reaches WorkflowPlacement.PlaceDevice, not
just greyed out in the picker, so bulk-placing via 'All' cannot
sneak a claimed channel onto the canvas either. A skipped-channel
count surfaces as a warning toast.

Live-verified: a channel already in one saved layout shows disabled
with a naming tooltip in another layout's palette, 'All' places only
the unclaimed channels with a correct skip count, and re-opening the
layout that actually owns those channels does not lock the user out
of their own work."
```

---

### Task 4: Canvas as the login landing page (D17, corrected mechanism)

**Files:**
- Modify: `Pages/Login.cshtml.cs`

**Interfaces:** none — a routing/redirect change only.

Read the "Correction from the design spec" section above before starting this task — it explains
why this touches `Pages/Login.cshtml.cs` instead of `WorkflowMenu.cs`/`TabService.cs` as the
original spec sketch assumed.

- [ ] **Step 1: Change both redirect targets**

In `Pages/Login.cshtml.cs`, in `OnGet`, replace:

```csharp
            if (_signInManager.IsSignedIn(User))
            {
                return Redirect("/");
            }
```

with:

```csharp
            if (_signInManager.IsSignedIn(User))
            {
                return Redirect("/workflows");
            }
```

In `OnPostAsync`, in the `if (result.Succeeded)` branch, replace:

```csharp
                return Redirect("/");
```

with:

```csharp
                return Redirect("/workflows");
```

- [ ] **Step 2: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj
```

Expected: PASS — this file has no existing test coverage (a Razor Pages handler, no integration
test harness in this project), and the full suite catches nothing else about this change; the
browser check in Step 3 is the actual verification.

- [ ] **Step 3: Verify in the browser**

Log out (or open a private/incognito session), then log back in. Confirm:
- The browser lands on `/workflows` immediately after a successful login, not `/` or `/Dashboard`.
- Navigating directly to `/login` while already signed in also redirects to `/workflows`.
- The "Display" (Dashboard) menu entry still opens the Dashboard at `/Dashboard`, completely
  unchanged from before this task.
- `/Dashboard` is still reachable by typing it directly into the address bar.

- [ ] **Step 4: Commit**

```bash
git add Pages/Login.cshtml.cs
git commit -m "feat(workflow-canvas): canvas is the login landing page (D17)

Login.cshtml.cs's two hardcoded Redirect(\"/\") calls (already-signed-in
short-circuit, and successful login) now redirect to /workflows
instead. The Dashboard is completely untouched and remains reachable
at its own /Dashboard route via its existing menu entry - this only
changes what a fresh sign-in lands on.

Corrected from the design spec's originally sketched mechanism (a
NavMenuItem.Url swap + a second @page \"/\" + a TabService change) after
finding during planning that TabView.razor already owns @page \"/\" -
a second component claiming that route would be an ambiguous-route
conflict, not a valid change - and that root routing in this app is
driven entirely by each page's own @page directive, never by
NavMenuItem.Url. The actual post-login destination was always decided
here, in the login handler, which had no ReturnUrl handling to
interact with either. D24 (generalizing TabService.AddTab's title
check) is dropped as unnecessary - TabService.cs is never touched.

Live-verified: fresh login and an already-signed-in visit to /login
both land on /workflows; the Dashboard menu entry and its /Dashboard
route are unchanged."
```

---

## Phase 6 Completion Checklist

- [ ] `GetClaimedChannelsAsync` correctly excludes the currently-open layout and never leaks another user's layouts
- [ ] A channel already placed in one saved layout renders disabled, with a naming tooltip, in every other layout's palette for that user
- [ ] Placing "All" on a device with some claimed channels places only the unclaimed ones, with a skip-count warning toast — enforcement holds even via the bulk path, not just the individual chip
- [ ] Editing the layout that actually owns a channel never locks the user out of their own channel
- [ ] Fresh login and an already-signed-in visit to `/login` both land on `/workflows`
- [ ] The Dashboard menu entry and its `/Dashboard` route are completely unchanged
- [ ] Full suite green
- [ ] `main` has no commits from this work; `DashboardView.razor`/`TransferDialog.razor` remain byte-identical to `main`
- [ ] This is the last phase in the D17-D24 spec — confirm nothing in Sections 1-7 was left unaddressed across Phases 4-6
