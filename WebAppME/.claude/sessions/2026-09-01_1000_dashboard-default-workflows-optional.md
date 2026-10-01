# Session — Dashboard stays default, Workflows becomes optional

> Started: 2026-09-01T10:00:00Z

## Goal
User request: after 5 days building the Workflow Canvas branch, the app's default
landing view had flipped to the canvas (`Features:WorkflowCanvas=true` +
`LegacyDashboard=false` in `appsettings.Development.json`). User wants the
**previous behavior back as default** — `DashboardView.razor` (tab view) — with
Workflows kept reachable as an **optional** nav entry, not forced on sign-in.

## What was found
The prior branch work (session 2026-08-27 area) had already built exactly the
right seams for this — `WorkflowFeatureOptions` (`Features:WorkflowCanvas` /
`Features:LegacyDashboard`), `WorkflowMenu.Build` (menu construction), and
`WorkflowDefaultTab` (which component `TabService` seeds as the non-closable
default tab — navigation here is TabViewer-based, not browser-routed, so the nav
menu and the default tab are two independent decisions).

The bug: `WorkflowDefaultTab.ComponentFor/TitleFor/IconFor` had **canvas always
wins whenever `WorkflowCanvas=true`**, regardless of `LegacyDashboard`. That was
a deliberate "migration state" decision baked in by the prior session (see the
now-renamed test `CanvasWins_WhenBothFeaturesAreOn`), but it's the opposite of
what the user wants now.

## What changed
- `Components/Layout/WorkflowDefaultTab.cs`: flipped the priority — the legacy
  dashboard now wins whenever `LegacyDashboard=true`, even with `WorkflowCanvas`
  also on. The canvas only becomes the default tab when `LegacyDashboard` is
  explicitly turned off. `ComponentFor`/`TitleFor`/`IconFor` all switched from
  `features?.WorkflowCanvas == true` to `features is { LegacyDashboard: false, WorkflowCanvas: true }`.
- `appsettings.Development.json`: `Features.LegacyDashboard` flipped back to
  `true` (was `false`). `WorkflowCanvas` stays `true` so the "Workflows" nav
  entry is still visible/reachable — it's just no longer the default tab.
- `BatteryTestingSystem.Tests/Components/WorkflowDefaultTabTests.cs`: updated
  to match the new priority — `CanvasEnabled_LandsOnTheWorkflowCanvas` and the
  two "matches the menu entry" tests now explicitly set `LegacyDashboard = false`
  (previously the canvas won even with the class default `LegacyDashboard=true`
  implicitly in play); renamed `CanvasWins_WhenBothFeaturesAreOn` →
  `LegacyDashboardWins_WhenBothFeaturesAreOn` with the assertion flipped to
  `DashboardView`.

`WorkflowMenu.Build` itself needed **no change** — with both flags true it
already emits `["Display", "Workflows", ...]`, i.e. Dashboard first (so the
brand-logo click, which navigates to `MenuItems.FirstOrDefault()`, still lands
on the dashboard) and Workflows as a normal, optional, second nav entry.

## Verification
- `dotnet test --filter "FullyQualifiedName~WorkflowDefaultTabTests|FullyQualifiedName~WorkflowMenuTests|FullyQualifiedName~WorkflowFeatureOptionsTests"` → 16/16 passed.
- `dotnet build BatteryTestingSystem.Tests` → succeeded, 0 errors.

## Gotcha for future sessions
`Features:WorkflowCanvas` controls **menu visibility only**. Which tab opens on
sign-in is a **separate** decision in `WorkflowDefaultTab`, keyed primarily off
`LegacyDashboard`. Don't assume enabling the canvas flag alone changes the
landing page — check `WorkflowDefaultTab.ComponentFor` too.
