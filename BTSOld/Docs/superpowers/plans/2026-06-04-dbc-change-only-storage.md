# DBC Change-Only Array Storage — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut DBC data storage from one full JSON dict per row to one index-aligned array per *change*, null everywhere else — with forward-fill on read.

**Architecture:** Three layers of change: (1) write the canonical signal-name order once at session start; (2) detect change in `EnqueueForStore` and mark only the first record of each changed batch; (3) convert marked records to compact arrays in `InsertRecordAsync`, then forward-fill on every read path. Format sniffing (`[` vs `{`) handles old session files transparently.

**Tech Stack:** C# / EF Core / SQLite / Newtonsoft.Json / Blazor Server

---

## File Map

| File | What changes |
|---|---|
| `Services/Implementations/CircuitCommandHandler.cs` | Add `_lastStoredDbcRecord` field; rewrite DBC block in `EnqueueForStore` |
| `Services/Implementations/SqliteBulkDatabaseManager.cs` | `InsertSessionAsync` writes `DbcSignalNames`; `InsertRecordAsync` converts dict→array; add `FetchDbcSignalNamesAsync` + `ApplyDbcForwardFill`; update three read methods |

No model changes. No schema changes. No migration needed.

---

## Task 1 — Change detection in `EnqueueForStore`

**Files:**
- Modify: `Services/Implementations/CircuitCommandHandler.cs`

### Context

`EnqueueForStore` is at line ~226. It currently serializes `dbcRecord` into **every** record in the batch:

```csharp
public void EnqueueForStore(recordStoreRequest dto, Dictionary<string, object>? dbcRecord)
{
    if(dbcRecord != null)
        foreach (var r in dto.RealStoreRecord)
        {
            r.dbcValues = JsonConvert.SerializeObject(dbcRecord);
        }
    _StoreQueue.Writer.TryWrite(dto);
    ...
}
```

The class already has `public DbcRecord dbcData { get; set; } = new();` (line 63). We add one private field next to it.

---

- [ ] **Step 1: Add `_lastStoredDbcRecord` field**

In `CircuitCommandHandler.cs`, directly below the `dbcData` property (line 63), add:

```csharp
private Dictionary<string, object>? _lastStoredDbcRecord;
```

- [ ] **Step 2: Add private equality helper**

Add this private static method anywhere inside the `CircuitCommandHandler` class (e.g. just before `#endregion` of the DataStore region):

```csharp
private static bool DbcRecordEquals(Dictionary<string, object>? a, Dictionary<string, object>? b)
{
    if (a == null && b == null) return true;
    if (a == null || b == null) return false;
    if (a.Count != b.Count) return false;
    foreach (var kv in a)
    {
        if (!b.TryGetValue(kv.Key, out var bVal)) return false;
        if (kv.Value?.ToString() != bVal?.ToString()) return false;
    }
    return true;
}
```

- [ ] **Step 3: Rewrite the DBC block in `EnqueueForStore`**

Replace the entire body of `EnqueueForStore`:

```csharp
public void EnqueueForStore(recordStoreRequest dto, Dictionary<string, object>? dbcRecord)
{
    if (dbcRecord != null && !DbcRecordEquals(dbcRecord, _lastStoredDbcRecord))
    {
        // Values changed — write to first record only, null the rest
        dto.RealStoreRecord[0].dbcValues = JsonConvert.SerializeObject(dbcRecord);
        for (int i = 1; i < dto.RealStoreRecord.Count; i++)
            dto.RealStoreRecord[i].dbcValues = null;

        _lastStoredDbcRecord = new Dictionary<string, object>(dbcRecord);
    }
    else
    {
        // No change (or null) — null all records
        foreach (var r in dto.RealStoreRecord)
            r.dbcValues = null;
    }

    _StoreQueue.Writer.TryWrite(dto);
    Session.Unstorerecordcount += dto.RealStoreRecord.Count;
}
```

- [ ] **Step 4: Build the project to confirm no compile errors**

```
dotnet build
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add Services/Implementations/CircuitCommandHandler.cs
git commit -m "feat: DBC change-only write — store on first record per changed batch only"
```

---

## Task 2 — Write `DbcSignalNames` header at session start

**Files:**
- Modify: `Services/Implementations/SqliteBulkDatabaseManager.cs`

### Context

`InsertSessionAsync` already writes a `"dbc"` config entry (full DbcDatabase JSON) at line ~173. We add a second entry `"DbcSignalNames"` — an ordered `string[]` of selected signal names. Order = ascending `SingalId`. This is the index mapping for all array rows in this file.

---

- [ ] **Step 1: Add `DbcSignalNames` entry inside `InsertSessionAsync`**

Find the `// -------- DBC --------` block (~line 172). Immediately **after** the existing `ctx.configurationEntities.Add(...)` for `"dbc"`, add:

```csharp
// -------- DBC SIGNAL NAMES (ordered index for compact array rows) --------
if (session.dbcDatabase?.Messages != null)
{
    var orderedNames = session.dbcDatabase.Messages
        .SelectMany(m => m.Value.Signals)
        .Where(s => s.IsSelected && s.SingalId != 0)
        .DistinctBy(s => s.SingalId)
        .OrderBy(s => s.SingalId)
        .Select(s => s.Name)
        .ToArray();

    if (orderedNames.Length > 0)
    {
        ctx.configurationEntities.Add(new ConfigurationEntity
        {
            Key = "DbcSignalNames",
            Value = JsonConvert.SerializeObject(orderedNames),
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            CreatedBy = user,
            UpdatedBy = user
        });
    }
}
```

- [ ] **Step 2: Add `FetchDbcSignalNamesAsync` private helper**

Add this method in the `#region Config` section, near `FetchDbcDatabaseAsync`:

```csharp
private async Task<string[]> FetchDbcSignalNamesAsync(SqliteDbContext ctx)
{
    var entity = await ctx.configurationEntities
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Key == "DbcSignalNames");

    if (entity is null || string.IsNullOrEmpty(entity.Value))
        return Array.Empty<string>();

    return JsonConvert.DeserializeObject<string[]>(entity.Value) ?? Array.Empty<string>();
}
```

- [ ] **Step 3: Build**

```
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add Services/Implementations/SqliteBulkDatabaseManager.cs
git commit -m "feat: write DbcSignalNames header at session start"
```

---

## Task 3 — Convert dict→array in `InsertRecordAsync`

**Files:**
- Modify: `Services/Implementations/SqliteBulkDatabaseManager.cs`

### Context

`InsertRecordAsync` (~line 81) fetches the DBC database and does a key-rename loop (signal ID → signal name). With the new scheme, rows where `dbcValues` is non-null contain the SAME dict format as before (`{"signalId": value}`) — but only the **first** changed record per batch has it set. We need to:

1. Fetch the ordered signal names (same sort as `DbcSignalNames`).
2. For non-null rows: convert the dict into a compact `string[]` array, store that back.
3. Null rows: leave null — no work needed.

The result stored in SQLite becomes `["val1","val2",""]` for changed rows and `null` for unchanged rows.

---

- [ ] **Step 1: Replace the DBC block in `InsertRecordAsync`**

The current block is:

```csharp
DbcDatabase dbcdata = await FetchDbcDatabaseAsync(ctx);

if (dbcdata != null && dbcdata.Messages?.Count > 0)
{
    var signalLookup = dbcdata.Messages
        .SelectMany(m => m.Value.Signals)
        .Where(s => s.IsSelected && s.SingalId != 0)
        .DistinctBy(s => s.SingalId)
        .ToDictionary(
            s => s.SingalId.ToString(),
            s => s.Name
        );

    foreach (var rec in recordDto.RealStoreRecord)
    {
        rec.DbcValuesParsed = rec.DbcValuesParsed
            .ToDictionary(
                kvp => signalLookup.TryGetValue(kvp.Key, out var name) ? name : kvp.Key,
                kvp => kvp.Value
            );
    }
}
```

Replace it entirely with:

```csharp
// Build ordered signal ID list (matches DbcSignalNames index order)
DbcDatabase dbcdata = await FetchDbcDatabaseAsync(ctx);

if (dbcdata?.Messages?.Count > 0)
{
    var orderedSignalIds = dbcdata.Messages
        .SelectMany(m => m.Value.Signals)
        .Where(s => s.IsSelected && s.SingalId != 0)
        .DistinctBy(s => s.SingalId)
        .OrderBy(s => s.SingalId)
        .Select(s => s.SingalId.ToString())
        .ToList();

    foreach (var rec in recordDto.RealStoreRecord)
    {
        if (string.IsNullOrEmpty(rec.dbcValues)) continue;

        // Parse the signal-ID-keyed dict written by EnqueueForStore
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(rec.dbcValues);
        if (dict == null) continue;

        // Convert to index-aligned string array
        var arr = orderedSignalIds
            .Select(id => dict.TryGetValue(id, out var v) ? v?.ToString() ?? "" : "")
            .ToArray();

        rec.dbcValues = JsonConvert.SerializeObject(arr);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add Services/Implementations/SqliteBulkDatabaseManager.cs
git commit -m "feat: InsertRecordAsync converts DBC dict to compact index-aligned array"
```

---

## Task 4 — Add `ApplyDbcForwardFill` helper

**Files:**
- Modify: `Services/Implementations/SqliteBulkDatabaseManager.cs`

### Context

All three read methods (`ReadSessionPageAsync`, `StreamSessionDataAsync`, `ReadSessionDataAsync`) need to forward-fill null rows. This shared helper does it in one place.

**Logic:**
- If `signalNames` is empty → return unchanged (old files without header, or no DBC session; every row already has a dict).
- `seedJson` is the last known `dbcValues` from rows before the current batch/page.
- For each row:
  - `null` → write the last known values as a proper dict.
  - Starts with `[` → new array format: parse, update `currentValues`, rewrite as dict.
  - Starts with `{` → old dict format: already correct, parse to update `currentValues` for carry-forward.

---

- [ ] **Step 1: Add the static helper in `SqliteBulkDatabaseManager`**

Add this private static method inside the class, before `#endregion` of `#region Read Data`:

```csharp
private static List<MeasurementData> ApplyDbcForwardFill(
    List<MeasurementData> rows, string[] signalNames, string? seedJson = null)
{
    if (signalNames.Length == 0) return rows;

    // Parse seed into currentValues
    string?[] currentValues = new string[signalNames.Length];
    if (!string.IsNullOrEmpty(seedJson))
    {
        if (seedJson.TrimStart().StartsWith('['))
        {
            var arr = JsonConvert.DeserializeObject<string[]>(seedJson);
            if (arr != null)
                for (int i = 0; i < Math.Min(arr.Length, currentValues.Length); i++)
                    currentValues[i] = arr[i];
        }
        else if (seedJson.TrimStart().StartsWith('{'))
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(seedJson);
            if (dict != null)
                for (int i = 0; i < signalNames.Length; i++)
                    currentValues[i] = dict.TryGetValue(signalNames[i], out var v) ? v?.ToString() : null;
        }
    }

    foreach (var row in rows)
    {
        var raw = row.dbcValues?.TrimStart();

        if (string.IsNullOrEmpty(raw))
        {
            // No change — apply last known values
            row.dbcValues = BuildDictJson(signalNames, currentValues);
        }
        else if (raw.StartsWith('['))
        {
            // New array format — update currentValues, rewrite as dict
            var arr = JsonConvert.DeserializeObject<string[]>(row.dbcValues!);
            if (arr != null)
                for (int i = 0; i < Math.Min(arr.Length, currentValues.Length); i++)
                    currentValues[i] = arr[i];
            row.dbcValues = BuildDictJson(signalNames, currentValues);
        }
        else if (raw.StartsWith('{'))
        {
            // Old dict format — carry forward signal values
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object?>>(row.dbcValues!);
            if (dict != null)
                for (int i = 0; i < signalNames.Length; i++)
                    currentValues[i] = dict.TryGetValue(signalNames[i], out var v) ? v?.ToString() : currentValues[i];
            // leave row.dbcValues as-is (already a valid dict)
        }
    }

    return rows;
}

private static string? BuildDictJson(string[] names, string?[] values)
{
    if (names.Length == 0) return null;
    var d = new Dictionary<string, object?>(names.Length);
    for (int i = 0; i < names.Length; i++)
        d[names[i]] = values.Length > i ? values[i] : null;
    return JsonConvert.SerializeObject(d);
}
```

- [ ] **Step 2: Build**

```
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add Services/Implementations/SqliteBulkDatabaseManager.cs
git commit -m "feat: add ApplyDbcForwardFill helper for DBC null-fill on read"
```

---

## Task 5 — Apply forward-fill in `ReadSessionPageAsync`

**Files:**
- Modify: `Services/Implementations/SqliteBulkDatabaseManager.cs`

### Context

`ReadSessionPageAsync` (~line 482) fetches one page of rows. We need to:
1. Fetch `signalNames` from the config.
2. Query the last non-null `dbcValues` before the first row of this page (the "seed").
3. Call `ApplyDbcForwardFill` before returning.

---

- [ ] **Step 1: Rewrite `ReadSessionPageAsync`**

Replace the full method body (keep the signature unchanged):

```csharp
public async Task<PagedResult<MeasurementData>> ReadSessionPageAsync(
    string filePath, int page, int pageSize, int? stepFilter = null)
{
    try
    {
        await using var ctx = GetContext(filePath);

        var query = ctx.Measurements.AsNoTracking();
        if (stepFilter.HasValue)
            query = query.Where(m => m.StepNumber == stepFilter.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MeasurementData
            {
                Id                   = m.Id,
                SessionID            = m.SessionID,
                DeviceId             = m.DeviceId,
                CircuitId            = m.CircuitId,
                StepNumber           = m.StepNumber,
                Operator             = m.Operator,
                CircuitStatus        = m.CircuitStatus,
                DateTime             = m.DateTime,
                ProgramRunningTime   = m.ProgramRunningTime,
                Current              = m.Current,
                Voltage              = m.Voltage,
                Temperature          = m.Temperature,
                Power                = m.Power,
                AccumulatedCapacity  = m.AccumulatedCapacity,
                ChargeCapacity       = m.ChargeCapacity,
                DischargeCapacity    = m.DischargeCapacity,
                StepCapacity         = m.StepCapacity,
                AccumulatedEnergy    = m.AccumulatedEnergy,
                ChargeEnergy         = m.ChargeEnergy,
                DischargeEnergy      = m.DischargeEnergy,
                StepEnergy           = m.StepEnergy,
                SystemErrorID        = m.SystemErrorID,
                ErrorId              = m.ErrorId,
                MessageId            = m.MessageId,
                Remark               = m.Remark,
                dbcValues            = m.dbcValues
            })
            .ToListAsync();

        // Forward-fill DBC nulls
        var signalNames = await FetchDbcSignalNamesAsync(ctx);
        string? seedJson = null;
        if (signalNames.Length > 0 && items.Count > 0)
        {
            var firstId = items[0].Id;
            seedJson = await ctx.Measurements
                .AsNoTracking()
                .Where(m => m.Id < firstId && m.dbcValues != null)
                .OrderByDescending(m => m.Id)
                .Select(m => m.dbcValues)
                .FirstOrDefaultAsync();
        }

        ApplyDbcForwardFill(items, signalNames, seedJson);

        return new PagedResult<MeasurementData>
        {
            Items     = items,
            TotalRows = total,
            Page      = page,
            PageSize  = pageSize
        };
    }
    catch (Exception ex)
    {
        Log.Error(ex, "[DB ERROR] ReadSessionPageAsync failed for {FilePath}", filePath);
        return PagedResult<MeasurementData>.Empty(pageSize);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```
git add Services/Implementations/SqliteBulkDatabaseManager.cs
git commit -m "feat: ReadSessionPageAsync forward-fills DBC nulls with pre-query seed"
```

---

## Task 6 — Apply forward-fill in `StreamSessionDataAsync` and `ReadSessionDataAsync`

**Files:**
- Modify: `Services/Implementations/SqliteBulkDatabaseManager.cs`

### Context

`StreamSessionDataAsync` (~line 620) streams in pages for export. Forward-fill state must carry across batch boundaries.

`ReadSessionDataAsync` (~line 465) loads the whole session at once (used for BmsDashboard). Simpler — no seed needed.

---

- [ ] **Step 1: Rewrite `StreamSessionDataAsync`**

Replace the full method body (keep the signature):

```csharp
public async IAsyncEnumerable<List<MeasurementData>> StreamSessionDataAsync(
    string filePath, int batchSize = 5000,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    // Fetch signal names once before streaming starts
    string[] signalNames;
    try
    {
        await using var initCtx = GetContext(filePath);
        signalNames = await FetchDbcSignalNamesAsync(initCtx);
    }
    catch
    {
        signalNames = Array.Empty<string>();
    }

    string? lastDbcJson = null;
    int page = 1;

    while (true)
    {
        List<MeasurementData> batch;
        try
        {
            await using var ctx = GetContext(filePath);
            batch = await ctx.Measurements
                .AsNoTracking()
                .OrderBy(m => m.Id)
                .Skip((page - 1) * batchSize)
                .Take(batchSize)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DB ERROR] StreamSessionDataAsync batch {Page} failed for {FilePath}", page, filePath);
            yield break;
        }

        if (batch.Count == 0) yield break;

        ApplyDbcForwardFill(batch, signalNames, lastDbcJson);

        // Carry the last non-null dbcValues forward to the next batch
        var lastNonNull = batch.LastOrDefault(r => !string.IsNullOrEmpty(r.dbcValues));
        if (lastNonNull != null) lastDbcJson = lastNonNull.dbcValues;

        yield return batch;
        if (batch.Count < batchSize) yield break;
        page++;
    }
}
```

- [ ] **Step 2: Rewrite `ReadSessionDataAsync`**

Replace the method body:

```csharp
public async Task<List<MeasurementData>> ReadSessionDataAsync(string filePath)
{
    try
    {
        using var ctx = GetContext(filePath);
        var rows = await ctx.Measurements.ToListAsync();
        var signalNames = await FetchDbcSignalNamesAsync(ctx);
        ApplyDbcForwardFill(rows, signalNames);
        return rows;
    }
    catch (Exception ex)
    {
        Log.Error($"[DB ERROR] ReadSessionDataAsync failed for {filePath}");
        return new List<MeasurementData>();
    }
}
```

- [ ] **Step 3: Build**

```
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add Services/Implementations/SqliteBulkDatabaseManager.cs
git commit -m "feat: forward-fill DBC nulls in StreamSessionDataAsync and ReadSessionDataAsync"
```

---

## Task 7 — End-to-end smoke test

**No test project exists** — verify manually by running the app.

- [ ] **Step 1: Start the app and connect a circuit (or simulator)**

Launch the app. Start a session on any circuit that has a DBC file assigned.

- [ ] **Step 2: Let it record for ~30 seconds, then open the session DB file**

Open the `.db` file in any SQLite viewer (e.g. DB Browser for SQLite).

Check the `MeasurementData` table:
- Most rows should have `dbcValues = NULL`.
- Rows where DBC values changed from the prior batch should have `["val1","val2",...]` (starts with `[`).
- No rows should have the old `{"key":"val"}` dict format.

- [ ] **Step 3: Open the DataViewer for that session**

Navigate to the DataViewer for the recorded session. Verify:
- DBC columns appear and show values on every row (forward-fill working).
- Rows that were null in the DB show the last-known DBC values.
- Paging to page 2+ shows correct DBC values on the first rows (seed pre-query working).

- [ ] **Step 4: Verify export**

Export the session to Excel. Confirm DBC columns are filled on every row (no blanks mid-session).

- [ ] **Step 5: Open an old session file (pre-migration)**

Load a session file recorded before this change (has `{"key":"val"}` dict on every row).

Verify it still displays correctly — old rows are read as-is (format sniff handles `{`).

- [ ] **Step 6: Commit any fixes found**

If any issue found during testing, fix and commit before marking done.

---

## Summary of commits expected

1. `feat: DBC change-only write — store on first record per changed batch only`
2. `feat: write DbcSignalNames header at session start`
3. `feat: InsertRecordAsync converts DBC dict to compact index-aligned array`
4. `feat: add ApplyDbcForwardFill helper for DBC null-fill on read`
5. `feat: ReadSessionPageAsync forward-fills DBC nulls with pre-query seed`
6. `feat: forward-fill DBC nulls in StreamSessionDataAsync and ReadSessionDataAsync`
