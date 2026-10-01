# Database Design Document (DBD)
**Document ID:** BTS-DBD-001  **Version:** 0.7  **Date:** September 2026  **Status:** Approved

---

## 1. Main Application Database (AppDbContext)

**Engine:** SQLite  
**File:** `{AppSettings.Data}/BtsAppdb.db`  
**ORM:** Entity Framework Core 8 with migrations  
**Schema:** Managed via 19 sequential EF Core migrations (see §4)

### 1.1 Entity Relationship Overview

```
Users (auth)
  └── UserCircuitAccess ──▶ Circuits (Channel) ──▶ SecondaryBoards ──▶ Devices
                              │
                              └── BatterySessions ──▶ Batteries ──▶ BatteryTypes
                                        │
                                        └── (data → Session SQLite DB)

Circuits (Channel) ──▶ CalibrationDataPoints
Devices  ──▶ DeviceSettings (bak)

Programs (BtsPrograms) ──▶ ProgramSchedules ──▶ ScheduleExecutionLogs
BatterySessions ──▶ ExportRecords (via SessionFilePath)

RegistrationStandards
DbcFileRecords
CodeMessages
ConfigurationEntity
AuditLogs
AlarmLog
```

Note: "Circuit" is the UI/domain term for a single testable channel — the underlying entity/table is `Channel` (`Models/Entities/Channel.cs`), keyed by the 3-part `(Device, SecondaryBoard, ChannelNumber)` address, not a 2-part `(Device, Circuit)` pair. A device may carry 0-8 `SecondaryBoard`s (`Range(0,8)`), each with up to 8 channels.

### 1.2 Table Definitions

#### auth.Users (ASP.NET Identity)
| Column | Type | Notes |
|---|---|---|
| Id | TEXT PK | GUID |
| UserName | TEXT | Login name |
| NormalizedUserName | TEXT | Indexed |
| Email | TEXT | |
| PasswordHash | TEXT | PBKDF2 |
| DisplayName | TEXT | Custom field |
| IsActive | INTEGER | bool |

#### auth.Roles
| Column | Type | Notes |
|---|---|---|
| Id | TEXT PK | GUID |
| Name | TEXT | Administrator / Operator / Viewer |

All tables below additionally carry `CreatedBy`/`UpdatedBy`/`CreatedAt`/`UpdatedAt`/`IsDeleted` from the shared `BaseEntity` base class unless noted otherwise.

#### Devices
| Column | Type | Notes |
|---|---|---|
| DeviceID | INTEGER PK | Not auto-increment (`DatabaseGeneratedOption.None`) — the physical device's own numeric ID |
| DeviceName | TEXT | max 100 |
| MACID | TEXT | max 100 |
| IPAddress | TEXT | |
| DHCPEnable | INTEGER | bool |
| ClientRemoteIPAddress / TcpClientRemotePort / UdpClientRemotePort / UdpStoreRemotePort | TEXT/INTEGER | nullable — remote-client override config (T-45) |
| PrimarySerialNumber | TEXT | From device registration packet |
| SwVersion / ComSwVersion | TEXT | max 10, nullable |
| ManufactureDateTime / CommissioningDateTime / AssemblyDate | TEXT | datetime, nullable |
| LastSyncedAt | TEXT | datetime nullable — last manufacturing fetch from device (T-39) |

#### SecondaryBoards
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| DeviceId | INTEGER | Not an EF-modeled FK — same physical device, one row per board |
| BoardNumber | INTEGER | 0-8 (board `0` is a real board, not "no board" — see PROTOCOL.md §2.4) |
| IsImplicit | INTEGER | bool — `true` only for the legacy pre-multi-board board `1` sentinel |

#### Channels (a.k.a. "Circuits" in the UI/domain)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| DeviceId | INTEGER | Not an EF-modeled FK (denormalized alongside SecondaryBoardId) |
| SecondaryBoardId | INTEGER FK → SecondaryBoards | |
| ChannelNumber | INTEGER | 1-8 |
| ChannelType | TEXT | "Single" / "Dual", max 10 chars |
| IsRegistered | INTEGER | bool |
| SecondarySerialNumber | TEXT | From device registration packet |
| SwVersion | TEXT | max 10 chars |
| AssemblyDate | TEXT | datetime nullable |
| LastSyncedAt | TEXT | datetime nullable — last manufacturing/factory fetch from device (T-39) |
| ZntMaxVoltage / LntMaxVoltage | REAL | nullable |
| ChannelMaxVoltage / ChannelMinVoltage | REAL | nullable |
| ChannelMaxChargingCurrent / ChannelMaxDischargingCurrent | REAL | nullable |

The full circuit label shown in the UI (`1-0-1`) is `DeviceId-BoardNumber-ChannelNumber`, not a single `CircuitId` column — see `ChannelAddressCodec` for the wire-address encoding of the same triple.

#### BatteryType
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Name | TEXT | max 100, e.g. Li-Ion |
| Description | TEXT | nullable, max 250 |

#### Batteries
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| Name | TEXT | |
| BatteryTypeId | INTEGER | Not an EF-modeled FK |
| Comments | TEXT | nullable |
| Quantity | INTEGER | default 1 |
| Producer | TEXT | |
| NominalVoltage / NominalCurrent / NominalCapacity | REAL | V / A / Ah |
| ChargeFactor / Impedance / EnergyDensity / ColdCrankingCurrent | REAL | |
| NumberOfCells | INTEGER | drives `VNC` unit resolution (`BatteryUnitResolver`) |
| MaximumVoltage / GassingVoltage / BreakVoltage | REAL | feed §12.3 `UMax`/`UGas`/`CutOff` tokens |
| Port1DbcId / Port2DbcId / Port3DbcId | INTEGER nullable | per-port DBC assignment (2026-07 migration) |

#### BatterySessions
| Column | Type | Notes |
|---|---|---|
| SessionID | INTEGER PK (long) | Not DB-generated — Unix-epoch-derived session id |
| StartTime | TEXT | datetime |
| EndTime | TEXT | datetime nullable |
| SessionName | TEXT | nullable |
| ProgramHash | TEXT | |
| DeviceID | INTEGER | |
| SecondaryBoardNumber | INTEGER | default 1 |
| ChannelNumber | INTEGER | |
| BatteryID | INTEGER | |
| BatteryName | TEXT | nullable, denormalized snapshot |
| ProgramID | INTEGER | |
| ProgramName | TEXT | nullable, denormalized snapshot |
| DbcFileRecordID / Port2DbcFileRecordID / Port3DbcFileRecordID | INTEGER nullable | one per DBC port |
| DbcName / Port2DbcName / Port3DbcName | TEXT | nullable, denormalized snapshots |
| SessionFilePath | TEXT | path of the per-session SQLite DB (§2) |

None of `DeviceID`/`ChannelNumber`/`BatteryID`/`ProgramID` are EF-modeled foreign keys — all denormalized ids, resolved by value at read time.

#### BtsPrograms
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| ProgramName | TEXT | max 200 |
| Description | TEXT | nullable |
| MaxAh | REAL | nullable |
| ProgramSteps | INTEGER | nullable — step count |
| ProgramJson | TEXT | nullable — full serialized step array |
| ProgramTimeTicks | INTEGER | nullable |
| ProgramHash | TEXT | nullable |
| IsVaild | INTEGER | bool (sic — real column name) |

#### RegistrationStandards (table `Standards`, schema `Programs`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| StandardName | TEXT | max 8 |
| Description | TEXT | nullable |
| UnitList | TEXT | nullable — serialized `List<string>` |

#### CalibrationDataPoints (table `CalibrationData`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| DeviceId | INTEGER | |
| SecondaryBoardNumber | INTEGER | |
| ChannelId | INTEGER | |
| Mode | INTEGER | Enum `CalibrationMode`: Charge/Discharge |
| Type | INTEGER | Enum `CalibrationType`: Voltage/Current |
| Range | INTEGER | Enum `CalibrationRange`, default Full_Range |
| DateTime | TEXT | datetime |
| Gain / Offset | REAL | |

#### DbcFileRecords (table `DBCFiles`, schema `Files`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| BatteryId | INTEGER | Not an EF-modeled FK, plus a `Battery` nav property |
| Name | TEXT | max 100 |
| Version | TEXT | |
| Description | TEXT | nullable, max 200 |
| OriginalFileName | TEXT | |
| FilePath | TEXT | |
| FileSizeBytes | INTEGER | |
| dbcstrJson | TEXT | parsed `DbcDatabase`, default `"{}"` |

#### CodeMessages (schema `Programs`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Index | INTEGER | |
| Type | INTEGER | |
| Message | TEXT | nullable, max 500 |

#### ConfigurationEntity (table `Configuration`, schema `auth`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Key | TEXT | max 200 |
| Value | TEXT | nullable — JSON-serialized state (per-user/per-component UI persistence) |

#### AuditLog (schema `Audit`, singular table name)
| Column | Type | Notes |
|---|---|---|
| LogId | INTEGER PK (long) | |
| Timestamp | TEXT | datetime |
| User | TEXT | nullable |
| Action | INTEGER | Enum `AuditActionType` |
| Module | INTEGER | Enum `ModuleName`, max 100 |
| EntityId | TEXT | nullable |
| Status | INTEGER | Enum `SeverityLevel`, max 100 |
| IPAddress / UserAgent / Details / Metadata | TEXT | all nullable |

#### AlarmLog (schema `Audit`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| AlarmKey | TEXT | max 200 — stable fault identity, e.g. `12/0/3/comms-loss`; repeats collapse into the active row |
| Severity | INTEGER | Enum `SeverityLevel`, default INFO |
| Source | INTEGER | Enum `AlarmSource`, default NONE |
| DeviceId | TEXT | nullable, max 50 |
| BoardNumber / ChannelNumber | INTEGER | nullable |
| Title | TEXT | max 150 |
| Message | TEXT | max 1000 |
| FirstSeenUtc / LastSeenUtc | TEXT | datetime |
| OccurrenceCount | INTEGER | default 1 |
| AcknowledgedAtUtc / ClearedAtUtc | TEXT | datetime nullable |
| AcknowledgedBy | TEXT | nullable, max 100 |

Added 2026-08-18 (T-25, ADR-5 — bypasses `EventBusService`, own singleton `AlarmService`).

#### ExportRecords
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| SessionFilePath | TEXT | max 512 — matches `BatterySession.SessionFilePath` |
| ExportFilePath | TEXT | nullable, max 512 |
| RequestedBy | TEXT | max 100 |
| RequestedAt / CompletedAt | TEXT | datetime, latter nullable |
| Status | INTEGER | Enum `ExportStatus`, default Pending |
| FileSizeBytes | INTEGER | 0 while pending/failed |
| DisplayName | TEXT | nullable, max 256 |
| ErrorMessage | TEXT | nullable, max 1024 |
| HangfireJobId | TEXT | nullable, max 64 |

Added 2026-06-02 (`AddExportRecord` migration) — Hangfire-backed async Excel export jobs (`ExportJobService`).

#### ProgramSchedules
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| Name | TEXT | max 100 |
| ProgramId | INTEGER | |
| BatteryId | INTEGER | |
| Port1DbcFileId / Port2DbcFileId / Port3DbcFileId | INTEGER nullable | per-port DBC, mirrors `Batteries` |
| ScheduledAt | TEXT | UTC datetime |
| TargetCircuitsJson | TEXT | JSON array of `{DeviceId, CircuitId}`, default `"[]"` |
| IsActive | INTEGER | bool — disabled schedules are skipped by Hangfire |
| HangfireJobId | TEXT | nullable, max 100 |

#### ScheduleExecutionLogs
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| ScheduleId | INTEGER FK → ProgramSchedules | |
| DeviceId / CircuitId | INTEGER | target |
| ExecutedAt | TEXT | datetime |
| Status | TEXT | max 50 — `Success` / `Failed` / `Skipped_Offline` / `Skipped_AlreadyRunning` |
| FailReason | TEXT | nullable, max 500 |

#### UserCircuitAccess (schema `auth`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| UserId | TEXT FK → Users | |
| DeviceId | INTEGER | |
| CircuitId | INTEGER | Denormalized channel id, not an EF-modeled FK |

Note: unlike the plan sketched in earlier drafts of this document, `UserCircuitAccess` carries no `CanStart`/`CanStop`/`CanCalibrate` columns — it is a coarse per-(user, device, circuit) grant, not a per-action ACL.

---

## 2. Session Databases (SqliteDbContext)

**Engine:** SQLite  
**File per session:** `{Data}/{dd-MM-yyyy}/{SessionId}_{DeviceId}_{CircuitId}.db`  
**Schema:** Created/synced at runtime by `SqliteSchemaSync` — no migrations

### 2.1 MeasurementData Table
Dynamically extended — base columns + one column per selected DBC signal

**Base columns:**

| Column | Type | Description |
|---|---|---|
| Id | INTEGER PK | Auto-increment |
| SessionId | TEXT | Links to BatterySessions.SessionId |
| StepNumber | INTEGER | Program step index |
| Operator | INTEGER | Operator code |
| ProgramRunningTime | INTEGER | ms elapsed |
| Current | REAL | A |
| Voltage | REAL | V |
| Temperature | REAL | °C |
| Power | REAL | W |
| AccumulatedCapacity | REAL | Ah |
| ChargeCapacity | REAL | Ah |
| DischargeCapacity | REAL | Ah |
| StepCapacity | REAL | Ah |
| AccumulatedEnergy | REAL | Wh |
| ChargeEnergy | REAL | Wh |
| DischargeEnergy | REAL | Wh |
| StepEnergy | REAL | Wh |
| SystemErrorId | INTEGER | Bitmask |
| MessageId | INTEGER | |
| ErrorId | INTEGER | |
| CustomRemark | INTEGER | |
| Timestamp | TEXT | datetime |

**DBC CAN columns (dynamic):**  
One column per selected DBC signal — column name = signal name, type = REAL or INTEGER based on signal's `ValueType`.

### 2.2 ProgramAuditExecution Table

| Column | Type | Description |
|---|---|---|
| Id | INTEGER PK | |
| SessionId | TEXT | |
| StepNumber | INTEGER | |
| Action | TEXT | Start/Stop/Pause/Continue |
| Timestamp | TEXT | datetime |

---

## 3. Workflow Canvas Database (WorkflowDbContext)

**Engine:** SQLite (separate file/context from `AppDbContext` — see ADR "workflow canvas branch isolation", T-47 Phase 1)  
**Schema:** 1 migration (`InitialWorkflowSchema`, 2026-08-24)

#### WorkflowLayouts (schema `Device`)
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK (long) | |
| Name | TEXT | max 120 |
| Description | TEXT | nullable, max 400 |
| OwnerUserId | TEXT | max 450 (matches `AspNetUsers.Id` length) |
| LayoutJson | TEXT | opaque graph document — node/edge shape can change freely with no migration |
| SchemaVersion | INTEGER | default 1 |

Deliberately no foreign keys to `Devices`/`Channels`/`BtsPrograms` — ids inside `LayoutJson` are soft references so deleting hardware never cascades into a saved layout.

---

## 4. Migration History

**AppDbContext** (19 migrations):

| Migration | Date | Changes |
|---|---|---|
| InitialCreate | 2025-12-18 | Base schema: Devices, Circuits (Channels), Programs, Sessions, Batteries |
| TableOP | 2025-12-23 | Added table operator file tracking |
| RegistrationStandards | 2025-12-30 | Added standards table |
| sessionidNoneDbGenerate | 2026-01-09 | Changed SessionId to non-DB-generated |
| SessionStorage | 2026-01-20 | Added ConfigurationEntity for server-side UI state |
| CodeMessage | 2026-01-21 | Added CodeMessages table |
| DbcFiles | 2026-03-08 | Added DbcFileRecords table |
| AuditLogsTable | 2026-03-17 | Added AuditLogs table |
| UserCircuitAccess | 2026-03-30 | Added per-user circuit ACL |
| BatterySessionProperties | 2026-04-02 | Added session summary columns |
| dbcpaser | 2026-04-02 | Added parsed DBC data column to DbcFileRecords |
| AddProgramScheduler | 2026-05-05 | Added ProgramSchedules + ScheduleExecutionLogs tables |
| AddCalibrationRangeColumn | 2026-06-01 | Added `Range` column to CalibrationDataPoints |
| AddExportRecord | 2026-06-02 | Added ExportRecords table (Hangfire async Excel export) |
| AddBatteryPortAssignments | 2026-07-02 | Added Port1/2/3DbcId columns to Batteries |
| AddSessionPortDbc | 2026-07-02 | Added Port2/3DbcFileRecordID columns to BatterySessions |
| AddDeviceRemoteClientConfig | 2026-07-03 | Added remote-client override columns to Devices (T-45) |
| AddScheduleDbcPorts | 2026-07-14 | Added Port1/2/3DbcFileId columns to ProgramSchedules |
| AddSecondaryBoardAndChannel | 2026-08-07 | Split flat Circuits into SecondaryBoards + Channels (device-connection multiplexing, T-45) |
| AlarmLogTable | 2026-08-18 | Added AlarmLog table (T-25) |
| AddLastSyncedAt | 2026-08-19 | Added LastSyncedAt to Devices + Channels (T-39) |

**WorkflowDbContext** (1 migration, isolated history — never touches `AppDbContext`'s snapshot):

| Migration | Date | Changes |
|---|---|---|
| InitialWorkflowSchema | 2026-08-24 | Added WorkflowLayouts table |
