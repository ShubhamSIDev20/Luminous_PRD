# Session #24 — Workflow Canvas Playground: design & spec

**Date:** 2026-08-22
**Agent:** Claude (Opus 5)
**Branch:** `feat/workflow-canvas-experiment` (created this session, from `main` @ `e59dc86`)
**Task:** [T-47](../tasks/2026-08-22_workflow-canvas-playground.md)
**Status:** ✅ Design approved, spec committed (`c99d0d5`), 17-task implementation plan committed (`1978942`). No application code written.

---

## Goal

User asked for an experimental n8n-style "workflows dashboard": a drag-and-drop canvas of
devices → secondary boards → channels, with batteries/programs/DBC attachable, a left properties
dock, a right palette, live glowing/flowing animations, saved layouts, a new menu entry, the
current dashboard hidden (not deleted), and a future analytics page. Explicitly a separate
branch that must not be merged until it proves itself.

## What was done

1. Classified as **architectural** per the brainstorming skill; ran the full flow
   (context → clarifying questions → design sections with approval gates → spec).
2. **Flagged scope decomposition:** the request was four projects. Agreed to scope this session
   to canvas + persistence + nav change, deferring the analytics page to its own spec.
3. Explored the codebase: nav/tab system, dashboard, services, repositories, theme vars, csproj.
4. Ran seven clarifying questions (scope, hardware semantics, CSS strategy, canvas engine, node
   model, live-data source, persistence shape, v1 feature set).
5. Presented the design in three approved sections.
6. Created branch `feat/workflow-canvas-experiment` and wrote the spec (`c99d0d5`).
7. Wrote the implementation plan via the writing-plans skill — 17 TDD tasks, ~7100 lines, every
   task carrying real test code and real implementation code (`1978942`).
8. Self-review of the plan caught and fixed three spec errors and two uncovered spec
   requirements; the spec was corrected in the same commit.

## Files created

- `docs/superpowers/specs/2026-08-22-workflow-canvas-design.md` — the design spec
- `docs/superpowers/plans/2026-08-22-workflow-canvas.md` — the 17-task implementation plan
- `.claude/tasks/2026-08-22_workflow-canvas-playground.md` — T-47

## Files changed

None in application code. Indexes updated: `SESSION.md`, `TASKS.md`, `AGENT.md`.

## Discoveries / gotchas

- **`wwwroot/css/app.min.css` has no rebuild pipeline (ADR-2).** This is the binding constraint on
  the whole feature — every glow/flow/battery animation must be hand-written plain CSS. Upside:
  theme vars are raw HSL triplets, so `hsl(var(--status-charge) / .55)` themes for free.
- **Navigation is tab-based, not route-based.** `MainLayout.razor:147` `_menuItems` →
  `Navbar.razor` → `TabService.AddTab(NavMenuItem)`. `ComponentType` is what actually gets
  rendered; `Url` is a label. New pages are registered here, not discovered by the router.
- **`Display` → `DashboardView` is both menu entry #1 and the `Navbar.razor:12` logo target.**
  Hiding the dashboard requires gating both.
- **Blazor Server forbids the obvious canvas implementation.** Pointer moves cannot round-trip.
  Precedent already exists in-repo: `wwwroot/js/windowDragManager.js`.
- **Telemetry must bypass Blazor rendering entirely** — CSS custom properties pushed through one
  batched interop call per coalesced tick, reusing `ChannelManager` + `DashboardRenderBatcher`.
- Repo already has the right precedent for testable design: `CircuitSelectionLogic.cs` (T-38) was
  extracted from the dashboard purely so it could be unit-tested. The spec follows it —
  `WorkflowGraphBuilder` / `WorkflowAutoLayout` / `WorkflowGraphValidator` are pure classes.

## Further discoveries (from writing the plan)

- **DI is in `Extensions/ServiceCollectionExtensions.cs:97-101`, not `Program.cs`.** Easy to get
  wrong; `Program.cs` has no `AddScoped` calls at all.
- **`Components/App.razor` hardcodes every asset** (`<link>` lines 9-10, `<script>` lines 11-26),
  with a `?version=` cache-buster convention that must be bumped on every JS change — a stale
  cached script produces symptoms that look exactly like broken C#.
- **`CircuitStatus.Countinue` is misspelled in the enum** (`Models/Enums/CircuitEnums.cs:54`)
  while the CSS variable is `--status-continue`. `status.ToString().ToLower()` therefore yields
  a variable that does not exist, and the element renders unstyled with no error anywhere. The
  plan gives this its own tested mapping function.
- **`Channel.Id` and `SecondaryBoard.Id` are `long`; `Device.DeviceID` is `int`** — and
  `SecondaryBoard` uses `DeviceId` while `Device` uses `DeviceID`. Easy to mistype.
- **`Repository<T>.AddAsync` does not call `SaveChangesAsync`** — already pinned by an existing
  test in `RepositoryTests.cs`. Any new repository must call it explicitly or writes vanish.

## Next step

Execute the plan, subagent-driven or inline. The branch stays unmerged until the task-17 verdict.
