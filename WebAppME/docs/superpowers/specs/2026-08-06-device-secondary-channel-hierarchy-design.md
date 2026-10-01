# Device → SecondaryBoard → Channel hierarchy (rename + 1-8 numbering + UI)

Status: **Designed, approved, not yet implemented.**

Supersedes/extends `docs/architecture/01-hardware-hierarchy-protocol-design.md`
("Sub-project 1: Device → SecondaryBoard → Circuit hierarchy"). That doc is being
rewritten in place to match this spec (see "Relationship to existing doc" below).

## Context

BTSME is a Blazor Server battery-testing app. Today every `Device` has a flat list
of `Circuit` rows, each identified by a single `CircuitID` (0-15, effectively
always 0-15 in production). This project introduces a `SecondaryBoard` level
between `Device` and `Circuit`, renames `Circuit` to `Channel` throughout the
codebase, and changes numbering from 0-indexed to 1-indexed with an explicit
per-device/per-board cap.

Confirmed decisions (this conversation):

1. **Full rename, everywhere**: `Circuit` → `Channel` in entity names, DTOs,
   services, repositories, Razor components, DB table/column names. No lingering
   `Circuit*` identifiers except where explicitly noted below.
2. **Capacity**: 1 `Device` has up to 8 `SecondaryBoard`s; each `SecondaryBoard`
   has up to 8 `Channel`s.
3. **Numbering is 1-8, not 0-7, and not 0-15** — there is no board/channel `0`.
   Both `BoardNumber` and `ChannelNumber` are stored, encoded on the wire, and
   displayed as 1-8 directly (no offset conversion at any layer).
4. **Wire format**: the address byte stays 4 upper bits (board) / 4 lower bits
   (channel) — unchanged from the original protocol design. The nibble simply
   carries the 1-8 value directly (fits comfortably in 4 bits, which can express
   0-15). `ChannelAddressCodec` validates 1-8, not 0-15 and not 0-7.
5. **Dashboard display**: `DeviceID-BoardNumber-ChannelNumber`, e.g. `1-1-1`,
   `1-2-1`, `1-3-4`. Since numbering is already 1-based in storage, no
   display-time transformation is needed.
6. **In-memory key-collision fix is in scope now** (not deferred): because
   multiple secondaries will really be used (not just an implicit board 1),
   `ChannelManager.MakeKey` and `DeviceController`'s handler key both become
   3-part (`DeviceId`, `BoardNumber`, `ChannelNumber`) instead of 2-part.
7. **Legacy data migration**: existing `CircuitID` values are 0-indexed in
   production. Mapping rule: `new ChannelNumber = old CircuitID + 1`, all existing
   channels attached to one implicit `SecondaryBoard` with `BoardNumber = 1`
   (`IsImplicit = true`) per device.
8. **Migration safety check**: before running the real migration, query
   `MAX(CircuitID)` against production data. If any device has a `CircuitID ≥ 8`
   (i.e. `old CircuitID + 1 > 8`), that overflows the new 8-channels-per-board cap
   and the migration must stop and surface this as a blocker rather than silently
   dropping/truncating data. (Original doc asserted current values are "always
   0-15 in practice" but never explicitly checked for values ≥ 8, which is exactly
   the boundary the new cap introduces.)

## Design

### 1. New entity: `SecondaryBoard`

New file `Models/Entities/SecondaryBoard.cs`, table `Device.SecondaryBoards`:
- `Id` (long, identity PK)
- `DeviceId` (int, required) — FK → `Device.DeviceID`
- `BoardNumber` (int, `[Range(1,8)]`) — upper nibble of wire address byte;
  `IsImplicit = true` rows use `BoardNumber = 1`
- `IsImplicit` (bool, default false) — true for the auto-created implicit board
- Inherits `BaseEntity` (CreatedBy/UpdatedBy/CreatedAt/UpdatedAt/IsDeleted)

Unique index on `(DeviceId, BoardNumber)`.

### 2. Renamed `Circuit` → `Channel` entity (`Models/Entities/Channel.cs`, was `Circuit.cs`)

- Rename file, class, and every property prefixed `Circuit*`:
  `CircuitID` → `ChannelNumber` (int, `[Range(1,8)]`, was `[MaxLength(1)]`),
  `CircuitType` → `ChannelType`, and so on for any other `Circuit*`-prefixed field.
- Add `SecondaryBoardId` (long, required) FK → `SecondaryBoard.Id`, plus
  navigation `public SecondaryBoard SecondaryBoard { get; set; }`.
- Uniqueness moves from implicit `(DeviceId, CircuitID)` to explicit
  `(SecondaryBoardId, ChannelNumber)` via a unique index.
- Keep `DeviceId` on `Channel` too (denormalized) — avoids restructuring every
  existing repository query shape.

### 3. `ChannelAddressCodec` — new file `Utils/ChannelAddressCodec.cs`

Matches the `Utils/BigEndian.cs` / `LittleEndian.cs` sibling convention: pure
static, no DB/EF dependency.

- `Encode(int boardNumber, int channelNumber) -> byte` — validates both are 1-8
  (throws `ArgumentOutOfRangeException` otherwise); returns
  `(byte)(((boardNumber & 0xF) << 4) | (channelNumber & 0xF))`. No offset math —
  the wire nibble carries the 1-8 value directly.
- `Decode(byte value) -> (int BoardNumber, int ChannelNumber)` —
  `((value >> 4) & 0xF, value & 0xF)`. Never throws (decoding untrusted wire
  bytes should not crash the decoder — validate at the point the decoded value is
  used, not at decode time).
- `EncodeLegacy(int channelNumber) => Encode(1, channelNumber)` — convenience for
  "legacy defaults to board 1".

### 4. EF Core migration + backfill

**Up, in order:**
1. Create table `Device.SecondaryBoards` (Id PK, DeviceId, BoardNumber, IsImplicit,
   audit cols).
2. FK `SecondaryBoards.DeviceId → Devices.DeviceID`.
3. Unique index `(DeviceId, BoardNumber)`.
4. Rename table `Circuits` → `Channels`; rename column `CircuitID` →
   `ChannelNumber`; rename any other `Circuit*`-prefixed columns to `Channel*`.
5. Add nullable `Channels.SecondaryBoardId` (long).
6. **Pre-backfill safety check**: run `SELECT DeviceId, MAX(ChannelNumber) FROM
   Channels GROUP BY DeviceId` (pre-rename this reads the old `CircuitID`) and
   abort the migration with a clear error if any value would exceed 8 after the
   `+1` shift — see decision #8 above. This must run and be confirmed clean
   against a copy of the production DB before this migration is applied for
   real.
7. Data backfill via `migrationBuilder.Sql(...)`:
   - Insert one `BoardNumber=1, IsImplicit=1` row per existing `Device` row.
   - Update every existing `Channels` row: `ChannelNumber = ChannelNumber + 1`
     (old 0-indexed → new 1-indexed), `SecondaryBoardId` = that device's new
     implicit board row.
8. Alter `Channels.SecondaryBoardId` to NOT NULL.
9. FK `Channels.SecondaryBoardId → SecondaryBoards.Id`.
10. Unique index `(SecondaryBoardId, ChannelNumber)`.

**Down:** reverse order (including shifting `ChannelNumber` back down by 1 and
renaming tables/columns back).

**Application-level guarantee going forward:** `EnsureBoardOneAsync(int deviceId)`
called wherever a `Device` row is first created — exact call site to be traced
during implementation (same open question the original doc carried).

### 5. `Models/DTOs/ChannelDto.cs` (renamed from `CircuitDto.cs`)

- `CircuitID` → `ChannelNumber` (now 1-8, no default-0 shortcut since there's no
  valid 0 value — every construction site must supply a real 1-8 number).
- Add `public int SecondaryBoardNumber { get; set; } = 1;` — defaulting to 1
  (the implicit board) means existing single-board call sites are automatically
  correct without being touched.
- Repository layer resolves `SecondaryBoardNumber → SecondaryBoard.Id`
  internally; DTO stays protocol-shaped (no raw PK).

### 6. `Data/AppDbContext.cs`

- Add `DbSet<SecondaryBoard> SecondaryBoards`.
- Rename `DbSet<Circuit> Circuits` → `DbSet<Channel> Channels`.
- Add **explicit** `HasOne/WithMany` config for `SecondaryBoard.DeviceId → Device`
  and `Channel.SecondaryBoardId → SecondaryBoard`, plus both unique indexes.

### 7. `Services/DecoderService.cs` — highest change density

Same call sites the original doc identified (`BuildCommand`,
`ParseRegistrationPacket`, `ParseRealTimeData`, `ParseDBCValues`,
`RealStoreData`/`RealStoreDataV2`, `FactoryParameters`, `BatteryParameters`,
calibration response parser, `GetSessionIdBytes`, `ParseRegistrationResponse` /
`BuildRegistration`), updated to:
- Use `ChannelAddressCodec.Encode/Decode` instead of raw byte math, now
  operating on 1-8 values everywhere (not 0-15, not 0-based).
- `BatteryParameters`'s pre-existing gap (`index += 4`, discards the byte
  entirely) gets fixed while touching this file: decode explicitly and
  validate/log against the expected channel instead of silently discarding.
- `GetSessionIdBytes` keeps taking the pre-encoded wire `byte` as a parameter
  (compile-time safety: a caller can't accidentally pass a bare channel number).
- `ParseDBCValues`'s decoded-but-seemingly-unused local: confirm via full-file
  read whether it's genuinely dead before deciding if `DbcRecord` needs the
  field.

### 8. `Services/ChannelManager.cs` (renamed from `CircuitManager.cs`)

- `MakeKey(long deviceId, int boardNumber, int channelNumber) =>
  $"{deviceId}-{boardNumber}-{channelNumber}"` — 3-part key now (was 2-part
  `deviceId-circuitId`); every call site threading `Channel.SecondaryBoard.BoardNumber`
  through.
- `ViewUdpData`: decode `(boardNumber, channelNumber)` fully and use both in the
  lookup — no more "assume board is always X" shortcut.
- `StoreUdpData`: same 3-part key threading, consistent with `GetSessionIdBytes`.
- `TrySendFailureNotification`'s 2-byte-header framing: confirm during
  implementation what actually produces this packet before assuming byte 1 is
  the same encoded address byte (unresolved question carried from original doc).

### 9. `Services/Implementations/ChannelCommandHandler.cs` (renamed from `CircuitCommandHandler.cs`)

- ~20 `CommandRequest` initializers: add `SecondaryBoardNumber =
  Channel.SecondaryBoard.BoardNumber` alongside `ChannelId = Channel.ChannelNumber`.
- `GetSessionIdBytes(Channel.DeviceID, ChannelAddressCodec.Encode(Channel.SecondaryBoard.BoardNumber,
  Channel.ChannelNumber), ...)`.
- `DecoderService.BuildRegistration(...)` — same encode-before-call treatment.

### 10. `Services/PacketAnalyzer.cs` / `PacketAnalyzerNoReverse.cs`

Manual read-through required (not grep-discoverable, per original doc). Update
diagnostic display from raw `"CircuitID: N"` to `"Board: X Channel: Y"` via
`ChannelAddressCodec.Decode`.

### 11. `Repositories/Implementations/DeviceChannelRepository.cs` (renamed from `DeviceCircuitRepository.cs`) — highest blast radius

~15 call sites doing `c.CircuitID == circuit.CircuitID && c.DeviceId ==
device.DeviceID`-style lookups. Re-scope to resolve
`channel.SecondaryBoardNumber` to the device's `SecondaryBoard.Id` via a new
`GetOrCreateBoardAsync(deviceId, boardNumber)` helper, then filter by
`SecondaryBoardId == resolvedBoardId && ChannelNumber == channel.ChannelNumber`
instead of `DeviceId == ... && CircuitID == ...`.

Do this as one dedicated, carefully-reviewed pass — a partially-migrated file
would silently reintroduce cross-board collisions.

Also add `EnsureBoardOneAsync(int deviceId)` here.

### 12. `Controllers/DeviceController.cs`

Unlike the original doc (which left this unchanged, deferring to "next
sub-project"), this now gets the same 3-part key fix: `"{req.DeviceID}-
{req.SecondaryBoardNumber}-{req.ChannelNumber}"` replacing the 2-part
`"{req.DeviceID}-{req.CircuitID}"` key (6+ sites), and `handler.Session.CircuitID`
reads become `handler.Session.ChannelNumber`.

### 13. Dashboard UI (`Components/UI/Dashboard/`, `Components/UI/Circuits/`)

- `DeviceCircuit.razor` → `DeviceChannel.razor`: identity label changes from
  `"{DeviceName} : {DeviceID} - {CircuitID}"` to
  `"{DeviceID}-{BoardNumber}-{ChannelNumber}"` (e.g. `1-1-1`, `1-2-1`), sourced
  directly from stored 1-8 values.
- `GridLayout.razor`: sort order becomes `DeviceID, BoardNumber, ChannelNumber`.
- `Components/UI/Circuits/CircuitModels.cs` → `ChannelModels.cs`,
  `CircuitActionBar.razor` → `ChannelActionBar.razor`, `CircuitFilter.razor` →
  `ChannelFilter.razor` — mechanical rename; filter gains a Secondary-board
  filter alongside the existing device filter.
- Cards stay a flat one-per-channel grid (no nested/grouped-by-board UI) — same
  layout as today, just relabeled and re-sorted.

## Relationship to existing doc

`docs/architecture/01-hardware-hierarchy-protocol-design.md` will be rewritten in
place to reflect this spec (rename, 1-8 numbering, folded-in key fix) rather than
kept as a separate superseded document, since it's the doc referenced directly
by the implementation task.

## Risks / open questions carried into implementation

1. **Migration overflow check (new)** — if any production device has
   `CircuitID ≥ 8` today, the `+1` mapping overflows the 8-channel cap. Must be
   checked against real data before the migration runs; this is a hard blocker,
   not a soft warning.
2. **Device-creation call site not yet located** — trace where `Device` rows are
   created before writing `EnsureBoardOneAsync`'s call site.
3. **`TrySendFailureNotification`'s 2-byte framing** — confirm what produces this
   packet before changing it.
4. **`ParseDBCValues`'s decoded ChannelNumber local** — confirm via full-file read
   whether it's truly unused.
5. **`PacketAnalyzer*.cs`** needs manual read-through (not grep-discoverable).
6. **`DeviceChannelRepository`'s re-scoping** — highest-blast-radius mechanical
   change (~15 call sites); do as one reviewed pass with integration-test
   coverage of registration/delete/approve flows before merging.
7. **Full rename touches ~7 files' worth of identifiers** beyond the original
   doc's scope (`CircuitManager`→`ChannelManager`,
   `CircuitCommandHandler`→`ChannelCommandHandler`,
   `DeviceCircuitRepository`→`DeviceChannelRepository`, plus 3 Razor files) —
   larger diff than the original sub-project; should still land as one
   dedicated pass rather than incrementally, to avoid a half-`Circuit`/half-
   `Channel` codebase in between commits.

## Critical files
- `Services/DecoderService.cs`
- `Models/Entities/Channel.cs` (renamed from `Circuit.cs`) + new
  `Models/Entities/SecondaryBoard.cs`
- `Data/AppDbContext.cs`
- `Repositories/Implementations/DeviceChannelRepository.cs` (renamed)
- `Services/ChannelManager.cs` (renamed)
- `Services/Implementations/ChannelCommandHandler.cs` (renamed)
- `Controllers/DeviceController.cs`
- new `Utils/ChannelAddressCodec.cs`
- `Components/UI/Dashboard/DeviceChannel.razor` (renamed) +
  `Components/UI/Dashboard/GridLayout.razor`
- `Components/UI/Circuits/ChannelModels.cs`, `ChannelActionBar.razor`,
  `ChannelFilter.razor` (renamed)

## Verification

1. Build: `dotnet build BatteryTestingSystem.sln` — warning-free on touched files.
2. Pre-migration data check: query `MAX(CircuitID)` per device against a copy of
   the production DB; confirm no value ≥ 8 before proceeding (decision #8).
3. `dotnet ef migrations script -o migration.sql -c AppDbContext` — inspect
   generated SQL backfill; confirm every device gets exactly one implicit
   board-1 row and every channel is repointed with `ChannelNumber` shifted +1.
4. Unit tests for `ChannelAddressCodec`: round-trip `Encode`/`Decode` for all
   1-8 × 1-8 combinations, boundary tests at 1 and 8, out-of-range (`0`, `9`,
   negative) `Encode` throws.
5. Run `TestDeviceSimulator.ps1` against a dev instance — registration → live
   data → session close — confirm session IDs / SQLite filenames use the new
   1-8 encoding correctly.
6. Manually inspect `PacketAnalyzer.cs` output before/after.
7. Dashboard: confirm cards render `DeviceID-BoardNumber-ChannelNumber` labels
   (e.g. `1-1-1`, `1-2-1`) and sort in that order.
