# DBC Change-Only Array Storage

**Date:** 2026-06-04  
**Status:** Approved

## Problem

Every `MeasurementData` row currently stores a full JSON dict in `dbcValues`:

```
{"EngineRPM":"1234","Speed":"50","Temp":"22.1"}
```

The same dict is written to **every row** in a batch, and to every subsequent batch until DBC values change. At 1 Hz over a 10-hour session with 5 signals, that is ~10 000 identical copies of ~60 bytes each ≈ 600 KB of repeated key-value overhead per circuit per session.

## Goal

Reduce DBC storage by:
1. Storing signal names **once** per session (no repeated keys).
2. Storing DBC values as a compact index-aligned **array**, and only when values actually change — `null` otherwise.
3. Forward-filling on read so the UI and export always see the last known values.

---

## Data Format

### Session header (written once at session start)

A new config entry alongside the existing `"dbc"` key:

```
Key   = "DbcSignalNames"
Value = ["EngineRPM", "Speed", "Temp"]
```

Order is fixed: selected signals sorted ascending by `SingalId`. This order is the canonical index mapping for all array rows in the session file.

### Per-row storage in `dbcValues` column

| Case | Value written |
|---|---|
| DBC values changed since last stored row | `["1234","50","22.1"]` |
| DBC values unchanged | `null` |

The column format is detected at read time by the first character (`[` → new array format, `{` → old dict format from pre-migration sessions).

### Example on disk

```
Id | dbcValues
---+---------------------------------
 1 | ["","3","4",""]       ← changed
 2 | null                  ← same
 3 | null                  ← same
 4 | ["","5","6","4"]      ← changed
```

### Forward-filled on read

```
Id | DbcValuesParsed (computed, never stored)
---+------------------------------------------
 1 | {"Speed":"3","Temp":"4"}
 2 | {"Speed":"3","Temp":"4"}   ← filled from row 1
 3 | {"Speed":"3","Temp":"4"}   ← filled from row 1
 4 | {"Speed":"5","Temp":"6","AuxTemp":"4"}
```

---

## Write Path

### 1. `InsertSessionAsync` — write signal names header

After saving the existing `"dbc"` config entry, also write:

```csharp
Key = "DbcSignalNames"
Value = JsonConvert.SerializeObject(orderedSelectedSignalNames)
```

Build `orderedSelectedSignalNames` from `session.dbcDatabase` by taking selected signals (`IsSelected && SingalId != 0`), sorted by `SingalId`, extracting `.Name`. If no DBC database, skip.

### 2. `CircuitCommandHandler` — change detection in `EnqueueForStore`

Add a private field:
```csharp
private Dictionary<string, object>? _lastStoredDbcRecord;
```

In `EnqueueForStore`:
```
if dbcRecord is null → set dbcValues = null on all records
if dbcRecord equals _lastStoredDbcRecord → set dbcValues = null on all records
if dbcRecord differs → set dbcValues on first record only (serialized array), null on rest; update _lastStoredDbcRecord
```

**Equality check:** compare by key count and per-key value `.ToString()` equality — no external library needed.

**Array serialization:** The dict keys at this point are still signal IDs (names are translated later in `InsertRecordAsync`). Write them as an ordered array matching the same `SingalId` sort used for `DbcSignalNames`. The array is built by iterating the canonical signal order and looking up each signal's current value (empty string `""` for absent signals).

### 3. `InsertRecordAsync` — translate and skip dict rename for null rows

Current code translates signal-ID keys → names for every record. With the new scheme:
- Skip translation for rows where `dbcValues` is null.
- For rows with a value: detect format — if it starts with `[`, it is already an ordered array (signal IDs in canonical order); translate by re-serializing as-is since `DbcSignalNames` header already maps index → name. No key renaming needed.
- Remove the per-row key-rename loop; it is no longer needed for new-format rows.

---

## Read Path

### `FetchDbcSignalNamesAsync` (new private helper)

```csharp
private async Task<string[]> FetchDbcSignalNamesAsync(SqliteDbContext ctx)
```

Reads the `"DbcSignalNames"` config entry. Returns `string[]` or empty array if absent (old session files without the header).

### Forward-fill helper (new private static method)

```csharp
private static List<MeasurementData> ApplyDbcForwardFill(
    List<MeasurementData> rows, string[] signalNames, string? seedJson = null)
```

- Maintains `currentValues` array (last known, starts from `seedJson` if provided).
- For each row:
  - If `dbcValues` starts with `[` → deserialize as `string[]`, update `currentValues`, rewrite `dbcValues` as dict JSON using `signalNames`.
  - If `dbcValues` starts with `{` → already a dict (old format), parse and carry forward as-is.
  - If null → copy `currentValues` into `dbcValues` as dict JSON.

### `ReadSessionPageAsync` — Option A pre-query

Before fetching the page, run a single scalar query:

```sql
SELECT dbcValues FROM Measurements
WHERE Id < <first_id_of_page> AND dbcValues IS NOT NULL
ORDER BY Id DESC LIMIT 1
```

Pass this as the `seedJson` to `ApplyDbcForwardFill`.

### `StreamSessionDataAsync` — carry-forward across batches

Thread a `string? lastDbcJson` variable through the `while` loop. After each batch is forward-filled, capture the last non-null `dbcValues` from the batch as the seed for the next batch.

### `ReadSessionDataAsync`

Call `ApplyDbcForwardFill` with no seed (reads whole session; seed starts empty).

---

## `DbcValuesParsed` on `MeasurementData`

No change to the property itself. The forward-fill writes a proper dict JSON string into `dbcValues` before returning rows to callers, so `DbcValuesParsed` continues to deserialize correctly.

---

## Backward Compatibility

Old session files have `dbcValues` as `{"key":"val"}` dicts on every row. The `[` vs `{` format sniff handles this transparently — old-format rows are passed through unchanged. `FetchDbcSignalNamesAsync` returns an empty array for old files; forward-fill still works (dict rows carry their own keys).

---

## Files Changed

| File | Change |
|---|---|
| `Services/Implementations/CircuitCommandHandler.cs` | Add `_lastStoredDbcRecord` field; rewrite `EnqueueForStore` DBC block |
| `Services/Implementations/SqliteBulkDatabaseManager.cs` | Add `FetchDbcSignalNamesAsync`, `ApplyDbcForwardFill`; update `InsertSessionAsync`, `InsertRecordAsync`, `ReadSessionPageAsync`, `StreamSessionDataAsync`, `ReadSessionDataAsync` |

No model changes. No schema changes. No migration needed.
