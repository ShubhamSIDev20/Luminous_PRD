# 8. Database Backup Process

← [7. Database Setup](07-database-setup.md) | Next: [9. Database Restore →](09-database-restore.md)

---

Because the database is a single SQLite file, backup is a **file-level copy** — no `mysqldump`/`pg_dump`-style tooling needed. Backups are written to `D:\BTS\Backups`, kept separate from both the app folder and the live data folder ([§1.5](01-prerequisites.md#folder-layout-decision-read-this-first)).

## 8.1 Cold backup (simplest, safest — requires brief downtime)

Use for scheduled maintenance windows.
```powershell
# 1. Stop the app so no writes are in-flight
Stop-WebSite -Name "BatteryTestingSystem"

# 2. Copy the DB file (and its -wal/-shm sidecar files if present) to a timestamped backup location
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
Copy-Item "D:\BTS\Data\BtsAppdb.db*" -Destination "D:\BTS\Backups\BtsAppdb_$stamp.db" -Force

# 3. Restart the app
Start-WebSite -Name "BatteryTestingSystem"
```
> The `*` in `BtsAppdb.db*` matters — SQLite may have `BtsAppdb.db-wal` and `BtsAppdb.db-shm` sidecar files while the app is running. Backing up only the `.db` file while the app is live can capture an inconsistent snapshot. Stopping the app first avoids this entirely.

> Stopping the site also stops the UDP registration listener for the duration of the backup — keep cold backups brief and scheduled for the customer's lowest-traffic window, and prefer the hot backup method below if the site truly cannot tolerate any gap.

## 8.2 Hot backup (no downtime — use the `sqlite3` CLI)

Use this if the customer cannot tolerate any downtime — including the brief UDP-listener gap a cold backup causes. Requires the `sqlite3` command-line tool on the server:
```powershell
sqlite3 "D:\BTS\Data\BtsAppdb.db" ".backup 'D:\BTS\Backups\BtsAppdb_$(Get-Date -Format yyyyMMdd_HHmmss).db'"
```
This uses SQLite's own online backup API and is safe to run while the app is writing to the DB.

## 8.3 Schedule it

Save the chosen commands from §8.1 or §8.2 into `D:\BTS\Backups\backup-script.ps1`, then register a nightly task:
```powershell
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-File D:\BTS\Backups\backup-script.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 2am
Register-ScheduledTask -TaskName "BTS-Nightly-Backup" -Action $action -Trigger $trigger -RunLevel Highest -User "SYSTEM"
```

```
🖼️ [SCREENSHOT — Windows Task Scheduler showing the "BTS-Nightly-Backup" task, Triggers tab set to Daily 2:00 AM]
```

## 8.4 Retention & storage

- Keep at minimum the last 7 daily backups on-site; agree retention policy with the customer (some sites require 30+ days or offsite copies)
- Store backups on a **different physical disk** than the live DB if possible — a single-disk failure should not take out both `D:\BTS\Data` and `D:\BTS\Backups`
- If the customer already has offsite/cloud backup tooling (e.g. Veeam, Windows Server Backup), simply add `D:\BTS\Backups\` (and optionally the live `D:\BTS\Data` path) to its included folders — no special SQLite handling is required for cold copies made per §8.1

## 8.5 Verify a backup is restorable

Do this at least once during install, and periodically after:
```powershell
sqlite3 "D:\BTS\Backups\BtsAppdb_<stamp>.db" "PRAGMA integrity_check;"
```
Expected output: `ok`. If you get anything else, that backup is corrupt — investigate before relying on it.

```
🖼️ [SCREENSHOT — terminal output of the PRAGMA integrity_check command returning "ok"]
```

---
← [7. Database Setup](07-database-setup.md) | Next: [9. Database Restore →](09-database-restore.md)
