# Device API
> Updated: 2026-09-04T00:00:00Z
> Controller: `Controllers/DeviceController.cs`

---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| GET | `/api/device` | List devices | None (no `[Authorize]`) |
| GET | `/api/device/logs` | Retrieve logs (`InMemoryLogStore`) | None |
| POST | `/api/device/SendProgram` | Send a program to devices — delegates to `CoreSendProgram` (supports bulk channels) | None |
| POST | `/api/device/Start` | Start test — delegates to `CoreStart` (supports bulk channels) | None |
| POST | `/api/device/Stop` | Stop test — delegates to `CoreStop` (supports bulk channels) | None |
| POST | `/api/device/Pause` | Pause test — delegates to `CorePause` (supports bulk channels) | None |
| POST | `/api/device/Continue` | Resume test — delegates to `CoreContinue` (supports bulk channels) | None |
| GET | `/api/device/GetSessions` | List battery sessions | None |
| POST | `/api/device/GetDevices` | Query devices | None |
| POST | `/api/device/GetPrograms` | Query programs | None |
| POST | `/api/device/GetBatteries` | Query batteries | None |
| POST | `/api/device/GetDbcFiles` | Query all registered DBC files (`IDbcService.GetAllAsync()`) | None |
| POST | `/api/device/GetLiveData` | Poll live data — delegates to `CoreLiveData` (supports bulk channels) | None |
| POST | `/api/device/GetSessionId` | Get active session IDs for channels (supports bulk channels) | None |
| GET | `/api/device/GetLiveSSE` | Server-Sent Events stream for live data | None |

---

## Bulk Channel Operations (`CommonRequest`)

The `CommonRequest` view model supports multi-channel commands via `ChannelList` only — `ChannelNumber` and `ChannelRange` were removed (2026-09-04). Every caller, including single-channel ones, now sends a one-element `ChannelList`. Expansion/validation is handled by `BatteryTestingSystem.Utils.ChannelExpander.GetChannels`, called per-request **inside** each `Core*` loop (not pre-expanded at the controller boundary like the old `ExpandChannels`-based flow).

### Request Model Fields
```csharp
public class CommonRequest
{
    public int DeviceID { get; set; }
    public int SecondaryBoardNumber { get; set; } = 1;
    public List<int> ChannelList { get; set; } = new();   // e.g. [1, 2, 5, 10]; single channel = [id]
    public int? ProgramId { get; set; }
    public int? BatteryId { get; set; }
    public int? dbcId { get; set; }
}
```

### Resolution Logic
`ChannelExpander.GetChannels(request)`:
1. Requires a non-null, non-empty `ChannelList`.
2. Filters to positive channel numbers, dedupes, sorts ascending.
3. Throws `ArgumentException` if nothing valid remains, or if count > `MaxChannels` (128).

Each `Core*` method in `DeviceController` catches that `ArgumentException` **per request** (not per batch): a request with an invalid `ChannelList` is skipped with an error message appended to the response, and the rest of the batch still processes. This was added alongside the `ChannelList`-only migration — previously an invalid list threw, which was uncaught by any registered JSON exception middleware (see Notes) and could surface as a raw error page instead of a clean 400-shaped response.

### Guards & Limits
- **Max Channels**: 128 channels per request object. Exceeding throws `ArgumentException`, caught per-request as above.
- **Empty/invalid list**: reported as `"{DeviceID}-{SecondaryBoardNumber} -> {message}"` in the response's message list; does not abort the rest of the batch.

### Endpoints with Bulk Channel Expansion
All endpoints that accept `List<CommonRequest>` expand each request's `ChannelList` inside the shared `Core*` helper: `SendProgram`, `Start`, `Stop`, `Pause`, `Continue`, `GetLiveData`, `GetSessionId`, `GetDevices`.
`GetLiveSSE` (single `CommonRequest` via query string, SSE) uses only the first entry of `ChannelList` — it does not call `ChannelExpander`.

### Performance note
`CoreSendProgram` fetches Program/Battery/DBC **once per request**, not once per channel — those three lookups are identical for every channel in the same `ChannelList`, so they're hoisted above the per-channel loop.

### VNC / ACNx battery-relative nominal values (2026-09-04)
`CoreSendProgram` now passes the resolved `battery.Data` into `handler.SetProgramAsync(program.Data, battery.Data)`, not just into `SetBatteryParamAsync`. This lets a program's steps use battery-relative units — `ACN`/`ACN1`/`ACN2`/`ACN4`/`ACN5`/`ACN10`/`ACN20` (current as a multiple of `battery.NominalCapacity / X hours`) and `VNC` (voltage as `value × battery.NumberOfCells`) — in the Nominal Value, Limit, and Registration columns. Resolution happens in `Utils/BatteryUnitResolver.cs`, invoked from `ProgramBuilder.ProcessNominalValues`/`ProcessStandardLimit`/`TryParseRegistration` just before the existing `ExtractFloatAsByteArraySafe`/`TryParseUnit` wire-encoding calls — the hardware protocol itself has no concept of these units, they're resolved to plain A/V before encoding. A program using a battery-relative unit with no valid battery (missing `BatteryId`, capacity/cell count of 0) fails that one `SetProgramAsync` call with a clear `BatteryUnitResolutionException`-derived message rather than sending an unscaled value. See `docs/manual-extract/VNC-ACN-battery-parameters.md` and `docs/VNC-ACN5-Implementation-Plan.md` for the manual excerpts, formulas, and full design.

### §12.3 battery-parameter bare tokens (2026-09-08)
10 more manual tokens (`CNom`, `NoCell`, `UGas`, `UMax`, `UNom`, `CutOff`, `INom`, `ICrank`, `ChargeF`, `EDensity`) are usable as **bare identifiers** — no multiplier, unlike `ACNx`/`VNC` — e.g. a Limit of `"> INom"` or a Nominal Value of just `"UGas"`. Unlike `ACNx`/`VNC` this needed no new resolve-and-scale step: `BatteryUnitResolver.GetBatteryGlobalVariables(battery)` turns the battery's fields into ordinary `GlobalVariable` entries, appended in `DecoderService.ConvertProgramIntoBytesPackets` right after `ProgramBuilder.ExtractGlobalVariables` — every consumer (`ProcessNominalValues`, `ProcessStandardLimit`, `AddRegistrations`) already resolves by-name against `globalVariables`, so nothing downstream changed. A step's own `SET`-defined variable always wins on a name collision (skipped when building the battery-derived list). `ProgramEditor.razor`'s `ValidateProgram()` seeds the same 10 names as placeholder `GlobalVariable`s (`Value="0"`) so they validate while authoring, before any battery is selected — added *after* the SET-duplicate-name check so a placeholder can never trigger a false "duplicate variable" error. **`Rin` (internal resistance) is excluded** — no `CutoffCondition`/`RegistrationType` byte exists for Ohms; see the Ramp/Resistance note in `docs/manual-extract/VNC-ACN-battery-parameters.md` §7 for why that (and Ramp) can't be done without an actual protocol/firmware change. Full design: `docs/Battery-Parameters-Intern-Table-Plan.md`.

---

## Notes
- `CoreSendProgram`, `CoreStart`, `CoreStop`, `CorePause`, `CoreContinue`, `CoreLiveData` are internal static helpers on `DeviceController` — the same helpers are called by `MCP/DeviceMcpTools.cs`. Changing their signatures affects both the REST surface and MCP tools. `[InternalsVisibleTo("BatteryTestingSystem.Tests")]` was added to `BatteryTestingSystem.csproj` so xUnit can exercise these `internal` helpers directly (see `BatteryTestingSystem.Tests/Controllers/DeviceControllerTests.cs`).
- `MCP/DeviceMcpTools.cs` builds a single-channel `CommonRequest` via `ChannelList = new List<int> { circuitId }` (it used to set `ChannelNumber`, which no longer exists — this was a build break fixed 2026-09-04).
- The controller does **not** route through `Middleware/ExceptionHandlerMiddleware.cs` (its JSON `ArgumentException` → 400 mapping) — `Program.cs` registers `app.UseExceptionHandler("/Error", ...)` instead and leaves `app.UseCustomExceptionHandler()` commented out. This is why `ChannelExpander`'s validation is caught locally per-request in each `Core*` method rather than relying on global middleware.
- **No `[Authorize]` on this controller** — known gap, tracked as `.claude/TASKS.md` T-2. This is the legacy hardware/session API and is intentionally kept stable during any future clean-architecture migration (per `docs/architecture/02-backend-clean-architecture-design.md`).
- Underlying device I/O goes through `ChannelManager` → `DeviceConnection` (multiplexed per-device TCP) → `ChannelCommandHandler` (channel slot). See `.claude/ARCHITECTURE.md` for the full data flow.