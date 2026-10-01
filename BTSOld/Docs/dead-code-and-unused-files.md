# Dead Code & Unused Files

> Generated: 2026-07-02  
> Scope: full repo scan via Gortex graph + working-tree inspection

---

## 1. Leftover Temp Files (safe to delete)

These are Gortex editor-overlay temp files left in the working tree. They are not tracked by git and serve no purpose.

| File |
|------|
| `Components/UI/Dashboard/TransferDialog.razor.gortex.tmp-3064864117` |
| `Components/UI/Dashboard/TransferDialog.razor.gortex.tmp-3227783060` |
| `Repositories/Implementations/BatteryRepository.cs.gortex.tmp-3936122679` |
| `Services/Implementations/BatteryServices.cs.gortex.tmp-343953154` |

**Action:** `git clean -f` or delete manually.

---

## 2. Backup Files (.bak)

Old backups of files that have been replaced or refactored. The `.bak` files in the project root are already excluded from the build via `<None Remove=...>` entries in the `.csproj`, but they clutter the tree.

| Backup File | Notes |
|-------------|-------|
| `Services/Implementations/SqliteDatabaseManager.bak` | Replaced by `SqliteBulkDatabaseManager` |
| `Services/Interfaces/ISqliteDatabaseManager.bak` | Interface for the old manager (also excluded in csproj) |
| `Services/DbcFileManager.bak` | Old DBC file manager |
| `Services/Implementations/DeviceSettingService.cs.bak` | Old device settings service |
| `Services/PopupService.bak` | Old popup service |
| `Services/TableFiles.bak` | Old table files service |
| `Repositories/Implementations/DeviceSettingsRepository.cs.bak` | Old device settings repo |
| `Components/Pages/Programs/ProgramList.razor.bak` | Old program list page |
| `Components/Pages/Reports/Reports.razor.bak` | Old reports page |
| `Components/Pages/Settings/CalibrationWindow.bak` | Old calibration window |
| `Components/UI/Datatable/DataTable.bak` | Old datatable component |
| `Components/UI/Program/LimitActionPairEditor.bak` | Old limit action pair editor |
| `Components/UI/Program/OperatorSelect.razor.bak` | Old operator select |
| `wwwroot/js/highcharts/highchartsHelper.bak` | Old highcharts helper (also in bin/obj) |

**Action:** Delete all `.bak` files. They are not referenced by any build target.

---

## 3. Dead Interface — `ISqliteDatabaseManager`

The old SQLite manager was replaced by `SqliteBulkDatabaseManager` but the interface file was left behind.

| Item | File | Status |
|------|------|--------|
| `ISqliteDatabaseManager` interface | `Services/Interfaces/ISqliteDatabaseManager.cs` | **File still exists** — implementation is `.bak`'d |
| DI registration | `Extensions/ServiceCollectionExtensions.cs:68` | **Commented out** — `//services.AddScoped<ISqliteDatabaseManager, SqliteDatabaseManager>();` |

**Action:** Delete `Services/Interfaces/ISqliteDatabaseManager.cs` if `SqliteBulkDatabaseManager` fully replaces it.

---

## 4. Commented-Out UI / Script References

### `Components/App.razor`
| Line | Code | Notes |
|------|------|-------|
| 8 | `@* <link rel="stylesheet" href="app.css" /> *@` | Old stylesheet, superseded by Tailwind |
| 23 | `@* <script src="js/windowManager.js?version=0.4"> *@` | Old window manager JS |
| 24 | `@* <script src="js/windowDragManager.js?version=0.4"> *@` | Old window drag JS |
| 25 | `@* <script src="js/highcharts/highchartsHelper.js?version=0.5"> *@` | Old highcharts helper JS |

### `Components/Layout/AppLayout.razor`
| Line | Code | Notes |
|------|------|-------|
| 20 | `@* <Footer Errors=...> *@` | Footer component disabled |

### `Components/Pages/Home/DashboardView.razor`
| Line | Code | Notes |
|------|------|-------|
| 80 | `@* <CircuitActionBar ... *@` | CircuitActionBar component disabled |

### `Components/Pages/Programs/ProgramEditor.razor`
| Line | Code | Notes |
|------|------|-------|
| 50 | `@* <th class="grid-header-cell w-28 border-r-0"></th> *@` | Empty header cell commented out |

**Action:** Remove or restore these as appropriate. At minimum, the JS scripts in `App.razor` can be deleted along with their `.bak` counterparts.

---

## 5. Open TODOs

Items flagged in code that represent incomplete functionality.

| File | Line | Tag | Description |
|------|------|-----|-------------|
| `Components/Layout/MainLayout.razor` | 280 | TODO | "Add logout logic (clear tokens, redirect, etc.)" |
| `Components/Pages/Batteries/BatteriesList.razor` | 937 | TODO | "trigger browser download when path.Data is available" |
| `Components/Pages/Programs/ProgramEditor.razor` | 1227 | TODO | "Log exception + show toaster" |
| `Components/UI/Program/StepRow.razor` | 229 | TODO | "Implement your table viewing logic here" |
| `Services/DbcParser.cs` | 198 | TODO | "Define serialization protocol" |
| `Services/ServerSessionStorageService.cs` | 235 | TODO | "log error" |
| `Services/DecoderService.cs` | 928 | NOTE | "DBC opcodes (31–250) are not supported in V2 realtime data packets" — separate from DBC signal IDs, no action needed |

---

## 6. Notable: Hotspot False Positives

Gortex flagged all EF migration `BuildTargetModel` methods as hotspots (fan-out ~300–500, no callers). These are EF-generated and expected — **do not delete them**.

---

## Priority Order

| Priority | Action |
|----------|--------|
| 🔴 High | Delete `.gortex.tmp-*` files (untracked working tree noise) |
| 🟡 Medium | Delete all `.bak` files |
| 🟡 Medium | Delete `ISqliteDatabaseManager.cs` + remove its commented DI line |
| 🟢 Low | Clean up commented-out script tags in `App.razor` |
| 🟢 Low | Address open TODOs above |
