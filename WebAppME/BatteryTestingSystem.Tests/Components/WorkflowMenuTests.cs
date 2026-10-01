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
