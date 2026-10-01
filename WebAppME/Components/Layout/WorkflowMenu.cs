using BatteryTestingSystem.Components.Pages.Workflows;
using BatteryTestingSystem.Components.Pages.Home;
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
