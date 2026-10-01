# Device/SecondaryBoard/Channel Hierarchy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a `SecondaryBoard` level between `Device` and `Circuit`, rename `Circuit`→`Channel` everywhere, switch numbering from 0-indexed to 1-indexed (1-8 for both board and channel), and fold in the in-memory handler-key fix so multiple secondaries per device work correctly.

**Architecture:** `Device` (1) → `SecondaryBoard` (1-8) → `Channel` (1-8). The wire address byte stays 4-bit/4-bit (unchanged protocol), carrying the 1-8 value directly with no offset math. `ChannelAddressCodec` centralizes encode/decode. The in-memory `ConcurrentDictionary<string, IChannelCommandHandler>` key becomes a 3-part string (`DeviceId-BoardNumber-ChannelNumber`) instead of 2-part.

**Tech Stack:** .NET / C#, Blazor Server, EF Core (SQLite), xUnit (new — no test project exists yet).

## Global Constraints

- Numbering: `BoardNumber` and `ChannelNumber` are both `[Range(1,8)]` — there is no valid `0`.
- Wire encoding: nibble carries the 1-8 value directly. `ChannelAddressCodec.Encode` validates 1-8 and throws `ArgumentOutOfRangeException` outside that range. `Decode` never throws.
- Full rename: every `Circuit`-prefixed identifier becomes `Channel`-prefixed (see per-task tables). No mixed `Circuit`/`Channel` naming left when this plan completes.
- Legacy data migration: `new ChannelNumber = old CircuitID + 1`; all pre-existing channels attach to one implicit `SecondaryBoard(BoardNumber=1, IsImplicit=true)` per device.
- Migration blocker: if any existing device has `MAX(CircuitID) >= 8`, the migration must fail loudly (the `+1` shift would exceed the new cap) — do not silently truncate.
- No test project exists in this repo today. Task 1 creates one. Existing behavior-preserving renames are verified by build + the existing `TestDeviceSimulator.ps1` manual run (per the design spec's Verification section), not by new automated tests, since there is no existing suite to extend safely within this plan's scope.

---

### Task 1: Create test project

**Files:**
- Create: `BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
- Create: `BatteryTestingSystem.Tests/Utils/ChannelAddressCodecTests.cs` (placeholder test to confirm wiring)
- Modify: `BatteryTestingSystem.sln`

**Interfaces:**
- Produces: a working `dotnet test` command that later tasks add real tests to.

- [ ] **Step 1: Create the test project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BatteryTestingSystem.csproj" />
  </ItemGroup>

</Project>
```

Save at `D:\WorkArea\Projects\WebAppME\BatteryTestingSystem.Tests\BatteryTestingSystem.Tests.csproj`.

- [ ] **Step 2: Add a placeholder test**

```csharp
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

public class ChannelAddressCodecTests
{
    [Fact]
    public void Placeholder_ProjectWiredUp()
    {
        Assert.True(true);
    }
}
```

Save at `D:\WorkArea\Projects\WebAppME\BatteryTestingSystem.Tests\Utils\ChannelAddressCodecTests.cs`.

- [ ] **Step 3: Add the project to the solution**

Run: `dotnet sln BatteryTestingSystem.sln add BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`

- [ ] **Step 4: Run the placeholder test to confirm wiring**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add BatteryTestingSystem.Tests/ BatteryTestingSystem.sln
git commit -m "test: add xUnit test project"
```

---

### Task 2: `ChannelAddressCodec` (TDD)

**Files:**
- Create: `Utils/ChannelAddressCodec.cs`
- Test: `BatteryTestingSystem.Tests/Utils/ChannelAddressCodecTests.cs` (replace placeholder)

**Interfaces:**
- Produces: `ChannelAddressCodec.Encode(int boardNumber, int channelNumber) -> byte`, `ChannelAddressCodec.Decode(byte value) -> (int BoardNumber, int ChannelNumber)`, `ChannelAddressCodec.EncodeLegacy(int channelNumber) -> byte`. Namespace `BatteryTestingSystem.Utils`, matching `BigEndian`/`LittleEndian` sibling style (plain static class, no XML docs, no bounds checking on `Decode`).

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using BatteryTestingSystem.Utils;
using Xunit;

namespace BatteryTestingSystem.Tests.Utils;

public class ChannelAddressCodecTests
{
    [Theory]
    [InlineData(1, 1, 0x11)]
    [InlineData(1, 8, 0x18)]
    [InlineData(8, 1, 0x81)]
    [InlineData(8, 8, 0x88)]
    [InlineData(3, 5, 0x35)]
    public void Encode_PacksBoardAndChannelIntoNibbles(int board, int channel, byte expected)
    {
        Assert.Equal(expected, ChannelAddressCodec.Encode(board, channel));
    }

    [Theory]
    [InlineData(0x11, 1, 1)]
    [InlineData(0x18, 1, 8)]
    [InlineData(0x81, 8, 1)]
    [InlineData(0x88, 8, 8)]
    public void Decode_UnpacksBoardAndChannelFromNibbles(byte value, int expectedBoard, int expectedChannel)
    {
        var (board, channel) = ChannelAddressCodec.Decode(value);
        Assert.Equal(expectedBoard, board);
        Assert.Equal(expectedChannel, channel);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 8)]
    [InlineData(8, 1)]
    [InlineData(8, 8)]
    [InlineData(4, 4)]
    public void EncodeThenDecode_RoundTrips(int board, int channel)
    {
        byte encoded = ChannelAddressCodec.Encode(board, channel);
        var (decodedBoard, decodedChannel) = ChannelAddressCodec.Decode(encoded);
        Assert.Equal(board, decodedBoard);
        Assert.Equal(channel, decodedChannel);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(9, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 9)]
    [InlineData(-1, 1)]
    public void Encode_OutOfRange_Throws(int board, int channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelAddressCodec.Encode(board, channel));
    }

    [Fact]
    public void EncodeLegacy_DefaultsToBoardOne()
    {
        Assert.Equal(ChannelAddressCodec.Encode(1, 5), ChannelAddressCodec.EncodeLegacy(5));
    }

    [Fact]
    public void Decode_NeverThrows_EvenForNonsenseByte()
    {
        var (board, channel) = ChannelAddressCodec.Decode(0xFF);
        Assert.Equal(15, board);
        Assert.Equal(15, channel);
    }
}
```

Replace the full contents of `BatteryTestingSystem.Tests/Utils/ChannelAddressCodecTests.cs` with the above.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: FAIL — `ChannelAddressCodec` does not exist (compile error).

- [ ] **Step 3: Write the implementation**

```csharp
namespace BatteryTestingSystem.Utils
{
    public class ChannelAddressCodec
    {
        public static byte Encode(int boardNumber, int channelNumber)
        {
            if (boardNumber < 1 || boardNumber > 8)
                throw new System.ArgumentOutOfRangeException(nameof(boardNumber), boardNumber, "BoardNumber must be 1-8.");
            if (channelNumber < 1 || channelNumber > 8)
                throw new System.ArgumentOutOfRangeException(nameof(channelNumber), channelNumber, "ChannelNumber must be 1-8.");

            return (byte)(((boardNumber & 0xF) << 4) | (channelNumber & 0xF));
        }

        public static (int BoardNumber, int ChannelNumber) Decode(byte value)
        {
            return ((value >> 4) & 0xF, value & 0xF);
        }

        public static byte EncodeLegacy(int channelNumber) => Encode(1, channelNumber);
    }
}
```

Save at `D:\WorkArea\Projects\WebAppME\Utils\ChannelAddressCodec.cs`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BatteryTestingSystem.Tests/BatteryTestingSystem.Tests.csproj`
Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Utils/ChannelAddressCodec.cs BatteryTestingSystem.Tests/Utils/ChannelAddressCodecTests.cs
git commit -m "feat: add ChannelAddressCodec for 1-8 board/channel wire encoding"
```

---

### Task 3: `SecondaryBoard` entity

**Files:**
- Create: `Models/Entities/SecondaryBoard.cs`

**Interfaces:**
- Consumes: `BaseEntity` (existing base class already used by `Circuit`/`CalibrationDataPoint` — provides `CreatedBy`/`UpdatedBy`/`CreatedAt`/`UpdatedAt`/`IsDeleted`).
- Produces: `SecondaryBoard` class with `Id` (long), `DeviceId` (int), `BoardNumber` (int, 1-8), `IsImplicit` (bool) — consumed by Task 4 (`Channel.SecondaryBoard` nav) and Task 8 (`AppDbContext`).

- [ ] **Step 1: Write the entity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "SecondaryBoards", Schema = "Device")]
    public class SecondaryBoard : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Range(1, 8)]
        public int BoardNumber { get; set; }

        public bool IsImplicit { get; set; } = false;
    }
}
```

Save at `D:\WorkArea\Projects\WebAppME\Models\Entities\SecondaryBoard.cs`.

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: builds with no new errors (the class isn't referenced yet, so it's inert).

- [ ] **Step 3: Commit**

```bash
git add Models/Entities/SecondaryBoard.cs
git commit -m "feat: add SecondaryBoard entity"
```

---

### Task 4: Rename `Circuit` entity → `Channel`, add `SecondaryBoardId`

**Files:**
- Modify: `Models/Entities/Circuit.cs` → rename to `Models/Entities/Channel.cs`

**Interfaces:**
- Consumes: `SecondaryBoard` (Task 3).
- Produces: `Channel` class with `Id`, `DeviceId`, `ChannelNumber` (renamed from `CircuitID`), `IsRegistered`, `SecondarySerialNumber`, `ChannelType` (renamed from `CircuitType`), `SwVersion`, `AssemblyDate`, `ZntMaxVoltage`, `LntMaxVoltage`, `ChannelMaxVoltage`/`ChannelMinVoltage`/`ChannelMaxDischargingCurrent`/`ChannelMaxChargingCurrent` (renamed from `CircuitMax*`), `SecondaryBoardId`, `SecondaryBoard` nav property. Consumed by every later task.

Current file (`Models/Entities/Circuit.cs`, 41 lines) is:

```csharp
using BatteryTestingSystem.DbSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "Circuits", Schema = "Device")]
    //[Encrypted]

    public class Circuit : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        //[NotEncrypted] 
        public long Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [MaxLength(1)]
        public int CircuitID { get; set; }

        public bool IsRegistered { get; set; } = false;

        public string? SecondarySerialNumber { get; set; }      // 4 bytes

        [MaxLength(10)]
        public string CircuitType { get; set; } = "Single"; // "Single" or "Dual"

        [MaxLength(10)]
        public string? SwVersion { get; set; } = string.Empty; // 11 bytes
        public DateTime? AssemblyDate { get; set; }  // 4 bytes
        public float? ZntMaxVoltage { get; set; }   // 4 bytes
        public float? LntMaxVoltage { get; set; }  // 4 bytes
        public float? CircuitMaxVoltage { get; set; }  // 4 bytes
        public float? CircuitMinVoltage { get; set; }  // 4 bytes
        public float? CircuitMaxDischargingCurrent { get; set; }  // 4 bytes
        public float? CircuitMaxChargingCurrent { get; set; }  // 4 bytes

    }
}
```

- [ ] **Step 1: Delete `Models/Entities/Circuit.cs` and create `Models/Entities/Channel.cs`**

```csharp
using BatteryTestingSystem.DbSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatteryTestingSystem.Models.Entities
{
    [Table(name: "Channels", Schema = "Device")]

    public class Channel : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public int DeviceId { get; set; }

        [Required]
        public long SecondaryBoardId { get; set; }

        public SecondaryBoard SecondaryBoard { get; set; } = null!;

        [Range(1, 8)]
        public int ChannelNumber { get; set; }

        public bool IsRegistered { get; set; } = false;

        public string? SecondarySerialNumber { get; set; }      // 4 bytes

        [MaxLength(10)]
        public string ChannelType { get; set; } = "Single"; // "Single" or "Dual"

        [MaxLength(10)]
        public string? SwVersion { get; set; } = string.Empty; // 11 bytes
        public DateTime? AssemblyDate { get; set; }  // 4 bytes
        public float? ZntMaxVoltage { get; set; }   // 4 bytes
        public float? LntMaxVoltage { get; set; }  // 4 bytes
        public float? ChannelMaxVoltage { get; set; }  // 4 bytes
        public float? ChannelMinVoltage { get; set; }  // 4 bytes
        public float? ChannelMaxDischargingCurrent { get; set; }  // 4 bytes
        public float? ChannelMaxChargingCurrent { get; set; }  // 4 bytes

    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Models/Entities/Channel.cs
git rm Models/Entities/Circuit.cs
git commit -m "feat: rename Circuit entity to Channel, add SecondaryBoardId"
```

(Build will fail until later tasks update every consumer — that's expected; each subsequent task fixes one more compile error group. Do not attempt to build green until Task 12 completes.)

---

### Task 5: Rename `CircuitDto` → `ChannelDto`, add `SecondaryBoardNumber`

**Files:**
- Modify: `Models/DTOs/CircuitDto.cs` → rename to `Models/DTOs/ChannelDto.cs`

**Interfaces:**
- Produces: `ChannelDto` with `ChannelNumber` (renamed from `CircuitID`), `ChannelType` (renamed from `CircuitType`), `ChannelMaxVoltage`/`ChannelMinVoltage`/`ChannelMaxDischargingCurrent`/`ChannelMaxChargingCurrent` (renamed from `CircuitMax*`), plus new `SecondaryBoardNumber` (int, default `1`). All other fields (`DeviceID`, `DeviceName`, `MACID`, `IPAddress`, etc.) unchanged. Consumed by every service/repository/UI task below.

Current file is (37 lines):

```csharp
using BatteryTestingSystem.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class CircuitDto
    {
        public int DeviceID { get; set; }
        public int CircuitID { get; set; }
        public string DeviceName { get; set; }
        public string MACID { get; set; }
        public string IPAddress { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsRegistered { get; set; } = false;
        public bool DHCPEnable { get; set; } = false;
        public string? MasterSwVersion { get; set; } = string.Empty;
        public string? ComSwVersion { get; set; } = string.Empty;
        public DateTime? ManufactureDateTime { get; set; }
        public DateTime? CommissioningDateTime { get; set; }
        public DateTime? AssemblyDate { get; set; }
        public string PrimarySerialNumber { get; set; } = string.Empty; // 4 bytes
        public string SecondarySerialNumber { get; set; } = string.Empty; // 4 bytes
        public string CircuitType { get; set; } = "Single"; // "Single" or "Dual"
        public string? SecondarySwVersion { get; set; } = string.Empty;
        public DateTime? SecondaryAssemblyDate { get; set; }
        public float? ZntMaxVoltage { get; set; }
        public float? LntMaxVoltage { get; set; }
        public float? CircuitMaxVoltage { get; set; }
        public float? CircuitMinVoltage { get; set; }
        public float? CircuitMaxDischargingCurrent { get; set; }
        public float? CircuitMaxChargingCurrent { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; } 
        public DateTime? UpdatedAt { get; set; } 

    }
}
```

- [ ] **Step 1: Delete `Models/DTOs/CircuitDto.cs`, create `Models/DTOs/ChannelDto.cs`**

```csharp
using BatteryTestingSystem.Models.Entities;
using System.ComponentModel.DataAnnotations;

namespace BatteryTestingSystem.Models.DTOs
{
    public class ChannelDto
    {
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; } = 1;
        public int ChannelNumber { get; set; }
        public string DeviceName { get; set; }
        public string MACID { get; set; }
        public string IPAddress { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsRegistered { get; set; } = false;
        public bool DHCPEnable { get; set; } = false;
        public string? MasterSwVersion { get; set; } = string.Empty;
        public string? ComSwVersion { get; set; } = string.Empty;
        public DateTime? ManufactureDateTime { get; set; }
        public DateTime? CommissioningDateTime { get; set; }
        public DateTime? AssemblyDate { get; set; }
        public string PrimarySerialNumber { get; set; } = string.Empty; // 4 bytes
        public string SecondarySerialNumber { get; set; } = string.Empty; // 4 bytes
        public string ChannelType { get; set; } = "Single"; // "Single" or "Dual"
        public string? SecondarySwVersion { get; set; } = string.Empty;
        public DateTime? SecondaryAssemblyDate { get; set; }
        public float? ZntMaxVoltage { get; set; }
        public float? LntMaxVoltage { get; set; }
        public float? ChannelMaxVoltage { get; set; }
        public float? ChannelMinVoltage { get; set; }
        public float? ChannelMaxDischargingCurrent { get; set; }
        public float? ChannelMaxChargingCurrent { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? CreatedAt { get; set; } 
        public DateTime? UpdatedAt { get; set; } 

    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Models/DTOs/ChannelDto.cs
git rm Models/DTOs/CircuitDto.cs
git commit -m "feat: rename CircuitDto to ChannelDto, add SecondaryBoardNumber"
```

---

### Task 6: Rename `CircuitID`/`CircuitId` fields on the remaining DTOs/entities, add `SecondaryBoardNumber` where relevant

**Files:**
- Modify: `Models/DTOs/RequestDTOs.cs` (`CommandRequest` class, lines 10-22)
- Modify: `Models/DTOs/SessionRecordDto.cs` (lines 6-67)
- Modify: `Models/SqliteEntities/MeasurementData.cs` (lines 9-139)
- Modify: `Models/DTOs/CalibrationDataPointDto.cs` (lines 10-27)
- Modify: `Models/DTOs/RealTimeRecordDto.cs` (lines 7-73)
- Modify: `Models/ViewModels/CommonRequest.cs` (lines 3-11)
- Modify: `Models/Entities/CalibrationDataPoint.cs` (lines 7-22)

**Interfaces:**
- Produces: every one of these types' `CircuitId`/`CircuitID` field renamed to `ChannelNumber`/`ChannelId` (match each type's existing casing convention exactly — see per-type table below) plus a new `int SecondaryBoardNumber` field (default `1` where the type is constructed with defaults; no default where every construction site already sets every field explicitly). Consumed by Task 8 (DecoderService), Task 9 (repository), Task 10 (ChannelManager), Task 11 (ChannelCommandHandler), Task 12 (DeviceController).

Rename table (old name → new name, per type):

| Type | Old field | New field | Add `SecondaryBoardNumber`? |
|---|---|---|---|
| `CommandRequest` | `CircuitId` | `ChannelId` | Yes, no default (every one of the 22 call sites in Task 11 sets it explicitly) |
| `SessionRecordDto` | `CircuitID` | `ChannelNumber` | Yes, no default |
| `MeasurementData` | `CircuitId` | `ChannelId` | Yes, no default |
| `CalibrationDataPointDto` | `CircuitId` | `ChannelId` | Yes, no default |
| `RealTimeRecordDto` | `CircuitID` | `ChannelNumber` | Yes, no default |
| `CommonRequest` (ViewModels) | `CircuitID` | `ChannelNumber` | Yes, default `1` (REST callers may omit it, matching today's optional-field pattern for `ProgramId`/`BatteryId`) |
| `CalibrationDataPoint` (entity) | `CircuitId` | `ChannelId` | Yes, no default |

- [ ] **Step 1: `Models/DTOs/RequestDTOs.cs` — `CommandRequest`**

Current (lines 10-22):
```csharp
public class CommandRequest
{
    public StartByte Start { get; set; }
    public int DeviceId { get; set; }
    public int CircuitId { get; set; }
    public byte QueryId { get; set; }
    public byte? Range { get; set; }

    // Optional payload (e.g., Epoch time, program metadata, program steps)
    public byte[]? Data { get; set; }
    public byte? SingleByte { get; set; }

}
```
Change to:
```csharp
public class CommandRequest
{
    public StartByte Start { get; set; }
    public int DeviceId { get; set; }
    public int SecondaryBoardNumber { get; set; }
    public int ChannelId { get; set; }
    public byte QueryId { get; set; }
    public byte? Range { get; set; }

    // Optional payload (e.g., Epoch time, program metadata, program steps)
    public byte[]? Data { get; set; }
    public byte? SingleByte { get; set; }

}
```

- [ ] **Step 2: `Models/DTOs/SessionRecordDto.cs`**

Change:
```csharp
        [Required]
        public long CircuitID { get; set; }
```
to:
```csharp
        [Required]
        public long SecondaryBoardNumber { get; set; }
        [Required]
        public long ChannelNumber { get; set; }
```

- [ ] **Step 3: `Models/SqliteEntities/MeasurementData.cs`**

Change:
```csharp
        public int DeviceId { get; set; }
        public int CircuitId { get; set; }
```
to:
```csharp
        public int DeviceId { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelId { get; set; }
```

- [ ] **Step 4: `Models/DTOs/CalibrationDataPointDto.cs`**

Change:
```csharp
        public int DeviceId { get; set; }
        public int CircuitId { get; set; }
```
to:
```csharp
        public int DeviceId { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelId { get; set; }
```

- [ ] **Step 5: `Models/DTOs/RealTimeRecordDto.cs`**

Change:
```csharp
        public int DeviceID { get; set; }
        public int CircuitID { get; set; }
```
to:
```csharp
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelNumber { get; set; }
```

- [ ] **Step 6: `Models/ViewModels/CommonRequest.cs`**

Change:
```csharp
    public class CommonRequest
    {
        public int DeviceID { get; set; }
        public int CircuitID { get; set; }
        public int? ProgramId { get; set; }   // for SetProgram
        public int? BatteryId { get; set; }   // for SetProgram
        public int? dbcId { get; set; } = 0;

    }
```
to:
```csharp
    public class CommonRequest
    {
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; } = 1;
        public int ChannelNumber { get; set; }
        public int? ProgramId { get; set; }   // for SetProgram
        public int? BatteryId { get; set; }   // for SetProgram
        public int? dbcId { get; set; } = 0;

    }
```

- [ ] **Step 7: `Models/Entities/CalibrationDataPoint.cs`**

Change:
```csharp
        public int DeviceId { get; set; }
        public int CircuitId { get; set; }
```
to:
```csharp
        public int DeviceId { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelId { get; set; }
```

- [ ] **Step 8: Commit**

```bash
git add Models/DTOs/RequestDTOs.cs Models/DTOs/SessionRecordDto.cs Models/SqliteEntities/MeasurementData.cs Models/DTOs/CalibrationDataPointDto.cs Models/DTOs/RealTimeRecordDto.cs Models/ViewModels/CommonRequest.cs Models/Entities/CalibrationDataPoint.cs
git commit -m "feat: rename CircuitId/CircuitID fields to Channel*, add SecondaryBoardNumber to supporting DTOs/entities"
```

(This still won't build — every consumer of the old field names errors until later tasks. Expected.)

---

### Task 7: `AppDbContext` — add `SecondaryBoards` DbSet, rename `Circuits`→`Channels`

**Files:**
- Modify: `Data/AppDbContext.cs`

**Interfaces:**
- Consumes: `Channel` (Task 4), `SecondaryBoard` (Task 3).
- Produces: `DbSet<SecondaryBoard> SecondaryBoards`, `DbSet<Channel> Channels` (renamed from `Circuits`), explicit Fluent config for both new FK relationships and unique indexes.

Current `OnModelCreating` has **no explicit Fluent config** for `Circuit`/`Device` — only Identity table renames. Per the design spec (§6), this now needs explicit config since a second FK chain onto `Device` (via `SecondaryBoard`) makes convention-based inference ambiguous.

- [ ] **Step 1: Update the DbSet declarations**

Change (line 23):
```csharp
        public DbSet<Circuit> Circuits { get; set; } = null!;
```
to:
```csharp
        public DbSet<Channel> Channels { get; set; } = null!;
        public DbSet<SecondaryBoard> SecondaryBoards { get; set; } = null!;
```

- [ ] **Step 2: Add explicit Fluent config in `OnModelCreating`**

After the existing Identity table-rename block (after line 60, before `base.OnModelCreating(modelBuilder);`), add:

```csharp
            modelBuilder.Entity<SecondaryBoard>(b =>
            {
                b.HasOne<Device>()
                    .WithMany()
                    .HasForeignKey(sb => sb.DeviceId)
                    .HasPrincipalKey(d => d.DeviceID);

                b.HasIndex(sb => new { sb.DeviceId, sb.BoardNumber }).IsUnique();
            });

            modelBuilder.Entity<Channel>(b =>
            {
                b.HasOne(c => c.SecondaryBoard)
                    .WithMany()
                    .HasForeignKey(c => c.SecondaryBoardId);

                b.HasIndex(c => new { c.SecondaryBoardId, c.ChannelNumber }).IsUnique();
            });
```

- [ ] **Step 3: Commit**

```bash
git add Data/AppDbContext.cs
git commit -m "feat: add SecondaryBoards DbSet, rename Circuits to Channels, explicit FK config"
```

---

### Task 8: EF Core migration + backfill

**Files:**
- Create: `Migrations/<timestamp>_AddSecondaryBoardAndChannel.cs` (generated by `dotnet ef migrations add`, then hand-edited for the backfill SQL)

**Interfaces:**
- Consumes: `AppDbContext` (Task 7), `SecondaryBoard`/`Channel` entities (Tasks 3-4).
- Produces: the `SecondaryBoards` table, the renamed `Channels` table/`ChannelNumber` column, and one-time backfill data.

- [ ] **Step 1: Generate the migration scaffold**

Run: `dotnet ef migrations add AddSecondaryBoardAndChannel -c AppDbContext`

This produces a scaffold that will already contain the table rename/create/column-rename operations EF infers from the model diff (matching the existing repo's migration-naming convention: `Add-Migration ... -Context AppDbContext`). Inspect the generated file before continuing — EF's auto-generated rename detection can sometimes emit drop+recreate instead of rename; if so, replace those operations with explicit `RenameTable`/`RenameColumn` calls to preserve data, following the exact style seen in `Migrations/20260714084006_AddScheduleDbcPorts.cs` (named-argument `migrationBuilder.RenameColumn(name:, table:, newName:)` / `AddColumn<T>(name:, table:, type: "INTEGER", nullable:)`).

- [ ] **Step 2: Hand-edit the `Up` method to insert the pre-backfill safety check and backfill SQL**

After the schema-shape operations (create `SecondaryBoards`, rename `Circuits`→`Channels`, add nullable `SecondaryBoardId`) and before the final NOT NULL/FK/unique-index operations, insert:

```csharp
            migrationBuilder.Sql(@"
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1 FROM Device.Channels
                        GROUP BY DeviceId
                        HAVING MAX(ChannelNumber) + 1 > 8
                    )
                    THEN RAISE(ABORT, 'Migration blocked: a device has CircuitID >= 8, which would overflow the new 8-channel-per-board cap after the +1 shift. Resolve the data before re-running this migration.')
                END;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO Device.SecondaryBoards (DeviceId, BoardNumber, IsImplicit, CreatedAt, UpdatedAt, IsDeleted)
                SELECT DISTINCT DeviceId, 1, 1, datetime('now'), datetime('now'), 0
                FROM Device.Channels;
            ");

            migrationBuilder.Sql(@"
                UPDATE Device.Channels
                SET ChannelNumber = ChannelNumber + 1,
                    SecondaryBoardId = (
                        SELECT sb.Id FROM Device.SecondaryBoards sb
                        WHERE sb.DeviceId = Device.Channels.DeviceId AND sb.BoardNumber = 1
                    );
            ");
```

Note: the `RAISE(ABORT, ...)` check runs against `ChannelNumber` (the renamed column, still holding the pre-shift 0-indexed value at this point in the migration) — confirm the column rename operation runs *before* this check in the generated `Up` method; if EF ordered it after, move this SQL block to run after the rename but before the `+1` update.

- [ ] **Step 3: Hand-edit the `Down` method to reverse the shift**

Before the schema-reversal operations (drop `SecondaryBoards`, rename `Channels`→`Circuits` back, drop `SecondaryBoardId`), insert:

```csharp
            migrationBuilder.Sql(@"
                UPDATE Device.Channels
                SET ChannelNumber = ChannelNumber - 1;
            ");
```

- [ ] **Step 4: Generate the SQL script for manual inspection**

Run: `dotnet ef migrations script -o migration.sql -c AppDbContext`

Read `migration.sql` and confirm: every existing device gets exactly one `BoardNumber=1, IsImplicit=1` row; every existing channel is repointed with `ChannelNumber` shifted `+1`; the abort check fires correctly against a copy of production data if any device has `CircuitID >= 8` today.

- [ ] **Step 5: Apply against a local dev DB copy and verify**

Run: `dotnet ef database update -c AppDbContext` (against a **copy** of the dev/production SQLite file, never the live file directly)
Expected: migration succeeds (or aborts with the custom message if overflow data exists — in that case, stop and resolve the data issue before proceeding, per Global Constraints).

- [ ] **Step 6: Commit**

```bash
git add Migrations/
git commit -m "feat: add EF migration for SecondaryBoard + Channel rename with 1-8 backfill"
```

---

### Task 9: Rename `IDeviceCircuitRepository`/`DeviceCircuitRepository` → `IDeviceChannelRepository`/`DeviceChannelRepository`, add board-scoping

**Files:**
- Modify: `Repositories/Interfaces/ISpecificRepositories.cs` (lines 1-35 only — the `IDeviceCircuitRepository` interface; leave `IProgramRepository` at line 37+ untouched)
- Modify: `Repositories/Implementations/DeviceCircuitRepository.cs` → rename to `Repositories/Implementations/DeviceChannelRepository.cs`

**Interfaces:**
- Consumes: `ChannelDto` (Task 5), `Channel`/`SecondaryBoard` entities (Tasks 3-4), `AppDbContext` (Task 7).
- Produces: `IDeviceChannelRepository` with every method signature using `ChannelDto`; `DeviceChannelRepository` implementation; new `GetOrCreateBoardAsync(int deviceId, int boardNumber) -> Task<SecondaryBoard>` helper; new `EnsureBoardOneAsync(int deviceId) -> Task` helper.

- [ ] **Step 1: Rename the interface (lines 1-35 of `ISpecificRepositories.cs`)**

Change:
```csharp
public interface IDeviceCircuitRepository : IRepository<Device>
{
    Task<CommonResponse<List<CircuitDto>>> GetCircuitsAsync();
    Task<CommonResponse<CircuitDto>> GetCircuitAsync(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> InsertAsync(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> UpdateRegistration(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> UpdateAsync(CircuitDto circuit);
    Task<CommonResponse<CircuitDto>> UpdateIsDeleteAsync(CircuitDto circuit, bool IsDelete = false);
    Task<CommonResponse<CircuitDto>> AllowCicuitAsync(CircuitDto circuit, bool IsRegistred);
    Task<CommonResponse<CircuitDto>> DeleteAsync(CircuitDto circuit);
    Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(CircuitDto circuit, ManufacturingDetailDTO Manufacturing);
    Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(CircuitDto circuit, FactoryConfigDetailDTO Factory);
    Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(CircuitDto circuit);
    Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(CircuitDto circuit);

    #region Calibration 

    Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(CircuitDto circuit, CalibrationDataPoint calibration);
    Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(CircuitDto circuit);
    Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(CircuitDto circuit);

    #endregion
}
```
to:
```csharp
public interface IDeviceChannelRepository : IRepository<Device>
{
    Task<CommonResponse<List<ChannelDto>>> GetChannelsAsync();
    Task<CommonResponse<ChannelDto>> GetChannelAsync(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> InsertAsync(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> UpdateRegistration(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> UpdateAsync(ChannelDto channel);
    Task<CommonResponse<ChannelDto>> UpdateIsDeleteAsync(ChannelDto channel, bool IsDelete = false);
    Task<CommonResponse<ChannelDto>> AllowCicuitAsync(ChannelDto channel, bool IsRegistred);
    Task<CommonResponse<ChannelDto>> DeleteAsync(ChannelDto channel);
    Task<CommonResponse<ManufacturingDetailDTO>> UpdateManufacturingAsync(ChannelDto channel, ManufacturingDetailDTO Manufacturing);
    Task<CommonResponse<FactoryConfigDetailDTO>> UpdateFactoryAsync(ChannelDto channel, FactoryConfigDetailDTO Factory);
    Task<CommonResponse<ManufacturingDetailDTO>> GetManufacturingAsync(ChannelDto channel);
    Task<CommonResponse<FactoryConfigDetailDTO>> GetFactoryAsync(ChannelDto channel);

    Task<SecondaryBoard> GetOrCreateBoardAsync(int deviceId, int boardNumber);
    Task EnsureBoardOneAsync(int deviceId);

    #region Calibration 

    Task<CommonResponse<bool>> CreateOrUpdateCalibrationDataAsync(ChannelDto channel, CalibrationDataPoint calibration);
    Task<CommonResponse<CalibrationDataPointDto>> GetCalibrationDataAsync(ChannelDto channel);
    Task<CommonResponse<CalibrationData>> GetAllCalibrationDataAsync(ChannelDto channel);

    #endregion
}
```

- [ ] **Step 2: Rename the implementation file and class, add `GetOrCreateBoardAsync`/`EnsureBoardOneAsync`**

Delete `Repositories/Implementations/DeviceCircuitRepository.cs`, create `Repositories/Implementations/DeviceChannelRepository.cs`. Class declaration changes from `public class DeviceCircuitRepository : Repository<Device>, IDeviceCircuitRepository` to `public class DeviceChannelRepository : Repository<Device>, IDeviceChannelRepository`. Add these two new methods (insert after the constructor):

```csharp
        public async Task<SecondaryBoard> GetOrCreateBoardAsync(int deviceId, int boardNumber)
        {
            var board = await _context.SecondaryBoards
                .FirstOrDefaultAsync(b => b.DeviceId == deviceId && b.BoardNumber == boardNumber);

            if (board != null)
                return board;

            board = new SecondaryBoard
            {
                DeviceId = deviceId,
                BoardNumber = boardNumber,
                IsImplicit = boardNumber == 1
            };

            await _context.SecondaryBoards.AddAsync(board);
            await _context.SaveChangesAsync();

            return board;
        }

        public async Task EnsureBoardOneAsync(int deviceId)
        {
            await GetOrCreateBoardAsync(deviceId, 1);
        }
```

- [ ] **Step 3: Rename every method and re-scope every `CircuitID`/`DeviceId` lookup to go through `SecondaryBoardId`**

For every method, apply the same three mechanical changes throughout the file:
1. Method name and parameter type: `CircuitDto circuit`/`dto` → `ChannelDto channel`/`dto` (rename the parameter identifier too, for readability — this file uses `circuit` as the parameter name throughout).
2. Field access: `.CircuitID` → `.ChannelNumber`, `.CircuitType` → `.ChannelType`, `.CircuitMaxVoltage` etc. → `.ChannelMaxVoltage` etc. (per Task 6's rename table), `_context.Circuits` → `_context.Channels`.
3. **Lookup re-scoping** (the actual behavior change): every `c.CircuitID == x.CircuitID && c.DeviceId == y.DeviceID`-shaped comparison becomes a two-step resolve-then-filter: first resolve the board via `await GetOrCreateBoardAsync(x.DeviceID, x.SecondaryBoardNumber)`, then filter by `c.SecondaryBoardId == board.Id && c.ChannelNumber == x.ChannelNumber` instead of comparing `DeviceId` directly on `Channel`.

Apply this to every one of the sites the recon identified (line numbers refer to the **original** `DeviceCircuitRepository.cs`; apply the same transform in the renamed file):

- `GetCircuitAsync` (now `GetChannelAsync`, was lines 21-73): the join `on device.DeviceID equals cir.DeviceId` plus `where ... cir.CircuitID == circuit.CircuitID` becomes a join through `SecondaryBoard`:
  ```csharp
        public async Task<CommonResponse<ChannelDto>> GetChannelAsync(ChannelDto channel)
        {
            try
            {
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);

                var result = await (
                        from device in _context.Devices
                        join ch in _context.Channels
                            on device.DeviceID equals ch.DeviceId
                        where device.DeviceID == channel.DeviceID &&
                              ch.SecondaryBoardId == board.Id &&
                              ch.ChannelNumber == channel.ChannelNumber
                        select new ChannelDto
                        {
                            DeviceID = device.DeviceID,
                            SecondaryBoardNumber = channel.SecondaryBoardNumber,
                            ChannelNumber = ch.ChannelNumber,
                            DeviceName = device.DeviceName,
                            MACID = device.MACID,
                            IPAddress = device.IPAddress,
                            IsRegistered = ch.IsRegistered,
                            IsDeleted = ch.IsDeleted,
                            DHCPEnable = device.DHCPEnable,
                            MasterSwVersion = device.SwVersion,
                            ComSwVersion = device.ComSwVersion,
                            ManufactureDateTime = device.ManufactureDateTime,
                            CommissioningDateTime = device.CommissioningDateTime,
                            AssemblyDate = device.AssemblyDate,

                            ChannelType = ch.ChannelType,
                            SecondarySwVersion = ch.SwVersion,
                            SecondaryAssemblyDate = ch.AssemblyDate,
                            ZntMaxVoltage = ch.ZntMaxVoltage,
                            LntMaxVoltage = ch.LntMaxVoltage,
                            ChannelMaxVoltage = ch.ChannelMaxVoltage,
                            ChannelMinVoltage = ch.ChannelMinVoltage,
                            ChannelMaxDischargingCurrent = ch.ChannelMaxDischargingCurrent,
                            ChannelMaxChargingCurrent = ch.ChannelMaxChargingCurrent,
                            CreatedAt = ch.CreatedAt,
                            CreatedBy = ch.CreatedBy,
                            UpdatedAt = ch.UpdatedAt,
                            UpdatedBy = ch.UpdatedBy
                        }
                    ).FirstOrDefaultAsync();

                if (result == null)
                    return CommonResponse<ChannelDto>.Fail("Channel not found.");

                return CommonResponse<ChannelDto>.Ok(result, "Channel retrieved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetChannelAsync Error: {ex.Message}");
                return CommonResponse<ChannelDto>.Fail("Error retrieving channel. Please try again.");
            }
        }
  ```

- `GetCircuitsAsync` (now `GetChannelsAsync`, was lines 75-124): this method returns *all* channels across all devices/boards, so it needs a join through `SecondaryBoard` to surface `SecondaryBoardNumber` per row, rather than a per-channel board resolve:
  ```csharp
        public async Task<CommonResponse<List<ChannelDto>>> GetChannelsAsync()
        {
            try
            {
                var result = await (
                    from device in _context.Devices
                    join channel in _context.Channels
                        on device.DeviceID equals channel.DeviceId
                    join board in _context.SecondaryBoards
                        on channel.SecondaryBoardId equals board.Id
                    select new ChannelDto
                    {
                        DeviceID = device.DeviceID,
                        SecondaryBoardNumber = board.BoardNumber,
                        ChannelNumber = channel.ChannelNumber,
                        DeviceName = device.DeviceName,
                        MACID = device.MACID,
                        IPAddress = device.IPAddress,
                        IsRegistered = channel.IsRegistered,
                        IsDeleted = channel.IsDeleted,
                        DHCPEnable = device.DHCPEnable,
                        MasterSwVersion = device.SwVersion,
                        ComSwVersion = device.ComSwVersion,
                        ManufactureDateTime = device.ManufactureDateTime,
                        CommissioningDateTime = device.CommissioningDateTime,
                        AssemblyDate = device.AssemblyDate,
                        ChannelType = channel.ChannelType,
                        SecondarySwVersion = channel.SwVersion,
                        SecondaryAssemblyDate = channel.AssemblyDate,
                        PrimarySerialNumber = device.PrimarySerialNumber,
                        SecondarySerialNumber = channel.SecondarySerialNumber ?? string.Empty,
                        ZntMaxVoltage = channel.ZntMaxVoltage,
                        LntMaxVoltage = channel.LntMaxVoltage,
                        ChannelMaxVoltage = channel.ChannelMaxVoltage,
                        ChannelMinVoltage = channel.ChannelMinVoltage,
                        ChannelMaxDischargingCurrent = channel.ChannelMaxDischargingCurrent,
                        ChannelMaxChargingCurrent = channel.ChannelMaxChargingCurrent,
                        CreatedAt = channel.CreatedAt,
                        CreatedBy = channel.CreatedBy,
                        UpdatedAt = channel.UpdatedAt,
                        UpdatedBy = channel.UpdatedBy
                    }
                ).ToListAsync();

                return CommonResponse<List<ChannelDto>>.Ok(result, "Channels retrieved successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetChannelsAsync Error: {ex.Message}");

                return CommonResponse<List<ChannelDto>>.Fail("Error retrieving channels. Please try again.");
            }
        }
  ```

- `InsertAsync` (was lines 126-228): after the existing device-check block, resolve the board before the channel-check:
  ```csharp
                var board = await GetOrCreateBoardAsync(dto.DeviceID, dto.SecondaryBoardNumber);

                var channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == dto.ChannelNumber);

                if (channel != null)
                {
                    channel.IsDeleted = false;
                    dto.IsRegistered = channel.IsRegistered;

                    await _context.SaveChangesAsync();

                    return CommonResponse<ChannelDto>.Ok(dto, "Channel already exists.");
                }

                channel = new Channel
                {
                    ChannelNumber = dto.ChannelNumber,
                    DeviceId = dto.DeviceID,
                    SecondaryBoardId = board.Id,
                    IsRegistered = dto.IsRegistered,
                    IsDeleted = false,
                    ChannelType = dto.ChannelType,
                    SwVersion = dto.SecondarySwVersion,
                    AssemblyDate = dto.SecondaryAssemblyDate,
                    ZntMaxVoltage = dto.ZntMaxVoltage,
                    LntMaxVoltage = dto.LntMaxVoltage,
                    ChannelMaxVoltage = dto.ChannelMaxVoltage,
                    ChannelMinVoltage = dto.ChannelMinVoltage,
                    ChannelMaxDischargingCurrent = dto.ChannelMaxDischargingCurrent,
                    ChannelMaxChargingCurrent = dto.ChannelMaxChargingCurrent,
                    SecondarySerialNumber = dto.SecondarySerialNumber,
                    CreatedAt = dto.CreatedAt ?? DateTime.Now,
                    CreatedBy = dto.CreatedBy,
                    UpdatedAt = dto.UpdatedAt ?? DateTime.Now,
                    UpdatedBy = dto.UpdatedBy
                };

                await _context.Channels.AddAsync(channel);
                await _context.SaveChangesAsync();

                dto.IsDeleted = channel.IsDeleted;
                dto.IsRegistered = channel.IsRegistered;

                await _audit.LogEventAsync(new AuditLog
                {
                    Action = Models.Enums.AuditActionType.CREATE,
                    Details = $"Channel {dto.DeviceID}-{dto.SecondaryBoardNumber}-{dto.ChannelNumber} Registered",
                    Status = Models.Enums.SeverityLevel.INFO,
                    Module = Models.Enums.ModuleName.CIRCUIT,
                    Timestamp = DateTime.Now
                });

                return CommonResponse<ChannelDto>.Ok(dto, "Channel inserted successfully.");
  ```
  (Keep the existing device-not-found-creates-new-device block above this unchanged apart from field renames — it doesn't touch `CircuitID`.)

- `UpdateAsync` (was lines 267-368), `DeleteAsync` (was lines 370-409), `UpdateIsDeleteAsync` (was lines 411-436), `AllowCicuitAsync` (was lines 438-481): each currently does `_context.Circuits.FirstOrDefaultAsync(c => c.CircuitID == circuit.CircuitID && c.DeviceId == circuit.DeviceID)`. Replace each with:
  ```csharp
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var existing = await _context.Channels
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == channel.ChannelNumber);
  ```
  (variable name `existing`/`existingCircuit` stays as-is apart from the query itself; keep the rest of each method's logic, renaming field accesses per Task 6's table).

- `UpdateManufacturingAsync` (was lines 483-548), `UpdateFactoryAsync` (was lines 551-621), `GetManufacturingAsync` (was lines 623-683), `GetFactoryAsync` (was lines 685-746): each currently does `_context.Circuits.FirstOrDefaultAsync(c => c.DeviceId == circuit.DeviceID && c.CircuitID == circuit.CircuitID)` (with or without `.AsNoTracking()`). Replace each with the same two-step resolve, preserving the `.AsNoTracking()` call where it exists:
  ```csharp
                var board = await GetOrCreateBoardAsync(channel.DeviceID, channel.SecondaryBoardNumber);
                var channelEntity = await _context.Channels
                    .AsNoTracking() // only where the original had it
                    .FirstOrDefaultAsync(c => c.SecondaryBoardId == board.Id && c.ChannelNumber == channel.ChannelNumber);
  ```

- `CreateOrUpdateCalibrationDataAsync` (was lines 749-826), `GetCalibrationDataAsync` (was lines 828-862), `GetAllCalibrationDataAsync` (was lines 864-925): each queries `_context.CalibrationDataPoints` filtered by `x.DeviceId == circuit.DeviceID && x.CircuitId == circuit.CircuitID`. `CalibrationDataPoint` (Task 6) now has `SecondaryBoardNumber` directly on it (no join needed), so replace with:
  ```csharp
                        x.DeviceId == channel.DeviceID &&
                        x.SecondaryBoardNumber == channel.SecondaryBoardNumber &&
                        x.ChannelId == channel.ChannelNumber
  ```
  and when constructing a new `CalibrationDataPoint` in the CREATE branch of `CreateOrUpdateCalibrationDataAsync`, add `calibration.SecondaryBoardNumber = channel.SecondaryBoardNumber;` alongside the existing `calibration.DeviceId = channel.DeviceID;` / `calibration.CircuitId = channel.ChannelNumber;` (renamed) lines.

- [ ] **Step 4: Rename every audit-log `Details` string's "Circuit" wording to "Channel" and update the 2-part `{DeviceID}-{CircuitID}` interpolations to 3-part**

Every `Details = $"Circuit {circuit.DeviceID}-{circuit.CircuitID} ..."` across the file (insert/update/delete/approve/manufacturing/factory/calibration audit logs) becomes `Details = $"Channel {channel.DeviceID}-{channel.SecondaryBoardNumber}-{channel.ChannelNumber} ..."`.

- [ ] **Step 5: Build to check for remaining compile errors in this file**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: this file compiles clean; other files still error (expected — later tasks fix them).

- [ ] **Step 6: Commit**

```bash
git add Repositories/Interfaces/ISpecificRepositories.cs Repositories/Implementations/DeviceChannelRepository.cs
git rm Repositories/Implementations/DeviceCircuitRepository.cs
git commit -m "feat: rename DeviceCircuitRepository to DeviceChannelRepository, re-scope lookups through SecondaryBoard"
```

Also update every other reference to `IDeviceCircuitRepository`/`DeviceCircuitRepository` found by build errors at this point — this includes the DI registration in `Program.cs` (search for `AddScoped<IDeviceCircuitRepository` or similar) and any `IDeviceCircuitServices`/`DeviceCircuitServices` service-layer wrapper referenced by `CircuitManager.cs` (`ServiceLocator.GetScoped<IDeviceCircuitServices>()`) — rename that service layer's interface/implementation and every method signature the same way (`ChannelDto` params, `GetChannelsAsync`/`GetChannelAsync` names), following this task's same pattern. Locate it with:

```bash
grep -rl "IDeviceCircuitServices" --include=*.cs .
```

and apply the identical rename treatment (interface + implementation + DI registration) before moving to Task 10.

---

### Task 10: `DecoderService.cs` — encode/decode sites

**Files:**
- Modify: `Services/DecoderService.cs`

**Interfaces:**
- Consumes: `ChannelAddressCodec` (Task 2), `ChannelDto`/`CommandRequest`/`MeasurementData`/`CalibrationDataPointDto` (Tasks 5-6).
- Produces: every encode/decode site now goes through `ChannelAddressCodec`, operating on 1-8 values.

- [ ] **Step 1: `BuildCommand` (lines 211-229)** — replace the double-cast address byte with the codec:

Change:
```csharp
            byte[] header = req.Range.HasValue
            ? new byte[]
            {
                (byte)req.Start,
                (byte)(int)req.DeviceId,
                (byte)(int)req.CircuitId,
                req.QueryId,
                (byte)req.Range.Value
            }
            : new byte[]
            {
                (byte)req.Start,
                (byte)(int)req.DeviceId,
                (byte)(int)req.CircuitId,
                req.QueryId
            };
```
to:
```csharp
            byte addressByte = ChannelAddressCodec.Encode(req.SecondaryBoardNumber, req.ChannelId);

            byte[] header = req.Range.HasValue
            ? new byte[]
            {
                (byte)req.Start,
                (byte)(int)req.DeviceId,
                addressByte,
                req.QueryId,
                (byte)req.Range.Value
            }
            : new byte[]
            {
                (byte)req.Start,
                (byte)(int)req.DeviceId,
                addressByte,
                req.QueryId
            };
```

- [ ] **Step 2: `ParseRegistrationPacket` (line 339)** — decode instead of raw read:

Change:
```csharp
            // 1 byte → Circuit ID
            dto.CircuitID = payload[index++];
```
to:
```csharp
            // 1 byte → Board/Channel address
            var (boardNumber, channelNumber) = ChannelAddressCodec.Decode(payload[index++]);
            dto.SecondaryBoardNumber = boardNumber;
            dto.ChannelNumber = channelNumber;
```
(This file constructs `CircuitDto dto = new CircuitDto();` at the top of the method — rename to `ChannelDto dto = new ChannelDto();`, and `dto.CircuitType = "Single";` → `dto.ChannelType = "Single";`.)

- [ ] **Step 3: `ParseRealTimeData` (line 409)**

Change:
```csharp
                // CircuitID (1B)
                recordRequest.RealTimeRecord.CircuitID = payload[index++];
```
to:
```csharp
                // Board/Channel address (1B)
                var (rtBoardNumber, rtChannelNumber) = ChannelAddressCodec.Decode(payload[index++]);
                recordRequest.RealTimeRecord.SecondaryBoardNumber = rtBoardNumber;
                recordRequest.RealTimeRecord.ChannelNumber = rtChannelNumber;
```

- [ ] **Step 4: `ParseDBCValues` (line 591)** — this local was confirmed dead (decoded, never used again in the method). Decode it anyway for consistency/future-proofing, but there's no DTO field to assign it to since `DbcRecord` doesn't carry a channel field — keep it local:

Change:
```csharp
                // CircuitID (1B)
                int CircuitID = payload[i++];
```
to:
```csharp
                // Board/Channel address (1B) — decoded for symmetry with other parsers; DbcRecord has no channel field.
                var (_, _) = ChannelAddressCodec.Decode(payload[i++]);
```

- [ ] **Step 5: `RealStoreData` (lines 668, 779) and `RealStoreDataV2` (lines 905, 992)** — both decode into a local then propagate into `MeasurementData`. In each method:

Change the decode (both methods, same shape):
```csharp
                // CircuitID (1B)
                int CircuitID = payload[i++];
```
to:
```csharp
                // Board/Channel address (1B)
                var (boardNumber, channelNumber) = ChannelAddressCodec.Decode(payload[i++]);
```
And change the propagation (both methods, same shape):
```csharp
                            current = new MeasurementData
                            {
                                DeviceId = DeviceID,
                                CircuitId = CircuitID,
```
to:
```csharp
                            current = new MeasurementData
                            {
                                DeviceId = DeviceID,
                                SecondaryBoardNumber = boardNumber,
                                ChannelId = channelNumber,
```

- [ ] **Step 6: `FactoryParameters` (line 1162)** — this local was also confirmed dead. Decode via codec for consistency, keep unused (do NOT confuse with the unrelated `CircuitNumber`/`crno` field at line 1210, which stays untouched apart from any DTO rename — check whether `FactoryConfigDetailDTO.CircuitNumber` should also become `ChannelNumber` per the design spec's open question in §7; if the design spec's decision was "decide during implementation," default to leaving `CircuitNumber` as-is since it's a distinct factory-config field, not the address byte):

Change:
```csharp
            byte circuitId = packet[index++];
```
to:
```csharp
            var (_, _) = ChannelAddressCodec.Decode(packet[index++]); // header board/channel address, unused here
```

- [ ] **Step 7: `BatteryParameters` (line 1237)** — fix the pre-existing gap by decoding explicitly instead of a blanket skip:

Change:
```csharp
            // Skip header bytes: Start (0xAA), DeviceID, CircuitID, QueryID (4 bytes total)
            index += 4;
```
to:
```csharp
            // Header: Start (0xAA), DeviceID (1B), Board/Channel address (1B), QueryID (1B)
            byte start = data[index++];
            byte deviceIdByte = data[index++];
            var (boardNumber, channelNumber) = ChannelAddressCodec.Decode(data[index++]);
            byte queryId = data[index++];
```
(Confirm no downstream code in this method already declares `start`/`deviceIdByte`/local names that would collide — read the full method body before applying, since this plan's recon only showed the header-skip line, not the whole method.)

- [ ] **Step 8: Calibration response parser (line 1370, propagated at lines 1391, 1403, 1420, 1429, 1438, 1447, 1465, and 3 further sites in the method beyond the recon's read window)**

Change the decode:
```csharp
                byte circuit = data[index++]; // 2
```
to:
```csharp
                var (calBoardNumber, calChannelNumber) = ChannelAddressCodec.Decode(data[index++]); // 2
```
Change every `CircuitId = circuit` propagation site (all ~10 of them, matching the exact shape shown in the recon, e.g. lines 1391/1403/1420/1429/1438/1447/1465) to:
```csharp
                        DeviceId  = device,
                        SecondaryBoardNumber = calBoardNumber,
                        ChannelId = calChannelNumber,
```
Search the full method body for every remaining `CircuitId = circuit` occurrence beyond the 7 explicitly shown (the recon noted ~10 total) and apply the identical substitution to each — do not stop at the 7 shown.

- [ ] **Step 9: `GetSessionIdBytes` (lines 1539-1559)** — change signature to take the pre-encoded byte, per the design spec's compile-time-safety decision:

Change:
```csharp
        public static byte[] GetSessionIdBytes(int deviceId, int circuitId, out int sessionId, out DateTime? dt)
        {
            DateTime dateTime = DateTime.UtcNow;
            dt = dateTime;

            uint epochSeconds = (uint)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

            // Pack: [DeviceID 8-bit][CircuitID 8-bit][epoch low 16-bit]
            uint packed = ((uint)(deviceId  & 0xFF) << 24)
                        | ((uint)(circuitId & 0xFF) << 16)
                        | (epochSeconds & 0xFFFF);

            sessionId = (int)packed;

            byte[] bytes = BitConverter.GetBytes(packed);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes; // Always 4 bytes
        }
```
to:
```csharp
        public static byte[] GetSessionIdBytes(int deviceId, byte channelAddressByte, out int sessionId, out DateTime? dt)
        {
            DateTime dateTime = DateTime.UtcNow;
            dt = dateTime;

            uint epochSeconds = (uint)(dateTime - new DateTime(1970, 1, 1)).TotalSeconds;

            // Pack: [DeviceID 8-bit][Board/Channel address 8-bit][epoch low 16-bit]
            uint packed = ((uint)(deviceId & 0xFF) << 24)
                        | ((uint)channelAddressByte << 16)
                        | (epochSeconds & 0xFFFF);

            sessionId = (int)packed;

            byte[] bytes = BitConverter.GetBytes(packed);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return bytes; // Always 4 bytes
        }
```
(Also fix the stray leading `\` typo noticed in the doc comment at line 445 of `CircuitCommandHandler.cs` — that's Task 11, not here, but flagging it now so it isn't missed.)

- [ ] **Step 10: `ParseRegistrationResponse` and `BuildRegistration` (lines 1561-1587)** — change to accept the pre-encoded byte:

Change:
```csharp
        public static byte[] ParseRegistrationResponse(int DeviceID, int circuitId, CommandStatus status)
        {
            byte[] payload = new byte[5];
            payload[0] = 0xDD; // Start byte
            payload[1] = 0x01; // Query ID
            payload[2] = (byte)DeviceID;
            payload[3] = (byte)circuitId;
            payload[4] = (byte)status;
            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            payload = BindCRC16(payload, crc);
            return payload;
        }
        public static byte[] BuildRegistration(int DeviceID, int circuitId, byte QueryID, byte? status = null)
        {
            byte[] payload = new byte[5];
            payload[0] = 0xDD; // Start byte
            payload[1] = QueryID; // Query ID Delete Device
            payload[2] = (byte)DeviceID;
            payload[3] = (byte)circuitId;
            
            if (status.HasValue)
                payload[4] = status.Value;

            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            payload = BindCRC16(payload, crc);
            return payload;
        }
```
to:
```csharp
        public static byte[] ParseRegistrationResponse(int DeviceID, byte channelAddressByte, CommandStatus status)
        {
            byte[] payload = new byte[5];
            payload[0] = 0xDD; // Start byte
            payload[1] = 0x01; // Query ID
            payload[2] = (byte)DeviceID;
            payload[3] = channelAddressByte;
            payload[4] = (byte)status;
            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            payload = BindCRC16(payload, crc);
            return payload;
        }
        public static byte[] BuildRegistration(int DeviceID, byte channelAddressByte, byte QueryID, byte? status = null)
        {
            byte[] payload = new byte[5];
            payload[0] = 0xDD; // Start byte
            payload[1] = QueryID; // Query ID Delete Device
            payload[2] = (byte)DeviceID;
            payload[3] = channelAddressByte;
            
            if (status.HasValue)
                payload[4] = status.Value;

            ushort crc = CalculateCRC16(payload, 0, payload.Length);
            payload = BindCRC16(payload, crc);
            return payload;
        }
```

- [ ] **Step 11: Build to check remaining errors in this file**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: `DecoderService.cs` compiles clean apart from call-site errors in `CircuitManager.cs`/`CircuitCommandHandler.cs` (fixed in Tasks 11-12).

- [ ] **Step 12: Commit**

```bash
git add Services/DecoderService.cs
git commit -m "feat: route all channel-address encode/decode through ChannelAddressCodec in DecoderService"
```

---

### Task 11: Rename `CircuitManager` → `ChannelManager`, 3-part dictionary key

**Files:**
- Modify: `Services/CircuitManager.cs` → rename to `Services/ChannelManager.cs`

**Interfaces:**
- Consumes: `ChannelAddressCodec` (Task 2), `ChannelDto` (Task 5), `IChannelCommandHandler` (Task 12 — renamed from `ICircuitCommandHandler`).
- Produces: `ChannelManager` class; `_devices` dictionary now conceptually keyed by 3 parts (`deviceId-boardNumber-channelNumber`); `Get`/`Add`/`Remove` take the board number into account.

- [ ] **Step 1: Rename the class and `MakeKey` (line 15, line 69)**

Change:
```csharp
    public class CircuitManager : BackgroundService
```
to:
```csharp
    public class ChannelManager : BackgroundService
```
Change:
```csharp
        private string MakeKey(long deviceId, long circuitId) => $"{deviceId}-{circuitId}";
```
to:
```csharp
        private string MakeKey(long deviceId, long boardNumber, long channelId) => $"{deviceId}-{boardNumber}-{channelId}";
```

- [ ] **Step 2: `Get`/`Add`/`Remove` (lines 227-355)** — thread the board number through every `MakeKey` call:

Change (in `Get`, line 231):
```csharp
            var key = MakeKey(circuit.DeviceID, circuit.CircuitID);
```
to:
```csharp
            var key = MakeKey(channel.DeviceID, channel.SecondaryBoardNumber, channel.ChannelNumber);
```
(rename the parameter `CircuitDto circuit` → `ChannelDto channel` throughout `Get`/`Add`/`Remove`, and every `circuit.CircuitID` → `channel.ChannelNumber`, `circuit.DeviceID` → `channel.DeviceID` reference, and every log-message/exception-message string's `"Circuit"` wording → `"Channel"`). Apply the identical `MakeKey(channel.DeviceID, channel.SecondaryBoardNumber, channel.ChannelNumber)` substitution at the two other call sites (`Add` at the original line 252, `Remove` at the original line 326).

- [ ] **Step 3: `ViewUdpData` (lines 379-440)** — decode board+channel fully, no more "always 0" shortcut:

Change:
```csharp
                byte identify = payload[0];
                int deviceId = payload[1];
                int circuitId = payload[2];
                byte type = payload[3];

                var handler = Get(new CircuitDto { DeviceID = deviceId, CircuitID = circuitId });
```
to:
```csharp
                byte identify = payload[0];
                int deviceId = payload[1];
                var (boardNumber, channelNumber) = Utils.ChannelAddressCodec.Decode(payload[2]);
                byte type = payload[3];

                var handler = Get(new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = boardNumber, ChannelNumber = channelNumber });
```

- [ ] **Step 4: `StoreUdpData` (lines 487-533)** — thread the decoded board number through consistently with `GetSessionIdBytes`:

Change:
```csharp
                var handlerResult = Get(new CircuitDto { DeviceID = first.DeviceId, CircuitID = first.CircuitId });
                if (!handlerResult.Success || handlerResult.Data == null)
                {
                    _log.Debug("No handler found for DeviceID={DeviceID}, CircuitID={CircuitID}", first.DeviceId, first.CircuitId);
                    return;
                }

                // Session ID is a packed value: [DeviceID 8b][CircuitID 8b][epoch-low16 16b]
                // Use SessionIdToDateTime to correctly reconstruct the date from the low-16 epoch bits.
                bool validEpoch = DecoderService.SessionIdToDateTime(first.SessionID, out DateTime sessionDateTime, true);
                if (!validEpoch)
                {
                    _log.Debug("StoreUdpData: could not reconstruct date from SessionID - falling back to DateTime.Now.");
                    sessionDateTime = DateTime.Now;
                }

                decoded.Data.filePath = Path.Combine(sessionDateTime.ToString("dd-MM-yyyy"), $"{first.SessionID}_{first.DeviceId}_{first.CircuitId}.db");
```
to:
```csharp
                var handlerResult = Get(new ChannelDto { DeviceID = first.DeviceId, SecondaryBoardNumber = first.SecondaryBoardNumber, ChannelNumber = first.ChannelId });
                if (!handlerResult.Success || handlerResult.Data == null)
                {
                    _log.Debug("No handler found for DeviceID={DeviceID}, Board={Board}, ChannelID={ChannelID}", first.DeviceId, first.SecondaryBoardNumber, first.ChannelId);
                    return;
                }

                // Session ID is a packed value: [DeviceID 8b][Board/Channel address 8b][epoch-low16 16b]
                // Use SessionIdToDateTime to correctly reconstruct the date from the low-16 epoch bits.
                bool validEpoch = DecoderService.SessionIdToDateTime(first.SessionID, out DateTime sessionDateTime, true);
                if (!validEpoch)
                {
                    _log.Debug("StoreUdpData: could not reconstruct date from SessionID - falling back to DateTime.Now.");
                    sessionDateTime = DateTime.Now;
                }

                decoded.Data.filePath = Path.Combine(sessionDateTime.ToString("dd-MM-yyyy"), $"{first.SessionID}_{first.DeviceId}_{first.SecondaryBoardNumber}_{first.ChannelId}.db");
```
(This changes the SQLite filename format from `{SessionID}_{DeviceID}_{CircuitID}.db` to `{SessionID}_{DeviceID}_{BoardNumber}_{ChannelID}.db` — a deliberate change since board is no longer always 1 in practice. Flag to the user/reviewer that any existing tooling parsing this filename format needs updating too.)

- [ ] **Step 5: `TrySendFailureNotification` (lines 535-553)** — per the design spec's open question (§8/risk #7 in the architecture doc), this 2-byte framing is unverified. Apply the minimal consistent change (decode via codec) but flag it for manual verification before merging:

Change:
```csharp
            int deviceId = payload[0];
            int circuitId = payload[1];

            var handler = Get(new CircuitDto { DeviceID = deviceId, CircuitID = circuitId });
```
to:
```csharp
            int deviceId = payload[0];
            // NOTE: unverified whether payload[1] is genuinely a full board/channel address byte
            // or a bare legacy channel number — confirm what actually produces this packet before
            // trusting this decode. See design doc risk #7.
            var (boardNumber, channelNumber) = Utils.ChannelAddressCodec.Decode(payload[1]);

            var handler = Get(new ChannelDto { DeviceID = deviceId, SecondaryBoardNumber = boardNumber, ChannelNumber = channelNumber });
```
(Leave the rest of the method's `circuitId`-derived strings renamed to use `channelNumber`/`boardNumber` per the field renames, but do not remove the caution comment — this is explicitly flagged as needing manual verification, not a confident behavior change.)

- [ ] **Step 6: `HandleCommandClientAsync` (lines 587-733)** — update every `newCircuit.CircuitID`/`newCircuit.DeviceID` reference and the `DecoderService.ParseRegistrationResponse(...)` calls:

Change every occurrence of:
```csharp
                                        (int)newCircuit.DeviceID,
                                        (int)newCircuit.CircuitID,
```
to:
```csharp
                                        (int)newCircuit.DeviceID,
                                        Utils.ChannelAddressCodec.Encode(newCircuit.SecondaryBoardNumber, newCircuit.ChannelNumber),
```
(Note: `ParseRegistrationResponse`'s signature changed in Task 10 Step 10 to take `byte channelAddressByte` instead of `int circuitId` — this call-site change must match that new signature exactly, i.e. drop the outer `(int)` cast pattern shown in the original and pass the codec's `byte` result directly.) Also rename the local variable `newCircuit` to `newChannel` throughout this method, and `ICircuitCommandHandler? circuitHandler` → `IChannelCommandHandler? channelHandler` (matching Task 12's interface rename), and `DecoderService.ParseRegistrationPacket(buffer)` result type from `CircuitDto` to `ChannelDto`.

- [ ] **Step 7: Build to check remaining errors in this file**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: this file compiles clean apart from `ICircuitCommandHandler`/`CircuitCommandHandler` references (Task 12) and `DeviceController.cs` (Task 13).

- [ ] **Step 8: Commit**

```bash
git add Services/ChannelManager.cs
git rm Services/CircuitManager.cs
git commit -m "feat: rename CircuitManager to ChannelManager, 3-part device-board-channel dictionary key"
```

---

### Task 12: Rename `ICircuitCommandHandler`/`CircuitCommandHandler` → `IChannelCommandHandler`/`ChannelCommandHandler`

**Files:**
- Modify: `Services/Interfaces/ICircuitCommandHandler.cs` → rename to `Services/Interfaces/IChannelCommandHandler.cs`
- Modify: `Services/Implementations/CircuitCommandHandler.cs` → rename to `Services/Implementations/ChannelCommandHandler.cs`

**Interfaces:**
- Consumes: `ChannelDto` (Task 5), `ChannelAddressCodec` (Task 2), `CommandRequest` (Task 6), `SessionRecordDto` (Task 6).
- Produces: `IChannelCommandHandler` with `ChannelDto Channel { get; set; }` (renamed from `CircuitDto Circuit`); `ChannelCommandHandler` implementation with all 22 `CommandRequest` sites carrying `SecondaryBoardNumber`.

- [ ] **Step 1: Rename the interface, `Circuit` property → `Channel`**

Change (line 15 of the interface):
```csharp
    public interface ICircuitCommandHandler
    {
        CircuitDto Circuit { get; set; }
```
to:
```csharp
    public interface IChannelCommandHandler
    {
        ChannelDto Channel { get; set; }
```
Every other member of the interface is unaffected by this rename (they don't reference `Circuit` by name).

- [ ] **Step 2: Rename the implementation class and every `Circuit.DeviceID`/`Circuit.CircuitID` reference**

Change class declaration:
```csharp
public class CircuitCommandHandler : ICircuitCommandHandler, IDisposable
```
to:
```csharp
public class ChannelCommandHandler : IChannelCommandHandler, IDisposable
```
Rename the backing property from `Circuit` to `Channel` (find its declaration in the class body — the interface now requires `ChannelDto Channel { get; set; }`).

- [ ] **Step 3: Update all 22 `CommandRequest` initializers** — each currently sets `DeviceId = Circuit.DeviceID, CircuitId = Circuit.CircuitID`. Add the board number and rename per Task 6's `CommandRequest.CircuitId → ChannelId` rename. Apply this exact substitution at every one of the 22 sites (line numbers below refer to the original file — apply after the class/property rename lands, so line numbers will shift slightly; match by surrounding method name instead):

Pattern (applies to all 22 sites — `StartProgram`, `StopProgram`, `PauseProgram`, `ContinueProgram`, `TimeSyn`, `ResetSystem`, `HWReadyToReadWriteAsync`, `SetBatteryParamAsync`, `SetProgramAsync` ×2, `GetManufacturingDetails`, `GetFactoryConfigDetails`, `HWReadyToCalibrationAsync`, `SendLiveCurrentandVoltage`, `CalibrationPointPreset`, `SetGainOffset`, `CancelCalibration`, `StopCalibration`, `StopverifyCalibration`, `PreviousCalibration`, `TransferDbcFile` ×2):

Change:
```csharp
                    DeviceId = Circuit.DeviceID,
                    CircuitId = Circuit.CircuitID,
```
to:
```csharp
                    DeviceId = Channel.DeviceID,
                    SecondaryBoardNumber = Channel.SecondaryBoardNumber,
                    ChannelId = Channel.ChannelNumber,
```

Verify all 22 sites are updated by running (after the edits):
```bash
grep -n "CircuitId = Circuit" Services/Implementations/ChannelCommandHandler.cs
```
Expected: zero matches.

- [ ] **Step 4: `GetSessionIdBytes` call site (`StartProgram`, originally lines 443-448)** — update to match Task 10 Step 9's new signature, and fix the stray `\` comment typo noted during recon:

Change:
```csharp
            // Use circuit-aware packed session ID to avoid UNIQUE constraint collisions
            // when multiple circuits are started in parallel from the dashboard.
            \ Layout: [DeviceID 8-bit][CircuitID 8-bit][epoch low 16-bit]  → always 4 bytes.
            byte[] sessionID = DecoderService.GetSessionIdBytes(
                Circuit.DeviceID, Circuit.CircuitID,
                out var epochSeconds, out var sessionDateTime);
```
to:
```csharp
            // Use channel-aware packed session ID to avoid UNIQUE constraint collisions
            // when multiple channels are started in parallel from the dashboard.
            // Layout: [DeviceID 8-bit][Board/Channel address 8-bit][epoch low 16-bit] → always 4 bytes.
            byte[] sessionID = DecoderService.GetSessionIdBytes(
                Channel.DeviceID, Utils.ChannelAddressCodec.Encode(Channel.SecondaryBoardNumber, Channel.ChannelNumber),
                out var epochSeconds, out var sessionDateTime);
```

- [ ] **Step 5: `BuildRegistration` call site (`UnregisterAsync`, originally line 922)** — update to match Task 10 Step 10's new signature:

Change:
```csharp
            byte[] payload = DecoderService.BuildRegistration(Circuit.DeviceID, Circuit.CircuitID, (byte)Models.Enums.RegistrationQuery.Delete);
```
to:
```csharp
            byte[] payload = DecoderService.BuildRegistration(Channel.DeviceID, Utils.ChannelAddressCodec.Encode(Channel.SecondaryBoardNumber, Channel.ChannelNumber), (byte)Models.Enums.RegistrationQuery.Delete);
```

- [ ] **Step 6: Sweep every remaining `Circuit.DeviceID`/`Circuit.CircuitID` string-interpolation reference in log/exception messages**

Search for any remaining `Circuit.` references this task's line-by-line pass didn't already cover (log messages, exception text, e.g. any `$"...{Circuit.DeviceID}..."` patterns not part of a `CommandRequest` initializer):
```bash
grep -n "Circuit\." Services/Implementations/ChannelCommandHandler.cs
```
Replace every hit with `Channel.` and rename any accompanying "Circuit" wording in the string text to "Channel".

- [ ] **Step 7: Build to check remaining errors**

Run: `dotnet build BatteryTestingSystem.csproj`
Expected: this file and `Services/Interfaces/ICircuitCommandHandler.cs`→`IChannelCommandHandler.cs` compile clean. `DeviceController.cs` (Task 13) is the last consumer with errors.

- [ ] **Step 8: Commit**

```bash
git add Services/Interfaces/IChannelCommandHandler.cs Services/Implementations/ChannelCommandHandler.cs
git rm Services/Interfaces/ICircuitCommandHandler.cs Services/Implementations/CircuitCommandHandler.cs
git commit -m "feat: rename ICircuitCommandHandler/CircuitCommandHandler to Channel equivalents, thread SecondaryBoardNumber through all 22 CommandRequest sites"
```

---

### Task 13: `DeviceController.cs` — rename, 3-part key

**Files:**
- Modify: `Controllers/DeviceController.cs`

**Interfaces:**
- Consumes: `ChannelManager` (Task 11), `IChannelCommandHandler` (Task 12), `CommonRequest` (Task 6).
- Produces: every `_devices` dictionary key becomes 3-part; `ICircuitCommandHandler` references become `IChannelCommandHandler`; `CircuitManager` parameter type becomes `ChannelManager`.

- [ ] **Step 1: Update the field/constructor parameter types**

Change:
```csharp
        private readonly CircuitManager _cm;
```
to:
```csharp
        private readonly ChannelManager _cm;
```
And the constructor parameter `CircuitManager cm` → `ChannelManager cm` (both in the main constructor and in every `internal static` helper method's `CircuitManager cm` parameter — `CoreSendProgram`, `CoreStart`, `CoreStop`, `CorePause`, `CoreContinue`, `CoreLiveData`).

- [ ] **Step 2: Update every 2-part key construction to 3-part**

This exact pattern appears at 9 sites (`CoreSendProgram` line 52, `CoreStart` line 115, `CoreStop` line 130, `CorePause` line 145, `CoreContinue` line 160, `CoreLiveData` line 175, `GetSessionId` action line 233, `GetDevices` action line 264, `SubscribeLive` line 310). Apply this substitution at every one:

Change:
```csharp
                var key = $"{req.DeviceID}-{req.CircuitID}";
```
to:
```csharp
                var key = $"{req.DeviceID}-{req.SecondaryBoardNumber}-{req.ChannelNumber}";
```
(For the `SubscribeLive` site, which inlines the key rather than assigning to a variable — `_cm._devices.TryGetValue($"{request.DeviceID}-{request.CircuitID}", out ...)` — apply the same 3-part substitution inline: `$"{request.DeviceID}-{request.SecondaryBoardNumber}-{request.ChannelNumber}"`.)

- [ ] **Step 3: Update `ICircuitCommandHandler` type references**

Change every `ICircuitCommandHandler? handler` / `out ICircuitCommandHandler? handler` parameter/variable type (in `CoreSendProgram`, `SubscribeLive`) to `IChannelCommandHandler? handler`.

- [ ] **Step 4: Update `GetSessionId` action's anonymous-object field references**

Change:
```csharp
                        handler.Session.DeviceID,
                        handler.Session.CircuitID,
```
to:
```csharp
                        handler.Session.DeviceID,
                        handler.Session.SecondaryBoardNumber,
                        handler.Session.ChannelNumber,
```
(Matches Task 6 Step 2's rename of `SessionRecordDto.CircuitID` → `ChannelNumber` plus the new `SecondaryBoardNumber` field.)

- [ ] **Step 5: Build the whole solution**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: **zero errors**. This is the task where the full chain of renames from Tasks 3-13 should finally all resolve together — if there are remaining errors, they indicate a missed reference somewhere in Tasks 3-12; fix forward rather than reverting.

- [ ] **Step 6: Commit**

```bash
git add Controllers/DeviceController.cs
git commit -m "feat: rename DeviceController's CircuitManager/ICircuitCommandHandler references, 3-part device-board-channel key"
```

---

### Task 14: Dashboard UI — rename Razor components, update display format

**Files:**
- Modify: `Components/UI/Dashboard/DeviceCircuit.razor` → rename to `Components/UI/Dashboard/DeviceChannel.razor`
- Modify: `Components/UI/Dashboard/CircuitActionBar.razor` → rename to `Components/UI/Dashboard/ChannelActionBar.razor`
- Modify: `Components/UI/Dashboard/CircuitFilter.razor` → rename to `Components/UI/Dashboard/ChannelFilter.razor`
- Modify: `Components/UI/Circuits/CircuitModels.cs` → rename to `Components/UI/Circuits/ChannelModels.cs`
- Modify: `Components/Pages/Home/DashboardView.razor` (references the renamed component + `circuits` list)

**Interfaces:**
- Consumes: `IChannelCommandHandler` (Task 12), `ChannelManager` (Task 11).
- Produces: dashboard cards labeled `{DeviceID}-{BoardNumber}-{ChannelNumber}`; every component reference updated.

- [ ] **Step 1: Rename `DeviceCircuit.razor` → `DeviceChannel.razor`, update the `[Parameter] public ICircuitCommandHandler Circuit` and every `Circuit.` reference**

Change (line 668):
```csharp
    [Parameter, EditorRequired] public ICircuitCommandHandler Circuit { get; set; } = null!;
```
to:
```csharp
    [Parameter, EditorRequired] public IChannelCommandHandler Channel { get; set; } = null!;
```
Change line 670's tuple type:
```csharp
    [Parameter] public EventCallback<(int DeviceId, int CircuitId, bool Selected)> OnSelected { get; set; }
```
to:
```csharp
    [Parameter] public EventCallback<(int DeviceId, int BoardNumber, int ChannelId, bool Selected)> OnSelected { get; set; }
```
And its invocation in `ToggleSelected` (line 838):
```csharp
            await OnSelected.InvokeAsync((Circuit.Circuit.DeviceID, Circuit.Circuit.CircuitID, Selected));
```
to:
```csharp
            await OnSelected.InvokeAsync((Channel.Channel.DeviceID, Channel.Channel.SecondaryBoardNumber, Channel.Channel.ChannelNumber, Selected));
```

Every other `Circuit.` reference in this 1791-line file (there are dozens — markup bindings like `@Circuit.Circuit.DeviceName`, `@Circuit.Program`, `@Circuit.Battery`, `@Circuit.RealTime`, `@Circuit.Session`, `@Circuit.calibration`, method bodies referencing `Circuit.RealTime.OnDataChanged`, etc.) becomes `Channel.` — this is a pure identifier rename (`Circuit` → `Channel`, `_previousCircuit` → `_previousChannel`) with **one semantic change**: every display string that shows `@Circuit.Circuit.DeviceID - @Circuit.Circuit.CircuitID` (lines 38, 52, 1058, 711, 718) becomes the new 3-part format:

Change (all 5 occurrences of this exact shape):
```
@Circuit.Circuit.DeviceID - @Circuit.Circuit.CircuitID
```
to:
```
@Channel.Channel.DeviceID-@Channel.Channel.SecondaryBoardNumber-@Channel.Channel.ChannelNumber
```
(Note the dash spacing changes from `" - "` to `-` per the design spec's `1-1-1` format — no spaces around the dashes.)

Also update the `OnDoubleClick` tab title (lines 709-719):
```csharp
        tbService.AddTab(new NavMenuItem
        {
            Title = $"Realtime: {Circuit?.Circuit?.DeviceID}-{Circuit?.Circuit?.CircuitID}",
            Icon = Lucide.ChartLine,
            ComponentType = typeof(BmsDashboard),
            Parameters = new Dictionary<string, object?>
            {
                ["SQLFilePath"] = Circuit?.Session?.SessionFilePath ?? string.Empty,
                ["Key"] = $"{Circuit?.Circuit?.DeviceID}-{Circuit?.Circuit?.CircuitID}"
            },
```
to:
```csharp
        tbService.AddTab(new NavMenuItem
        {
            Title = $"Realtime: {Channel?.Channel?.DeviceID}-{Channel?.Channel?.SecondaryBoardNumber}-{Channel?.Channel?.ChannelNumber}",
            Icon = Lucide.ChartLine,
            ComponentType = typeof(BmsDashboard),
            Parameters = new Dictionary<string, object?>
            {
                ["SQLFilePath"] = Channel?.Session?.SessionFilePath ?? string.Empty,
                ["Key"] = $"{Channel?.Channel?.DeviceID}-{Channel?.Channel?.SecondaryBoardNumber}-{Channel?.Channel?.ChannelNumber}"
            },
```

Rename the header display (line 1057-1059):
```
<span class="font-bold text-[1em] truncate">
    @Circuit.Circuit.DeviceID-@Circuit.Circuit.CircuitID
</span>
```
to:
```
<span class="font-bold text-[1em] truncate">
    @Channel.Channel.DeviceID-@Channel.Channel.SecondaryBoardNumber-@Channel.Channel.ChannelNumber
</span>
```

- [ ] **Step 2: `Components/UI/Circuits/CircuitModels.cs` → `ChannelModels.cs`**

This file (`CircuitCardModel`) has no `Circuit`-specific fields (`Id`, `Name`, `X`, `Y`, `Z` — generic positioning model). Rename the class for naming consistency even though its fields don't change:
```csharp
namespace BatteryTestingSystem.Components.UI.Circuits;

public class ChannelCardModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public ChannelCardModel(double x, double y)
    {
        X = x;
        Y = y;
    }
   
}
```
(Search the codebase for `CircuitCardModel` usages and rename them to `ChannelCardModel` before committing — if this class turns out to be unused/dead code, per the "no lingering Circuit identifiers" constraint it should still be renamed rather than left as-is, since deleting unused code is out of scope for this plan.)

- [ ] **Step 3: `CircuitActionBar.razor` → `ChannelActionBar.razor`**

This file has no `CircuitDto`/`CircuitID` field references at all — it operates purely on `CircuitStatus`/`ProgramRunningStatus` enums and an `ActionBarState` DTO with no circuit-identity fields. The only rename needed is the file name and the `@namespace` — content is otherwise unaffected. Update the file's usages elsewhere (in `DashboardView.razor`) from `<CircuitActionBar ...>` to `<ChannelActionBar ...>`.

- [ ] **Step 4: `CircuitFilter.razor` → `ChannelFilter.razor`**

This file has real `CircuitID`/`DeviceID` tuple fields throughout (`AccessCircuits`, `VisibleCircuits`, `_hidden`, `CircuitViewPref`, `CircuitKey`). Rename:

Change the parameter/field declarations:
```csharp
    [Parameter] public List<(int DeviceID, int CircuitID, string Name)> AccessCircuits { get; set; } = new();

    [Parameter] public HashSet<(int DeviceID, int CircuitID)> VisibleCircuits { get; set; } = new();
    [Parameter] public EventCallback<HashSet<(int DeviceID, int CircuitID)>> VisibleCircuitsChanged { get; set; }

    private HashSet<(int DeviceID, int CircuitID)> _hidden = new();
```
to:
```csharp
    [Parameter] public List<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber, string Name)> AccessChannels { get; set; } = new();

    [Parameter] public HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber)> VisibleChannels { get; set; } = new();
    [Parameter] public EventCallback<HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber)>> VisibleChannelsChanged { get; set; }

    private HashSet<(int DeviceID, int SecondaryBoardNumber, int ChannelNumber)> _hidden = new();
```
Every tuple construction throughout the file (`(c.DeviceID, c.CircuitID)`, `(k.DeviceID, k.CircuitID)`, etc.) grows a third element `c.SecondaryBoardNumber`/`k.SecondaryBoardNumber` in the same position. The persisted-preference DTOs:
```csharp
    private class CircuitViewPref
    {
        public List<CircuitKey> HiddenCircuits { get; set; } = new();
    }

    private class CircuitKey
    {
        public int DeviceID { get; set; }
        public int CircuitID { get; set; }
    }
```
become:
```csharp
    private class ChannelViewPref
    {
        public List<ChannelKey> HiddenChannels { get; set; } = new();
    }

    private class ChannelKey
    {
        public int DeviceID { get; set; }
        public int SecondaryBoardNumber { get; set; }
        public int ChannelNumber { get; set; }
    }
```
And the display label in the checkbox list (line 62):
```
<span class="text-foreground">@c.Name – C@(c.CircuitID)</span>
```
to:
```
<span class="text-foreground">@c.Name – @(c.SecondaryBoardNumber)-@(c.ChannelNumber)</span>
```
Note: **this is a breaking change to persisted user preferences** — the `StorageKey` is `$"{CurrentUser.UserId}_circuit_view_pref_v2"` and stores `CircuitViewPref` JSON via `sessionStorage`. Renaming the DTO's shape means old stored JSON (`HiddenCircuits`/`CircuitID`) won't deserialize into the new shape (`HiddenChannels`/`ChannelNumber`+`SecondaryBoardNumber`). Bump the storage key version suffix to force a clean reset:
```csharp
    private string StorageKey => $"{CurrentUser.UserId}_channel_view_pref_v3";
```

- [ ] **Step 5: `DashboardView.razor` — update the component tag, `circuits` list type, and filter logic**

Update the component reference (line 76-78 area):
```
<GridLayout MinColumnWidth="@($"{CConfig.CardSize}px")" Gap="0.75rem">

    @foreach (var dev in circuits)
```
The `<DeviceCircuit>` tag itself (not shown in the recon excerpt but implied by the `@foreach` producing cards) needs its tag name changed from `<DeviceCircuit Circuit="@dev" ... />` to `<DeviceChannel Channel="@dev" ... />` — locate this tag inside the loop body and apply the rename.

Update the field declaration (line 194):
```csharp
    private List<ICircuitCommandHandler> circuits = new();
```
to:
```csharp
    private List<IChannelCommandHandler> circuits = new();
```
(Renaming the variable `circuits` itself to `channels` is optional cleanup within this task's scope — do it for consistency, updating both declaration sites, lines 372 and 521-527:)
```csharp
        circuits = CM._devices.Values.ToList();
```
appears twice (lines 372, 521) — change `CM` reference stays the same (it's already the `ChannelManager` instance from Task 11), just rename the local variable `circuits` → `channels` throughout the file if doing the optional cleanup, or leave as `circuits` (referring now to `IChannelCommandHandler` instances) if minimizing diff — **recommend leaving the variable name as `circuits` for this task** to keep the diff focused on the required renames; note this as an accepted naming inconsistency, not a defect.

Update the filter predicate (lines 523-527):
```csharp
            circuits = circuits
                .Where(cmd =>
                    dbCircuits.Any(e =>
                        (e.DeviceID == cmd.Circuit.DeviceID &&
                        e.CircuitID == cmd.Circuit.CircuitID) && e.IsRegistered
```
to:
```csharp
            circuits = circuits
                .Where(cmd =>
                    dbCircuits.Any(e =>
                        (e.DeviceID == cmd.Channel.DeviceID &&
                        e.SecondaryBoardNumber == cmd.Channel.SecondaryBoardNumber &&
                        e.ChannelNumber == cmd.Channel.ChannelNumber) && e.IsRegistered
```
(`dbCircuits` here is presumably a `List<ChannelDto>` or similar per-user access list fetched elsewhere in this file — read the surrounding context in `get_editing_context`/full file read before applying, since the recon didn't capture where `dbCircuits` is populated; ensure its element type also exposes `SecondaryBoardNumber` by this point, which Task 5 already added to `ChannelDto`.)

Add a sort by the new hierarchy after the filter, per the design spec's "sort order becomes DeviceID, BoardNumber, ChannelNumber" decision:
```csharp
            circuits = circuits
                .OrderBy(cmd => cmd.Channel.DeviceID)
                .ThenBy(cmd => cmd.Channel.SecondaryBoardNumber)
                .ThenBy(cmd => cmd.Channel.ChannelNumber)
                .ToList();
```
Insert this immediately after the `.Where(...)` chain (and after any equivalent unfiltered assignment path in the file, e.g. line 372's `circuits = CM._devices.Values.ToList();`, so both the filtered and unfiltered paths end up sorted).

- [ ] **Step 6: Build and manually smoke-test the dashboard**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: zero errors.

Run the app (`dotnet run` or F5 in the IDE), open the dashboard view, and confirm cards render with the `DeviceID-BoardNumber-ChannelNumber` label (e.g. `1-1-1`) and appear sorted in that order. This is a UI-facing change with no automated test coverage in this repo, so manual verification is required per this plan's Global Constraints.

- [ ] **Step 7: Commit**

```bash
git add Components/UI/Dashboard/DeviceChannel.razor Components/UI/Dashboard/ChannelActionBar.razor Components/UI/Dashboard/ChannelFilter.razor Components/UI/Circuits/ChannelModels.cs Components/Pages/Home/DashboardView.razor
git rm Components/UI/Dashboard/DeviceCircuit.razor Components/UI/Dashboard/CircuitActionBar.razor Components/UI/Dashboard/CircuitFilter.razor Components/UI/Circuits/CircuitModels.cs
git commit -m "feat: rename dashboard Razor components to Channel terminology, update card label to DeviceID-BoardNumber-ChannelNumber format"
```

---

### Task 15: `PacketAnalyzer.cs`/`PacketAnalyzerNoReverse.cs` — confirm no change needed

**Files:**
- None (verification-only task)

**Interfaces:**
- None.

- [ ] **Step 1: Confirm via search that these files have no circuit/channel-identifier logic**

Run:
```bash
grep -n "Circuit\|circuit" Services/PacketAnalyzer.cs Services/PacketAnalyzerNoReverse.cs
```
Expected: no matches (confirmed during recon — both files parse the program-step packet format, shared header→step-offset→step-ID→operator-body→registrations→footer, with zero circuit/channel identifier handling).

- [ ] **Step 2: No commit needed** — this task exists to close out the design spec's open question #5/#6 with a confirmed negative result, not to make a change. Note in the plan's tracking (or a PR description) that this was verified, not skipped.

---

### Task 16: Full-solution build, migration dry-run, and manual regression pass

**Files:**
- None (verification-only task)

**Interfaces:**
- None.

- [ ] **Step 1: Full solution build**

Run: `dotnet build BatteryTestingSystem.sln`
Expected: zero errors, zero new warnings on touched files.

- [ ] **Step 2: Run the test suite**

Run: `dotnet test BatteryTestingSystem.sln`
Expected: all `ChannelAddressCodec` tests (Task 2) pass.

- [ ] **Step 3: Sweep for any remaining `Circuit`-prefixed identifier**

Run:
```bash
grep -rn "CircuitID\|CircuitId\|CircuitManager\|CircuitCommandHandler\|CircuitDto\|CircuitType\|CircuitMaxVoltage\|CircuitMinVoltage\|CircuitMaxDischargingCurrent\|CircuitMaxChargingCurrent\|DeviceCircuitRepository" --include=*.cs --include=*.razor .
```
Expected: zero matches outside of `Migrations/` (historical migration files legitimately still reference the old `Circuits`/`CircuitID` table/column names for their `Up`/`Down` bodies — those are correct as-is and must not be edited) and `CircuitStatus` (an existing unrelated enum name — `CircuitStatus.Idle`/`.Charge`/etc. — that was never in scope for this rename per the design spec, which only covers the `Circuit` *entity/DTO/identifier* naming, not this status enum).

If any unexpected match surfaces, trace it back to the file and apply the same rename pattern used in the task that should have covered it.

- [ ] **Step 4: Migration dry-run against a DB copy**

Run: `dotnet ef migrations script -o migration.sql -c AppDbContext` (if not already done in Task 8) and inspect the output once more now that all application code changes have landed, to confirm the migration still matches the final model shape.

- [ ] **Step 5: Manual TestDeviceSimulator run**

Run: `.\TestDeviceSimulator.ps1` against a dev instance with the migration applied. Exercise: registration → live data → session close. Confirm:
- Session IDs / SQLite filenames now include the board segment (per Task 11 Step 4's filename format change).
- Dashboard cards show the `1-1-1`-style label for the simulator's device/channel.
- No exceptions in the console/logs during the full flow.

- [ ] **Step 6: Commit any final cleanup**

```bash
git add -A
git commit -m "chore: final verification pass for Device/SecondaryBoard/Channel hierarchy migration"
```
