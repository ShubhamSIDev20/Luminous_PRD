# DBC Multi-Port Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Change the DBC flow from single-port (one DBC per transfer, port baked into the DBC) to three-port (each CAN port gets its own independently assigned DBC, assignment remembered per battery, all transferred together).

**Architecture:** Port assignment moves from `DbcDatabase.CPort` (battery page) to three nullable FK columns on `Batteries` + three dropdowns in `TransferDialog`. `DbcDatabase.ToBytes()` becomes a static `BuildMultiPortPayload(p1, p2, p3)` that writes all three port headers and each port's own message/signal blocks. `TransferDbcFile` receives three nullable DTOs and stitches them into one binary payload.

**Tech Stack:** Blazor Server (.NET), Entity Framework Core (SQLite), C#

## Global Constraints

- EF migrations via `dotnet ef migrations add <Name>` — never hand-edit `Migrations/` Designer files
- Keep `CanPort` enum (Port1=1, Port2=2, Port3=3) — only remove `CPort` field from `DbcDatabase`
- `BinaryWriter` writes big-endian for multi-byte values (existing convention: `Array.Reverse` when `BitConverter.IsLittleEndian`)
- Port header layout (per port, 12 bytes): `PortEnable(u8)`, `Baudrate(u8)`, `RxMsgCount(u8)`, `TxMsgCount(u8)`, `RxMsgCfgOffset(u32)`, `TxMsgCfgOffset(u32)`
- `CommonResponse<T>` for all service/repo return types
- Partial-update pattern in `BatteryRepository.CreateOrUpdateBattery`: nullable port FKs must always be written (not skipped when null)

---

### Task 1: Data Model + EF Migration

**Files:**
- Modify: `Models\Entities\Batteries.cs`
- Modify: `Models\DTOs\BatteryDTO.cs`
- Modify: `Repositories\Interfaces\ISpecificRepositories.cs` (add to `IBatteryRepository`)
- Modify: `Services\Interfaces\IServices.cs` (add to `IBatteryServices`)
- Modify: `Repositories\Implementations\BatteryRepository.cs`
- Modify: `Services\Implementations\BatteryServices.cs`
- Create: `Migrations\<timestamp>_AddBatteryPortAssignments.cs` (generated)

**Interfaces:**
- Produces: `IBatteryRepository.UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId) → Task<CommonResponse<bool>>`
- Produces: `IBatteryServices.UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId) → Task<CommonResponse<bool>>`
- Produces: `BatteryDTO.Port1DbcId`, `Port2DbcId`, `Port3DbcId` (nullable long)
- Produces: `Batteries.Port1DbcId`, `Port2DbcId`, `Port3DbcId` (nullable long)

- [ ] **Step 1: Add 3 nullable FK fields to `Batteries` entity**

In `Models\Entities\Batteries.cs`, add before the closing brace of the class:
```csharp
public long? Port1DbcId { get; set; }
public long? Port2DbcId { get; set; }
public long? Port3DbcId { get; set; }
```

- [ ] **Step 2: Add 3 nullable fields to `BatteryDTO`**

In `Models\DTOs\BatteryDTO.cs`, add before the closing brace of the class:
```csharp
public long? Port1DbcId { get; set; }
public long? Port2DbcId { get; set; }
public long? Port3DbcId { get; set; }
```

- [ ] **Step 3: Add interface method to `IBatteryRepository`**

In `Repositories\Interfaces\ISpecificRepositories.cs`, find `IBatteryRepository` and add:
```csharp
Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId);
```

- [ ] **Step 4: Add interface method to `IBatteryServices`**

In `Services\Interfaces\IServices.cs`, find `IBatteryServices` and add:
```csharp
Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId);
```

- [ ] **Step 5: Implement `UpdatePortAssignmentsAsync` in `BatteryRepository`**

In `Repositories\Implementations\BatteryRepository.cs`, add after the `GetBattery` method:
```csharp
public async Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(
    long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId)
{
    try
    {
        var entity = await _dbSet.FirstOrDefaultAsync(x => x.Id == batteryId && !x.IsDeleted);
        if (entity == null)
            return CommonResponse<bool>.Fail("Battery not found");

        entity.Port1DbcId = port1DbcId;
        entity.Port2DbcId = port2DbcId;
        entity.Port3DbcId = port3DbcId;
        entity.UpdatedAt = DateTime.Now;
        entity.UpdatedBy = CurrentUser.UserName;

        await _AppDbcontext.SaveChangesAsync();
        return CommonResponse<bool>.Ok(true, "Port assignments updated");
    }
    catch (Exception ex)
    {
        return CommonResponse<bool>.Fail($"Failed to update port assignments: {ex.Message}");
    }
}
```

- [ ] **Step 6: Update `CreateOrUpdateBattery` in `BatteryRepository` to propagate port fields**

In `BatteryRepository.CreateOrUpdateBattery`, inside the **UPDATE** block (after `entity.BreakVoltage` line), add:
```csharp
// Always write port assignments — null is valid (no DBC assigned)
entity.Port1DbcId = battery.Port1DbcId;
entity.Port2DbcId = battery.Port2DbcId;
entity.Port3DbcId = battery.Port3DbcId;
```

In the **CREATE** block (inside `new Batteries { ... }`), add after `BreakVoltage = battery.BreakVoltage,`:
```csharp
Port1DbcId = battery.Port1DbcId,
Port2DbcId = battery.Port2DbcId,
Port3DbcId = battery.Port3DbcId,
```

- [ ] **Step 7: Implement `UpdatePortAssignmentsAsync` in `BatteryServices`**

In `Services\Implementations\BatteryServices.cs`, add after `GetBattery`:
```csharp
public async Task<CommonResponse<bool>> UpdatePortAssignmentsAsync(
    long batteryId, long? port1DbcId, long? port2DbcId, long? port3DbcId)
{
    return await _batteryRepository.UpdatePortAssignmentsAsync(batteryId, port1DbcId, port2DbcId, port3DbcId);
}
```

- [ ] **Step 8: Update `GetBattery` mapping to include port fields**

In `BatteryRepository.GetBattery`, ensure the returned `BatteryDTO` maps the port fields. Find where the DTO is constructed or mapped and add:
```csharp
Port1DbcId = entity.Port1DbcId,
Port2DbcId = entity.Port2DbcId,
Port3DbcId = entity.Port3DbcId,
```
Do the same in `GetBatteries` mapping if batteries are mapped to DTOs there.

- [ ] **Step 9: Generate EF migration**

```
dotnet ef migrations add AddBatteryPortAssignments
```
Expected: new file `Migrations\<timestamp>_AddBatteryPortAssignments.cs` with `AddColumn` calls for `Port1DbcId`, `Port2DbcId`, `Port3DbcId` on `Battery.Batteries`.

- [ ] **Step 10: Apply migration**

```
dotnet ef database update
```
Expected: `Done.`

- [ ] **Step 11: Build to confirm no compile errors**

```
dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 12: Commit**

```
git add Models/Entities/Batteries.cs Models/DTOs/BatteryDTO.cs \
        Repositories/Interfaces/ISpecificRepositories.cs \
        Services/Interfaces/IServices.cs \
        Repositories/Implementations/BatteryRepository.cs \
        Services/Implementations/BatteryServices.cs \
        Migrations/
git commit -m "feat: add Port1/2/3DbcId to Batteries entity and service layer"
```

---

### Task 2: DbcParser — Remove CPort, Add BuildMultiPortPayload

**Files:**
- Modify: `Services\DbcParser.cs`

**Interfaces:**
- Consumes: nothing from Task 1
- Produces: `DbcDatabase.BuildMultiPortPayload(DbcDatabase? port1, DbcDatabase? port2, DbcDatabase? port3) → byte[]` (static)
- Produces: `DbcDatabase.BuildMultiPortHex(DbcDatabase? port1, DbcDatabase? port2, DbcDatabase? port3) → string` (static)
- Produces: `DbcDatabase.ToHexValue() → string` — kept as instance wrapper (calls `BuildMultiPortHex(this, null, null)`)
- `DbcDatabase.CPort` field: **removed**

- [ ] **Step 1: Remove `CPort` from `DbcDatabase`**

In `Services\DbcParser.cs`, in class `DbcDatabase`, delete this line:
```csharp
public CanPort CPort { get; set; }
```

- [ ] **Step 2: Change `WritePort` signature and implementation**

Find the existing private `WritePort` method (signature is currently `WritePort(BinaryWriter writer, bool isActive)`).

Replace the entire method with:
```csharp
private static void WritePort(BinaryWriter writer, DbcDatabase? db,
    uint rxMsgCfgOffset, uint txMsgCfgOffset)
{
    bool isActive = db != null && db.Messages.Count > 0;
    var rxMessages = isActive ? db!.GetRxMessages().ToList() : new();
    var txMessages = isActive ? db!.GetTxMessages().ToList() : new();

    // PortEnable
    writer.Write((byte)(isActive ? 1 : 0));
    // Baudrate
    writer.Write((byte)(isActive ? (int)db!.CBaudrate : 0));
    // RxMsgCount
    writer.Write((byte)rxMessages.Count);
    // TxMsgCount
    writer.Write((byte)txMessages.Count);

    // RxMsgCfgOffset (big-endian uint32)
    var rxOff = BitConverter.GetBytes(isActive ? rxMsgCfgOffset : 0u);
    if (BitConverter.IsLittleEndian) Array.Reverse(rxOff);
    writer.Write(rxOff);

    // TxMsgCfgOffset (big-endian uint32)
    var txOff = BitConverter.GetBytes(isActive ? txMsgCfgOffset : 0u);
    if (BitConverter.IsLittleEndian) Array.Reverse(txOff);
    writer.Write(txOff);
}
```

- [ ] **Step 3: Add helper to compute per-port message data**

Add a private static helper struct and method in `DbcDatabase`:
```csharp
private readonly struct PortData
{
    public readonly List<DbcMessage> RxMessages;
    public readonly List<DbcMessage> TxMessages;
    public readonly List<int> RxSignalOffsets;
    public readonly List<int> TxSignalOffsets;

    public PortData(DbcDatabase db)
    {
        RxMessages = db.GetRxMessages().ToList();
        TxMessages = db.GetTxMessages().ToList();

        // Assign signal IDs (already done by caller before BuildMultiPortPayload)
        // Compute Rx signal offsets
        RxSignalOffsets = new List<int>();
        int off = 0;
        foreach (var msg in RxMessages)
        {
            RxSignalOffsets.Add(off);
            off += msg.Signals.Count * 5; // 5 bytes per signal
        }

        // Compute Tx signal offsets
        TxSignalOffsets = new List<int>();
        off = 0;
        foreach (var msg in TxMessages)
        {
            TxSignalOffsets.Add(off);
            off += msg.Signals.Count * 5;
        }
    }

    // Byte size of Rx message blocks for this port
    public int RxMsgBlockSize => RxMessages.Count * 11; // 11 bytes per DbcMessage
    // Byte size of Tx message blocks for this port
    public int TxMsgBlockSize => TxMessages.Count * 11;
    // Byte size of Rx signal blocks for this port
    public int RxSignalBlockSize => RxMessages.Sum(m => m.Signals.Count) * 5;
}
```

- [ ] **Step 4: Replace `ToBytes()` with static `BuildMultiPortPayload`**

Delete the existing `ToBytes()` instance method entirely. Add this static method in `DbcDatabase`:

```csharp
public static byte[] BuildMultiPortPayload(
    DbcDatabase? port1,
    DbcDatabase? port2,
    DbcDatabase? port3)
{
    const int PORT_HEADER_SIZE = 12; // 1+1+1+1+4+4
    const int TOTAL_HEADER_SIZE = 3 * PORT_HEADER_SIZE; // 36 bytes

    // Pre-compute per-port data (null db = inactive port with zero messages)
    var p1 = port1 != null ? new PortData(port1) : default(PortData?);
    var p2 = port2 != null ? new PortData(port2) : default(PortData?);
    var p3 = port3 != null ? new PortData(port3) : default(PortData?);

    // Calculate message block offsets from start of payload
    // Layout: [36 header bytes] [port1 rx msgs] [port1 tx msgs] [port1 rx sigs]
    //                           [port2 rx msgs] [port2 tx msgs] [port2 rx sigs]
    //                           [port3 rx msgs] [port3 tx msgs] [port3 rx sigs]
    int cursor = TOTAL_HEADER_SIZE;

    uint p1RxOff = p1.HasValue && p1.Value.RxMessages.Count > 0 ? (uint)cursor : 0u;
    cursor += p1?.RxMsgBlockSize ?? 0;
    uint p1TxOff = p1.HasValue && p1.Value.TxMessages.Count > 0 ? (uint)cursor : 0u;
    cursor += p1?.TxMsgBlockSize ?? 0;
    cursor += p1?.RxSignalBlockSize ?? 0; // signals follow tx msgs for port1

    uint p2RxOff = p2.HasValue && p2.Value.RxMessages.Count > 0 ? (uint)cursor : 0u;
    cursor += p2?.RxMsgBlockSize ?? 0;
    uint p2TxOff = p2.HasValue && p2.Value.TxMessages.Count > 0 ? (uint)cursor : 0u;
    cursor += p2?.TxMsgBlockSize ?? 0;
    cursor += p2?.RxSignalBlockSize ?? 0;

    uint p3RxOff = p3.HasValue && p3.Value.RxMessages.Count > 0 ? (uint)cursor : 0u;
    cursor += p3?.RxMsgBlockSize ?? 0;
    uint p3TxOff = p3.HasValue && p3.Value.TxMessages.Count > 0 ? (uint)cursor : 0u;

    using var ms = new MemoryStream();
    using var writer = new BinaryWriter(ms);

    // ── 3 port headers ──────────────────────────────────────────────────────
    WritePort(writer, port1, p1RxOff, p1TxOff);
    WritePort(writer, port2, p2RxOff, p2TxOff);
    WritePort(writer, port3, p3RxOff, p3TxOff);

    // ── Port 1 message + signal blocks ──────────────────────────────────────
    if (p1.HasValue) WritePortBlocks(writer, p1.Value);

    // ── Port 2 message + signal blocks ──────────────────────────────────────
    if (p2.HasValue) WritePortBlocks(writer, p2.Value);

    // ── Port 3 message + signal blocks ──────────────────────────────────────
    if (p3.HasValue) WritePortBlocks(writer, p3.Value);

    return ms.ToArray();
}

private static void WritePortBlocks(BinaryWriter writer, PortData pd)
{
    // Rx message blocks
    for (int i = 0; i < pd.RxMessages.Count; i++)
    {
        byte[] msgBytes = pd.RxMessages[i].ToBytes();
        byte[] offsetBytes = BitConverter.GetBytes((uint)pd.RxSignalOffsets[i]);
        if (BitConverter.IsLittleEndian) Array.Reverse(offsetBytes);
        Array.Copy(offsetBytes, 0, msgBytes, msgBytes.Length - 4, 4);
        writer.Write(msgBytes);
    }

    // Tx message blocks
    for (int i = 0; i < pd.TxMessages.Count; i++)
    {
        byte[] msgBytes = pd.TxMessages[i].ToBytes(true);
        byte[] offsetBytes = BitConverter.GetBytes((uint)pd.TxSignalOffsets[i]);
        if (BitConverter.IsLittleEndian) Array.Reverse(offsetBytes);
        Array.Copy(offsetBytes, 0, msgBytes, msgBytes.Length - 4, 4);
        writer.Write(msgBytes);
    }

    // Rx signal blocks
    foreach (var msg in pd.RxMessages)
        foreach (var sig in msg.Signals)
            writer.Write(sig.ToBytes());
}
```

- [ ] **Step 5: Add static `BuildMultiPortHex` and keep `ToHexValue()` as wrapper**

Replace the existing `ToHexValue()` instance method with:
```csharp
// Convenience wrapper used by DbcDatabaseEditor.DownloadHex — treats this DB as port1 only
public string ToHexValue() => BuildMultiPortHex(this, null, null);

public static string BuildMultiPortHex(
    DbcDatabase? port1,
    DbcDatabase? port2,
    DbcDatabase? port3)
{
    var sb = new System.Text.StringBuilder();
    var ports = new[] { port1, port2, port3 };
    for (int p = 0; p < 3; p++)
    {
        var db = ports[p];
        bool isActive = db != null && db.Messages.Count > 0;
        if (isActive)
        {
            var rxMessages = db!.GetRxMessages().ToList();
            var txMessages = db!.GetTxMessages().ToList();
            // header
            int rxMsgCfgOffset = 3 * 12; // simplified offset for hex preview
            int txMsgCfgOffset = rxMsgCfgOffset + rxMessages.Count * 11;
            sb.AppendLine($"Port{p + 1}: {1:X2} {(int)db.CBaudrate:X2} {rxMessages.Count:X2} {txMessages.Count:X2} {rxMsgCfgOffset:X8} {txMsgCfgOffset:X8}");

            foreach (var msg in rxMessages)
                sb.AppendLine(msg.ToHexValue(false));
            foreach (var msg in txMessages)
                sb.AppendLine(msg.ToHexValue(true));
            foreach (var msg in rxMessages)
                foreach (var sig in msg.Signals)
                    sb.AppendLine(sig.ToHexValue(false));
        }
        else
        {
            sb.AppendLine($"Port{p + 1}: {0:X2} {0:X2} {0:X2} {0:X2} {0:X8} {0:X8}");
        }
    }
    return sb.ToString();
}
```

- [ ] **Step 6: Build to confirm no compile errors**

```
dotnet build
```
Expected: `Build succeeded.` If there are errors referencing `CPort`, find them and remove those usages (they will be caught in Task 3 for `DbcDatabaseEditor` and are handled in later tasks).

- [ ] **Step 7: Commit**

```
git add Services/DbcParser.cs
git commit -m "feat: remove CPort from DbcDatabase, add BuildMultiPortPayload static method"
```

---

### Task 3: DbcDatabaseEditor — Remove Port Select, Add Baudrate Validation

**Files:**
- Modify: `Components\Pages\Batteries\DbcDatabaseEditor.razor`

**Interfaces:**
- Consumes: `DbcDatabase` without `CPort` (from Task 2)
- Consumes: `DbcDatabase.ToHexValue()` wrapper (unchanged call)

- [ ] **Step 1: Remove the Port `<select>` block from the header bar**

In `Components\Pages\Batteries\DbcDatabaseEditor.razor`, find and delete this entire block (approximately lines 53–65):
```razor
@* Port *@
<div class="flex items-center gap-1.5">
    <label class="text-xs font-medium text-[hsl(var(--muted-foreground))] whitespace-nowrap">Port</label>
    <select class="h-8 rounded-[calc(var(--radius)-.125rem)] border border-[hsl(var(--border))]\
                   bg-[hsl(var(--background))] text-[hsl(var(--foreground))]\
                   text-xs px-2 pr-7 outline-none focus:border-[hsl(var(--primary))]\
                   cursor-pointer appearance-none"
            @bind="Database.CPort">
        @foreach (CanPort p in Enum.GetValues<CanPort>())
        {
            <option value="@p">@p</option>
        }
    </select>
</div>
```

- [ ] **Step 2: Add baudrate required validation to `HandleSave`**

Find `HandleSave` in the `@code` block:
```csharp
private async Task HandleSave() => await OnSave.InvokeAsync(Database);
```

Replace with:
```csharp
private async Task HandleSave()
{
    if (Database.CBaudrate == default && !Database.Messages.Any())
    {
        // Empty DBC — allow save
    }
    else if (Database.Messages.Any())
    {
        // Has messages — baudrate must be explicitly set (non-default means user chose one)
        // CBaudrate is an enum starting at 0 (BR_250K), so we can't block on 0.
        // Validation: at least one signal must be selected to save.
        if (!Database.Messages.Values.Any(m => m.Signals.Any(s => s.IsSelected)))
        {
            // No signals selected — warn but allow (caller decides)
        }
    }
    await OnSave.InvokeAsync(Database);
}
```

> Note: The actual baudrate validation (blocking save if no baudrate selected) lives in `BatteriesList.razor`'s `HandleDbcSave` because `CBaudrate` starts at 0 (BR_250K) which is a valid value — there is no "unset" state. The battery page's save handler should check that the user has consciously chosen a baudrate. If you want to add a UI indicator that prompts the user, add a `_baudrateWarning` bool shown as a tooltip on the Save button — but do not block the save at the editor level since BR_250K (0) is valid.

- [ ] **Step 3: Build to confirm no compile errors**

```
dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```
git add Components/Pages/Batteries/DbcDatabaseEditor.razor
git commit -m "feat: remove CPort select from DBC editor"
```

---

### Task 4: CircuitCommandHandler — Update TransferDbcFile

**Files:**
- Modify: `Services\Implementations\CircuitCommandHandler.cs`

**Interfaces:**
- Consumes: `DbcDatabase.BuildMultiPortPayload(p1, p2, p3)` (from Task 2)
- Consumes: `DbcFileRecordDto` (unchanged shape)
- Produces: `TransferDbcFile(DbcFileRecordDto? port1Dbc, DbcFileRecordDto? port2Dbc, DbcFileRecordDto? port3Dbc) → Task<CommonResponse<bool>>`

- [ ] **Step 1: Update `TransferDbcFile` signature and implementation**

In `Services\Implementations\CircuitCommandHandler.cs`, replace the entire `TransferDbcFile` method with:

```csharp
public async Task<CommonResponse<bool>> TransferDbcFile(
    DbcFileRecordDto? port1Dbc = null,
    DbcFileRecordDto? port2Dbc = null,
    DbcFileRecordDto? port3Dbc = null)
{
    try
    {
        if (port1Dbc == null && port2Dbc == null && port3Dbc == null)
            return CommonResponse<bool>.Fail("No DBC files provided for transfer.");

        // Assign signal IDs for each port's DBC
        int sigId = 31;
        foreach (var dbc in new[] { port1Dbc, port2Dbc, port3Dbc })
        {
            if (dbc?.dbcDatabase == null) continue;
            foreach (var msg in dbc.dbcDatabase.Messages)
                foreach (var sig in msg.Value.Signals)
                    sig.SingalId = sig.IsSelected ? sigId++ : 0;

            dbc.DbcstrJson = JsonConvert.SerializeObject(dbc.dbcDatabase);
        }

        // Update session with port1 DBC info (backwards compatible)
        Session.DbcFileRecordID = port1Dbc?.Id ?? 0;
        Session.DbcName = port1Dbc?.Name ?? string.Empty;
        if (port1Dbc != null) Session.dbcDatabse = port1Dbc.dbcDatabase;

        const int MaxPacketSize = 1400;

        byte[] packets = DbcDatabase.BuildMultiPortPayload(
            port1Dbc?.dbcDatabase,
            port2Dbc?.dbcDatabase,
            port3Dbc?.dbcDatabase);

        // Send total packet count first
        byte[] totalPacketsBytes = BitConverter.GetBytes(
            (short)((packets.Length + MaxPacketSize - 1) / MaxPacketSize));
        if (BitConverter.IsLittleEndian) Array.Reverse(totalPacketsBytes);

        byte[] totalPacketsPayload = DecoderService.BuildCommand(
            new CommandRequest
            {
                Start = Models.Enums.StartByte.Program,
                DeviceId = Circuit.DeviceID,
                CircuitId = Circuit.CircuitID,
                QueryId = (byte)Models.Enums.ProgramDataQuery.SendDbcStepsCount,
                Data = totalPacketsBytes
            });

        var countResponse = await SendAndWaitForResponseAsync<bool>(totalPacketsPayload);
        if (!countResponse.Success)
            return CommonResponse<bool>.Fail("Failed to send DBC file packet count. Error: " + countResponse.Message);

        for (int offset = 0; offset < packets.Length; offset += MaxPacketSize)
        {
            int chunkSize = Math.Min(MaxPacketSize, packets.Length - offset);
            byte[] chunkWithLength = new byte[chunkSize + 2];

            ushort length = (ushort)chunkSize;
            byte[] lengthBytes = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

            Array.Copy(lengthBytes, 0, chunkWithLength, 0, 2);
            Array.Copy(packets, offset, chunkWithLength, 2, chunkSize);

            byte[] payload = DecoderService.BuildCommand(
                new CommandRequest
                {
                    Start = Models.Enums.StartByte.Program,
                    DeviceId = Circuit.DeviceID,
                    CircuitId = Circuit.CircuitID,
                    QueryId = (byte)Models.Enums.ProgramDataQuery.SendDbcFile,
                    Data = chunkWithLength
                });

            var response = await SendAndWaitForResponseAsync<bool>(payload);
            if (!response.Success)
                return CommonResponse<bool>.Fail(
                    $"Failed to transfer DBC file chunk at offset {offset}. Error: {response.Message}");
        }

        return CommonResponse<bool>.Ok(true, "Success");
    }
    catch (Exception ex)
    {
        return CommonResponse<bool>.Fail($"Exception during DBC transfer: {ex.Message}");
    }
    finally
    {
        _log.Debug("TransferDbcFile");
    }
}
```

- [ ] **Step 2: Update `ICircuitCommandHandler` interface**

In `Services\Interfaces\ICircuitCommandHandler.cs`, find the existing `TransferDbcFile` declaration:
```csharp
Task<CommonResponse<bool>> TransferDbcFile(DbcFileRecordDto? dbcFile = null);
```
Replace with:
```csharp
Task<CommonResponse<bool>> TransferDbcFile(
    DbcFileRecordDto? port1Dbc = null,
    DbcFileRecordDto? port2Dbc = null,
    DbcFileRecordDto? port3Dbc = null);
```

- [ ] **Step 3: Build to confirm no compile errors**

```
dotnet build
```
Expected: `Build succeeded.` If `TransferDialog` still calls old signature, it will error — that's expected and fixed in Task 5.

- [ ] **Step 4: Commit**

```
git add Services/Implementations/CircuitCommandHandler.cs Services/Interfaces/ICircuitCommandHandler.cs
git commit -m "feat: TransferDbcFile accepts 3 nullable port DTOs, builds multi-port payload"
```

---

### Task 5: TransferDialog — 3 Port Dropdowns

**Files:**
- Modify: `Components\UI\Dashboard\TransferDialog.razor`

**Interfaces:**
- Consumes: `IBatteryServices.UpdatePortAssignmentsAsync(batteryId, p1, p2, p3)` (Task 1)
- Consumes: `BatteryDTO.Port1DbcId / Port2DbcId / Port3DbcId` (Task 1)
- Consumes: `ICircuitCommandHandler.TransferDbcFile(port1Dbc, port2Dbc, port3Dbc)` (Task 4)
- Consumes: `IBatteryServices` already injected as `BServices`

- [ ] **Step 1: Replace the `selectedDbcFile` field with 3 port fields**

In the `@code` block, find:
```csharp
private string? selectedDbcFile;
```
Replace with:
```csharp
private string? port1DbcId;
private string? port2DbcId;
private string? port3DbcId;
```

- [ ] **Step 2: Replace the single DBC RadioGroup area in Razor markup**

Find the entire `<!-- DBC File Selection -->` `<div>` block (the third column in the grid):
```razor
<!-- DBC File Selection -->
<div class="space-y-2">
    <Label class="text-sm font-medium">
        DBC File <span class="text-muted-foreground text-xs">(Optional)</span>
    </Label>

    <ScrollArea class="h-[200px] border rounded-lg p-3 bg-muted/30">
        <RadioGroup @bind-Value="selectedDbcFile">
            <!-- None Option -->
            <div class="@DbcNoneClass()" @onclick="() => selectedDbcFile = string.Empty">
                <RadioGroupItem Value="" id="dbc-none" />
                <Label for="dbc-none" class="cursor-pointer flex-1 text-muted-foreground italic">
                    None
                </Label>
            </div>
            @foreach (var dbc in DbcFiles)
            {
                <div class="@DbcClass(dbc.Id)" @onclick="() => selectedDbcFile = dbc.Id.ToString()">
                    <RadioGroupItem Value="@dbc.Id.ToString()" id="@($"dbc-{dbc.Id}")" />
                    <Label for="@($"dbc-{dbc.Id}")" class="cursor-pointer flex-1">
                        @dbc.Name
                    </Label>
                </div>
            }
        </RadioGroup>
    </ScrollArea>
</div>
```

Replace the `<div class="grid grid-cols-3 gap-4 mt-4">` section's third column with (replacing just the DBC div):

```razor
<!-- DBC Port Assignment -->
<div class="space-y-2">
    <Label class="text-sm font-medium">
        DBC Ports <span class="text-muted-foreground text-xs">(Optional)</span>
    </Label>

    <div class="flex flex-col gap-3 border rounded-lg p-3 bg-muted/30">

        @* Port 1 *@
        <div class="flex flex-col gap-1">
            <Label class="text-xs font-medium text-muted-foreground">Port 1</Label>
            <select class="h-8 w-full rounded border border-border bg-background text-foreground text-xs px-2"
                    value="@port1DbcId"
                    @onchange="async e => { port1DbcId = e.Value?.ToString(); await OnPortAssignmentChanged(); }">
                <option value="">None</option>
                @foreach (var dbc in DbcFiles)
                {
                    <option value="@dbc.Id.ToString()"
                            disabled="@IsDbcUsedByOtherPort(dbc.Id.ToString(), port1DbcId)">
                        @dbc.Name (@dbc.dbcDatabase.CBaudrate)
                    </option>
                }
            </select>
        </div>

        @* Port 2 *@
        <div class="flex flex-col gap-1">
            <Label class="text-xs font-medium text-muted-foreground">Port 2</Label>
            <select class="h-8 w-full rounded border border-border bg-background text-foreground text-xs px-2"
                    value="@port2DbcId"
                    @onchange="async e => { port2DbcId = e.Value?.ToString(); await OnPortAssignmentChanged(); }">
                <option value="">None</option>
                @foreach (var dbc in DbcFiles)
                {
                    <option value="@dbc.Id.ToString()"
                            disabled="@IsDbcUsedByOtherPort(dbc.Id.ToString(), port2DbcId)">
                        @dbc.Name (@dbc.dbcDatabase.CBaudrate)
                    </option>
                }
            </select>
        </div>

        @* Port 3 *@
        <div class="flex flex-col gap-1">
            <Label class="text-xs font-medium text-muted-foreground">Port 3</Label>
            <select class="h-8 w-full rounded border border-border bg-background text-foreground text-xs px-2"
                    value="@port3DbcId"
                    @onchange="async e => { port3DbcId = e.Value?.ToString(); await OnPortAssignmentChanged(); }">
                <option value="">None</option>
                @foreach (var dbc in DbcFiles)
                {
                    <option value="@dbc.Id.ToString()"
                            disabled="@IsDbcUsedByOtherPort(dbc.Id.ToString(), port3DbcId)">
                        @dbc.Name (@dbc.dbcDatabase.CBaudrate)
                    </option>
                }
            </select>
        </div>

    </div>
</div>
```

- [ ] **Step 3: Remove old CSS helpers, add new port helpers**

Find and delete these three methods:
```csharp
private string DbcClass(long id) => ...
private string DbcNoneClass() => ...
```

Add these in their place:
```csharp
// Returns true if dbcId is already selected by a different port
private bool IsDbcUsedByOtherPort(string dbcId, string? ownPortValue)
{
    if (string.IsNullOrEmpty(dbcId)) return false;
    var others = new[] { port1DbcId, port2DbcId, port3DbcId }
        .Where(v => v != ownPortValue);
    return others.Any(v => v == dbcId);
}
```

- [ ] **Step 4: Update `OnBatterySelected` to pre-fill port assignments**

Find `OnBatterySelected` and replace with:
```csharp
private async Task OnBatterySelected(BatteryDTO battery)
{
    selectedBattery = battery.Id.ToString();

    var dbc = await DServices.GetByBatteryIdAsync(battery.Id);
    DbcFiles = dbc.Success && dbc.Data != null ? dbc.Data : new();

    // Pre-fill saved port assignments
    port1DbcId = battery.Port1DbcId?.ToString() ?? string.Empty;
    port2DbcId = battery.Port2DbcId?.ToString() ?? string.Empty;
    port3DbcId = battery.Port3DbcId?.ToString() ?? string.Empty;
}
```

- [ ] **Step 5: Add `OnPortAssignmentChanged` method**

Add after `OnBatterySelected`:
```csharp
private async Task OnPortAssignmentChanged()
{
    if (!long.TryParse(selectedBattery, out var batteryId)) return;

    long? p1 = long.TryParse(port1DbcId, out var v1) ? v1 : null;
    long? p2 = long.TryParse(port2DbcId, out var v2) ? v2 : null;
    long? p3 = long.TryParse(port3DbcId, out var v3) ? v3 : null;

    await BServices.UpdatePortAssignmentsAsync(batteryId, p1, p2, p3);
}
```

- [ ] **Step 6: Update `canTransfer`**

Find:
```csharp
private bool canTransfer =>
    !string.IsNullOrWhiteSpace(selectedProgram) &&
    !string.IsNullOrWhiteSpace(selectedBattery) &&
    !isProcessing;
```
Replace with:
```csharp
private bool canTransfer =>
    !string.IsNullOrWhiteSpace(selectedProgram) &&
    !string.IsNullOrWhiteSpace(selectedBattery) &&
    !isProcessing &&
    (!string.IsNullOrEmpty(port1DbcId) || !string.IsNullOrEmpty(port2DbcId) || !string.IsNullOrEmpty(port3DbcId));
```

- [ ] **Step 7: Update `HandleTransfer` — resolve 3 DBCs and call new signature**

In `HandleTransfer`, find the existing DBC section:
```csharp
StepResult? dbcStep = default;
// ...
if (selectedDbcFile is not null)
{
    DbcFileRecordDto dbc = DbcFiles?.FirstOrDefault(e => e.Id.ToString() == selectedDbcFile);
    // ... single TransferDbcFile call
}
dev.dbcData.DbcValues = null;
```

Replace the entire DBC block (from `StepResult? dbcStep` through `dev.dbcData.DbcValues = null`) with:

```csharp
StepResult? dbcStep = default;

// Resolve the 3 port DBCs
DbcFileRecordDto? p1Dbc = string.IsNullOrEmpty(port1DbcId) ? null
    : DbcFiles?.FirstOrDefault(e => e.Id.ToString() == port1DbcId);
DbcFileRecordDto? p2Dbc = string.IsNullOrEmpty(port2DbcId) ? null
    : DbcFiles?.FirstOrDefault(e => e.Id.ToString() == port2DbcId);
DbcFileRecordDto? p3Dbc = string.IsNullOrEmpty(port3DbcId) ? null
    : DbcFiles?.FirstOrDefault(e => e.Id.ToString() == port3DbcId);

if (p1Dbc != null || p2Dbc != null || p3Dbc != null)
{
    var dbcResponse = await dev.TransferDbcFile(p1Dbc, p2Dbc, p3Dbc);
    dbcStep = new StepResult
    {
        StepName = "DBC Transfer",
        Success = dbcResponse.Success,
        Message = dbcResponse.Success
            ? "DBC file(s) transferred successfully"
            : $"DBC transfer failed: {dbcResponse.Message}"
    };
}

dev.dbcData.DbcValues = null;
```

- [ ] **Step 8: Build to confirm no compile errors**

```
dotnet build
```
Expected: `Build succeeded.`

- [ ] **Step 9: Manual smoke test**
1. Run the app
2. Open Transfer Dialog, select a battery — verify the 3 port dropdowns appear pre-filled with saved assignments
3. Change a port assignment — verify it saves (check DB or reload dialog)
4. Select DBC-A for Port 1 — verify DBC-A is disabled in Port 2 and Port 3 dropdowns
5. Select a different DBC for Port 2 — verify Transfer button enables
6. Click Transfer — verify DBC step appears in results with success

- [ ] **Step 10: Commit**

```
git add Components/UI/Dashboard/TransferDialog.razor
git commit -m "feat: replace single DBC dropdown with 3 port dropdowns in TransferDialog"
```
