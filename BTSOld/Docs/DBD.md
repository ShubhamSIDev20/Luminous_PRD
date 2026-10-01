# Database Design Document (DBD)
**Document ID:** BTS-DBD-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Main Application Database (AppDbContext)

**Engine:** SQLite  
**File:** `{AppSettings.Data}/BtsAppdb.db`  
**ORM:** Entity Framework Core 8 with migrations  
**Schema:** Managed via 11 sequential EF Core migrations

### 1.1 Entity Relationship Overview

```
Users (auth)
  └── UserCircuitAccess ──▶ Circuits ──▶ Devices
                              │
                              └── BatterySessions ──▶ Batteries ──▶ BatteryTypes
                                        │
                                        └── (data → Session SQLite DB)

Circuits ──▶ CalibrationDataPoints
Devices  ──▶ DeviceSettings (bak)

Programs (BtsPrograms)
RegistrationStandards
DbcFileRecords
CodeMessages
ConfigurationEntity
AuditLogs
```

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

#### Devices
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | Auto-increment |
| DeviceName | TEXT | 16-char max |
| DeviceIp | TEXT | Dot-notation |
| MacAddress | TEXT | Colon-notation |
| IsActive | INTEGER | bool |
| CreatedAt | TEXT | datetime |
| CreatedBy | TEXT | |

#### Circuits
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| DeviceId | INTEGER FK → Devices | |
| CircuitNumber | INTEGER | 0-based index |
| CircuitType | INTEGER | Enum: Single/Dual Bank |
| MaxVoltage | REAL | float V |
| MinVoltage | REAL | float V |
| MaxChargeCurrent | REAL | float A |
| MaxDischargeCurrent | REAL | float A |
| IsActive | INTEGER | bool |

#### BatteryTypes
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| TypeName | TEXT | e.g. Li-Ion |
| Chemistry | TEXT | |
| NominalVoltage | REAL | V |
| Capacity | REAL | Ah |

#### Batteries
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| SerialNumber | TEXT | Unique |
| BatteryTypeId | INTEGER FK → BatteryTypes | |
| ManufactureDate | TEXT | datetime |
| IsActive | INTEGER | bool |

#### BatterySessions
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| SessionId | TEXT | Unix epoch timestamp (non DB-generated) |
| CircuitId | INTEGER FK → Circuits | |
| BatteryId | INTEGER FK → Batteries | |
| ProgramId | INTEGER FK → Programs | |
| StartTime | TEXT | datetime |
| EndTime | TEXT | datetime nullable |
| Status | INTEGER | Enum: Running/Completed/Stopped/Error |
| StandardId | INTEGER FK → RegistrationStandards | |
| TotalCapacity | REAL | Ah |
| TotalEnergy | REAL | Wh |
| CycleCount | INTEGER | |

#### BtsPrograms
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| ProgramName | TEXT | |
| Description | TEXT | |
| Steps | TEXT | JSON serialized step array |
| DbcFileId | INTEGER FK → DbcFileRecords nullable | |
| CreatedAt | TEXT | |
| CreatedBy | TEXT | |

#### RegistrationStandards
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| StandardName | TEXT | |
| MaxVoltage | REAL | |
| MinVoltage | REAL | |
| MaxCurrent | REAL | |
| MinTemperature | REAL | |
| MaxTemperature | REAL | |

#### CalibrationDataPoints
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| CircuitId | INTEGER FK → Circuits | |
| CalibrationType | INTEGER | Enum |
| Gain | REAL | |
| Offset | REAL | |
| CalibratedAt | TEXT | datetime |
| CalibratedBy | TEXT | |

#### DbcFileRecords
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| FileName | TEXT | |
| ParsedData | TEXT | JSON (DbcDatabase) |
| UploadedAt | TEXT | datetime |
| UploadedBy | TEXT | |

#### CodeMessages
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Code | INTEGER | Hardware error code |
| Message | TEXT | Human-readable description |
| Severity | INTEGER | Enum |
| Category | TEXT | |

#### ConfigurationEntity
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Key | TEXT | Unique — composite of user+component |
| Value | TEXT | JSON-serialized state |
| CreatedAt | TEXT | |
| UpdatedAt | TEXT | |

#### AuditLogs
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| Action | TEXT | Create/Update/Delete/Login etc. |
| EntityType | TEXT | Table name |
| EntityId | TEXT | |
| OldValue | TEXT | JSON |
| NewValue | TEXT | JSON |
| UserId | TEXT | FK → Users |
| Timestamp | TEXT | datetime |
| IpAddress | TEXT | |

#### UserCircuitAccess
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | |
| UserId | TEXT FK → Users | |
| CircuitId | INTEGER FK → Circuits | |
| CanStart | INTEGER | bool |
| CanStop | INTEGER | bool |
| CanCalibrate | INTEGER | bool |

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

## 3. Migration History

| Migration | Date | Changes |
|---|---|---|
| InitialCreate | 2025-12-18 | Base schema: Devices, Circuits, Programs, Sessions, Batteries |
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


#### ProgramSchedules
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | Auto-increment |
| Name | TEXT | Max 100 chars |
| ProgramId | INTEGER FK → BtsPrograms | Program to upload |
| BatteryId | INTEGER FK → Batteries | Battery to assign |
| DbcFileId | INTEGER FK → DbcFileRecords nullable | Optional DBC file |
| ScheduledAt | TEXT | UTC datetime — when to execute |
| TargetCircuitsJson | TEXT | JSON array of `{DeviceId, CircuitId}` targets |
| IsActive | INTEGER | bool — disabled schedules are skipped by Hangfire |
| HangfireJobId | TEXT | Hangfire job ID for reschedule/delete; max 100 chars |
| CreatedAt | TEXT | datetime |
| CreatedBy | TEXT | |
| UpdatedAt | TEXT | datetime nullable |
| IsDeleted | INTEGER | soft-delete bool |

#### ScheduleExecutionLogs
| Column | Type | Notes |
|---|---|---|
| Id | INTEGER PK | Auto-increment |
| ScheduleId | INTEGER FK → ProgramSchedules | Parent schedule |
| DeviceId | INTEGER | Target device |
| CircuitId | INTEGER | Target circuit |
| ExecutedAt | TEXT | datetime |
| Status | TEXT | `Success` / `Failed` / `Skipped_Offline` / `Skipped_AlreadyRunning` |
| FailReason | TEXT | Error detail if Status = Failed; max 500 chars; nullable |


---

## 3. Migration History

| Migration | Date | Changes |
|---|---|---|
| InitialCreate | 2025-12-18 | Base schema: Devices, Circuits, Programs, Sessions, Batteries |
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
