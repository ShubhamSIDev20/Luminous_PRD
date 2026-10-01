# T-36/T-37 — Test project: repository pass + bUnit component pass

> Started: 2026-08-19
> Completed: 2026-08-19
> Session: [sessions/2026-08-19_0227_test-project-pure-logic-coverage.md](../sessions/2026-08-19_0227_test-project-pure-logic-coverage.md)

## Request
User: "do it next" — go ahead with the two passes explicitly deferred from T-35 (repository tests against real EF Core, and bUnit Razor component tests).

## Pass 2 — Repository tests (T-36)
New file `BatteryTestingSystem.Tests/Repositories/RepositoryTests.cs`, reusing the `NewContext()` SQLite in-memory pattern already established by `SharedDbContextConcurrencyTests`. Covers `Repository<T>` (generic base), `SchedulerRepository`, `ExportRepository`, `UserCircuitAccessRepository`, `AuditRepository`. 31 new tests.

Gotchas found while writing (all documented as tests, none are bugs):
- `Repository<T>.AddAsync`/`DeleteAsync` only stage the change — they never call `SaveChangesAsync()`. `SchedulerRepository.AddLogAsync` and `ExportRepository`'s methods DO save immediately. That base-vs-subtype inconsistency is a real footgun for any new caller of the generic repository.
- `ExportRepository.DeleteAsync` is a **soft** delete (`IsDeleted=true`), but `GetByIdAsync` has no `IsDeleted` filter while `GetByUserAsync`/`GetBySessionAsync` do — a deleted record is invisible in list views but still directly reachable by id.
- Every `ExportRepository` mutator (`UpdateStatusAsync`, `MarkReadyAsync`, `DeleteAsync`, `MarkSupersededAsync`) silently no-ops on an unknown id (`if (rec == null) return;`) — no error surfaced anywhere.
- `UserCircuitAccess.UserId` is a real FK onto Identity's `AspNetUsers`, and Sqlite's EF Core provider enforces foreign keys by default — tests had to seed a real `ApplicationUser` row before calling `SetUserCircuitAccessAsync`, or the (caught) `SaveChangesAsync` fails and the repo returns `Success=false` with no exception visible to the caller.
- `AuditRepository.LogEventAsync` has a hidden side effect: every single log write also runs a 3-month retention purge (`DELETE WHERE Timestamp < now-3mo`). Not documented anywhere in the method name.
- `AuditRepository.GetAuditRecordsAsync` returns `CommonResponse.Fail("No audit logs found...")` when the result set is empty — "no matches" is modeled as a failure, not `Ok(emptyList)`. Documented as current behavior, not fixed (would need product-team sign-off since callers may already depend on it).

Result: **156 passed, 0 failed** (was 125/125).

## Pass 3 — bUnit component tests (T-37)
Added the **bUnit** package (`bunit` 1.32.7) to `BatteryTestingSystem.Tests.csproj` — first new test dependency in this project.

New files:
- `BatteryTestingSystem.Tests/Components/FakeConfigStorageService.cs` — `IConfigStorageService` fake.
- `BatteryTestingSystem.Tests/Components/ChannelFilterTests.cs` — 9 tests rendering the **real** `ChannelFilter.razor` (the My Channels popover from T-34), not a hand-extracted copy of its logic.

Key gotcha found and worked around: `ChannelFilter` injects `ServerSessionStorageService`, whose constructor unconditionally calls `ServiceLocator.GetScoped<IConfigStorageService>()` to preload DB-persisted state — and `ServiceLocator.CreateScope()` throws `InvalidOperationException` if `SetProvider` was never called. Every test's constructor now wires a minimal `ServiceCollection` (with `FakeConfigStorageService`) through `ServiceLocator.SetProvider` before the component is rendered. **`ServiceLocator` is a process-wide static** — this is a real test-isolation risk if a future test class also touches it concurrently (xunit runs distinct test classes in parallel by default). No conflict exists today since nothing else in the test project uses it, but flagging for whoever adds the next one.

Tests directly exercise the actual rendered markup/click handlers (real `Popover`/`PopoverTrigger`/`PopoverContent`/`Button` components, not mocks) and pin:
- The T-34 fix itself: opening the popover renders three distinct indentation levels (device: no `pl-*` class, secondary board: `pl-6`, channel: `pl-9`).
- Tri-state checkbox correctness: hiding one of several channels under a board marks it **Indeterminate** (`opacity-70`), not Unchecked; hiding every channel under a device marks the device **Unchecked**, not Indeterminate.
- `VisibleChannelsChanged` fires with exactly the access set minus hidden channels.
- Search filtering narrows the tree; Hide All / Show All round-trip; empty-access-list renders the "No channels match." message.

One authoring bug caught during development (not a product bug): caching a `FindAll(...)` result across multiple state-changing interactions in a loop threw `UnknownEventHandlerIdException` — bUnit's render tree replaces node references after a re-render, so each interaction must re-`FindAll` (or filter to nodes still matching a live predicate) rather than reuse a captured list. Fixed by re-querying inside the loop.

Result: **165 passed, 0 failed** (was 156/156).

## Explicitly out of scope — DashboardView bUnit tests
`DashboardView.razor` (the dashboard page with `FilteredCircuits`/`SelectAll` from T-34) was **not** attempted via bUnit. It injects 7 services including `ChannelManager` — a `BackgroundService` that owns the real TCP 9999 / UDP 10000+10001 hardware listeners (51KB implementation) — plus `[Authorize]`, `[StreamRendering]`, and `IJSRuntime`. Instantiating or faking `ChannelManager` safely inside a unit test is a meaningfully larger undertaking than rendering `ChannelFilter` was, and forcing it through bUnit as-is risks either a fragile fixture or accidentally exercising real listener startup code. Recommended (not done): extract `FilteredCircuits`/`SelectAll`'s logic into a plain, DI-free class that both the Razor page and a unit test can call directly — the same pattern already used for `DashboardRenderBatcher`. Left as a future task, not filed as a numbered task since the user hasn't asked for that refactor yet.

## Update — T-38 completed same day
User asked to proceed with the recommended DashboardView refactor. Extracted `FilteredCircuits`/`AnchorCircuit`/`SelectAll` into a new static, DI-free `Services/Implementations/CircuitSelectionLogic.cs` (mirrors `DashboardRenderBatcher`'s isolation). `DashboardView.razor` now delegates to it and only owns the side effects (Toast messages, `StateHasChanged`) plus its existing state fields (`circuits`, `selectedCircuits`, `_accessCircuits`, etc.) — none of that state moved, keeping the blast radius small.

Caught during extraction (not a pre-existing bug — introduced and fixed within the same edit): the original `SelectAll()` returned silently (no toast) when the filtered view was empty, but showed a warning toast when the filtered view had circuits but none eligible to anchor. An early version of the extraction collapsed both into one `SelectAllOutcome`, which would have added a toast that never fired before. Split into `NothingToSelect` (silent) vs `NoOnlineCircuit` (toast) to preserve exact prior behavior.

Added `BatteryTestingSystem.Tests/Services/CircuitSelectionLogicTests.cs` (15 tests: `Filter`, `ResolveAnchor`, `SelectAll` — anchor reuse vs re-pick, CircuitStatus+ProgramStatus matching, double-select idempotency) and `FakeChannelCommandHandler.cs` (minimal `IChannelCommandHandler` stand-in — every unused member throws `NotImplementedException` so an accidental dependency fails loudly rather than returning a silent null).

Result: 180 passed, 0 failed (was 165/165). Main app rebuilt and restarted cleanly on `:5066`/`:9999`/`:10000`/`:10001` with no errors; the running hardware simulator reconnected automatically.

## Final state
`dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj` → **165 passed, 0 failed** (session started this task at 125/125, ended at 165/165 — +40 net new tests across both passes).

## Files changed
- `BatteryTestingSystem.Tests/Repositories/RepositoryTests.cs` (new)
- `BatteryTestingSystem.Tests/Components/FakeConfigStorageService.cs` (new)
- `BatteryTestingSystem.Tests/Components/ChannelFilterTests.cs` (new)
- `BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj` (added `bunit` 1.32.7 package reference)
