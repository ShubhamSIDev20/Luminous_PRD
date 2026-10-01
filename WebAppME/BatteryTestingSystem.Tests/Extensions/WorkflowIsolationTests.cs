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

        // The split is layouts-only, by design: the canvas WRITES saved layouts to
        // WorkflowDbContext but READS hardware topology (devices, boards, channels) from
        // AppDbContext, which still owns it. WorkflowTopologyProvider therefore depends on
        // AppDbContext, and a container without it does not reflect the real application.
        services.AddDbContext<AppDbContext>(o => o.UseSqlite("Data Source=:memory:"));

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
        // Note: IWorkflowTopologyProvider is declared beside its implementation, not in
        // Services.Interfaces like the other two.
        Assert.NotNull(sp.GetService<BatteryTestingSystem.Services.Implementations.Workflow.IWorkflowTopologyProvider>());
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
