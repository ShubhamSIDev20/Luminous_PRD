# 11. Troubleshooting

← [10. Post-Install Verification](10-post-install-verification.md) | [Back to index](README.md)

---

| Symptom | Likely cause | Fix |
|---|---|---|
| IIS shows a generic 502.5 / "Process Failure" error | Hosting Bundle not installed, or app crashed on startup | Enable stdout logging ([§3.7](03-iis-installation.md#37-confirm-webconfig-is-correct)), check `logs\stdout*.log` for the real exception |
| Site loads but every page 500s | DB file path wrong or app pool lacks write permission | Re-check `ConnectionStrings:DefaultConnection` ([§6](06-configuration.md)) points at `D:\BTS\Data`, and re-run [§3.5](03-iis-installation.md#35-set-folder-permissions) |
| "Access to the path ... is denied" in logs | Folder ACLs not granted to the app pool identity | Re-run the `icacls` commands in [§3.5](03-iis-installation.md#35-set-folder-permissions), confirm the app pool name matches exactly |
| Devices show offline even though hardware is powered on | Firewall blocking the device TCP/UDP port, or wrong port configured | Check the firewall rule in [§3.8](03-iis-installation.md#38-start-the-site-and-open-the-firewall) and the hardware setup sheet for the correct port |
| **Device registrations silently go missing overnight or over a weekend, no error shown anywhere** | Host machine went to sleep/hibernated, or a network adapter powered down to save energy | Apply every step in [§2](02-power-and-uptime-settings.md), especially [§2.4](02-power-and-uptime-settings.md#24-network-adapter-power-management--this-is-the-one-most-likely-to-actually-cause-the-udp-loss) (network adapter power management) — this is the single most common root cause reported for this symptom |
| **Device registrations go missing on a regular ~20-minute or ~29-hour cadence even though the PC never sleeps** | IIS's default Idle Time-out (20 min) or periodic recycling (~29h) stopped/restarted the worker process | Apply [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) — set both `processModel.idleTimeout` and `recycling.periodicRestart.time` to 0 |
| Backup file fails `PRAGMA integrity_check` | Backup was taken while the app was writing, without the `-wal` sidecar copied (cold backup) or without the `.backup` API (hot copy) | Re-backup using [§8.1](08-database-backup.md#81-cold-backup-simplest-safest--requires-brief-downtime) (app fully stopped) or [§8.2](08-database-backup.md#82-hot-backup-no-downtime--use-the-sqlite3-cli) (`sqlite3 .backup`), never a raw copy of a live-written file |
| Container restarts in a loop (Docker) | Data volume not mounted, or `ConnectionStrings` pointing inside the container's ephemeral layer | Confirm the `-v` mount in [§4.4](04-docker-installation.md#44-run-the-container) and that the connection string path is under the mounted path |
| After an app upgrade, data looks "reset" or missing | Data folder was accidentally nested inside the app folder and got wiped by the redeploy | This is exactly what [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first) exists to prevent — restore from the last backup ([§9](09-database-restore.md)) and fix the folder layout before the next upgrade |
| After an app/site recreate, sleep-related symptoms come back | Recreating the IIS app pool or site resets Idle Time-out/recycling to IIS defaults | Re-apply [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) any time the app pool or site is recreated, not just on first install |

---
← [10. Post-Install Verification](10-post-install-verification.md) | [Back to index](README.md)
