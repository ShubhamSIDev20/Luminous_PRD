# 5. Application Deployment

← [3. IIS Installation](03-iis-installation.md) | Next: [6. Configuration →](06-configuration.md)

---

This step assumes you've completed either [3. IIS Installation](03-iis-installation.md) or [4. Docker Installation](04-docker-installation.md), and the folders from [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first) already exist.

## 5.1 Copy the published application (IIS path)

Copy the entire contents of the release package (the folder containing `BatteryTestingSystem.exe`, `web.config`, `appsettings.json`, and the supporting DLLs) into `C:\BTS\App`.

```
🖼️ [SCREENSHOT — Windows Explorer showing C:\BTS\App populated with BatteryTestingSystem.exe, web.config, appsettings.json, and DLLs]
```

If you were instead handed source code and must publish yourself:
```powershell
cd <path-to-repo>
dotnet publish BatteryTestingSystem.csproj -c Release -r win-x64 --self-contained false -o C:\BTS\App
```

```
🖼️ [SCREENSHOT — terminal output of `dotnet publish` completing with "Build succeeded"]
```

## 5.2 Confirm the data path does NOT point inside the app folder

Before starting the app for the first time, open `C:\BTS\App\appsettings.json` and confirm:
- `ConnectionStrings:DefaultConnection` points at `D:\BTS\Data\BtsAppdb.db` (not anywhere under `C:\BTS\App`)
- `AppSettings:Data` points at `D:\BTS\Data`

Full walkthrough of every setting is in [6. Configuration](06-configuration.md) — do that step next, before first startup.

## 5.3 Upgrading later (for future reference)

Because binaries and data are separate ([§1.5](01-prerequisites.md#folder-layout-decision-read-this-first)), a future version upgrade is just:
1. Stop the site/container
2. Take a database backup regardless ([8. Database Backup](08-database-backup.md)) — a version upgrade may include a schema migration, and migrations only run forward
3. Replace the contents of `C:\BTS\App` with the new release (the `D:\BTS\Data` folder is untouched)
4. Start the site/container — the app applies any new EF Core migrations automatically on startup
5. Re-confirm the settings from [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) survived the upgrade — recreating the app pool or site during an upgrade resets idle-timeout/recycling back to IIS defaults

---
← [3. IIS Installation](03-iis-installation.md) | Next: [6. Configuration →](06-configuration.md)
