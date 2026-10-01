# 7. Database Setup

← [6. Configuration](06-configuration.md) | Next: [8. Database Backup →](08-database-backup.md)

---

## 7.1 Engine & location

- **Engine:** SQLite (file-based, no separate DB server to install)
- **Location:** wherever `ConnectionStrings:DefaultConnection` points — per [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first) and [6. Configuration](06-configuration.md), this must be `D:\BTS\Data\BtsAppdb.db` (or your site's equivalent separate data path), **never** under the application binaries folder.
- **Schema creation:** automatic. On first startup, the application applies all EF Core migrations and seeds initial data itself — **you do not need to run any manual schema/setup scripts.**

## 7.2 Encryption (optional)

Controlled by `AppSettings:DbEncryption` / `AppSettings:Password` ([§6](06-configuration.md)). If the customer requires encryption-at-rest, these must be set **before** first startup — changing them after data already exists requires a re-encryption migration, not a simple config edit. Confirm this requirement with the customer before go-live.

## 7.3 First-run checklist

1. Confirm the app pool / container identity has write access to `D:\BTS\Data` (done in [§3.5](03-iis-installation.md#35-set-folder-permissions) or [§4.2](04-docker-installation.md#42-prepare-persistent-storage--keep-it-off-the-apps-container-path))
2. Confirm [§2](02-power-and-uptime-settings.md) and [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) are both done — running the first-run checklist without those in place risks the very data loss this document set exists to prevent, from the very first day
3. Start the app
4. Watch the log (`C:\BTS\App\logs\stdout*.log` or `docker logs`) for `"Applying migrations"` and confirm no exceptions

```
🖼️ [SCREENSHOT — stdout log excerpt showing migrations applying successfully on first startup]
```

5. Confirm the DB file was created at the expected path:
   ```powershell
   Test-Path "D:\BTS\Data\BtsAppdb.db"
   ```
6. Log into the UI with the seeded default admin account (from engineering release notes) — **change the password immediately** after first login

```
🖼️ [SCREENSHOT — the admin "Change Password" screen after first login]
```

---
← [6. Configuration](06-configuration.md) | Next: [8. Database Backup →](08-database-backup.md)
