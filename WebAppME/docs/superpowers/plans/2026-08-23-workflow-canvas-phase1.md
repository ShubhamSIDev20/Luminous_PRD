# Workflow Canvas Phase 1 — Isolation and Correctness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Structurally isolate the workflow-canvas branch from `main`, then fix the three correctness defects a live review found — database ids shown instead of physical channel addresses, device nodes permanently reading "offline", and missing card-footer data.

**Architecture:** Workflow persistence moves to its own `WorkflowDbContext` with a private EF migration history, so `main`'s migrations and ours never touch the same generated file. All canvas DI collapses behind a single `AddWorkflowCanvas(configuration)` call, and menu registration behind a single `WorkflowMenu.Build(...)` call, reducing the branch's footprint in `main`-owned files from ~130 lines across 7 files to ~10 lines across 4. The correctness fixes then reuse data the canvas already loads: a new `WorkflowNodeLabeller` indexes `TopologySnapshot` once for O(1) physical addressing, and device/board online counts ride the **existing telemetry path** rather than Blazor parameters.

**Tech Stack:** .NET 8, Blazor Server (`InteractiveServer`), EF Core 8.0.0 + SQLite, xUnit, bUnit 1.32.7.

**Spec:** [docs/superpowers/specs/2026-08-23-workflow-canvas-dashboard-parity-design.md](../specs/2026-08-23-workflow-canvas-dashboard-parity-design.md)

## Global Constraints

- **Branch `feat/workflow-canvas-experiment` is NEVER merged to `main`.** Every task commits here only. Never run `git merge` into `main`, never push to `main`.
- **Telemetry must never trigger a Blazor re-render.** Live values reach the DOM only as CSS custom properties or direct text writes through one batched `IJSRuntime` call per tick. Any live value rendered via a Blazor parameter is a defect.
- **Never modify `DashboardView.razor` or `TransferDialog.razor`** (spec D13 — the canvas is the executor's only consumer in this phase).
- **Do not modify `Migrations/AppDbContextModelSnapshot.cs` except to revert this branch's additions.** It is EF-generated and unmergeable.
- Channel address format is exactly `{DeviceID}-{SecondaryBoardNumber}-{ChannelNumber}` with no spaces, e.g. `1-8-2` — byte-identical to `DeviceChannel.razor:1268`.
- Node-face and dock live values are written by `workflow-canvas.js` into `data-role="..."` slots. Never render them server-side after first paint.
- Run the full suite with `dotnet test BatteryTestingSystem.Tests`. **`DashboardRenderBatcherTests` is known-flaky under parallel load** — if it alone fails, re-run it in isolation before treating it as a regression.
- **The running app locks its own build output.** Stop it before `dotnet build` / `dotnet test`, restart after. Launch via the **PowerShell tool**, never the Bash tool (Bash runs in a network-isolated sandbox unreachable from the real browser). App listens on **port 5066**.

---

## File Structure

**Created**

| File | Responsibility |
|---|---|
| `Data/WorkflowDbContext.cs` | Owns `WorkflowLayouts` only. Private migration history table. |
| `Extensions/WorkflowServiceCollectionExtensions.cs` | The branch's single DI entry point: options binding, DbContext, three services, startup migration. |
| `Components/Layout/WorkflowMenu.cs` | Builds the menu list from feature flags. Keeps that logic out of `MainLayout.razor`. |
| `Services/Implementations/Workflow/WorkflowNodeLabeller.cs` | Indexes `TopologySnapshot` once; returns physical labels and channel keys in O(1). |
| `Migrations/Workflow/*` | EF-generated, owned solely by `WorkflowDbContext`. |
| `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodeLabellerTests.cs` | |
| `BatteryTestingSystem.Tests/Components/WorkflowMenuTests.cs` | |
| `BatteryTestingSystem.Tests/Extensions/WorkflowIsolationTests.cs` | Guards the isolation invariants so they cannot silently regress. |

**Modified**

| File | Change |
|---|---|
| `Data/AppDbContext.cs` | Remove the `WorkflowLayouts` DbSet (line 51). |
| `Repositories/Implementations/WorkflowLayoutRepository.cs` | Depend on `WorkflowDbContext`. |
| `Extensions/ServiceCollectionExtensions.cs` | 6 lines → 1. |
| `Program.cs` | 5 lines → 1. |
| `Components/Layout/MainLayout.razor` | +47/−8 → ~+4/−8. |
| `Models/DTOs/Workflow/TelemetryEntry.cs` | Add `NodeRollup`; add footer fields to `ChannelTelemetry`/`TelemetryEntry`. |
| `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs` | Add `BuildRollups`; carry footer fields. |
| `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor` | Replace dead `IsOnline` with a live `data-role="online"` slot. |
| `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor` | Add the same slot. |
| `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` | Add program-status and last-update slots. |
| `Components/UI/WorkflowCanvas/PropertiesDock.razor` | Add the same two rows. |
| `Components/Pages/Workflows/WorkflowCanvasPage.razor` | Use the labeller; push rollups. |
| `wwwroot/js/workflow-canvas.js` | Accept and apply rollups; write the new slots. |

**Deleted:** `Migrations/20260822152559_AddWorkflowLayout.cs` and its `.Designer.cs`.

---

## Task 1: Isolate persistence behind its own DbContext

The whole task is one commit because the app is unbootable between the code change and the database baseline — `Program.cs:137` calls `Database.Migrate()` at startup, and a half-applied state would crash on "table already exists."

**No data is destroyed.** The schema the new migration generates is identical to the live table (SQLite silently ignored the `Device` schema annotation, so the old migration also produced a plain `WorkflowLayouts`). So the new context is *baselined* onto the existing table — its migration id is inserted into a fresh history table rather than its DDL being executed. A JSON backup is taken first as insurance only.

**Files:**
- Create: `Data/WorkflowDbContext.cs`
- Create: `Extensions/WorkflowServiceCollectionExtensions.cs`
- Create: `BatteryTestingSystem.Tests/Extensions/WorkflowIsolationTests.cs`
- Modify: `Data/AppDbContext.cs:51`
- Modify: `Repositories/Implementations/WorkflowLayoutRepository.cs:17-22`
- Modify: `Extensions/ServiceCollectionExtensions.cs:105-108`
- Modify: `Program.cs:79-81`, `Program.cs:137`
- Delete: `Migrations/20260822152559_AddWorkflowLayout.cs`, `Migrations/20260822152559_AddWorkflowLayout.Designer.cs`
- Revert: `Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Consumes: `WorkflowLayout` entity, `WorkflowFeatureOptions` (`Services/Implementations/Workflow/WorkflowFeatureOptions.cs`), existing `IWorkflowLayoutRepository` / `IWorkflowLayoutService` / `IWorkflowTopologyProvider`.
- Produces: `WorkflowDbContext`; `IServiceCollection.AddWorkflowCanvas(IConfiguration)`; `IApplicationBuilder.MigrateWorkflowCanvas()`.

- [ ] **Step 1: Back up the existing layouts**

```bash
cd "D:\WorkArea\Projects\WebAppME\Application"
python -c "
import sqlite3, json
con = sqlite3.connect('file:D:/MEWebApp/BtsAppdb.db?mode=ro', uri=True)
con.row_factory = sqlite3.Row
rows = [dict(r) for r in con.execute('select * from WorkflowLayouts')]
open('workflow_layouts_backup.json','w',encoding='utf-8').write(json.dumps(rows, indent=2, default=str))
print('backed up', len(rows), 'rows')
con.close()"
```

Expected: `backed up 4 rows`. This file is scratch — do **not** commit it (verify it is covered by `.gitignore`, or delete it after Step 12).

- [ ] **Step 2: Write the failing isolation tests**

Create `BatteryTestingSystem.Tests/Extensions/WorkflowIsolationTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Extensions;
using BatteryTestingSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BatteryTestingSystem.Tests.Extensions;

/// <summary>
/// These are not feature tests — they pin the branch-isolation invariants from spec section 3.
/// main modified every shared file this branch touches over its last 40 commits, so each of
/// these guarantees is one that would otherwise erode silently during a rebase.
/// </summary>
public class WorkflowIsolationTests
{
    private static ServiceProvider Build()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",
                ["Features:WorkflowCanvas"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWorkflowCanvas(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AppDbContext_NoLongerExposesWorkflowLayouts()
    {
        // The entire point of the split: main's snapshot must never mention our table again.
        Assert.Null(typeof(AppDbContext).GetProperty("WorkflowLayouts"));
    }

    [Fact]
    public void WorkflowDbContext_ExposesWorkflowLayouts()
    {
        Assert.NotNull(typeof(WorkflowDbContext).GetProperty("WorkflowLayouts"));
    }

    [Fact]
    public void WorkflowDbContext_UsesItsOwnMigrationsHistoryTable()
    {
        // Two contexts sharing __EFMigrationsHistory would fight over it; this is the
        // single line that keeps the two migration histories independent.
        using var scope = Build().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        var relational = ctx.GetService<IDbContextOptions>()
            .Extensions.OfType<RelationalOptionsExtension>().Single();

        Assert.Equal("__EFMigrationsHistory_Workflow", relational.MigrationsHistoryTableName);
    }

    [Fact]
    public void AddWorkflowCanvas_RegistersEveryCanvasService()
    {
        using var scope = Build().CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetService<BatteryTestingSystem.Repositories.Interfaces.IWorkflowLayoutRepository>());
        Assert.NotNull(sp.GetService<BatteryTestingSystem.Services.Interfaces.IWorkflowLayoutService>());
        Assert.NotNull(sp.GetService<BatteryTestingSystem.Services.Interfaces.IWorkflowTopologyProvider>());
    }

    [Fact]
    public void AddWorkflowCanvas_BindsTheFeatureFlags()
    {
        using var scope = Build().CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<
                BatteryTestingSystem.Services.Implementations.Workflow.WorkflowFeatureOptions>>();

        Assert.True(options.Value.WorkflowCanvas);
        Assert.True(options.Value.LegacyDashboard);   // absent flag keeps its default
    }

    [Fact]
    public void WorkflowDbContext_MapsToAnUnschemaedWorkflowLayoutsTable()
    {
        // SQLite ignores schemas and warns about them. Dropping the schema here keeps the
        // generated DDL identical to the live table, which is what makes baselining safe.
        using var scope = Build().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var entity = ctx.Model.FindEntityType(typeof(WorkflowLayout))!;

        Assert.Equal("WorkflowLayouts", entity.GetTableName());
        Assert.Null(entity.GetSchema());
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowIsolationTests"
```

Expected: FAIL — compile error, `WorkflowDbContext` and `AddWorkflowCanvas` do not exist.

- [ ] **Step 4: Create the DbContext**

Create `Data/WorkflowDbContext.cs`:

```csharp
using BatteryTestingSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BatteryTestingSystem.Data;

/// <summary>
/// Workflow-canvas persistence, deliberately separate from AppDbContext.
///
/// This exists for branch hygiene, not domain modelling. feat/workflow-canvas-experiment is a
/// permanent side branch that is never merged, and every EF migration rewrites the single
/// generated file Migrations/AppDbContextModelSnapshot.cs — which git cannot merge meaningfully.
/// Giving the canvas its own context and its own migration history removes that entire class of
/// conflict with main (spec section 3).
///
/// The schema annotation on the WorkflowLayout entity is dropped here on purpose: SQLite ignores
/// schemas and warns about them, and an unschemaed mapping keeps the generated DDL byte-identical
/// to the table that already exists in the field.
/// </summary>
public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<WorkflowLayout> WorkflowLayouts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<WorkflowLayout>().ToTable("WorkflowLayouts");
    }
}
```

- [ ] **Step 5: Create the single DI entry point**

Create `Extensions/WorkflowServiceCollectionExtensions.cs`:

```csharp
using BatteryTestingSystem.Data;
using BatteryTestingSystem.Repositories.Implementations;
using BatteryTestingSystem.Repositories.Interfaces;
using BatteryTestingSystem.Services.Implementations.Workflow;
using BatteryTestingSystem.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BatteryTestingSystem.Extensions;

/// <summary>
/// Every wire the workflow-canvas experiment needs, behind one call.
///
/// The point is footprint: ServiceCollectionExtensions.cs is the file main edits most often
/// (8 times in its last 40 commits), so this branch leaves exactly one stable line there instead
/// of a block that conflicts on every rebase. Program.cs likewise keeps one line.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowCanvas(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WorkflowFeatureOptions>(
            configuration.GetSection(WorkflowFeatureOptions.SectionName));

        // Its own history table: without this, both contexts write to __EFMigrationsHistory and
        // each would try to "revert" migrations belonging to the other.
        services.AddDbContext<WorkflowDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("DefaultConnection"),
                sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Workflow")));

        services.AddScoped<IWorkflowLayoutRepository, WorkflowLayoutRepository>();
        services.AddScoped<IWorkflowLayoutService, WorkflowLayoutService>();
        services.AddScoped<IWorkflowTopologyProvider, WorkflowTopologyProvider>();

        return services;
    }

    /// <summary>
    /// Applies the canvas's own migrations at startup, mirroring what Program.cs already does for
    /// AppDbContext. Kept here so Program.cs holds one line rather than a block.
    /// </summary>
    public static IApplicationBuilder MigrateWorkflowCanvas(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        scope.ServiceProvider.GetRequiredService<WorkflowDbContext>().Database.Migrate();
        return app;
    }
}
```

- [ ] **Step 6: Point the repository at the new context**

In `Repositories/Implementations/WorkflowLayoutRepository.cs`, replace the field and constructor (lines 17-22):

```csharp
    private readonly WorkflowDbContext _context;

    public WorkflowLayoutRepository(WorkflowDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
```

No other line in the file changes — `_context.WorkflowLayouts` resolves against the new context unchanged.

- [ ] **Step 7: Shrink the footprint in main-owned files**

`Data/AppDbContext.cs` — delete line 51 entirely:

```csharp
        public DbSet<WorkflowLayout> WorkflowLayouts { get; set; } = null!;
```

`Extensions/ServiceCollectionExtensions.cs` — replace lines 105-108 with one line, and delete the now-unused `using BatteryTestingSystem.Services.Implementations.Workflow;` on line 14:

```csharp
        services.AddWorkflowCanvas(configuration);
```

`Program.cs` — delete lines 79-81 and the `using BatteryTestingSystem.Services.Implementations.Workflow;` on line 12. Then, immediately after the existing `dbContext.Service?.Database.Migrate();` on line 137, add:

```csharp
        app.MigrateWorkflowCanvas();
```

- [ ] **Step 8: Remove the migration from AppDbContext's history**

```bash
cd "D:\WorkArea\Projects\WebAppME\Application"
rm Migrations/20260822152559_AddWorkflowLayout.cs Migrations/20260822152559_AddWorkflowLayout.Designer.cs
git checkout main -- Migrations/AppDbContextModelSnapshot.cs
```

Verify the snapshot no longer mentions the entity — expect **no output**:

```bash
grep -i workflow Migrations/AppDbContextModelSnapshot.cs
```

- [ ] **Step 9: Generate the workflow migration**

Stop the running app first (it locks build output), then:

```bash
dotnet ef migrations add InitialWorkflowSchema --context WorkflowDbContext --output-dir Migrations/Workflow
```

Confirm the generated `Up()` creates `WorkflowLayouts` with **no schema argument** and the same eleven columns as the deleted migration (`Id, Name, Description, OwnerUserId, LayoutJson, SchemaVersion, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt, IsDeleted`). If it differs, stop — baselining is only safe when the DDL matches the live table.

- [ ] **Step 10: Baseline the existing table into the new history**

This is what preserves the data: the migration is recorded as applied without executing its DDL, because the table it would create is already there.

```bash
cd "D:\WorkArea\Projects\WebAppME\Application"
python -c "
import sqlite3, glob, os, re
mig = [os.path.basename(p) for p in glob.glob('Migrations/Workflow/*_InitialWorkflowSchema.cs')
       if not p.endswith('.Designer.cs')][0]
mid = re.sub(r'\.cs$', '', mig)
con = sqlite3.connect('D:/MEWebApp/BtsAppdb.db')
con.execute('''CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory_Workflow\" (
    \"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory_Workflow\" PRIMARY KEY,
    \"ProductVersion\" TEXT NOT NULL)''')
con.execute('INSERT OR REPLACE INTO \"__EFMigrationsHistory_Workflow\" VALUES (?,?)', (mid,'8.0.0'))
con.execute('DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"=?',
            ('20260822152559_AddWorkflowLayout',))
con.commit()
print('baselined', mid)
print('layout rows still present:', con.execute('select count(*) from WorkflowLayouts').fetchone()[0])
con.close()"
```

Expected: `baselined 2026...._InitialWorkflowSchema` and `layout rows still present: 4`.

- [ ] **Step 11: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowIsolationTests"
dotnet test BatteryTestingSystem.Tests
```

Expected: isolation tests PASS; full suite PASS (see the flaky-test note in Global Constraints).

- [ ] **Step 12: Verify the footprint actually shrank, and in the browser**

```bash
git diff --stat main...HEAD -- Data/AppDbContext.cs Extensions/ServiceCollectionExtensions.cs \
  Program.cs Migrations/AppDbContextModelSnapshot.cs
```

Expected: `AppDbContextModelSnapshot.cs` absent from the output entirely; the other three showing 1-2 changed lines each.

Then start the app via the **PowerShell tool** and confirm at `http://localhost:5066/workflows` that the layout dropdown still lists **Test** and that loading it restores the saved nodes. This is the real proof the baseline worked. Delete `workflow_layouts_backup.json` once confirmed.

- [ ] **Step 13: Commit**

```bash
git add Data/WorkflowDbContext.cs Extensions/WorkflowServiceCollectionExtensions.cs \
  BatteryTestingSystem.Tests/Extensions/WorkflowIsolationTests.cs \
  Data/AppDbContext.cs Extensions/ServiceCollectionExtensions.cs Program.cs \
  Repositories/Implementations/WorkflowLayoutRepository.cs \
  Migrations/
git commit -m "refactor(workflow-canvas): isolate persistence behind its own DbContext

main modified every shared file this branch touches over its last 40
commits, and AppDbContextModelSnapshot.cs is EF-generated and unmergeable.
WorkflowDbContext takes its own migration history table, removing that
conflict class entirely. DI and startup migration collapse behind
AddWorkflowCanvas/MigrateWorkflowCanvas so main's hottest file keeps one
stable line.

Existing layouts are preserved by baselining rather than recreating: the
generated DDL is identical to the live table, so the migration id is
recorded as applied without running it.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Move menu construction out of MainLayout

**Files:**
- Create: `Components/Layout/WorkflowMenu.cs`
- Create: `BatteryTestingSystem.Tests/Components/WorkflowMenuTests.cs`
- Modify: `Components/Layout/MainLayout.razor`

**Interfaces:**
- Consumes: `WorkflowFeatureOptions`, `NavMenuItem`, `DashboardView`, `WorkflowCanvasPage`.
- Produces: `WorkflowMenu.Build(WorkflowFeatureOptions features, IEnumerable<NavMenuItem> alwaysOn) → List<NavMenuItem>`.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Components/WorkflowMenuTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Menu order is load-bearing beyond appearance: Navbar.razor navigates the brand-logo click to
/// MenuItems.FirstOrDefault(), so whichever entry is first is also the app's home target.
/// </summary>
public class WorkflowMenuTests
{
    private static readonly List<NavMenuItem> AlwaysOn = new()
    {
        new NavMenuItem { Title = "Circuits", Url = "/device/list" },
    };

    [Fact]
    public void DefaultFlags_ShowLegacyDashboardAndHideTheCanvas()
    {
        var items = WorkflowMenu.Build(new WorkflowFeatureOptions(), AlwaysOn);

        Assert.Equal("Display", items[0].Title);
        Assert.DoesNotContain(items, i => i.Title == "Workflows");
    }

    [Fact]
    public void CanvasEnabled_AddsWorkflowsAfterDisplay()
    {
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true };

        var items = WorkflowMenu.Build(features, AlwaysOn);

        Assert.Equal(new[] { "Display", "Workflows", "Circuits" }, items.Select(i => i.Title));
    }

    [Fact]
    public void LegacyDashboardHidden_MakesWorkflowsTheHomeTarget()
    {
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true, LegacyDashboard = false };

        var items = WorkflowMenu.Build(features, AlwaysOn);

        Assert.Equal("Workflows", items[0].Title);
    }

    [Fact]
    public void BothDisabled_LeavesOnlyTheAlwaysOnEntries()
    {
        var features = new WorkflowFeatureOptions { LegacyDashboard = false };

        var items = WorkflowMenu.Build(features, AlwaysOn);

        Assert.Equal(new[] { "Circuits" }, items.Select(i => i.Title));
    }

    [Fact]
    public void TheWorkflowsEntryIsKeptAliveAcrossTabSwitches()
    {
        // The canvas holds JS-side viewport and selection state; letting the tab dispose would
        // silently reset the operator's view every time they switch away.
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true };

        var workflows = WorkflowMenu.Build(features, AlwaysOn).Single(i => i.Title == "Workflows");

        Assert.True(workflows.keepAlive);
        Assert.True(workflows.Unique);
    }

    [Fact]
    public void Build_DoesNotMutateTheCallersAlwaysOnList()
    {
        // MainLayout holds _alwaysOnMenuItems in a static field; appending to it would make the
        // menu grow on every construction.
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true };
        var alwaysOn = new List<NavMenuItem> { new() { Title = "Circuits", Url = "/device/list" } };

        WorkflowMenu.Build(features, alwaysOn);
        WorkflowMenu.Build(features, alwaysOn);

        Assert.Single(alwaysOn);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowMenuTests"
```

Expected: FAIL — compile error, `WorkflowMenu` does not exist.

- [ ] **Step 3: Create the helper**

Create `Components/Layout/WorkflowMenu.cs`:

```csharp
using BatteryTestingSystem.Components.Pages.Workflows;
using BatteryTestingSystem.Components.UI.Dashboard;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Blazicons;

namespace BatteryTestingSystem.Components.Layout;

/// <summary>
/// Builds the navigation list for the workflow-canvas experiment.
///
/// This lives outside MainLayout.razor purely to keep this branch's diff against a main-owned
/// file small — main edited MainLayout five times in its last 40 commits, and a 47-line insertion
/// there conflicts on every rebase (spec section 3).
///
/// "Display" is gated on Features:LegacyDashboard so the old dashboard can be hidden without
/// being deleted — it stays reachable directly at /Dashboard. "Workflows" is gated on
/// Features:WorkflowCanvas so the whole experiment switches off with one config flag.
/// </summary>
public static class WorkflowMenu
{
    public static List<NavMenuItem> Build(
        WorkflowFeatureOptions features, IEnumerable<NavMenuItem> alwaysOn)
    {
        var items = new List<NavMenuItem>();

        if (features.LegacyDashboard)
        {
            items.Add(new NavMenuItem
            {
                Title = "Display",
                Url = "/",
                Icon = Lucide.Fullscreen,
                ComponentType = typeof(DashboardView),
                Unique = true,
            });
        }

        if (features.WorkflowCanvas)
        {
            items.Add(new NavMenuItem
            {
                Title = "Workflows",
                Url = "/workflows",
                Icon = Lucide.Workflow,
                ComponentType = typeof(WorkflowCanvasPage),
                Unique = true,
                keepAlive = true,
            });
        }

        items.AddRange(alwaysOn);
        return items;
    }
}
```

If `NavMenuItem` or `DashboardView` resolve to different namespaces, correct the `using` lines — do not move the types.

- [ ] **Step 4: Reduce MainLayout to a single call**

In `Components/Layout/MainLayout.razor`, delete the entire `BuildMenuItems()` method and the `@using BatteryTestingSystem.Components.Pages.Workflows` line. Keep `@inject IOptions<WorkflowFeatureOptions> Features` and the `@using` lines it needs. Replace the `BuildMenuItems();` call in `OnInitialized` with:

```csharp
        _menuItems = WorkflowMenu.Build(Features.Value, _alwaysOnMenuItems);
```

Leave the `_menuItems` / `_alwaysOnMenuItems` field split exactly as it is — conditionally including "Display" requires it.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowMenuTests"
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 6: Verify the diff shrank and the menu still works**

```bash
git diff --stat main...HEAD -- Components/Layout/MainLayout.razor
```

Expected: roughly `+5 -8`, down from `+47 -8`.

Start the app and confirm both **Workflows** and **Display** appear in the navbar, and that clicking the **BMS Monitor** brand logo navigates to the first entry.

- [ ] **Step 7: Commit**

```bash
git add Components/Layout/WorkflowMenu.cs Components/Layout/MainLayout.razor \
  BatteryTestingSystem.Tests/Components/WorkflowMenuTests.cs
git commit -m "refactor(workflow-canvas): move menu construction out of MainLayout

Cuts this branch's diff against a file main edits often from +47/-8 to
roughly +5/-8, and makes the flag combinations directly testable -
including that hiding the legacy dashboard promotes Workflows to the
brand-logo home target via Navbar's MenuItems.FirstOrDefault().

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Physical channel addressing

Replaces database primary keys in node labels (`Channel 579`, `Board 76`) with the physical address the operator reads everywhere else (`1-8-2`, `Board 8`). All required data is already in the loaded `TopologySnapshot` and was simply never consulted.

The labeller also subsumes `WorkflowCanvasPage._channelKeys` — the telemetry lookup builds the identical channelId → (device, board, channel) index — so the page keeps one index instead of two.

**Files:**
- Create: `Services/Implementations/Workflow/WorkflowNodeLabeller.cs`
- Create: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodeLabellerTests.cs`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor` (`LabelFor`, `RefreshChannelKeyLookup`)

**Interfaces:**
- Consumes: `TopologySnapshot`, `TopologyDevice`, `TopologyBoard`, `TopologyChannel` (`Models/DTOs/Workflow/TopologySnapshot.cs`); `WorkflowNode`, `NodeKind`.
- Produces:
  - `new WorkflowNodeLabeller(TopologySnapshot topology)`
  - `string Label(WorkflowNode node)`
  - `bool TryGetChannelKey(long channelId, out (int DeviceId, int BoardNumber, int ChannelNumber) key)`
  - `IReadOnlyCollection<long> ChannelIdsForNodeEntity(NodeKind kind, long entityId)` — used by Task 4's rollups.

- [ ] **Step 1: Write the failing test**

Create `BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodeLabellerTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using BatteryTestingSystem.Models.DTOs.Workflow;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Services.Workflow;

/// <summary>
/// Node labels must read as the physical address an operator sees on the dashboard
/// (DeviceChannel.razor:1268), not the database primary key. Showing "Channel 579" for what the
/// rest of the app calls "1-8-2" makes the two surfaces impossible to cross-reference.
/// </summary>
public class WorkflowNodeLabellerTests
{
    private static TopologySnapshot Topology() => new(new List<TopologyDevice>
    {
        new(1, "SIM_DEVICE_001_1", new List<TopologyBoard>
        {
            new(76, 8, new List<TopologyChannel> { new(579, 2), new(580, 3) }),
            new(70, 1, new List<TopologyChannel> { new(500, 1) }),
        }),
        new(2, "SIM_DEVICE_002_1", new List<TopologyBoard>
        {
            new(90, 4, new List<TopologyChannel> { new(700, 5) }),
        }),
    });

    private static WorkflowNode Node(NodeKind kind, long? entityId) =>
        new($"{kind}-{entityId}", kind, entityId, 0, 0, null);

    [Fact]
    public void ChannelLabelIsTheThreePartPhysicalAddress()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("1-8-2", labeller.Label(Node(NodeKind.Channel, 579)));
    }

    [Fact]
    public void ChannelLabelDisambiguatesAcrossDevices()
    {
        // Channel number 5 on device 2 must not be confusable with any channel on device 1.
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("2-4-5", labeller.Label(Node(NodeKind.Channel, 700)));
    }

    [Fact]
    public void BoardLabelUsesTheBoardNumberNotItsPrimaryKey()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Board 8", labeller.Label(Node(NodeKind.Board, 76)));
    }

    [Fact]
    public void DeviceLabelIsTheDeviceName()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("SIM_DEVICE_001_1", labeller.Label(Node(NodeKind.Device, 1)));
    }

    [Fact]
    public void BatteryLabelIsAConstant()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Battery", labeller.Label(Node(NodeKind.Battery, 9)));
    }

    [Fact]
    public void UnresolvableIdsFallBackVisiblyRatherThanThrowing()
    {
        // A stale node points at deleted hardware. It must still render, and must look obviously
        // unresolved rather than silently borrowing another channel's address.
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Channel ?", labeller.Label(Node(NodeKind.Channel, 99999)));
        Assert.Equal("Board ?", labeller.Label(Node(NodeKind.Board, 99999)));
        Assert.Equal("Device ?", labeller.Label(Node(NodeKind.Device, 99999)));
    }

    [Fact]
    public void ANodeWithNoEntityIdFallsBackToo()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal("Channel ?", labeller.Label(Node(NodeKind.Channel, null)));
    }

    [Fact]
    public void TryGetChannelKeyReturnsTheCircuitMatchingTuple()
    {
        // Circuits are matched by (DeviceID, SecondaryBoardNumber, ChannelNumber), never by the
        // database Channel.Id — this lookup is what bridges the two.
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.True(labeller.TryGetChannelKey(579, out var key));
        Assert.Equal((1, 8, 2), key);
    }

    [Fact]
    public void TryGetChannelKeyFailsClosedForUnknownIds()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.False(labeller.TryGetChannelKey(99999, out _));
    }

    [Fact]
    public void ChannelIdsForNodeEntityRollsUpABoard()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal(new[] { 579L, 580L },
            labeller.ChannelIdsForNodeEntity(NodeKind.Board, 76).OrderBy(i => i));
    }

    [Fact]
    public void ChannelIdsForNodeEntityRollsUpEveryBoardOnADevice()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Equal(new[] { 500L, 579L, 580L },
            labeller.ChannelIdsForNodeEntity(NodeKind.Device, 1).OrderBy(i => i));
    }

    [Fact]
    public void ChannelIdsForNodeEntityIsEmptyForUnknownEntities()
    {
        var labeller = new WorkflowNodeLabeller(Topology());

        Assert.Empty(labeller.ChannelIdsForNodeEntity(NodeKind.Device, 99999));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowNodeLabellerTests"
```

Expected: FAIL — compile error, `WorkflowNodeLabeller` does not exist.

- [ ] **Step 3: Implement the labeller**

Create `Services/Implementations/Workflow/WorkflowNodeLabeller.cs`:

```csharp
using BatteryTestingSystem.Models.DTOs.Workflow;

namespace BatteryTestingSystem.Services.Implementations.Workflow;

/// <summary>
/// Turns a node's soft EntityId reference into the physical address the operator already reads on
/// the dashboard: "1-8-2" for a channel, "Board 8" for a board.
///
/// The canvas previously rendered EntityId directly, which is the DATABASE PRIMARY KEY — so a
/// channel the rest of the app calls 1-8-2 appeared as "Channel 579". The correct values were
/// always present in the loaded TopologySnapshot and simply never looked up.
///
/// Indexes are built once per snapshot rather than scanned per node: at 640 channels a linear
/// scan per label would be ~450k comparisons on every structural re-render.
///
/// Unresolvable ids yield a visible "?" rather than throwing. Stale nodes are a supported state
/// (their hardware was deleted), and they must still render.
/// </summary>
public sealed class WorkflowNodeLabeller
{
    private readonly Dictionary<int, string> _deviceNames = new();
    private readonly Dictionary<long, int> _boardNumbers = new();
    private readonly Dictionary<long, (int DeviceId, int BoardNumber, int ChannelNumber)> _channelKeys = new();
    private readonly Dictionary<long, List<long>> _channelsByBoardId = new();
    private readonly Dictionary<int, List<long>> _channelsByDeviceId = new();

    public WorkflowNodeLabeller(TopologySnapshot topology)
    {
        foreach (var device in topology.Devices)
        {
            _deviceNames[device.DeviceId] = device.DeviceName;
            var deviceChannels = _channelsByDeviceId[device.DeviceId] = new List<long>();

            foreach (var board in device.Boards)
            {
                _boardNumbers[board.BoardId] = board.BoardNumber;
                var boardChannels = _channelsByBoardId[board.BoardId] = new List<long>();

                foreach (var channel in board.Channels)
                {
                    _channelKeys[channel.ChannelId] =
                        (device.DeviceId, board.BoardNumber, channel.ChannelNumber);
                    boardChannels.Add(channel.ChannelId);
                    deviceChannels.Add(channel.ChannelId);
                }
            }
        }
    }

    public string Label(WorkflowNode node) => node.Kind switch
    {
        NodeKind.Device => node.EntityId is { } d && _deviceNames.TryGetValue((int)d, out var name)
            ? name : "Device ?",

        NodeKind.Board => node.EntityId is { } b && _boardNumbers.TryGetValue(b, out var number)
            ? $"Board {number}" : "Board ?",

        NodeKind.Channel => node.EntityId is { } c && _channelKeys.TryGetValue(c, out var key)
            ? $"{key.DeviceId}-{key.BoardNumber}-{key.ChannelNumber}" : "Channel ?",

        NodeKind.Battery => "Battery",
        _ => node.Id,
    };

    /// <summary>ChannelId to the (DeviceID, SecondaryBoardNumber, ChannelNumber) tuple that
    /// ChannelManager matches circuits by. Database ids never match a circuit.</summary>
    public bool TryGetChannelKey(
        long channelId, out (int DeviceId, int BoardNumber, int ChannelNumber) key) =>
        _channelKeys.TryGetValue(channelId, out key);

    /// <summary>Every channel id beneath a device or board node, for online rollups.</summary>
    public IReadOnlyCollection<long> ChannelIdsForNodeEntity(NodeKind kind, long entityId) => kind switch
    {
        NodeKind.Device => _channelsByDeviceId.TryGetValue((int)entityId, out var d)
            ? d : Array.Empty<long>(),
        NodeKind.Board => _channelsByBoardId.TryGetValue(entityId, out var b)
            ? b : Array.Empty<long>(),
        _ => Array.Empty<long>(),
    };
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowNodeLabellerTests"
```

Expected: PASS (12 tests).

- [ ] **Step 5: Use the labeller from the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`:

Add the field next to `_channelKeys`:

```csharp
    private WorkflowNodeLabeller _labeller = new(TopologySnapshot.Empty);
```

Replace the whole `LabelFor` method with:

```csharp
    private string LabelFor(WorkflowNode node) => _labeller.Label(node);
```

Replace the body of `RefreshChannelKeyLookup` so the labeller is the single index — delete the LINQ that built `_channelKeys` and the `_channelKeys` field itself:

```csharp
    /// <summary>Rebuilds the topology index the labels and telemetry both read. One index, not
    /// two: the labeller already holds channelId -> (device, board, channel).</summary>
    private void RefreshChannelKeyLookup() => _labeller = new WorkflowNodeLabeller(_topology);
```

In `PushTelemetryAsync`, replace the `_channelKeys.TryGetValue(channelId, out var key)` call with:

```csharp
            if (!_labeller.TryGetChannelKey(channelId, out var key)) continue;
```

- [ ] **Step 6: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 7: Verify in the browser**

Start the app, open `http://localhost:5066/workflows`, and place one channel from `SIM_DEVICE_002_1` board 8, channel 2. Confirm the node title reads **`2-8-2`** (not `Channel 579`) and the board node reads **`Board 8`** (not `Board 76`). Confirm live telemetry still updates — the labeller now backs the telemetry lookup, so a mistake here shows as values freezing.

- [ ] **Step 8: Commit**

```bash
git add Services/Implementations/Workflow/WorkflowNodeLabeller.cs \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowNodeLabellerTests.cs \
  Components/Pages/Workflows/WorkflowCanvasPage.razor
git commit -m "fix(workflow-canvas): label nodes by physical address, not database id

Nodes rendered EntityId directly, so a channel the rest of the app calls
1-8-2 appeared as 'Channel 579' - impossible to cross-reference against
the dashboard. The physical values were already in the loaded
TopologySnapshot and never consulted.

WorkflowNodeLabeller indexes the snapshot once and also subsumes the
page's separate _channelKeys telemetry lookup, which held the identical
mapping.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Live online counts on device and board nodes

`DeviceNode.razor` declares an `IsOnline` parameter that `CanvasSurface.razor:24-26` never passes, so every device has read "offline" since the feature shipped.

**The spec's phrasing — "begin passing the `IsOnline` parameter" — is wrong, and this task deliberately does not do that.** Online state is *live data*. Routing it through a Blazor parameter would re-render all 730 nodes on every hardware event and destroy the no-re-render guarantee. It instead rides the existing telemetry path: a static initial value in markup, then JS text writes into a `data-role` slot — exactly the pattern `ChannelNode`'s `InitialStatusText` + `data-role="status"` already uses.

A count (`6 / 8 online`) is used rather than a boolean: it tells the operator *how much* of a device is alive, which a binary cannot.

**Files:**
- Modify: `Models/DTOs/Workflow/TelemetryEntry.cs`
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Modify: `Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor`
- Modify: `Components/UI/WorkflowCanvas/Nodes/BoardNode.razor`
- Modify: `Components/UI/WorkflowCanvas/CanvasSurface.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Modify: `wwwroot/js/workflow-canvas.js`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `WorkflowNodeLabeller.ChannelIdsForNodeEntity` (Task 3), `ChannelTelemetry`, `WorkflowGraph`.
- Produces:
  - `record NodeRollup(string NodeId, string OnlineText)`
  - `WorkflowTelemetryBridge.BuildRollups(WorkflowGraph graph, IReadOnlyDictionary<long, ChannelTelemetry> telemetry, Func<NodeKind, long, IReadOnlyCollection<long>> childChannelIds) → IReadOnlyList<NodeRollup>`
  - JS: `workflowCanvas.applyTelemetry(host, entries, rollups)`

- [ ] **Step 1: Write the failing test**

Append to `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`, inside the existing class:

```csharp
    // ============================================================ BuildRollups

    private static IReadOnlyCollection<long> Children(NodeKind kind, long entityId) =>
        (kind, entityId) switch
        {
            (NodeKind.Board, 76) => new[] { 100L, 101L, 102L },
            (NodeKind.Device, 1) => new[] { 100L, 101L, 102L, 103L },
            _ => System.Array.Empty<long>(),
        };

    [Fact]
    public void BuildRollups_CountsOnlyChannelsThatAreNotOffline()
    {
        var graph = GraphWith(new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
            [101] = new(CircuitStatus.Offline, 0, 0, 0),
            [102] = new(CircuitStatus.Pause, 0, 0, 0),
        };

        var rollups = WorkflowTelemetryBridge.BuildRollups(graph, data, Children);

        Assert.Equal("2 / 3 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_TreatsAChannelWithNoTelemetryAsOffline()
    {
        // A placed channel whose device never registered has no reading at all. Counting it as
        // online would report hardware that is not there.
        var graph = GraphWith(new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0),
        };

        var rollups = WorkflowTelemetryBridge.BuildRollups(graph, data, Children);

        Assert.Equal("1 / 4 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_EmitsOneEntryPerDeviceAndBoardNode()
    {
        var graph = GraphWith(
            new WorkflowNode("dev-1", NodeKind.Device, 1, 0, 0, null),
            new WorkflowNode("brd-76", NodeKind.Board, 76, 0, 0, null),
            Channel(100));

        var rollups = WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children);

        Assert.Equal(new[] { "brd-76", "dev-1" }, rollups.Select(r => r.NodeId).OrderBy(i => i));
    }

    [Fact]
    public void BuildRollups_ReportsZeroOfZeroForAnEntityWithNoChannels()
    {
        // A device whose channels were all removed from the canvas, or a stale node.
        var graph = GraphWith(new WorkflowNode("dev-9", NodeKind.Device, 9, 0, 0, null));

        var rollups = WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children);

        Assert.Equal("0 / 0 online", Assert.Single(rollups).OnlineText);
    }

    [Fact]
    public void BuildRollups_SkipsNodesWithNoEntityId()
    {
        var graph = GraphWith(new WorkflowNode("dev-x", NodeKind.Device, null, 0, 0, null));

        Assert.Empty(WorkflowTelemetryBridge.BuildRollups(
            graph, new Dictionary<long, ChannelTelemetry>(), Children));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: FAIL — `BuildRollups` does not exist.

- [ ] **Step 3: Add the record and the builder**

Append to `Models/DTOs/Workflow/TelemetryEntry.cs`:

```csharp
/// <summary>
/// A device- or board-level summary line, written to the DOM the same way channel telemetry is —
/// as a text write into a data-role slot, never a Blazor parameter. Online state changes whenever
/// hardware does; rendering it server-side would re-render every node on the canvas per event.
/// </summary>
public record NodeRollup(string NodeId, string OnlineText);
```

Append to `WorkflowTelemetryBridge`:

```csharp
    /// <summary>
    /// Per-device and per-board "N / M online" summaries.
    ///
    /// A channel counts as online when it has a reading AND that reading is not Offline. A
    /// missing reading counts as offline: a placed channel whose device never registered has no
    /// entry at all, and reporting it as online would claim hardware that is not there.
    ///
    /// childChannelIds is injected rather than derived from graph edges so the count reflects the
    /// real topology, not what the operator happens to have placed — a device with two of eight
    /// channels on the canvas is still a device with eight channels.
    /// </summary>
    public static IReadOnlyList<NodeRollup> BuildRollups(
        WorkflowGraph graph,
        IReadOnlyDictionary<long, ChannelTelemetry> telemetry,
        Func<NodeKind, long, IReadOnlyCollection<long>> childChannelIds)
    {
        var rollups = new List<NodeRollup>();

        foreach (var node in graph.Nodes)
        {
            if (node.Kind is not (NodeKind.Device or NodeKind.Board)) continue;
            if (node.EntityId is not { } entityId) continue;

            var children = childChannelIds(node.Kind, entityId);
            var online = children.Count(id =>
                telemetry.TryGetValue(id, out var reading) && reading.Status != CircuitStatus.Offline);

            rollups.Add(new NodeRollup(node.Id, $"{online} / {children.Count} online"));
        }

        return rollups;
    }
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: PASS.

- [ ] **Step 5: Give the nodes a live slot**

`Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor` — replace the markup block and drop the dead `IsOnline` parameter:

```razor
<CanvasNode Node="@Node" Title="@Title" IsSelected="@IsSelected" IsStale="@IsStale"
            ShowInPort="false" ShowOutPort="true">
    <div class="flex items-center justify-between">
        <span class="opacity-60">Device</span>
        <span data-role="online">-- online</span>
    </div>
    <div class="opacity-60">@BoardCount board(s)</div>
</CanvasNode>

@code {
    [Parameter, EditorRequired] public WorkflowNode Node { get; set; } = default!;
    [Parameter] public string Title { get; set; } = "Device";
    [Parameter] public bool IsSelected { get; set; }
    [Parameter] public bool IsStale { get; set; }
    [Parameter] public int BoardCount { get; set; }
}
```

`Components/UI/WorkflowCanvas/Nodes/BoardNode.razor` — add the same slot above the collapse button:

```razor
    <div class="flex items-center justify-between">
        <span class="opacity-60">@ChannelCount channel(s) placed</span>
        <span data-role="online">-- online</span>
    </div>
```

Delete the old `<div class="opacity-60">@ChannelCount channel(s) placed</div>` line it replaces. `CanvasSurface.razor` needs no change — it never passed `IsOnline`, and now no longer needs to.

- [ ] **Step 6: Apply rollups in JS**

In `wwwroot/js/workflow-canvas.js`, change the `applyTelemetry` signature and append the rollup loop just before its closing brace:

```javascript
        applyTelemetry(host, entries, rollups) {
```

```javascript
            // Device and board summaries. Same contract as channel telemetry: a text write into
            // an existing slot, never a structural change.
            if (rollups) {
                for (const r of rollups) {
                    const node = s.world.querySelector(`.wf-node[data-node-id="${r.nodeId}"]`);
                    if (node) setText(node, "online", r.onlineText);
                }
            }
```

- [ ] **Step 7: Push rollups from the page**

In `Components/Pages/Workflows/WorkflowCanvasPage.razor`, inside `PushTelemetryAsync`, replace the final two statements:

```csharp
        var entries = WorkflowTelemetryBridge.BuildEntries(
            _graph, readings, ErrorMessages.Errors, ErrorMessages.Messages);
        var rollups = WorkflowTelemetryBridge.BuildRollups(
            _graph, readings, _labeller.ChannelIdsForNodeEntity);

        if (entries.Count == 0 && rollups.Count == 0) return;

        // ONE interop call for the whole batch, and no StateHasChanged anywhere in this method.
        await JS.InvokeVoidAsync("workflowCanvas.applyTelemetry", _canvasHost, entries, rollups);
```

Note the guard changed: previously an empty `entries` returned early, which would now also suppress rollups.

- [ ] **Step 8: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 9: Verify in the browser, including the no-re-render guarantee**

Start the app and the simulator (`python run_sim.py -d 10 -n 64` from `HardwareSimulator`, via the **PowerShell tool**). Place a full device with the palette's **All** button. Confirm the device node reads a real count such as `64 / 64 online` rather than `offline`, and each board reads its own count.

Then confirm telemetry still causes no structural churn — paste into the browser console and wait ~5 seconds:

```javascript
let n = 0;
new MutationObserver(ms => ms.forEach(m => { if (m.type === 'childList') n++; }))
  .observe(document.querySelector('.wf-world'), {childList: true, subtree: true});
setTimeout(() => console.log('childList mutations:', n), 5000);
```

Expected: `childList mutations: 0`. Anything above zero means a live value leaked into a Blazor parameter — fix before committing.

- [ ] **Step 10: Commit**

```bash
git add Models/DTOs/Workflow/TelemetryEntry.cs \
  Services/Implementations/Workflow/WorkflowTelemetryBridge.cs \
  Components/UI/WorkflowCanvas/Nodes/DeviceNode.razor \
  Components/UI/WorkflowCanvas/Nodes/BoardNode.razor \
  Components/Pages/Workflows/WorkflowCanvasPage.razor \
  wwwroot/js/workflow-canvas.js \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "fix(workflow-canvas): show live online counts on device and board nodes

DeviceNode declared an IsOnline parameter that CanvasSurface never
passed, so every device has read 'offline' since the feature shipped -
the same unwired-parameter class of bug as SelectedNodeId.

Fixed via the telemetry path rather than by wiring the parameter: online
state is live data, and a Blazor parameter would re-render all 730 nodes
on every hardware event. A count rather than a boolean, because 6/8
tells an operator something 'online' cannot.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Card-footer parity on channel nodes

Adds what the dashboard card's footer shows (`DeviceChannel.razor:1301-1327`): program status and the last-update timestamp. The timestamp is load-bearing — without a clock, a frozen reading is indistinguishable from a live one.

The system-error **reset button** is deliberately out of scope: it is an action, and belongs with Phase 3's action work.

**Files:**
- Modify: `Models/DTOs/Workflow/TelemetryEntry.cs`
- Modify: `Services/Implementations/Workflow/WorkflowTelemetryBridge.cs`
- Modify: `Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor`
- Modify: `Components/UI/WorkflowCanvas/PropertiesDock.razor`
- Modify: `Components/Pages/Workflows/WorkflowCanvasPage.razor`
- Modify: `wwwroot/js/workflow-canvas.js`
- Test: `BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs`

**Interfaces:**
- Consumes: `ProgramRunningStatus` (`Models/Enums/CircuitEnums.cs`), `RealTimeRecordDto.TimeStamp` / `.ProgramStatus`.
- Produces: two extra `ChannelTelemetry` parameters (`ProgramStatus`, `TimeStamp`) and two extra `TelemetryEntry` fields (`ProgramStatusText`, `LastUpdateText`).

- [ ] **Step 1: Write the failing test**

Append to `WorkflowTelemetryBridgeTests`:

```csharp
    // ============================================================ Footer parity

    [Fact]
    public void BuildEntries_ReportsProgramStatusUsingTheDashboardsWording()
    {
        // DeviceChannel.GetProgramStatusText maps Running -> "Running" and everything else to
        // "Stop". The canvas must not invent different words for the same states.
        var graph = GraphWith(Channel(100));
        var running = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0,
                        ProgramStatus: ProgramRunningStatus.Running),
        };
        var stopped = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Idle, 0, 0, 0, ProgramStatus: ProgramRunningStatus.Stop),
        };

        Assert.Equal("Running", WorkflowTelemetryBridge.BuildEntries(graph, running)[0].ProgramStatusText);
        Assert.Equal("Stop", WorkflowTelemetryBridge.BuildEntries(graph, stopped)[0].ProgramStatusText);
    }

    [Fact]
    public void BuildEntries_FormatsTheLastUpdateAsAWallClock()
    {
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Charge, 0, 0, 0,
                        TimeStamp: new DateTime(2026, 8, 23, 14, 5, 9)),
        };

        Assert.Equal("14:05:09", WorkflowTelemetryBridge.BuildEntries(graph, data)[0].LastUpdateText);
    }

    [Fact]
    public void BuildEntries_ShowsNoClockWhenNoReadingHasEverArrived()
    {
        // default(DateTime) means "never updated". Rendering it as 00:00:00 would look like a
        // real reading taken at midnight.
        var graph = GraphWith(Channel(100));
        var data = new Dictionary<long, ChannelTelemetry>
        {
            [100] = new(CircuitStatus.Offline, 0, 0, 0),
        };

        Assert.Equal("--", WorkflowTelemetryBridge.BuildEntries(graph, data)[0].LastUpdateText);
    }
```

Add `using BatteryTestingSystem.Models.Enums;` if it is not already imported.

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: FAIL — `ProgramStatus`, `TimeStamp`, `ProgramStatusText`, `LastUpdateText` do not exist.

- [ ] **Step 3: Extend the records**

In `Models/DTOs/Workflow/TelemetryEntry.cs`, append two parameters to `ChannelTelemetry` (after `SystemErrorId`, keeping defaults so existing positional calls still compile):

```csharp
    ProgramRunningStatus ProgramStatus = ProgramRunningStatus.Stop,
    DateTime TimeStamp = default);
```

Append two fields to `TelemetryEntry` (after `ErrorText`):

```csharp
    string ProgramStatusText,
    string LastUpdateText);
```

- [ ] **Step 4: Populate them in the bridge**

In `WorkflowTelemetryBridge.BuildEntries`, add to the `entries.Add(new TelemetryEntry(...))` call, after `ErrorText:`:

```csharp
                ProgramStatusText: reading.ProgramStatus == ProgramRunningStatus.Running
                    ? "Running" : "Stop",
                LastUpdateText: reading.TimeStamp == default
                    ? "--" : reading.TimeStamp.ToString("HH:mm:ss")));
```

- [ ] **Step 5: Run the test to verify it passes**

```bash
dotnet test BatteryTestingSystem.Tests --filter "FullyQualifiedName~WorkflowTelemetryBridgeTests"
```

Expected: PASS.

- [ ] **Step 6: Add the slots and wire them up**

`Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor` — add below the power/temperature row:

```razor
    <div class="flex items-center justify-between opacity-60 text-xs">
        <span data-role="program-status">--</span>
        <span data-role="last-update">--</span>
    </div>
```

`Components/UI/WorkflowCanvas/PropertiesDock.razor` — add two rows at the top of the `.wf-dock__live` `<dl>`, before `<dt>Power</dt>`:

```razor
                <dt>Program</dt><dd data-role="dock-program-status">--</dd>
                <dt>Last update</dt><dd data-role="dock-last-update">--</dd>
```

`wwwroot/js/workflow-canvas.js` — in `applyTelemetry`'s per-entry block, after the temperature write:

```javascript
                setText(node, "program-status", e.programStatusText);
                setText(node, "last-update", e.lastUpdateText);
```

and in `applyDockLiveData`, before the `dock-power` write:

```javascript
        setText(dock, "dock-program-status", e.programStatusText);
        setText(dock, "dock-last-update", e.lastUpdateText);
```

`Components/Pages/Workflows/WorkflowCanvasPage.razor` — in `PushTelemetryAsync`, append to the `new ChannelTelemetry(...)` construction, after `SystemErrorId:`:

```csharp
                ProgramStatus: record.ProgramStatus,
                TimeStamp: record.TimeStamp);
```

- [ ] **Step 7: Run the full suite**

```bash
dotnet test BatteryTestingSystem.Tests
```

Expected: PASS.

- [ ] **Step 8: Verify in the browser**

With the app and simulator running, place a channel on a registered device. Confirm the node's fourth row shows a program status and a clock, and that **the clock advances** — a static clock means telemetry is not reaching the node. Select the node and confirm the dock's Live data section leads with Program and Last update.

Re-run the MutationObserver snippet from Task 4 Step 9 and confirm it still reports `childList mutations: 0`.

- [ ] **Step 9: Commit**

```bash
git add Models/DTOs/Workflow/TelemetryEntry.cs \
  Services/Implementations/Workflow/WorkflowTelemetryBridge.cs \
  Components/UI/WorkflowCanvas/Nodes/ChannelNode.razor \
  Components/UI/WorkflowCanvas/PropertiesDock.razor \
  Components/Pages/Workflows/WorkflowCanvasPage.razor \
  wwwroot/js/workflow-canvas.js \
  BatteryTestingSystem.Tests/Services/Workflow/WorkflowTelemetryBridgeTests.cs
git commit -m "feat(workflow-canvas): add program status and last-update to channel nodes

Matches the dashboard card footer (DeviceChannel.razor:1301-1327). The
timestamp matters more than it looks: without a clock, a frozen reading
is indistinguishable from a live one, and 'never updated' renders as --
rather than 00:00:00 so it cannot be mistaken for a midnight reading.

The system-error reset button stays out - it is an action, and belongs
with the Phase 3 action work.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

## Phase 1 Completion Checklist

- [ ] `git diff --stat main...HEAD -- Migrations/AppDbContextModelSnapshot.cs` produces **no output**
- [ ] Shared-file footprint is ~4 files / ~10 lines (`App.razor`, `AppDbContext.cs`, `ServiceCollectionExtensions.cs`, `Program.cs`, `MainLayout.razor`, appsettings)
- [ ] All 4 pre-existing saved layouts still load
- [ ] Channels read `1-8-2`; boards read `Board 8`
- [ ] Device and board nodes show live `N / M online`
- [ ] Channel nodes show program status and an advancing clock
- [ ] MutationObserver reports 0 childList mutations during live telemetry
- [ ] Full suite green (`DashboardRenderBatcherTests` flake excepted — confirm in isolation)
- [ ] `main` has no commits from this work

**Deferred to Phase 2:** lane layout, level-of-detail, node property configuration.
**Deferred to Phase 3:** marquee selection, `ChannelActionExecutor`, actions, system-error reset.
