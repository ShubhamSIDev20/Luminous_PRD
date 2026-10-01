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
