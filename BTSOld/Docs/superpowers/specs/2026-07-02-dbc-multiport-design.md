# DBC Multi-Port Assignment Design

**Date:** 2026-07-02  
**Status:** Approved

## Overview

Change the DBC flow from a single-port model (one DBC per transfer, port selected on the battery page) to a three-port model (each CAN port gets its own independently assigned DBC, port assignment happens in the Transfer Dialog and is remembered per battery).

## Current Flow

1. Battery page: upload DBCs, set baudrate, **select CPort** on the DBC.
2. Transfer Dialog: single DBC dropdown — user picks one DBC, it transfers on the port stored in that DBC.
3. `DbcDatabase.ToBytes()` uses `CPort` field to mark one of 3 port headers as active.

## New Flow

1. Battery page: upload DBCs, set baudrate. **CPort removed** — no port selection here.
2. Transfer Dialog: 3 port dropdowns (Port 1 / Port 2 / Port 3), each with None + all battery DBCs. Assignment is saved back to the battery immediately on change (remembered).
3. On Transfer: all 3 port DBCs are sent to device as one multi-port binary payload via `DbcDatabase.BuildMultiPortPayload(p1, p2, p3)`.

## Design Decisions

- **Port assignment ownership moves to Transfer Dialog.** Baudrate stays on the DBC (set at upload time, required).
- **Same DBC cannot be assigned to two ports.** A DBC already chosen in one port is disabled in the other two dropdowns.
- **All 3 ports can have different DBCs.** Sending DBC-A on Port 1, DBC-B on Port 2, DBC-C on Port 3 is fully supported.
- **None is valid for any port.** All 3 None = transfer blocked. At least one port must have a DBC.
- **Baudrate from DBC.** `CBaudrate` is read from each selected `DbcDatabase` at transfer time. No override in Transfer Dialog.
- **Baudrate required at upload.** Saving a DBC without `CBaudrate` set is blocked with a validation error.
- **Port assignments saved on `Batteries` entity** (3 nullable FK columns). Pre-filled when battery is selected in Transfer Dialog.
- **`CPort` removed from `DbcDatabase`.** Port is a transfer-time concern, not a database-level concern.
- **Approach A chosen** for `ToBytes()`: static `BuildMultiPortPayload(p1, p2, p3)`, null = inactive port.

## Data Model Changes

### `Batteries` entity
```csharp
public long? Port1DbcId { get; set; }
public long? Port2DbcId { get; set; }
public long? Port3DbcId { get; set; }
```
Nullable FKs to `DbcFileRecord.Id`. No cascade delete — orphaned FK set to null in app logic when a DBC is deleted.

### `BatteryDTO`
```csharp
public long? Port1DbcId { get; set; }
public long? Port2DbcId { get; set; }
public long? Port3DbcId { get; set; }
```

### `DbcDatabase`
- Remove `public CanPort CPort { get; set; }`
- `CanPort` enum stays (Port1/Port2/Port3 still used as index concept)

### DB Migration
```sql
ALTER TABLE Batteries ADD COLUMN Port1DbcId INTEGER NULL REFERENCES DBCFiles(Id);
ALTER TABLE Batteries ADD COLUMN Port2DbcId INTEGER NULL REFERENCES DBCFiles(Id);
ALTER TABLE Batteries ADD COLUMN Port3DbcId INTEGER NULL REFERENCES DBCFiles(Id);
```

## Component Changes

### `BatteriesList.razor`
- Remove `CPort` dropdown from DBC editor/upload section.
- Add validation on DBC save: if `CBaudrate` is not set, block with message "Baudrate is required."

### `TransferDialog.razor`

**State fields — replace:**
```csharp
// Remove
private string? selectedDbcFile;

// Add
private string? port1DbcId;
private string? port2DbcId;
private string? port3DbcId;
```

**Razor markup — replace single DBC RadioGroup scroll area with:**
- 3 `Select` dropdowns (Port 1, Port 2, Port 3)
- Each dropdown: None option + all DBCs for selected battery
- A DBC already selected in another port is disabled in this dropdown

**CSS helpers — replace:**
```csharp
// Remove
DbcClass(long id)
DbcNoneClass()

// Add
PortDbcClass(string? portVal, long id)   // reused for all 3 ports
PortNoneClass(string? portVal)            // reused for all 3 ports
```

**`OnBatterySelected` additions:**
- After loading `DbcFiles`, read `battery.Port1DbcId/Port2DbcId/Port3DbcId`
- Pre-fill `port1DbcId`, `port2DbcId`, `port3DbcId`

**New `OnPortAssignmentChanged()`:**
- Called when any port dropdown changes
- Calls `BatteryServices.UpdatePortAssignmentsAsync(batteryId, port1DbcId, port2DbcId, port3DbcId)`

**`canTransfer` update:**
```csharp
private bool canTransfer =>
    !string.IsNullOrWhiteSpace(selectedProgram) &&
    !string.IsNullOrWhiteSpace(selectedBattery) &&
    !isProcessing &&
    (port1DbcId != null || port2DbcId != null || port3DbcId != null);
```

**`HandleTransfer` update:**
- Resolve 3 nullable `DbcFileRecordDto` from port1/2/3 IDs
- Call `dev.TransferDbcFile(port1Dbc, port2Dbc, port3Dbc)` instead of single-DBC call
- `dbcStep` tracking covers all 3 ports

### `DbcParser.cs` — `DbcDatabase`

**Remove:** `public CanPort CPort { get; set; }`

**Replace `ToBytes()`** with static method:
```csharp
public static byte[] BuildMultiPortPayload(
    DbcDatabase? port1,
    DbcDatabase? port2,
    DbcDatabase? port3)
```

`WritePort` signature changes:
```csharp
// Old
void WritePort(BinaryWriter w, bool isActive)

// New
void WritePort(BinaryWriter w, DbcDatabase? db)
// db != null → active, uses db.CBaudrate, db's Rx/Tx messages
// db == null → inactive, writes zeroed header
```

Internal calls:
```csharp
WritePort(writer, port1);
WritePort(writer, port2);
WritePort(writer, port3);
```

**`ToHexValue()`** gets same treatment → `BuildMultiPortHex(DbcDatabase? p1, DbcDatabase? p2, DbcDatabase? p3)`

### `CircuitCommandHandler.cs` — `TransferDbcFile`

**New signature:**
```csharp
public async Task<CommonResponse<bool>> TransferDbcFile(
    DbcFileRecordDto? port1Dbc = null,
    DbcFileRecordDto? port2Dbc = null,
    DbcFileRecordDto? port3Dbc = null)
```

**Validation guard:**
```csharp
if (port1Dbc == null && port2Dbc == null && port3Dbc == null)
    return CommonResponse<bool>.Fail("No DBC files provided for transfer.");
```

**Payload build:**
```csharp
byte[] packets = DbcDatabase.BuildMultiPortPayload(
    port1Dbc?.dbcDatabase,
    port2Dbc?.dbcDatabase,
    port3Dbc?.dbcDatabase);
```

**Session tracking:**
```csharp
Session.Port1DbcFileRecordID = port1Dbc?.Id ?? 0;
Session.Port2DbcFileRecordID = port2Dbc?.Id ?? 0;
Session.Port3DbcFileRecordID = port3Dbc?.Id ?? 0;
```

Packet chunking and send loop after payload build: **unchanged**.

### `BatteryServices` — new method
```csharp
Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(
    long batteryId,
    long? port1DbcId,
    long? port2DbcId,
    long? port3DbcId);
```

## Full Change Surface

| File | Change |
|---|---|
| `Models\Entities\Batteries.cs` | Add 3 nullable FK fields |
| `Models\DTOs\BatteryDTO.cs` | Add 3 nullable port fields |
| `Services\Implementations\BatteryServices.cs` | Add `UpdatePortAssignmentsAsync` |
| `Services\Interfaces\IBatteryServices.cs` | Add interface method |
| `Components\Pages\Batteries\BatteriesList.razor` | Remove `CPort` dropdown; baudrate required validation |
| `Components\UI\Dashboard\TransferDialog.razor` | Replace `selectedDbcFile` with 3 port dropdowns; pre-fill + save; pass 3 DBCs |
| `Services\DbcParser.cs` | Remove `CPort`; `BuildMultiPortPayload`; `WritePort` takes `DbcDatabase?` |
| `Services\Implementations\CircuitCommandHandler.cs` | `TransferDbcFile` takes 3 nullable DTOs; session tracks 3 port IDs |
| DB Migration | Add 3 columns to Batteries table |
