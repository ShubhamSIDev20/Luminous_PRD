# T-35 — Test project: pure-logic coverage pass (BatteryTestingSystem.Tests)

> Started: 2026-08-19
> Completed: 2026-08-19
> Session: [sessions/2026-08-19_0227_test-project-pure-logic-coverage.md](../sessions/2026-08-19_0227_test-project-pure-logic-coverage.md)

## Request
User: "we have test case project as well there. want to add all test there." Scoped down via brainstorming to a first pass over pure-logic units (no DB/HTTP/hardware dependency) — repositories and Razor/bUnit coverage deferred to later passes at the user's own choice.

## Baseline
58 tests, 57 passing. One pre-existing failure: `ChannelAddressCodecTests.Encode_OutOfRange_Throws(board: 0, channel: 1)` — the test asserted board 0 throws, but production's `Encode` guard (`boardNumber < 0`) treats board 0 as valid, and T-19 (session #11) explicitly fixed the codec to accept board 0 for `1-0-x` topology. Confirmed with the user: fix the test, not the code.

## What changed
- `BatteryTestingSystem.Tests/Utils/ChannelAddressCodecTests.cs` — removed the bad InlineData row; added board-0 coverage and `Decode`/`Encode` asymmetry tests (Decode never throws, Encode is strict — a decoded byte isn't always re-encodable).
- `BatteryTestingSystem.Tests/Utils/EndianTests.cs` (new) — `BigEndian`/`LittleEndian`, cross-checked against `BitConverter` and explicit literals (not just round-tripped against themselves), plus offset-into-frame and out-of-bounds behavior.
- `BatteryTestingSystem.Tests/Utils/LttbDownsamplerTests.cs` (new) — shape-fidelity tests (spike survival, naive-stride comparison) plus the tight `n = threshold + 1` bucket-boundary case.
- `BatteryTestingSystem.Tests/Services/DashboardRenderBatcherTests.cs` (new) — the 64-cards-into-1-dispatch coalescing batcher; timing tests with a short injected interval; covers dedup, re-arm, dispatcher-attached-late, inline fallback, concurrency.

## Result
125 passed, 0 failed (was 58 total / 57 passing). +67 net new tests.

## Explicitly out of scope this pass (deferred, not declined)
- `UserCircuitAccessService` — pure pass-through, no logic to test.
- Repository/EF tests (real SQLite in-memory).
- Razor component tests (would need bUnit — new dependency).

## Gotcha
Writing `DistinctLambdasOverTheSameCard_AreNotDeduped` initially failed because `() => card.Render()` inside a loop reuses one closure instance across iterations (compiler doesn't reallocate when the captured variable doesn't change) — the delegates are equal by Target+Method and correctly dedupe. Not a product bug; fixed by capturing a per-iteration local to force a fresh closure. See session file for detail.

## Next
Pending: user decision on pass 2 (repositories) and pass 3 (bUnit for Razor components).
