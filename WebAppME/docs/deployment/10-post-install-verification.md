# 10. Post-Install Smoke Test

← [9. Database Restore](09-database-restore.md) | Next: [11. Troubleshooting →](11-troubleshooting.md)

---

Run this checklist before leaving site.

1. [ ] Browse to the site URL from a machine other than the server — confirms network/firewall path works, not just localhost
2. [ ] Log in with the default admin account, then **change its password**
3. [ ] Create a test device/channel entry and confirm it saves (validates DB write path to `D:\BTS\Data`)
4. [ ] If hardware is already connected, confirm the dashboard shows a live/online status for at least one device
5. [ ] Trigger one export (Reports page) and confirm the Hangfire background job completes and the file downloads
6. [ ] Run a manual backup per [§8](08-database-backup.md) and verify it per [§8.5](08-database-backup.md#85-verify-a-backup-is-restorable)
7. [ ] Confirm the app auto-starts after a server reboot: reboot the machine, wait 2 minutes, browse to the site again without manually starting anything
8. [ ] Confirm `D:\BTS\Data` and `D:\BTS\Backups` are on a disk with adequate free space and, ideally, included in the customer's existing backup/DR tooling
9. [ ] **Sleep/idle-timeout check (critical, don't skip):** confirm [§2](02-power-and-uptime-settings.md) power settings and [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) IIS idle-timeout/recycling are both applied. Practical test: leave the site idle (no browser tabs open, no clicks) for at least 25 minutes, then confirm a device can still register/reconnect without you touching the browser first — this catches the exact "IIS stopped the pool after 20 minutes" failure mode before it becomes a customer complaint
10. [ ] Hand off to the customer: site URL, default admin username (password already changed by you in step 2), backup folder location, and this documentation set

```
🖼️ [SCREENSHOT — final dashboard view with a live/online device, used as the "install complete" reference for the handoff report]
```

---
← [9. Database Restore](09-database-restore.md) | Next: [11. Troubleshooting →](11-troubleshooting.md)
