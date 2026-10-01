using System;
using System.Linq;
using BatteryTestingSystem.Components.Layout;
using BatteryTestingSystem.Components.Pages.Home;
using BatteryTestingSystem.Components.Pages.Workflows;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Xunit;

namespace BatteryTestingSystem.Tests.Components;

/// <summary>
/// Sub-project D. Navigation in this app is TabViewer-based, not browser-routed: TabService seeds
/// one non-closable default tab and every nav click adds another. That default was hard-coded to
/// DashboardView, so the legacy dashboard still loaded on sign-in even with Features:LegacyDashboard
/// off — hiding its nav entry never removed it, because the default tab does not come from the menu.
///
/// The decision lives here rather than inline in TabService for the same reason WorkflowMenu exists:
/// TabService.cs is main-owned, and a testable static keeps this branch's diff there to two lines.
/// </summary>
public class WorkflowDefaultTabTests
{
    [Fact]
    public void DefaultFlags_LandOnTheLegacyDashboard()
    {
        // A deployment that never heard of this feature must behave exactly as it did before.
        var features = new WorkflowFeatureOptions();

        Assert.Equal(typeof(DashboardView), WorkflowDefaultTab.ComponentFor(features));
        Assert.Equal("Home", WorkflowDefaultTab.TitleFor(features));
    }

    [Fact]
    public void CanvasEnabled_LandsOnTheWorkflowCanvas()
    {
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true, LegacyDashboard = false };

        Assert.Equal(typeof(WorkflowCanvasPage), WorkflowDefaultTab.ComponentFor(features));
    }

    // Each parity test below selects the menu entry by ONE axis and asserts the OTHER, so neither
    // can pass tautologically. Note the entry is not items[0] here: LegacyDashboard defaults to
    // true, so "Display" still leads the menu in this configuration.
    private static NavMenuItem CanvasMenuEntry(WorkflowFeatureOptions features) =>
        WorkflowMenu.Build(features, Array.Empty<NavMenuItem>())
            .Single(i => i.ComponentType == typeof(WorkflowCanvasPage));

    [Fact]
    public void CanvasEnabled_TitleMatchesTheMenuEntryExactly()
    {
        // TabService.AddTab dedupes Unique tabs BY TITLE. If this title and the menu item's drift
        // apart, clicking "Workflows" opens a second canvas tab beside the default one.
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true, LegacyDashboard = false };

        Assert.Equal(CanvasMenuEntry(features).Title, WorkflowDefaultTab.TitleFor(features));
    }

    [Fact]
    public void CanvasEnabled_DefaultTabMatchesTheMenuEntrysComponent()
    {
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true, LegacyDashboard = false };
        var byTitle = WorkflowMenu.Build(features, Array.Empty<NavMenuItem>())
            .Single(i => i.Title == WorkflowDefaultTab.TitleFor(features));

        Assert.Equal(byTitle.ComponentType, WorkflowDefaultTab.ComponentFor(features));
    }

    [Fact]
    public void LegacyDashboardWins_WhenBothFeaturesAreOn()
    {
        // Both on is the everyday state: Workflows is reachable from the menu as an optional
        // feature, but the legacy dashboard is still what opens on sign-in.
        var features = new WorkflowFeatureOptions { WorkflowCanvas = true, LegacyDashboard = true };

        Assert.Equal(typeof(DashboardView), WorkflowDefaultTab.ComponentFor(features));
    }

    [Fact]
    public void BothFeaturesOff_StillYieldsAComponent_RatherThanNull()
    {
        // The default tab is non-closable, so a null ComponentType renders a permanently blank
        // shell with no way to recover.
        var features = new WorkflowFeatureOptions { WorkflowCanvas = false, LegacyDashboard = false };

        Assert.NotNull(WorkflowDefaultTab.ComponentFor(features));
    }

    [Fact]
    public void NullFeatures_FallBackToMainsBehaviour()
    {
        // TabService takes IOptions as optional so the many existing constructions of it keep
        // compiling; a null must not throw inside a DI constructor.
        Assert.Equal(typeof(DashboardView), WorkflowDefaultTab.ComponentFor(null));
        Assert.Equal("Home", WorkflowDefaultTab.TitleFor(null));
    }
}
