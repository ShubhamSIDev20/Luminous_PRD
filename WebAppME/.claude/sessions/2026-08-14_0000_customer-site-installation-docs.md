# Session: Customer-Site Installation Doc Set
> Started: 2026-08-14T00:00:00Z | Agent: Claude (Sonnet 5)
> Prev session: [2026-08-12_1000_live-test-bugfixes-registration-status-preview.md](2026-08-12_1000_live-test-bugfixes-registration-status-preview.md)

## Goal
Produce a field/support-engineer-facing installation procedure for deploying BatteryTestingSystem at a customer site: prerequisites, IIS install, Docker install (alternative), post-install config, DB setup, DB backup, DB restore, verification, troubleshooting.

## What Was Done
- Investigated actual deployment shape: no `docker-compose`/`Dockerfile` exists in the repo; real deployment is `dotnet publish -r win-x64` + IIS, using the existing `web.config` (AspNetCoreModuleV2, in-process hosting). DB is SQLite (`ConnectionStrings:DefaultConnection`), optional SQLCipher via `AppSettings:DbEncryption`/`Password`, migrations auto-applied at startup (per `.claude/CONTEXT/database.md`).
- First draft (round 1) was a single combined doc, superseded by round 2 below — see final file list there.
- Adopted `C:\BTS\App` (binaries) / `D:\BTS\Data` (DB + exports) / `D:\BTS\Backups` as the reference paths, explicitly called out as separate roots — a redeploy/upgrade of the app folder can never touch data.
- Screenshots: browser automation (claude-in-chrome) can only capture web pages, not native Windows dialogs (Server Manager, IIS Manager, Docker Desktop installer) — told the user this directly rather than faking/skipping it silently. Every step that would benefit from a screenshot has a `🖼️ [SCREENSHOT — ...]` placeholder block describing exactly what to capture, so the user can drop in real screenshots from their own first install pass.

## Round 2 (same session) — user feedback and revision
User asked for three changes:
1. Split the single doc into separate per-topic files ("easy to handle") — done, see above.
2. Data/DB folder must be kept physically separate from the application folder "for safety" — already addressed in round 1 via `C:\BTS\App` vs `D:\BTS\Data`/`D:\BTS\Backups`.
3. **Critical new requirement**: explicitly document preventing the host PC from sleeping/hibernating (UDP device-registration packets are lost with no retry if the network stack goes down) AND preventing IIS from idling out / recycling the application pool when not in use — since test programs run unattended for months, either failure silently drops registrations.

Changes made:
- Added **`02-power-and-uptime-settings.md`** (new file, inserted as document #2): `powercfg` commands + GUI steps to force High Performance / never-sleep / no-hibernate, laptop lid-close and USB-selective-suspend settings, and — the most likely actual root cause — **network adapter power management** ("Allow the computer to turn off this device to save power" must be unchecked on every NIC), plus Windows Update active-hours guidance and a UPS note.
- Added **§3.9 "Prevent IIS From Idling Out The Application Pool (Critical)"** to `03-iis-installation.md`: disables IIS's default 20-minute Idle Time-out and ~29-hour periodic recycling (`processModel.idleTimeout` and `recycling.periodicRestart.time` both set to `0`), plus Preload/AlwaysRunning confirmation and a trade-off note about disabling self-healing recycling entirely vs. a scheduled time.
- Renumbered every subsequent file (was 02–10, now 03–11) to slot the new power doc in at position 2; re-validated every internal `§`, heading, and cross-file link after the shift (confirmed via a final grep pass — no stale references).
- Added new troubleshooting rows distinguishing the two failure modes (OS sleep vs. IIS idle-timeout) since they have different root causes and fixes, and a Docker-path note that OS sleep prevention still applies to the container host even though Docker itself has no IIS-style idle-timeout equivalent (`--restart unless-stopped` already covers container-level restarts).
- Reinforced in the post-install smoke test (§10, item 9): leave the site idle 25+ minutes and confirm device registration still works without touching the browser first — a practical test that catches the IIS idle-timeout failure mode before the customer does.

Final file list (`docs/deployment/`): `README.md`, `01-prerequisites.md`, `02-power-and-uptime-settings.md`, `03-iis-installation.md`, `04-docker-installation.md`, `05-application-deployment.md`, `06-configuration.md`, `07-database-setup.md`, `08-database-backup.md`, `09-database-restore.md`, `10-post-install-verification.md`, `11-troubleshooting.md`.

## Discoveries / Gotchas
- **No Docker artifact exists yet** — `04-docker-installation.md` is generic/best-effort and explicitly tells the field engineer to confirm with engineering before using it on a real site.
- Per `AGENT.md` rule #8, did **not** propagate the real dev secret values found in `appsettings.json` (`AppSettings:DbEncryption`/`Password`) into the docs — replaced with guidance to generate new per-customer values.
- SQLite backup while the app is live requires either stopping the app (cold copy, safest) or `sqlite3 .backup` (online-safe); documented explicitly in `08-database-backup.md` why a raw file copy of a live DB is risky (`-wal`/`-shm` sidecars).
- claude-in-chrome/browser automation cannot screenshot native OS dialogs — only web pages. Worth remembering for any future "give me screenshots of an install process" request in this or other projects.
- **IIS has its own "sleep" independent of the OS**: default Idle Time-out (20 min) and periodic recycling (~29h) will stop/restart the worker process regardless of whether Windows itself is awake. Both OS-level (`powercfg`, NIC power management) and IIS-level (`processModel.idleTimeout`, `recycling.periodicRestart.time`) settings must be zeroed out for an unattended, months-long test run to be reliable — fixing only one leaves a real data-loss gap. Worth remembering for any future long-running Windows service/host deployment doc.

## Status (final)
✅ Complete — 12-file doc set under `docs/deployment/` (README + 01–11), links/numbering verified consistent after the mid-session renumber.

## Next Agent Should Do
Nothing pending. If a real Dockerfile is added to the repo, update `docs/deployment/04-docker-installation.md` to reference it directly. If the user provides real screenshots, they replace the `🖼️` placeholder blocks in place — no doc restructuring needed. If a future file is inserted into this numbered sequence, repeat the renumber-then-grep-verify approach used in round 2 rather than hand-editing links piecemeal.
