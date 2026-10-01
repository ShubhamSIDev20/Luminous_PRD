# Database
> Updated: 2026-08-10T00:00:00Z
> Type: SQLite (with optional SQLCipher encryption) | ORM: Entity Framework Core 8 | Migration tool: EF Core Migrations (`Migrations/`)

---

## Context
`Data/AppDbContext.cs` — inherits `IdentityDbContext<ApplicationUser, ApplicationRole, string>`. Connection string: `ConnectionStrings:DefaultConnection` in `appsettings.json` (local file path — do not hardcode elsewhere; read from config).

## DbSets (Entities)
| DbSet | Entity | Notes |
|-------|--------|-------|
| `Devices` | `Device` | Physical device; has `SecondaryBoard`s and `Channel`s |
| `Channels` | `Channel` | Belongs to a device/board; multiplexed via shared `DeviceConnection` |
| `SecondaryBoards` | `SecondaryBoard` | Sub-board under a device |
| `Programs` | `BtsPrograms` | Battery test program definitions |
| `RegistrationStandards` | `RegistrationStandard` | |
| `CalibrationDataPoints` | `CalibrationDataPoint` | |
| `BatterySessions` | `BatterySession` | A running/completed test session |
| `Batteries` | `Batteries` | |
| `BatteryTypes` | `BatteryType` | |
| `dbcFileRecords` | `DbcFileRecord` | DBC file records (CAN bus definitions) |
| `ConfigurationEntitys` | `ConfigurationEntity` | |
| `CodeMessages` | `CodeMessage` | |
| `AuditLogs` | `AuditLog` | |
| `UserCircuitAccesses` | `UserCircuitAccess` | Per-user circuit/device access control |
| `ProgramSchedules` | `ProgramSchedule` | |
| `ScheduleExecutionLogs` | `ScheduleExecutionLog` | |
| `ExportRecords` | `ExportRecord` | Backs `ExportController` async export flow |
| (Identity tables) | `ApplicationUser`, `ApplicationRole` | Via `IdentityDbContext<ApplicationUser, ApplicationRole, string>` |

## Encryption
`DbSecurity/` provides column-level encryption:
- `IColumnEncryptionService`, `IColumnEncryptionServiceWithSalt` — encryption service contracts
- `EncryptionAttribute` — marks entity properties for encryption
- `ModelBuilderEncryptionExtensions` — wires encryption into `OnModelCreating`

`AppSettings:DbEncryption` / `AppSettings:Password` in `appsettings.json` configure DB-level encryption (SQLCipher-related). Treat these as secrets — never copy real values into docs or commits.

## Migrations
- Located in `Migrations/`
- Applied automatically at startup via migration/seeding logic in `Program.cs` (through the static `ServiceLocator`)
- Standard EF Core rule: never edit an applied migration file — create a new one instead

## Repository Pattern
| Interface | Implementation | Notes |
|-----------|-----------------|-------|
| `IRepository` | `Repository.cs` | Generic base — namespace typo documented in clean-arch design doc, verify before relying on it |
| `IExportRepository` | `ExportRepository.cs` | Backs `ExportController` |
| `ISchedulerRepository` | `SchedulerRepository.cs` | |
| `ISpecificRepositories` | various (`AuditRepository`, `BatteryRepository`, `CodeMessageRepository`, `ConfigStorageRepository`, `DbcRepository`, `DeviceChannelRepository`, `ProgramRepository`, `UserCircuitAccessRepository`) | Domain-specific repositories |

## Notes
- Architecture design doc (`docs/architecture/02-backend-clean-architecture-design.md`) flags direct component injection of `IExportRepository` into Blazor components as a coupling risk in the current (pre-clean-architecture) code — not yet addressed.
- Background jobs (Hangfire) may share the same SQLite database or use a separate store — verify `Program.cs` Hangfire configuration before assuming which.