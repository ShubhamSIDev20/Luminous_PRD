# 9. Database Restoration Steps

← [8. Database Backup](08-database-backup.md) | Next: [10. Post-Install Verification →](10-post-install-verification.md)

---

Use this if the live database is lost, corrupted, or you need to roll back to a known-good point. Paths assume the layout from [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first): app at `C:\BTS\App`, data at `D:\BTS\Data`, backups at `D:\BTS\Backups`.

```powershell
# 1. Stop the application
Stop-WebSite -Name "BatteryTestingSystem"          # IIS
# or: docker stop batterytestingsystem              # Docker

# 2. Move the current (broken) DB aside — don't delete it, you may need it for diagnosis
Rename-Item "D:\BTS\Data\BtsAppdb.db" "BtsAppdb_broken_$(Get-Date -Format yyyyMMdd_HHmmss).db"
# Also move aside BtsAppdb.db-wal / BtsAppdb.db-shm if present

# 3. Copy the chosen backup file into place with the exact original filename
Copy-Item "D:\BTS\Backups\BtsAppdb_<stamp>.db" "D:\BTS\Data\BtsAppdb.db"

# 4. Confirm file permissions still allow the app pool identity access (backups sometimes inherit different ACLs)
icacls "D:\BTS\Data\BtsAppdb.db" /grant "IIS AppPool\BatteryTestingSystemPool:F"

# 5. Restart the application
Start-WebSite -Name "BatteryTestingSystem"
# or: docker start batterytestingsystem
```

```
🖼️ [SCREENSHOT — Windows Explorer showing D:\BTS\Data with the restored BtsAppdb.db in place and the "_broken" copy renamed aside]
```

## Verify

Log in, spot-check recent data (devices, sessions, programs) matches what's expected for the backup's timestamp. Anything created/changed after that timestamp is gone and must be re-entered or re-tested.

```
🖼️ [SCREENSHOT — the app's dashboard after restore, confirming data loads correctly]
```

If the app fails to start after restore, check the stdout log ([§3.7](03-iis-installation.md#37-confirm-webconfig-is-correct)) — a common cause is a schema mismatch if the backup predates a version upgrade that added new migrations; the app attempts to apply the missing migrations automatically on startup, which is expected and safe.

---
← [8. Database Backup](08-database-backup.md) | Next: [10. Post-Install Verification →](10-post-install-verification.md)
