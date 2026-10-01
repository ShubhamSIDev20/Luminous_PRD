# BatteryTestingSystem — Customer Site Installation Docs

> **Audience:** Support/field engineer installing this application at a customer site.
> No prior familiarity with .NET, IIS, or Docker is assumed.
>
> **Screenshots:** Steps that benefit from a screenshot have a placeholder block like the one below. These dialogs are native Windows/Docker Desktop UI, not web pages, so they can't be captured by browser automation — insert your own screenshot from the actual install when you run it the first time, then this doc is screenshot-complete for every future install.
>
> ```
> 🖼️ [SCREENSHOT — <what it should show>]
> ```

## Read in this order

| # | Document | What it covers |
|---|---|---|
| 1 | [01-prerequisites.md](01-prerequisites.md) | Hardware/OS requirements, what to bring on-site, choosing IIS vs Docker |
| 2 | [02-power-and-uptime-settings.md](02-power-and-uptime-settings.md) | **Critical.** Prevent the host from sleeping/hibernating and stop network adapters from powering down — otherwise UDP device-registration packets are silently lost |
| 3 | [03-iis-installation.md](03-iis-installation.md) | **Default path.** Enable IIS, install .NET Hosting Bundle, create app pool + site, and (§3.9) disable IIS's own idle-timeout/recycling — an app-pool "sleep" separate from OS sleep, equally critical |
| 4 | [04-docker-installation.md](04-docker-installation.md) | Alternative path — only if the customer requires containers |
| 5 | [05-application-deployment.md](05-application-deployment.md) | Copy/publish the app, folder layout, permissions |
| 6 | [06-configuration.md](06-configuration.md) | `appsettings.json` / environment variables, secrets handling |
| 7 | [07-database-setup.md](07-database-setup.md) | SQLite DB location, encryption, first-run migration checklist |
| 8 | [08-database-backup.md](08-database-backup.md) | Cold/hot backup procedures, scheduling, retention |
| 9 | [09-database-restore.md](09-database-restore.md) | Step-by-step restore from a backup |
| 10 | [10-post-install-verification.md](10-post-install-verification.md) | Smoke-test checklist before leaving site, including a real idle-timeout test |
| 11 | [11-troubleshooting.md](11-troubleshooting.md) | Common symptoms → fixes |

## The two rules that matter most

1. **Application binaries and application data/database must live in separate locations — never one under the other.** Reinstalling, upgrading, or wiping the app folder must never be able to touch the database or exported files. See [01-prerequisites.md](01-prerequisites.md#folder-layout-decision-read-this-first) for the recommended layout.

2. **The host must never sleep/hibernate, and IIS must never idle-out or recycle the application pool.** Test programs run unattended for months; either failure silently drops incoming UDP device-registration packets with no retry. See [02-power-and-uptime-settings.md](02-power-and-uptime-settings.md) (OS-level) and [03-iis-installation.md §3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) (IIS-level) — **both** are required, neither alone is sufficient.

---
*Reflects the release as of 2026-08-14 (ASP.NET Core 8 / Blazor Server, SQLite, IIS `web.config` with in-process hosting). No Dockerfile ships with the current release — see [04-docker-installation.md](04-docker-installation.md) before committing to that path.*
