using BatteryTestingSystem.Components.Pages.Home;
using BatteryTestingSystem.Components.Pages.Workflows;
using BatteryTestingSystem.Services.Implementations.Workflow;
using Blazicons;

namespace BatteryTestingSystem.Components.Layout;

/// <summary>
/// Chooses the one non-closable tab TabService seeds a fresh session with (spec D17/D24).
///
/// This app does not navigate by browser route — TabViewer hosts every page as a tab, and the
/// default tab is created in code rather than taken from the nav menu. That is why turning
/// Features:LegacyDashboard off was not enough to stop the legacy dashboard loading on sign-in:
/// hiding its menu entry never touched the hard-coded default.
///
/// Lives outside TabService.cs (which main owns) for the same reason WorkflowMenu does — a testable
/// static keeps this branch's diff in a main-owned file down to a couple of lines, and lets a test
/// pin the default against the menu entry instead of trusting two literals to stay in step.
/// </summary>
public static class WorkflowDefaultTab
{
    /// <summary>
    /// The legacy dashboard wins whenever it is enabled, even with the canvas also on: Workflows is
    /// an optional, reachable-from-the-menu feature, not a replacement landing page. The canvas only
    /// becomes the default tab once LegacyDashboard is explicitly turned off. A null
    /// <paramref name="features"/> reproduces main's behaviour, because TabService takes its options
    /// as optional and must not throw in a DI constructor.
    /// </summary>
    public static Type ComponentFor(WorkflowFeatureOptions? features) =>
        features is { LegacyDashboard: false, WorkflowCanvas: true } ? typeof(WorkflowCanvasPage) : typeof(DashboardView);

    /// <summary>
    /// Must stay byte-identical to the matching WorkflowMenu entry's title: TabService.AddTab
    /// dedupes Unique tabs by title, so any drift makes clicking "Workflows" open a second canvas
    /// tab next to the default one instead of activating it. WorkflowDefaultTabTests pins this.
    /// </summary>
    public static string TitleFor(WorkflowFeatureOptions? features) =>
        features is { LegacyDashboard: false, WorkflowCanvas: true } ? "Workflows" : "Home";

    public static SvgIcon IconFor(WorkflowFeatureOptions? features) =>
        features is { LegacyDashboard: false, WorkflowCanvas: true } ? Lucide.Workflow : Lucide.House;
}
