# Session — T-47 Phase 6: Home Landing & Cross-Workflow Exclusivity

**Date:** 2026-08-25
**Agent:** Claude (Sonnet 5)
**Branch:** `feat/workflow-canvas-experiment` (permanent, never merged to `main` — D14/D15)

## Goal

Implement Sections 4-5 (D22/D17) of `docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md`:
per-user cross-workflow channel exclusivity, and making the canvas the page a user lands on after
logging in. **This was the last phase in the D17-D24 spec.**

## Status

✅ All 4 Phase 6 tasks complete, live-verified end to end — including against real data left over
from earlier sessions. 536 tests (up from 531 at Phase 5's end), 4 commits `6b9a8a9`..`5ed6fdb`.

## Design correction found during planning (before any code was written)

The design spec's Section 5 sketched a mechanism for D17 that turned out not to work: swap which
`NavMenuItem` owns `Url = "/"` in `WorkflowMenu.cs`, add a second `@page "/"` to
`WorkflowCanvasPage.razor`, and generalize `TabService.AddTab`'s hardcoded `Title == "Display"`
check (D24). Investigation found:
- `Components/Pages/Home/TabView.razor` already declares `@page "/"` — a second component
  claiming that route is an ambiguous-route conflict, not a valid change.
- Root routing in this app is driven entirely by each page's own `@page` directive
  (`DashboardView.razor` → `/Dashboard`, `WorkflowCanvasPage.razor` → `/workflows`, `DeviceList
  .razor` → `/device/list`, etc.) — `NavMenuItem.Url` only affects nav-highlighting and the
  separate in-memory "virtual tab" overlay system (`TabService`/`TabViewer`), never actual routing.
- The real post-login destination was always decided by `Pages/Login.cshtml.cs`, which hardcodes
  `return Redirect("/");` in two places with **no `ReturnUrl` handling at all** to interact with.

**Corrected fix:** change both `Redirect("/")` calls in `Pages/Login.cshtml.cs` to
`Redirect("/workflows")`. Smaller and lower-risk than the spec's original sketch, and touches no
shared tab-system code. D24 is dropped entirely — `TabService.cs` is never touched. This was found
and corrected *before* writing any task steps, and documented explicitly in the plan's own
"Correction from the design spec" section so the deviation was visible before execution — applying
the lesson from Phase 4's mid-flight board-node correction (surface a mechanism deviation up front,
don't just quietly implement something different from what the spec says).

## What was done

1. **`IWorkflowLayoutService.GetClaimedChannelsAsync(userId, excludingLayoutId)`** — maps every
   channel `EntityId` already placed in one of a user's *other* saved layouts to that layout's
   name, by reusing the exact `ListForUserAsync` + `WorkflowGraphJson.TryDeserialize` pattern
   `LoadAsync` already relies on (no new repository method needed). 5 tests. Commit `6b9a8a9`.
2. **`PalettePanel.ClaimedChannels` parameter** — a claimed channel's chip renders disabled with a
   tooltip naming the owning workflow, same visual treatment as an already-on-canvas channel.
   Purely additive markup change, not yet wired to real data. Commit `bcfd51c`.
3. **Real enforcement in `WorkflowCanvasPage`** — `_claimedChannels` refreshed on initial load and
   on every `Load`/`Save`/`Save as`/`Delete` (anywhere `_currentLayoutId` changes, since the
   excluded-current-layout set is only correct for whichever layout is now open).
   `HandlePlaceDevice` filters claimed channel ids out **before** calling
   `WorkflowPlacement.PlaceDevice` — enforcement holds through the bulk "All" path, not just the
   individual chip's `disabled` attribute, matching D22's explicit requirement that a claimed
   channel must never reach the canvas via any path. A skipped-channel count surfaces as a warning
   toast. Commit `6a98181`.
4. **`Pages/Login.cshtml.cs`** — both hardcoded `Redirect("/")` calls now redirect to
   `/workflows`. Commit `5ed6fdb`.

## Verification

- Full suite: 536/536 passing (started at 531).
- Task 3's enforcement was verified against **real leftover data from earlier sessions in this
  same conversation**, not synthetic test data: the "Test" saved layout (from Phases 3-5) already
  contained `SIM_DEVICE_001_1`'s Board 1 (8 channels). Opening a fresh unsaved layout and expanding
  that device in the palette showed exactly those 8 channels disabled with
  `"Already placed in workflow 'Test'"`, while Boards 2-8 stayed available; clicking "All" placed
  7 of 8 boards (56 channels), correctly skipping Board 1's 8 claimed channels; re-opening "Test"
  itself showed those same 8 channels disabled for the ordinary already-on-canvas reason (empty
  tooltip) — never falsely flagged as claimed by another workflow. This is about as strong a live
  verification as this feature could get, since it happened to be tested against genuinely
  pre-existing cross-session state rather than a contrived scenario.
- Task 4 required an actual login cycle (logout, then log back in) to verify the redirect target.
  Per the standing rule that this agent never enters passwords, the user performed the login
  themselves and confirmed landing on `/workflows`; the Dashboard's continued reachability at
  `/Dashboard` (all 640 circuits rendering correctly) was verified directly by this agent since
  that required no credential entry, only navigation.
- Confirmed via `git diff main..feat/workflow-canvas-experiment` on `DashboardView.razor`/
  `TransferDialog.razor` → 0 lines; `main` (HEAD `e59dc86`) received no commits from this work.

## Phase completion note

This is the last phase in the D17-D24 spec (`docs/superpowers/specs/2026-08-25-workflow-canvas-home-and-usability-design.md`).
All 7 sections (board consolidation, animation correctness, refresh rate, cross-workflow
exclusivity, home landing, status legend, consolidated toolbar) are now implemented across Phases
4-6. The original D1-D16 dashboard-parity spec (Phases 1-3) and this D17-D24 usability spec
(Phases 4-6) are both fully complete as of this session.
