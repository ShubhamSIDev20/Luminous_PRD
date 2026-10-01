# PRODUCER Operator Feature — Implementation Plan

> **Status: ✅ FULLY IMPLEMENTED** — v0.6 (May 2026)  
> All 11 implementation steps completed. Build verified: 0 errors, 0 warnings.  
> Phase 2 (offline viewer) also completed — see §15 below.

> **Feature:** "Program inside Program" — a new `PRODUCER` operator that embeds another
> program's steps inline, with a viewer, a hardware-transfer expansion, and full session
> persistence for the referenced data.

---

## 1. What We Are Building (Plain English)

| Touchpoint | Behaviour |
|---|---|
| **Program Editor — Operator dropdown** | New `PRODUCER` (code 20) operator appears alongside CC Chg, TABLE, etc. |
| **Program Editor — Nominal Values cell** | When `PRODUCER` is selected, show a **dropdown** listing all saved program names (same UX as TABLE shows a file dropdown) |
| **Program Editor — Eye / View button** | Next to the dropdown, an 👁 **View** button opens an embedded `ProgramViewer` dialog showing the referenced program's steps (read-only) |
| **Validation** | The selected program name must actually exist in the DB; PRODUCER is validated like TABLE validates its file name |
| **Hardware Transfer (`SetProgramAsync`)** | Before converting steps to bytes, the PRODUCER step is **expanded inline**: skip the first step (SET) and last step (STO) of the referenced program, and insert all middle steps into the outer program. Step numbers are renumbered. |
| **Session file dump (`StartProgram`)** | When a session starts, the full `ProgramDTO` of every referenced PRODUCER program is serialised into the session `.db` file so the session is self-contained |
| **TABLE operator — session dump** | Same idea: the raw content of any TABLE file referenced in a step is captured at session-start time and stored in the session `.db` |
| **DataViewer** | Already reads `Program` from session; with the new config keys it will also be able to show which producer sub-programs and table files were used |

---

## 2. New Operator Constant: PRODUCER = 20

**File:** `Components/UI/Program/OperatorConstants.cs`

### 2.1 Add the constant
```csharp
public const byte PRODUCER = 20;   // add after CV_DCHG = 19
```

### 2.2 Add to `Operators` list
```csharp
new() { Code = PRODUCER, Name = "PRODUCER", Color = "control", Category = "control" },
```

### 2.3 Add to `ToName()` switch
```csharp
PRODUCER => nameof(PRODUCER),
```

### 2.4 Add to `LimitActionAllowed`
```csharp
[PRODUCER] = false,   // No limit-action pairs on a PRODUCER step
```

### 2.5 Add to `NominalConfig.Configs`
```csharp
[OperatorConstants.PRODUCER] = new()
{
    Fields = new() { new() { Label = "Program", Unit = "", Placeholder = "select program" } }
},
```
> Note: We leave `AllowCustom = false` so the standard field config path is taken — but we will
> intercept it in `NominalValuesEditor` (just like TABLE) before reaching the generic field renderer.

---

## 3. `NominalValuesEditor.razor` — Add PRODUCER Branch

**File:** `Components/UI/Program/NominalValuesEditor.razor`

### 3.1 New parameters
```csharp
[Parameter] public List<ProgramDTO> AllPrograms { get; set; } = new();
[Parameter] public EventCallback<string> OnViewProducer { get; set; }
```

### 3.2 New branch in markup (insert between TABLE branch and the generic `Fields.Any()` branch)
```razor
else if (OperatorCode == OperatorConstants.PRODUCER)
{
    <div class="flex items-center gap-2">
        <select @onchange="@((e) => UpdateFieldValue(0, e.Value?.ToString() ?? ""))"
                class="px-2 py-1 rounded text-xs font-mono border border-border
                       bg-background focus:outline-none focus:ring-1 focus:ring-primary">
            <option value="">Select Program</option>
            @foreach (var prog in AllPrograms)
            {
                <option value="@prog.ProgramName"
                        selected="@(Values.FirstOrDefault() == prog.ProgramName)">
                    @prog.ProgramName
                </option>
            }
        </select>
        @if (!string.IsNullOrEmpty(Values.FirstOrDefault()))
        {
            <button @onclick="@(() => OnViewProducer.InvokeAsync(Values.FirstOrDefault()))"
                    class="p-1 rounded hover:bg-secondary transition-colors"
                    title="View producer program">
                <Blazicon Svg="Lucide.Eye" class="w-4 h-4 text-primary" />
            </button>
        }
    </div>
}
```

> **⚠️ Impact:** Only adds a branch — no existing TABLE or generic path is changed.

---

## 4. `StepRow.razor` — Thread through AllPrograms & OnViewProducer

**File:** `Components/UI/Program/StepRow.razor`

### 4.1 New parameters (in `@code`)
```csharp
[Parameter] public List<ProgramDTO> AllPrograms { get; set; } = new();
[Parameter] public EventCallback<string> OnViewProducer { get; set; }
```

### 4.2 Update `<NominalValuesEditor>` call
```razor
<NominalValuesEditor
    Values="@Step.NominalValues"
    OperatorCode="@Step.OperatorCode"
    OnAdd="@OnAddNominalValue"
    OnRemove="@OnRemoveNominalValue"
    OnUpdate="@OnUpdateNominalValue"
    Disabled="@IsNominalDisabled()"
    GlobalVariables="@GlobalVariables"
    AllLabels="@AllLabels"
    OnViewTable="@OnViewTable"
    AllPrograms="@AllPrograms"
    OnViewProducer="@OnViewProducer" />   ← ADD THESE TWO
```

> **⚠️ Impact:** Only adds two new parameters to an existing component call. The existing
> `OnViewTable` private stub is unchanged.

---

## 5. `ProgramEditor.razor` — Load Programs, Handle Viewer Dialog, Pass Down

**File:** `Components/Pages/Programs/ProgramEditor.razor`

### 5.1 New state fields (in `@code`)
```csharp
private List<ProgramDTO> allPrograms = new();
private bool showProducerDialog = false;
private List<StepModel> producerViewerSteps = new();
private string producerViewerName = string.Empty;
```

### 5.2 Load programs in `InitializeProgram()`
After loading the current program, also load the programs list:
```csharp
var progsResponse = await ProgramService.GetAllProgramsAsync();
if (progsResponse.Success && progsResponse.Data != null)
    allPrograms = progsResponse.Data
        .Where(p => p.ProgramId != id)   // exclude the program being edited
        .ToList();
StateHasChanged();
```
> This ensures the PRODUCER dropdown shows other programs. Exclude the current program to avoid
> self-reference.

### 5.3 New handler
```csharp
private void HandleViewProducer(string programName)
{
    var prog = allPrograms.FirstOrDefault(p => p.ProgramName == programName);
    if (prog != null)
    {
        producerViewerSteps = prog.ProgramStepModel;
        producerViewerName = prog.ProgramName;
        showProducerDialog = true;
        StateHasChanged();
    }
}
```

### 5.4 Pass new props to every `<StepRow>`
```razor
<StepRow ...existing props...
    AllPrograms="@allPrograms"
    OnViewProducer="@HandleViewProducer" />
```

### 5.5 Add Producer Viewer Dialog (next to the existing save dialog)
```razor
<Dialog @bind-Open="@showProducerDialog">
    <DialogContent Size="DialogSize.XLarge">
        <DialogHeader>
            <DialogTitle>Producer Program — @producerViewerName</DialogTitle>
        </DialogHeader>
        <ProgramViewer InitialSteps="@producerViewerSteps"
                       InitialProgramName="@producerViewerName" />
    </DialogContent>
</Dialog>
```

### 5.6 Validation update (in `ValidateStep` — PRODUCER nominal validation)
Inside `ValidateStep(StepModel step)`:
```csharp
// ── PRODUCER nominal validation ──────────────────────────────────────────
if (step.OperatorCode == OperatorConstants.PRODUCER)
{
    var programName = step.NominalValues.FirstOrDefault();
    if (string.IsNullOrWhiteSpace(programName))
    {
        errors.Add(new ValidationError
        {
            StepId = step.Id,
            StepNumber = step.StepNumber,
            Field = "NominalValues",
            Message = "PRODUCER: select a program"
        });
    }
    else if (!allPrograms.Any(p => p.ProgramName == programName))
    {
        errors.Add(new ValidationError
        {
            StepId = step.Id,
            StepNumber = step.StepNumber,
            Field = "NominalValues",
            Message = $"PRODUCER: program '{programName}' not found"
        });
    }
}
```

> **⚠️ Impact check:** The existing `ValidateProgram()` loop iterates all steps and calls
> `ValidateStep`. The PRODUCER check adds a block for `step.OperatorCode == PRODUCER`. All
> other operator paths are untouched.

---

## 6. `ProgramBuilder.cs` — `ExpandProducerSteps()` Helper

**File:** `Services/ProgramBuilder.cs`

Add this static method to the `ProgramBuilder` class:

```csharp
/// <summary>
/// Replaces each PRODUCER step with the inner steps of the referenced program,
/// skipping that program's first (SET) and last (STO) steps.
/// Re-numbers all steps after expansion.
/// </summary>
/// <param name="steps">The outer program's steps (may contain PRODUCER steps).</param>
/// <param name="resolvedPrograms">
///   Map of program name → steps, pre-loaded by the caller.
///   If a referenced program is missing from this map the PRODUCER step is silently skipped.
/// </param>
public static List<StepModel> ExpandProducerSteps(
    List<StepModel> steps,
    Dictionary<string, List<StepModel>> resolvedPrograms)
{
    var result = new List<StepModel>();

    foreach (var step in steps)
    {
        if (step.OperatorCode == OperatorConstants.PRODUCER)
        {
            var programName = step.NominalValues.FirstOrDefault();

            if (!string.IsNullOrEmpty(programName) &&
                resolvedPrograms.TryGetValue(programName, out var innerSteps) &&
                innerSteps.Count > 2)           // must have at least SET + 1 body + STO
            {
                // Take everything except first (SET) and last (STO)
                var middle = innerSteps
                    .Skip(1)
                    .Take(innerSteps.Count - 2)
                    .Select(s => new StepModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        OperatorCode = s.OperatorCode,
                        Label = s.Label,
                        Comment = string.IsNullOrEmpty(s.Comment)
                            ? $"[{programName}]"
                            : $"[{programName}] {s.Comment}",
                        NominalValues = new List<string>(s.NominalValues),
                        Limits = new List<string>(s.Limits),
                        Actions = new List<string>(s.Actions),
                        Registrations = new List<string>(s.Registrations)
                    })
                    .ToList();

                result.AddRange(middle);
            }
            // else: PRODUCER with unknown program name → skip (validation should have caught this)
        }
        else
        {
            result.Add(step);
        }
    }

    // Re-number every step sequentially
    for (int i = 0; i < result.Count; i++)
        result[i].StepNumber = i + 1;

    return result;
}
```

> **⚠️ Impact:** Pure addition. `ExpandProducerSteps` is not called by anything yet — that
> happens in DecoderService and CircuitCommandHandler (Steps 7 and 8).

---

## 7. `DecoderService.cs` — Expand Before Converting to Bytes

**File:** `Services/DecoderService.cs`

### 7.1 Overload `ConvertProgramIntoBytesPackets`

Keep the existing signature as is. Add an overload that accepts resolved programs:

```csharp
/// <summary>
/// Converts program steps to hardware byte packets.
/// PRODUCER steps are automatically expanded using resolvedPrograms.
/// </summary>
public static List<byte[]> ConvertProgramIntoBytesPackets(
    List<StepModel> programStepsDTO,
    Dictionary<string, List<StepModel>>? resolvedPrograms)
{
    // Expand PRODUCER steps before encoding
    var expandedSteps = resolvedPrograms != null && resolvedPrograms.Count > 0
        ? ProgramBuilder.ExpandProducerSteps(programStepsDTO, resolvedPrograms)
        : programStepsDTO;

    return ConvertProgramIntoBytesPackets(expandedSteps);   // calls existing method
}
```

> **⚠️ Impact:** The original `ConvertProgramIntoBytesPackets(List<StepModel>)` is **not changed**.
> We add a second overload that pre-processes and then delegates. `ConvertProgramBackup` is also
> unaffected.

---

## 8. `CircuitCommandHandler.cs` — Load Producer Programs Before Transfer & Session

**File:** `Services/Implementations/CircuitCommandHandler.cs`

### 8.1 Helper: collect resolved programs
Add a private helper method:

```csharp
private async Task<Dictionary<string, List<StepModel>>> ResolveProducerProgramsAsync(
    List<StepModel> steps)
{
    var result = new Dictionary<string, List<StepModel>>(StringComparer.OrdinalIgnoreCase);

    var producerNames = steps
        .Where(s => s.OperatorCode == OperatorConstants.PRODUCER)
        .Select(s => s.NominalValues.FirstOrDefault())
        .Where(n => !string.IsNullOrEmpty(n))
        .Distinct()
        .ToList();

    if (!producerNames.Any())
        return result;

    using var scope = ServiceLocator.CreateScope();
    var programService = scope.ServiceProvider.GetRequiredService<IProgramServices>();

    foreach (var name in producerNames)
    {
        var response = await programService.GetProgramByNameAsync(name);
        if (response.Success && response.Data != null)
            result[name] = response.Data.ProgramStepModel;
    }

    return result;
}
```

> **Note:** This requires `IProgramServices` to expose `GetProgramByNameAsync(string name)`.
> If not already present, add it (see §8.3 below).

### 8.2 Update `SetProgramAsync`
```csharp
public async Task<CommonResponse<bool>> SetProgramAsync(ProgramDTO ProgramDto)
{
    if (RealTime?.RealTimeRecord?.ProgramStatus == ProgramRunningStatus.Running)
        return CommonResponse<bool>.Fail("Program is running, cannot SetProgram.");

    var steps = ProgramDto.ProgramStepModel;

    // ── NEW: resolve PRODUCER references ─────────────────────────────────
    var resolvedPrograms = await ResolveProducerProgramsAsync(steps);

    // ── Use the overload that expands PRODUCER steps ──────────────────────
    List<byte[]> packets = DecoderService.ConvertProgramIntoBytesPackets(steps, resolvedPrograms);

    // ... rest of method unchanged ...
}
```

### 8.3 Update `StartProgram` — collect ProducerPrograms + TableFileData for session
Inside `StartProgram()`, before `InsertSessionAsync`:

```csharp
// ── Collect PRODUCER sub-programs ────────────────────────────────────────
var resolvedProducers = await ResolveProducerProgramsAsync(Program.ProgramStepModel);
List<ProgramDTO>? producerPrograms = resolvedProducers.Count > 0
    ? resolvedProducers.Values
        .Select(steps =>
        {
            var name = resolvedProducers.First(kv => kv.Value == steps).Key;
            return new ProgramDTO { ProgramName = name, ProgramStepModel = steps };
        })
        .ToList()
    : null;

// ── Collect TABLE file data ───────────────────────────────────────────────
Dictionary<string, string[]>? tableFileData = null;
var tableSteps = Program.ProgramStepModel
    .Where(s => s.OperatorCode == OperatorConstants.TABLE)
    .SelectMany(s => s.NominalValues)
    .Where(n => !string.IsNullOrEmpty(n))
    .Distinct()
    .ToList();

if (tableSteps.Any())
{
    tableFileData = new();
    foreach (var fileName in tableSteps)
    {
        var fileResult = FileManagerService.ReadFileLines(fileName);
        if (fileResult.Success && fileResult.Data != null)
            tableFileData[fileName] = fileResult.Data;
    }
}

await StoreService.Service.InsertSessionAsync(new SessionRequest
{
    filePath      = Session.SessionFilePath,
    Program       = Program,
    Battery       = Battery,
    dbcDatabase   = Session.dbcDatabse,
    ProducerPrograms = producerPrograms,    // NEW
    TableFileData    = tableFileData        // NEW
});
```

### 8.4 New method on IProgramServices (if not already present)
`IProgramServices` needs:
```csharp
Task<CommonResponse<ProgramDTO>> GetProgramByNameAsync(string name);
```
Implementation in `ProgramServices.cs`:
```csharp
public async Task<CommonResponse<ProgramDTO>> GetProgramByNameAsync(string name)
{
    var prog = await _db.Programs
        .FirstOrDefaultAsync(p => p.ProgramName == name && !p.IsDeleted);
    if (prog == null)
        return CommonResponse<ProgramDTO>.Fail($"Program '{name}' not found.");
    return CommonResponse<ProgramDTO>.Ok(MapToDto(prog));
}
```

---

## 9. `RequestDTOs.cs` — Extend `SessionRequest`

**File:** `Models/DTOs/RequestDTOs.cs`

```csharp
public class SessionRequest
{
    public string filePath { get; set; } = string.Empty;
    public ProgramDTO? Program { get; set; }
    public BatteryDTO? Battery { get; set; }
    public DbcDatabase? dbcDatabase { get; set; }

    // NEW ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full ProgramDTO for every program referenced by a PRODUCER step.
    /// Stored in session DB so the viewer remains self-contained.
    /// </summary>
    public List<ProgramDTO>? ProducerPrograms { get; set; }

    /// <summary>
    /// Raw content of every TABLE file referenced in the program.
    /// Key = file name (as stored in NominalValues). Value = file lines.
    /// </summary>
    public Dictionary<string, string[]>? TableFileData { get; set; }
}
```

> **⚠️ Impact:** Only adds two nullable properties. All existing callers of `SessionRequest`
> that don't set these fields will default to `null` — no breaking changes.

---

## 10. `SqliteBulkDatabaseManager.cs` — Persist & Retrieve New Session Data

**File:** `Services/Implementations/SqliteBulkDatabaseManager.cs`

### 10.1 `InsertSessionAsync` — save new data
After the existing Battery / DBC save block, add:

```csharp
// ── PRODUCER sub-programs ─────────────────────────────────────────────────
if (session.ProducerPrograms?.Any() == true)
{
    foreach (var prog in session.ProducerPrograms)
    {
        ctx.configurationEntities.Add(new ConfigurationEntity
        {
            Key       = $"ProducerProgram_{prog.ProgramName}",
            Value     = JsonConvert.SerializeObject(prog),
            CreatedAt = now, UpdatedAt = now,
            IsDeleted = false,
            CreatedBy = user, UpdatedBy = user
        });
    }
}

// ── TABLE file data ───────────────────────────────────────────────────────
if (session.TableFileData?.Any() == true)
{
    foreach (var kv in session.TableFileData)
    {
        ctx.configurationEntities.Add(new ConfigurationEntity
        {
            Key       = $"TableFile_{kv.Key}",
            Value     = JsonConvert.SerializeObject(kv.Value),
            CreatedAt = now, UpdatedAt = now,
            IsDeleted = false,
            CreatedBy = user, UpdatedBy = user
        });
    }
}
```

### 10.2 `GetSessionAsync` — load new data
```csharp
public async Task<SessionRequest?> GetSessionAsync(string filePath)
{
    // ...existing code...
    var program          = await FetchProgramAsync(ctx);
    var battery          = await FetchBatteryAsync(ctx);
    var dbcDatabase      = await FetchDbcDatabaseAsync(ctx);
    var producerPrograms = await FetchProducerProgramsAsync(ctx);   // NEW
    var tableFileData    = await FetchTableFileDataAsync(ctx);       // NEW

    return new SessionRequest
    {
        filePath         = filePath,
        Program          = program,
        Battery          = battery,
        dbcDatabase      = dbcDatabase,
        ProducerPrograms = producerPrograms,    // NEW
        TableFileData    = tableFileData        // NEW
    };
}
```

### 10.3 New private fetch methods
```csharp
private async Task<List<ProgramDTO>?> FetchProducerProgramsAsync(SqliteDbContext ctx)
{
    var entries = await ctx.configurationEntities
        .AsNoTracking()
        .Where(e => e.Key.StartsWith("ProducerProgram_"))
        .ToListAsync();

    if (!entries.Any()) return null;

    return entries
        .Select(e => JsonConvert.DeserializeObject<ProgramDTO>(e.Value))
        .Where(p => p != null)
        .ToList()!;
}

private async Task<Dictionary<string, string[]>?> FetchTableFileDataAsync(SqliteDbContext ctx)
{
    var entries = await ctx.configurationEntities
        .AsNoTracking()
        .Where(e => e.Key.StartsWith("TableFile_"))
        .ToListAsync();

    if (!entries.Any()) return null;

    return entries.ToDictionary(
        e => e.Key["TableFile_".Length..],
        e => JsonConvert.DeserializeObject<string[]>(e.Value) ?? Array.Empty<string>()
    );
}
```

---

## 11. Cross-Cutting: What Changes Impact What

| Changed File | Who is Affected |
|---|---|
| `OperatorConstants.cs` — adds `PRODUCER=20` | `OperatorSelect.razor` (auto-shows it in dropdown), `OperatorBadge.razor` (auto-shows badge), `ProgramBuilder`, `DecoderService`, `ValidationHelper` |
| `NominalValuesEditor.razor` — adds PRODUCER branch | `StepRow` (uses it), `ProgramViewer` does NOT use `NominalValuesEditor` — only displays, no impact |
| `StepRow.razor` — adds `AllPrograms` + `OnViewProducer` | `ProgramEditor` (must now pass these two props) |
| `ProgramEditor.razor` — loads allPrograms, handles dialog | Standalone page; no other component depends on it |
| `ProgramBuilder.cs` — adds `ExpandProducerSteps` | `DecoderService` (new overload calls it); `ConvertProgramBackup` is **NOT** changed |
| `DecoderService.cs` — adds overload | `CircuitCommandHandler.SetProgramAsync` switches to new overload |
| `CircuitCommandHandler.cs` | Standalone handler; exposes the same `ICircuitCommandHandler` interface; adds private helper only |
| `RequestDTOs.cs` — extends `SessionRequest` | `SqliteBulkDatabaseManager` (reads new props); `CircuitCommandHandler` (writes new props) |
| `SqliteBulkDatabaseManager.cs` | `GetSessionAsync` consumers (BmsDashboard, DataViewer) get richer data — backwards compatible since new fields are nullable |

---

## 12. Step-by-Step Implementation Order

Do these in this order to avoid compile errors at each step:

1. **`OperatorConstants.cs`** — add PRODUCER constant (no deps yet)
2. **`RequestDTOs.cs`** — extend `SessionRequest` (no deps yet)
3. **`ProgramBuilder.cs`** — add `ExpandProducerSteps` (depends only on `OperatorConstants`)
4. **`DecoderService.cs`** — add new overload (depends on `ProgramBuilder.ExpandProducerSteps`)
5. **`IProgramServices` interface + `ProgramServices.cs`** — add `GetProgramByNameAsync`
6. **`CircuitCommandHandler.cs`** — add helper + update `SetProgramAsync` + update `StartProgram`
7. **`SqliteBulkDatabaseManager.cs`** — add new fetch/store logic (depends on `RequestDTOs` change)
8. **`NominalValuesEditor.razor`** — add PRODUCER branch + new params
9. **`StepRow.razor`** — add new params + forward them
10. **`ProgramEditor.razor`** — load programs, handle dialog, pass props, add validation
11. **Test end-to-end:**
    - Create ProgramB with some steps
    - In ProgramA add a PRODUCER step → select ProgramB → click Eye to verify viewer
    - Transfer ProgramA → verify hardware bytes contain ProgramB's inner steps
    - Start a session → open the `.db` file → verify ProducerProgram_ config key exists

---

## 13. Validation Logic Summary

| Operator | Nominal Validation |
|---|---|
| TABLE | `FileManagerService.ValidateFile(value)` in `ValidationHelper.ValidateNominal` |
| **PRODUCER** | Check `allPrograms.Any(p => p.ProgramName == value)` **directly in ProgramEditor** (because the program list is only available at the editor level, not in the static `ValidationHelper`). The error is added to the `errors` list like any other `ValidationError`. |

> This is a slight asymmetry vs TABLE (which is validated inside `ValidationHelper`), but it
> is the same pattern: the editor owns the list of valid choices, so validation happens there.

---

## 14. Edge Cases & Notes

| Case | Handling |
|---|---|
| PRODUCER references a program that references another PRODUCER (nested) | Currently **not expanded** — only one level of expansion is done. If nested support is needed, `ExpandProducerSteps` can be called recursively. |
| PRODUCER references the program being edited (self-reference) | Prevented by filtering `p.ProgramId != id` in the programs loaded for the dropdown. |
| Referenced program has fewer than 3 steps (SET + STO only) | `innerSteps.Count > 2` guard in `ExpandProducerSteps` → PRODUCER step silently produces 0 inner steps. Validation should catch this as an error (add a check: referenced program must have at least 3 steps). |
| Renumbering breaks GOTO step-number actions | GOTO actions that reference **labels** work fine after renumbering. GOTO actions that reference **step numbers** (hardcoded integers) may be wrong after expansion. Document this constraint: prefer labels over step numbers in any program that may be used as a PRODUCER sub-program. |
| Session opened on a machine without the TABLE file | The raw file content is now embedded in the session DB (as `TableFile_<name>` config key) so the viewer remains self-contained. |

---

*End of plan. All implementation decisions are reversible; none of the existing operator codes or byte-build paths are modified — only new operators and new code paths are added.*

---

## 15. Phase 2 — Offline Session Viewer (Implemented v0.6)

This phase extended the session data viewer (BmsDashboard, BmsProgramTable) and the Reports page to consume the PRODUCER/TABLE data stored in the session SQLite file.

### 15.1 Files Changed

| File | Change |
|---|---|
| `Components/UI/DataViewer/BmsDashboard.razor` | `LoadAsync()` now reads `SessionData.ProducerPrograms` and `SessionData.TableFileData`; passes both to `BmsProgramTable` |
| `Components/UI/DataViewer/BmsProgramTable.razor` | Two new parameters (`ProducerPrograms`, `TableFileData`); Nominal Values column renders PRODUCER/TABLE rows with eye/view buttons; two inline modals (sub-program steps viewer + TABLE file content viewer) |
| `Components/Pages/Reports/Reports.razor` | Injected `ISqliteBulkDatabaseManager`; added ℹ Info button per session row; `OpenSessionPanel()` loads session metadata on demand; session properties modal shows PRODUCER sub-programs and TABLE files used |

### 15.2 PRODUCER Eye Button — Data Flow (Offline)

```
BmsProgramTable renders PRODUCER step row
    │
    User clicks 👁 button
    │
OpenProducerViewer(programName)
    │
    ProducerPrograms.FirstOrDefault(p => p.ProgramName == name)
    │                    ↑ loaded from session DB — NOT live program DB
    _producerDialogSteps = prog.ProgramStepModel
    _showProducerDialog  = true
    │
Inline modal renders sub-program steps table (read-only)
```

### 15.3 TABLE Eye Button — Data Flow (Offline)

```
BmsProgramTable renders TABLE step row
    │
    User clicks 📄 button
    │
OpenTableViewer(fileName)
    │
    TableFileData[fileName]
    │          ↑ loaded from session DB — NOT filesystem
    _tableDialogLines = lines
    _showTableDialog  = true
    │
Inline modal renders file content as pre-formatted text
```

### 15.4 Session Properties Panel — Data Flow

```
Reports page: User clicks ℹ on session row
    │
OpenSessionPanel(session)
    │
dbManager.GetSessionAsync(session.SessionFilePath)
    │
    → ProducerPrograms: List<ProgramDTO>
    → TableFileData:    Dictionary<string, string[]>
    │
Modal renders:
    - Basic info (program, battery, device/circuit, times, duration)
    - PRODUCER sub-programs (name + step count each)
    - TABLE files (filename + line count each)
    - "Open Full Session" button → TabService.AddTab(BmsDashboard)
```

### 15.5 Backward Compatibility

Sessions recorded before v0.6 (no `ProducerProgram_` / `TableFile_` keys in their SQLite file) are handled gracefully: `GetSessionAsync` returns empty lists for both fields, and the eye buttons simply do not appear (no programs/files in `ProducerPrograms` / `TableFileData` to match against).
