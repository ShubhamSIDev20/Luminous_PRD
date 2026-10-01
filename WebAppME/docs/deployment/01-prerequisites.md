# 1. Prerequisites & System Requirements

← [Back to index](README.md) | Next: [2. Power & Uptime Settings →](02-power-and-uptime-settings.md)

---

## 1.1 What you're installing

**BatteryTestingSystem** is a Windows-hosted web application (ASP.NET Core 8, Blazor Server) that:
- Serves a browser-based UI to operators (dashboard, device/battery/program management, reports)
- Talks to physical test hardware over TCP (one connection per device, up to 64 channels each)
- Stores all data in a single **SQLite** database file on disk
- Runs background jobs (Hangfire) for exports

## 1.2 Choose your install path

| Option | When to use |
|---|---|
| **IIS** (§3) | **Default choice for every site.** Matches how the app is built and published — a `web.config` targeting IIS ships with every release. |
| **Docker** (§4) | Only if the customer specifically requires containers or Linux hosting. No Dockerfile ships with the current release — confirm with engineering before promising this to a customer. |

If unsure, **use IIS**. Either way, **§2 (Power & Uptime Settings) is not optional** — read it next regardless of which path you pick.

## 1.3 Hardware (minimum)

| Component | Minimum | Recommended |
|---|---|---|
| CPU | 2 cores | 4 cores |
| RAM | 4 GB | 8 GB |
| Disk (app) | 5 GB free | 10 GB free |
| Disk (data — separate volume, see §1.5) | 20 GB free | 100 GB+ (DB + logs + exports grow over time) |
| Network | 1 NIC for LAN/browser access | + separate NIC/VLAN for device TCP traffic if the customer requires isolation |

## 1.4 Operating System & software

- Windows Server 2019/2022, or Windows 10/11 Pro (small sites / single-PC install)
- Administrator access on the machine for the entire install
- **IIS path:** the .NET 8 Hosting Bundle (different from the plain runtime — adds the IIS module). Download: `https://dotnet.microsoft.com/download/dotnet/8.0` → "Hosting Bundle" under ASP.NET Core Runtime 8.0.x
- **Docker path:** Docker Desktop (Windows) or Docker Engine (Linux), WSL2 if on Windows 10/11, virtualization enabled in BIOS/UEFI
- A current Edge/Chrome for operators to use the UI

## 1.5 Folder layout decision — read this first

**Application binaries and application data must live in separate locations.** Do not nest the data folder inside the app's install folder, and do not nest the app inside the data folder.

Why this matters: redeploying, upgrading, or wiping the application folder (a routine support action) must never be able to delete or corrupt the customer's database, exports, or uploaded files. Keeping them apart also makes backup tooling simpler (back up one folder, not "everything except these three subfolders").

**Recommended layout** (use a second physical drive/volume for data if the machine has one):

```
C:\BTS\App\              ← application binaries, web.config, appsettings.json (replaced on every upgrade)
D:\BTS\Data\             ← SQLite database, exports, uploads (NEVER touched by an app upgrade)
D:\BTS\Backups\          ← scheduled + manual DB backups
```

If the machine has only one drive, still keep them as **siblings**, never parent/child:
```
C:\BTS\App\
C:\BTS\Data\
C:\BTS\Backups\
```

This guide uses `C:\BTS\App` and `D:\BTS\Data` / `D:\BTS\Backups` throughout — substitute your actual paths consistently across every doc in this set if the customer's site differs, and write down the paths you chose before moving on (you'll need them repeatedly).

> **On-site record — fill this in before you start:**
> - App folder: `______________________`
> - Data folder: `______________________`
> - Backup folder: `______________________`
> - Site hostname/URL: `______________________`

## 1.6 What to bring / prepare before arriving on-site

- [ ] The application release package (published output — a folder containing `BatteryTestingSystem.exe`, `web.config`, `appsettings.json`, and supporting DLLs), or the Docker image if that path was pre-approved
- [ ] This documentation set
- [ ] The customer's assigned domain/hostname (or plan to use the machine's IP/hostname if none exists)
- [ ] Default admin credentials from engineering release notes (to log in the first time — you will change this password during install)

---
← [Back to index](README.md) | Next: [2. Power & Uptime Settings →](02-power-and-uptime-settings.md)
