# Research & Findings — Pending Fixes and Design Notes

Pure research/design document except where noted "FIXED." Written so the investigation doesn't need to be repeated when these items are picked up.

> **Standing note on the Python hardware simulator (`HardwareSimulator/simulator.py`):** it is a test harness, not protocol authority, and may contain incorrect/false implementations (confirmed at least twice below — items 2.4's manufacturing and factory byte-padding bugs). `Services/DecoderService.cs` is the correct, authoritative reference for the wire protocol as it stands today — do not change the protocol/decoder to match the simulator. When the simulator and decoder disagree, rewire the simulator to match the decoder, not the other way around. If a real protocol change is ever needed, it will be provided explicitly rather than inferred from the simulator.

---

## 1. Store Registration Logs — attributing a user's remark (Stop/Pause + comment) to the correct async log entry

**Goal:** when a user clicks Stop/Pause, capture their name + a free-text comment and store it against the *specific* registration log row that resulted from that action — even though registration data streams in asynchronously, at high frequency, decoupled from the button click.

### What already exists (no schema change needed)

- **`Models/SqliteEntities/MeasurementData.cs:119`** already has a `public string? Remark { get; set; } = string.Empty;` field. This is the natural place to store `"{UserName}: {Comment}"` — no migration required.
- **`CircuitCommandHandler.StopProgram()`** (`Services/Implementations/CircuitCommandHandler.cs:526`) and **`PauseProgram()`** (line 581) already have `CurrentUser.UserName` available synchronously, at the exact moment the button is clicked — before anything async happens. Evidence: both already build a `CommandTracker.AddCommand($"... executed by {CurrentUser.UserName}!")` string today. Neither method currently accepts a `comment` parameter — that's the one signature change needed on the UI-trigger side.
- **The store pipeline already has the exact pattern needed** — inspecting a field on an incoming async record to trigger a side effect. In `StartStoreWorkerAsync` (`CircuitCommandHandler.cs:256`):
  ```csharp
  var IsEnd = item.RealStoreRecord.FirstOrDefault(e => (byte)e.Operator == OperatorConstants.STO);
  if (IsEnd != null) { ... await PService.Service.EndSession(Session); }
  ```
  This proves the codebase already correlates "a specific record in an async batch" → "a side effect" by inspecting a field on that record (`Operator`). The remark feature follows the same shape.

### The pipeline (for context)

`EnqueueForStore(recordStoreRequest dto, ...)` (line 234-254) → writes to `Channel<recordStoreRequest> _StoreQueue` (per-circuit, one channel) → `StartStoreWorkerAsync` (line 256) dequeues single-threaded via `await foreach (var item in _StoreQueue.Reader.ReadAllAsync(...))` → `StoreService.Service.InsertRecordAsync(item)` persists the batch. Because everything flows through one channel consumed single-threaded, **ordering is preserved** — this is important for the correlation design below.

### Recommended design: pending annotation, consumed by the next matching record

1. **Capture at click time.** Extend `StopProgram(string? comment = null)` / `PauseProgram(string? comment = null)`. The UI (Dashboard action buttons) needs a small prompt/dialog to collect the comment before invoking the action — today `DoAction("stop"/"pause")` in `DashboardView.razor` calls these with no arguments.
2. **Stash pending state on `CircuitCommandHandler` itself** — it's already the long-lived, per-circuit object (owned by `CircuitManager._devices`) that holds `RealTime`/`Session` state, so it's the natural owner. Add something like:
   ```csharp
   private PendingRemark? _pendingRemark; // { UserName, Comment, IssuedAt }
   ```
3. **Consume it in the store pipeline** (in `EnqueueForStore` or the top of the loop body in `StartStoreWorkerAsync`, before `InsertRecordAsync`): if `_pendingRemark` is set, and one of the records in the current batch reflects the action's result (e.g. `RealTimeRecord.CircuitStatus == Pause` or `== Stop`, following the same idiom as the existing `Operator == STO` check), stamp `record.Remark = $"{UserName}: {Comment}"` on that specific record, then clear `_pendingRemark` (one-shot).
4. **Stamp before persistence**, not after — mutate the in-memory `MeasurementData`/`RealStoreRecord` object before it reaches `InsertRecordAsync`, so it's a single DB write, not a write-then-update.

### Two design decisions to make explicitly before implementing

- **Staleness guard:** if no matching record arrives within a few seconds (device disconnected, command failed silently), drop `_pendingRemark` rather than let it attach to a later, unrelated action. Cheap: check `DateTime.Now - _pendingRemark.IssuedAt` before applying.
- **Rapid double-actions:** a single nullable field can lose an annotation if two actions fire before the first's matching record arrives (e.g. Pause then immediate Resume). Given Stop/Pause aren't typically fired back-to-back, start with the single-slot version; upgrade to a small FIFO queue only if this is observed in practice.

### Alternative (lower risk, different tradeoff)

Skip embedding into `MeasurementData.Remark` entirely. Write a row to the existing **`AuditLog`** table (already used elsewhere in this app — e.g. DBC transfers, manufacturing/factory config downloads) at the exact moment of the click, with its own precise timestamp. Correlate at *display/report* time (nearest `MeasurementData` row by timestamp or status transition) instead of fusing the two streams in real time. Zero raciness, but the remark lives in a separate table rather than literally inside the log row. Worth considering if "in the same row" isn't a hard requirement.

**Decision needed before implementation:** which of the two approaches (embedded pending-annotation vs. separate AuditLog + display-time correlation) fits the actual reporting requirement. Recommend embedded approach if operators need to see the remark inline in the Data Viewer's log grid; AuditLog approach if a separate "action history" view is acceptable.

---

## 2. UI / Bug Backlog

### 2.1 Dashboard Transfer dialog — DBC Ports dropdowns don't match theme

**File:** `Components/UI/Dashboard/TransferDialog.razor`

**Root cause (confirmed):** in the same dialog, **Program** and **Battery** selection use a fully custom-styled component (`<RadioGroup>` + `<ScrollArea>`, div-based, theme-aware via Tailwind classes like `bg-primary/20`, `hover:bg-muted`). **DBC Ports (Port 1/2/3)** use plain native HTML `<select><option>` elements instead:
```html
<select class="h-8 w-full rounded border border-border bg-background text-foreground text-xs px-2" ...>
```
The closed `<select>` box picks up the Tailwind classes fine, but **native browser `<option>` popups are rendered by the OS/browser, not by CSS** — in dark mode, clicking the dropdown shows an unstyled white-background/black-text native popup, clashing with the rest of the themed dialog. This is a known, common Blazor/web pitfall with plain `<select>` vs. custom dropdown components.

**Fix direction:** replace the three native `<select>` elements with the same custom dropdown pattern already used for Program/Battery in this exact file (or an existing reusable custom `<Select>` component elsewhere in the app, if one exists — worth checking `Components/UI/` for a themed select component before building a new one). Purely a component-choice change; the underlying `port1DbcId`/`port2DbcId`/`port3DbcId` binding logic and `IsDbcUsedByOtherPort` exclusivity check do not need to change.

**Resolution — FIXED.** Found a first-party, already-theme-aware, reusable dropdown family in this codebase (`Components/UI/DropdownMenu/`: `DropdownMenu`, `DropdownMenuTrigger`, `DropdownMenuContent`, `DropdownMenuItem` — styled with `bg-popover`/`text-popover-foreground` theme tokens, already used elsewhere e.g. `Components/Pages/Programs/Registrations.razor`'s registration-standard picker). All 3 native `<select>` elements replaced with this component, each showing a `Button` trigger (current selection + chevron icon) and a `DropdownMenuContent` list of DBC files + "None". Added `port1Open`/`port2Open`/`port3Open` state fields and a `DbcPortLabel(string? dbcId)` helper for the trigger label text. `IsDbcUsedByOtherPort`/`OnPortAssignmentChanged` logic untouched — only the rendering component changed, exactly as scoped.

---

### 2.2 Dashboard — redundant status-filter buttons; reuse the existing colored status counts as the filter

**File:** `Components/Pages/Home/DashboardView.razor` (lines ~24-53)

**Current state (confirmed):** the dashboard header renders **two separate, overlapping pieces of UI** showing the same information:
1. A colored text row (lines 24-36): `circuits: X`, followed by one `<span class="text-status-@status.ToString().ToLower()">` per `CircuitStatus` enum value, each showing `"{status}: {count}"` — already color-coded per status via CSS classes like `text-status-charge`, `text-status-discharging`, etc.
2. A separate row of **chip buttons** (lines 38-49) driven by a `StatusChips` array (`(string Label, CircuitStatus? Status)[]`, defined at line 239/246), each rendered as a clickable `<button>` with its own count recomputed independently, used to set `_statusChip` for filtering.

**The ask:** eliminate the second (redundant) row of chip buttons entirely. Make the **existing colored status-count spans** (item 1 above) themselves clickable, setting `_statusChip` directly — same filtering behavior, no separate button row needed.

**Fix direction:** convert the `<span class="text-status-@status...">` elements (lines 26-35) into `@onclick="() => _statusChip = (status == CircuitStatus.Idle ? null : status)"` (need to decide how "All" is represented — currently the chip array has an explicit `Status = null` entry labeled "All"; the color-coded row has no such "show everything" text today, so either add one or make clicking the already-active filter's status toggle back to `null`). Remove the `StatusChips` array and its rendering block once the color-coded row takes over the filtering role. `CircuitFilter`/`CardSettings`/`ColorsSettings` next to it are unrelated (visibility preferences and card display settings, not status filtering) and should not be touched.

**Resolution — FIXED.** The chip-button row (and the now-unused `StatusChips` array) was removed entirely. The `circuits: @circuits.Count` label became the "All" filter (click to clear `_statusChip`), and each per-status colored span is now clickable — click sets `_statusChip = status`, clicking the already-active one toggles it back to `null` (no separate "All" button needed for that case). Active/inactive/dimmed states use opacity + underline instead of new color classes, so the existing `text-status-*` color-coding is untouched. `_statusChip`'s consumption at the circuit-grid filter (`_inChip`) was not touched — same field, same semantics, just a new way to set it.

---

### 2.3 Scheduler — DBC assignment doesn't reflect the Dashboard's 3-port DBC model

**Files:** `Components/Pages/Programs/SchedulerPage.razor`, `Services/Implementations/SchedulerService.cs:229` (`ExecuteScheduleAsync`)

**Root cause (confirmed, both UI and execution side):**
- `SchedulerPage.razor`'s "New Schedule" dialog still uses the **old single-DBC-per-battery model**: a single `selectedDbcId` bound to a single-select list (lines ~277-306), saved as `CreateScheduleRequest.DbcFileId` (nullable `long`, singular).
- The actual scheduled-run execution, `SchedulerService.ExecuteScheduleAsync` (line 229), confirms this end-to-end:
  ```csharp
  DbcFileRecordDto? dbcFile = null;
  if (entity.DbcFileId.HasValue) { ... dbcFile = dbcResult.Data; }
  ...
  if (dbcFile != null)
  {
      var dbcRes = await handler.TransferDbcFile(dbcFile);   // ← single positional arg = Port 1 only
  }
  ```
  `TransferDbcFile`'s actual signature (from the multi-port DBC work already shipped to the Dashboard) is `TransferDbcFile(DbcFileRecordDto? port1Dbc = null, DbcFileRecordDto? port2Dbc = null, DbcFileRecordDto? port3Dbc = null)`. Calling it with one positional argument means **Port 2 and Port 3 are always `null`**, regardless of what's assigned on the `Battery` entity's `Port1DbcId`/`Port2DbcId`/`Port3DbcId` fields (which the Dashboard's `TransferDialog.razor` already reads/writes correctly via `BServices.UpdatePortAssignmentsAsync`).

**Conclusion:** the Scheduler feature was never updated when the Dashboard moved from "one DBC per battery" to "one DBC per port, three ports." Any battery whose Ports 2/3 have DBCs assigned via the Dashboard will silently lose those assignments when transferred via a Schedule.

**Resolution — FIXED.**
1. `Models/Entities/ProgramSchedule.cs`: `DbcFileId` (singular) replaced with `Port1DbcFileId`/`Port2DbcFileId`/`Port3DbcFileId`.
2. `Models/DTOs/SchedulerDTOs.cs`: same 3-port split applied to `ProgramScheduleDto` (plus `Port1DbcFileName`/`Port2DbcFileName`/`Port3DbcFileName`) and `CreateScheduleRequest`.
3. `SchedulerService.cs`: `GetAllSchedulesAsync` now resolves all 3 DBC names (this incidentally also fixed a separate pre-existing bug — `DbcFileName` was never populated at all before, so the DBC badge in the schedule list always rendered blank/"None" even for schedules that did have one assigned); `CreateScheduleAsync` maps the 3 request fields; `ExecuteScheduleAsync` resolves all 3 DBC files via a small `ResolveDbcAsync` local function and calls `handler.TransferDbcFile(port1Dbc, port2Dbc, port3Dbc)` with all three instead of one.
4. `SchedulerPage.razor`: single DBC radio-list replaced with 3 `<select>` dropdowns (Port 1/2/3), with cross-port exclusivity via a new `IsDbcUsedByOtherPort` helper (mirrors `TransferDialog.IsDbcUsedByOtherPort`). **`OnBatterySelected` now pre-fills `port1DbcId`/`port2DbcId`/`port3DbcId` from `battery.Port1DbcId`/`Port2DbcId`/`Port3DbcId`** — this is the actual "reflect Dashboard changes" fix; the schedule's own selection is still stored independently on the schedule (not written back to the battery), so a schedule can still intentionally diverge from the live Dashboard assignment. The Schedules list's DBC column now shows up to 3 badges (P1/P2/P3) instead of one.
5. Migration `20260714084006_AddScheduleDbcPorts` generated via `dotnet ef migrations add`; applies automatically on next app start (`Program.cs` already calls `Database.Migrate()` at startup).

Build verified clean after each step.

---

### 2.4 Manufacturing Details — Master SW / Communication SW / Primary Serial show wrong values

**Files:** `Services/DecoderService.cs::ManufacturingParameters` (byte decoder), `HardwareSimulator/simulator.py::build_manufacturing_config_response` (test-harness byte builder)

**Root cause (confirmed via byte-offset analysis) — likely a simulator bug, not necessarily a production bug:**

The C# decoder reads three fixed-width **11-byte** string fields (matching the `// 11 bytes` comments already on `ManufacturingDetailDTO`'s own fields):
```csharp
int index = 4;
MasterSWVersion = Encoding.ASCII.GetString(data, index, 11).TrimEnd('\0');
ComSWVersion = Encoding.ASCII.GetString(data, index += 11, 11).TrimEnd('\0');
SecondarySWVersion = Encoding.ASCII.GetString(data, index += 11, 11).TrimEnd('\0');
PrimarySerialNumber = BitConverter.ToUInt32(data.Skip(index += 11).Take(4).Reverse().ToArray(), 0).ToString();
```

The Python simulator's `build_manufacturing_config_response` writes **13-byte** fields instead:
```python
payload += b"V4.26.2022\x00\x00\x00"   # 10 chars + 3 nulls = 13 bytes (decoder expects 11)
payload += b"COM_V1.0\x00\x00\x00\x00\x00"  # 8 chars + 5 nulls = 13 bytes (decoder expects 11)
payload += b"SEC_V2.1\x00\x00\x00\x00\x00"  # 8 chars + 5 nulls = 13 bytes (decoder expects 11)
payload += struct.pack(">I", device.serial_number)   # intended as PrimarySerialNumber
```

Walking the decoder's fixed 11-byte offsets against the simulator's actual 13-byte field boundaries:
- **MasterSWVersion** decodes correctly by coincidence (`"V4.26.2022"` is exactly 10 chars; reading 11 bytes grabs the 10 chars + 1 trailing null, which `TrimEnd('\0')` removes cleanly).
- **ComSWVersion** decodes to a corrupted string: the decoder's window `[15:26)` actually lands 2 bytes into MasterSWVersion's padding tail (2 leftover null bytes) followed by 9 bytes of the real ComSWVersion field — result is something like `"\0\0COM_V1.0"` (**leading** null characters, which `TrimEnd` does not strip, since it only trims from the end).
- **SecondarySWVersion** is similarly misaligned (reads across the ComSWVersion/SecondarySWVersion boundary).
- **PrimarySerialNumber**: by the time the decoder reaches this field, the accumulated 2-byte-per-field drift (3 fields × 2 extra bytes = 6 bytes) means the decoder's `[37:41)` window is still reading from inside the tail of the simulator's SecondarySWVersion string padding — **not the actual serial number bytes at all**. This produces a nonsensical `PrimarySerialNumber` value, which matches exactly what was reported.

**Resolution — FIXED.** Per direction: the Python hardware simulator is a test harness that can contain false/incorrect implementations and is not to be treated as protocol authority; `DecoderService` is the authoritative, unchanged reference. `HardwareSimulator/simulator.py::build_manufacturing_config_response` was rewired to pad each string field to exactly 11 bytes (`"V4.26.2022\x00"`, `"COM_V1.0\x00\x00\x00"`, `"SEC_V2.1\x00\x00\x00"`), matching `ManufacturingParameters`'s fixed 11-byte reads. Verified by replicating the decoder's exact byte-offset logic in a standalone script against the new simulator output — all fields (`MasterSWVersion`, `ComSWVersion`, `SecondarySWVersion`, `PrimarySerialNumber`, `SecondarySerialNumber`) now decode correctly. No changes made to `DecoderService.cs` or any protocol/wire format.

**Bonus fix found during the same pass:** `build_factory_config_response` had the identical class of bug — `DhcpEnabled` and `CircuitType` were packed together as one 2-byte value (`struct.pack(">HHHH", ..., 0x01)`), but `FactoryParameters` reads them as two separate 1-byte fields. This made `DhcpEnabled` always decode as `False` and left `CircuitType` picking up an arbitrary byte rather than a deliberate value. Fixed to `struct.pack(">HHH", ...)` + `struct.pack("BB", dhcp, circuitType)`, verified the same way.

**Follow-up — a second, unrelated bug found after the byte-padding fix.** After the simulator fix, Manufacturing details still showed empty in the UI even though "Download Details" reported success and the DB genuinely had correct values (confirmed by the user directly). Root cause: `DeviceCircuitRepository.GetManufacturingAsync`/`GetFactoryAsync` queried `_context.Devices`/`_context.Circuits` with **no `.AsNoTracking()`**. `AppDbContext`, `IDeviceCircuitServices`, and `IDeviceCircuitRepository` are all registered `Scoped` (`Extensions/ServiceCollectionExtensions.cs`) — and in **Blazor Server, a "Scoped" lifetime spans the entire SignalR circuit (the whole browser-tab session), not a single request**. `DeviceCircuit.razor`'s injected `IDeviceCircuitServices` therefore holds one long-lived `AppDbContext` for the page's whole life. Once that context loaded a `Device` entity once (e.g. on first dialog open, before "Download Details" had ever run), EF Core's change tracker kept that entity **tracked**. `GetManufacturingDetails()`'s actual DB write happens on a separate, freshly-scoped `DbContext` (via `ServiceLocator.GetScoped<IDeviceCircuitServices>()` in `CircuitCommandHandler`) and commits correctly — but every subsequent `GetManufacturingAsync`/`GetFactoryAsync` call on the dialog's long-lived context hit EF Core's identity resolution and got back the **stale, already-tracked in-memory object** instead of re-reading the updated row, regardless of how many times "Download Details" was re-run.

**Resolution — FIXED.** Added `.AsNoTracking()` to both `Device` and `Circuit` queries in `GetManufacturingAsync` and `GetFactoryAsync` (`Repositories/Implementations/DeviceCircuitRepository.cs`), forcing every read to materialize fresh column values from the database instead of reusing a cached tracked instance. No schema/protocol change. This is a **systemic risk pattern worth checking elsewhere** in this repository: any read-only repository method called from a long-lived Blazor component (not just these two) that queries an entity by key without `.AsNoTracking()`, where the same entity can also be updated via a separately-scoped DbContext elsewhere (e.g. `CircuitCommandHandler`'s `ServiceLocator.GetScoped` pattern), is susceptible to the same staleness bug.

---

### 2.5 Factory Details dialog — remove Remote IP Address field

**File:** `Components/UI/Dashboard/DeviceCircuit.razor` (Factory Details tab, ~line 367)

**Context:** earlier in this project's history, `FactoryConfigDetailDTO.ClientRemoteIPAddress` was found to always display empty because the `Device` entity had no backing column for it (fixed via migration `20260703085212_AddDeviceRemoteClientConfig`, adding `ClientRemoteIPAddress`/`TcpClientRemotePort`/`UdpClientRemotePort`/`UdpStoreRemotePort` columns and wiring `UpdateFactoryAsync`/`GetFactoryAsync` to persist/read them).

**New ask:** rather than continuing to show it now that it's populated, **remove the Remote IP Address row from the Factory Details display entirely** — decided it isn't useful/relevant information for operators to see in this dialog.

**Fix direction:** delete the `@factoryDetailDTO.ClientRemoteIPAddress` display block from the Factory Details tab markup in `DeviceCircuit.razor`. This is purely a UI-removal — **no backend change**: the DB columns, `UpdateFactoryAsync`/`GetFactoryAsync` mapping, and the underlying `FactoryConfigDetailDTO.ClientRemoteIPAddress` property should all stay as-is (the data is still legitimately captured from the device; it's just not worth surfacing in this particular dialog). Note: `TcpClientRemotePort`/`UdpClientRemotePort` are separate display rows — the ask specifically names "Remote IP Address," so those port fields are presumably staying unless clarified otherwise.

**Resolution — FIXED.** The "Remote IP Address" label + `@factoryDetailDTO.ClientRemoteIPAddress` value block removed from the Factory Details tab. No other changes — DB columns, `UpdateFactoryAsync`/`GetFactoryAsync`, and `TcpClientRemotePort`/`UdpClientRemotePort` rows all untouched, exactly as scoped.

---

### 2.6 DeviceCircuit dialog — Digital Inputs (IOStatus) view has a CSS/layout issue

**File:** `Components/UI/Dashboard/DeviceCircuit.razor` (DigitalSignals tab)

**Root cause (confirmed earlier in this project's investigation, not re-verified this pass but high-confidence):** the DigitalSignals tab markup uses **Bootstrap-style grid classes** (`<div class="row g-3">`) inside an app that is styled entirely with **Tailwind CSS** (every other component in this codebase uses `flex`/`grid-cols-N`/`gap-N` utility classes, custom CSS-variable-driven colors like `hsl(var(--border))`, etc. — no Bootstrap CSS is loaded anywhere in the app). Bootstrap's `row`/`g-3` classes have **no effect at all** without Bootstrap's CSS framework loaded, so the digital-signal light/label elements inside that div render unstyled and stack/wrap incorrectly instead of forming the intended grid.

**Fix direction:** replace the Bootstrap `row g-3` wrapper (and any other Bootstrap-only classes in that same tab, e.g. potential `col-*` classes on the signal items) with the equivalent Tailwind layout already used elsewhere in this file (e.g. `class="grid grid-cols-4 gap-3"` or `class="flex flex-wrap gap-3"`, matching the visual density of the signal-light/signal-label pairs). Pure CSS-class swap — the underlying data binding (`Circuit.RealTime.IOStatus`, already fixed earlier this project to actually populate) does not need to change.

**Resolution — FIXED.** Re-verification during the fix turned up a bigger gap than originally scoped: not just the `row g-3` wrapper, but *every* custom class in this block (`board-card`, `board-header`, `section-title`, `signals-grid`, `signal-indicator`, `signal-light`, `signal-label`) had **zero CSS definitions anywhere in the repo** (confirmed via repo-wide search) — the whole tab was completely unstyled, not just the grid. Rewired to reuse this file's own already-imported `Card`/`CardHeader`/`CardTitle`/`CardContent` components (same ones used in the Overview tab) instead of hand-rolled `board-card`/`board-header` divs, and replaced the signal grid/light/label with plain Tailwind utility classes (`grid grid-cols-4 sm:grid-cols-6 gap-2`, a small `rounded-full` dot colored `bg-green-500`/`bg-muted-foreground/30` for active/inactive, `text-[10px] font-mono` label). The Primary-vs-other-board 1:2 width ratio from the old `col-md-4`/`col-md-8` was preserved via `md:col-span-1`/`md:col-span-2` on a `grid-cols-3` container. Data binding (`Circuit.RealTime.IOStatus`) untouched.

---

### 2.7 Calibration — Temperature section reference-input symbol should read "Ohms," not "°C"

**File:** `Components/UI/Calibration/Calibration.razor`, `RenderTemperatureCalibrationFormAllVisible()` (lines 640-675)

**Current state (confirmed):**
```csharp
label: "1. Low Ref (°C)",   // line 648
...
label: "2. High Ref (°C)",  // line 661
```

**The ask:** these two reference-input labels are wrong — the temperature sensor is apparently calibrated via known **resistance** reference points (a common approach for thermistor/RTD-based temperature sensors: you supply known resistance values corresponding to known temperatures, not the temperature directly), so the unit symbol shown should be **"Ohms"** (or "Ω"), not "°C." Explicitly scoped: **change only the symbol text on these two labels — nothing else** (not the underlying `min`/`max` range, not the calibration math in `CalculateTemperatureCalibrationValues()`, not any other calibration mode).

**Fix direction:** change `"1. Low Ref (°C)"` → `"1. Low Ref (Ohms)"` and `"2. High Ref (°C)"` → `"2. High Ref (Ohms)"` at lines 648 and 661. Do not touch the `min: -40, max: 200` bounds on the same calls even though that numeric range reads oddly for a resistance value in Ohms — the user explicitly said "rest do not change any," so that range is out of scope for this fix and worth a separate conversation if it also needs correcting.

**Resolution — FIXED.** Both label strings changed exactly as scoped — nothing else in `RenderTemperatureCalibrationFormAllVisible()` touched (`min`/`max` bounds, calibration math, live-readout label at line 159 all untouched).

**Note for later:** the *live readout* label at line 159, `<div class="text-muted-foreground mb-0.5">Temperature (°C)</div>`, is a different piece of UI (the actual measured temperature during calibration, not a reference-input label) and was **not** mentioned in the ask — leave it as "°C," which is correct for a live temperature display.

---

## Summary table

| # | Item | Scope | DB change? | Confidence |
|---|---|---|---|---|
| 1 | Store-log remark attribution | New feature, design decision needed (embedded vs. AuditLog) | No | Design proposed, needs decision |
| 2.1 | TransferDialog DBC dropdown theme | **FIXED** — native `<select>`s replaced with existing `DropdownMenu` component family | No | Verified via clean build |
| 2.2 | Redundant status filter chips | **FIXED** — chip row + `StatusChips` array removed; colored counts now clickable | No | Verified via clean build |
| 2.3 | Scheduler DBC not port-aware | **FIXED** — entity/DTOs/service/UI rewired to 3 ports; battery-port pre-fill added; migration `20260714084006_AddScheduleDbcPorts` | **Yes (applied)** | Verified via clean build after each step |
| 2.4 | Manufacturing Details wrong values | **FIXED** — simulator byte-padding rewired to match decoder (11-byte fields); Factory dhcp/circuitType byte-split bug found and fixed the same pass | No | Verified via decoder-replica byte simulation |
| 2.5 | Factory Details remove Remote IP | **FIXED** — display row removed, no backend change | No | Verified via clean build |
| 2.6 | Digital Inputs CSS issue | **FIXED** — turned out to be a fully unstyled block (not just the grid wrapper); rewired to reuse `Card`/`CardHeader`/`CardTitle`/`CardContent` + Tailwind utilities | No | Verified via clean build |
| 2.7 | Calibration Temperature symbol | **FIXED** — 2 label strings changed ("°C" → "Ohms"), nothing else touched | No | Verified via clean build |
