# 3. IIS Installation (Default Path)

← [2. Power & Uptime Settings](02-power-and-uptime-settings.md) | Next: [4. Docker Installation (alternative) →](04-docker-installation.md) | [Skip to 5. Application Deployment →](05-application-deployment.md)

---

Paths below use `C:\BTS\App` (binaries) and `D:\BTS\Data` (database/exports) per the folder-layout decision in [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first). Substitute the paths you recorded there if different.

> Complete [2. Power & Uptime Settings](02-power-and-uptime-settings.md) before this section if you haven't already. §3.9 below covers IIS's *own* idle-timeout/recycling, which is a separate risk from OS sleep — both must be handled.

## 3.1 Enable IIS

Run in an **elevated PowerShell** (Run as Administrator):
```powershell
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-HttpErrors, IIS-HttpRedirect, IIS-ApplicationDevelopment, IIS-NetFxExtensibility45, IIS-HealthAndDiagnostics, IIS-HttpLogging, IIS-Security, IIS-RequestFiltering, IIS-Performance, IIS-WebServerManagementTools, IIS-ManagementConsole, IIS-IIS6ManagementCompatibility, IIS-Metabase, IIS-ASPNET45
```

```
🖼️ [SCREENSHOT — PowerShell output after this command completes, showing "RestartNeeded/FeatureResult" success]
```

Verify: browse to `http://localhost` on the machine — you should see the default IIS "Welcome" page.

```
🖼️ [SCREENSHOT — the default IIS Welcome page in a browser, confirming IIS is running]
```

## 3.2 Install the .NET 8 Hosting Bundle

1. Run the Hosting Bundle installer downloaded per [§1.4](01-prerequisites.md#14-operating-system--software) (`dotnet-hosting-8.x.x-win.exe`).

```
🖼️ [SCREENSHOT — the Hosting Bundle installer's first screen / license agreement]
```

```
🖼️ [SCREENSHOT — the Hosting Bundle installer's "Install completed" confirmation screen]
```

2. **Restart IIS** afterward so it picks up the new module:
   ```powershell
   net stop was /y
   net start w3svc
   ```
3. Verify the module is installed:
   ```powershell
   Get-WebGlobalModule | Where-Object { $_.Name -like "*AspNetCore*" }
   ```
   You should see `AspNetCoreModuleV2` in the output.

```
🖼️ [SCREENSHOT — PowerShell output of the Get-WebGlobalModule command above, showing AspNetCoreModuleV2 listed]
```

## 3.3 Create the folders

```powershell
New-Item -ItemType Directory -Path "C:\BTS\App" -Force        # application binaries
New-Item -ItemType Directory -Path "D:\BTS\Data" -Force        # SQLite DB + exports/uploads — SEPARATE from the app folder
New-Item -ItemType Directory -Path "D:\BTS\Backups" -Force     # DB backups — also separate
New-Item -ItemType Directory -Path "C:\BTS\App\logs" -Force    # stdout/Serilog logs live with the binaries (fine to lose on redeploy)
```

> Keep `App`, `Data`, and `Backups` as three independent roots, per [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first). Never place `Data` or `Backups` inside `App`.

## 3.4 Create the IIS Application Pool

```powershell
Import-Module WebAdministration
New-WebAppPool -Name "BatteryTestingSystemPool"
Set-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name "managedRuntimeVersion" -Value ""   # "No Managed Code" — ASP.NET Core is self-hosted via the module
Set-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name "startMode" -Value "AlwaysRunning"
```

```
🖼️ [SCREENSHOT — IIS Manager, Application Pools list, showing "BatteryTestingSystemPool" with .NET CLR Version = "No Managed Code"]
```

> ⚠️ Do not stop here — the app pool as created above still has IIS's default 20-minute idle timeout and ~29-hour periodic recycle active. Continue to [§3.9](#39-prevent-iis-from-idling-out-the-application-pool-critical) before going live.

## 3.5 Set folder permissions

The app pool identity needs read/write access to **both** the app folder (logs) and the data folder (DB, exports):
```powershell
icacls "C:\BTS\App" /grant "IIS AppPool\BatteryTestingSystemPool:(OI)(CI)F" /T
icacls "D:\BTS\Data" /grant "IIS AppPool\BatteryTestingSystemPool:(OI)(CI)F" /T
icacls "D:\BTS\Backups" /grant "IIS AppPool\BatteryTestingSystemPool:(OI)(CI)F" /T
```
(Grants Full Control, inherited to subfolders. Re-run if you ever recreate the app pool under a different name.)

## 3.6 Create the IIS Site

```powershell
New-WebSite -Name "BatteryTestingSystem" `
  -Port 80 `
  -PhysicalPath "C:\BTS\App" `
  -ApplicationPool "BatteryTestingSystemPool"
```

```
🖼️ [SCREENSHOT — IIS Manager, Sites list, showing "BatteryTestingSystem" bound to the physical path C:\BTS\App]
```

If the customer wants HTTPS (recommended):
```powershell
New-WebBinding -Name "BatteryTestingSystem" -Protocol https -Port 443 -SslFlags 0
```
Then bind the certificate via IIS Manager: **Site → Bindings → https → Select SSL certificate**.

```
🖼️ [SCREENSHOT — IIS Manager "Site Bindings" dialog with the https binding and certificate selected]
```

If port 80 is already taken by another site, pick an unused port and note the resulting URL (e.g. `http://<server>:8080`) on your on-site record from [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first).

## 3.7 Confirm `web.config` is correct

`C:\BTS\App\web.config` should already exist from the publish output (see [5. Application Deployment](05-application-deployment.md)) and look like:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath=".\BatteryTestingSystem.exe" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```
Don't hand-edit this unless troubleshooting (see [11. Troubleshooting](11-troubleshooting.md)). To debug a startup failure, temporarily set `stdoutLogEnabled="true"`, check `C:\BTS\App\logs\stdout*.log`, then set it back to `false` once resolved.

## 3.8 Start the site and open the firewall

```powershell
Start-WebSite -Name "BatteryTestingSystem"

New-NetFirewallRule -DisplayName "BatteryTestingSystem HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
# Repeat for 443 if using HTTPS, and for any custom port chosen in §3.6
```

Browse to the site from the server itself first, then from another machine on the network to confirm firewall/network access.

```
🖼️ [SCREENSHOT — the app's login page loading successfully in a browser on a second machine]
```

Also open whatever TCP/UDP port(s) the physical test devices use to reach this server, per the customer's hardware setup sheet (hardware-specific — not covered here). **Don't forget the UDP registration port specifically** — it's easy to open the HTTP/TCP ports and forget the UDP one, which is the exact traffic this whole document set exists to protect.

## 3.9 Prevent IIS From Idling Out The Application Pool (Critical)

**This is separate from — and in addition to — the OS-level sleep prevention in [§2](02-power-and-uptime-settings.md).** Even with Windows fully awake, IIS will by default:
- **Stop the worker process after 20 minutes with no HTTP request** (the "Idle Time-out"). Browser/API traffic counts as activity; a background UDP listener sitting quietly waiting for device registrations does **not** count, so on a machine nobody is actively browsing, IIS will shut the whole process down — including that UDP listener — every 20 minutes, by design.
- **Recycle the worker process automatically on a timer** (default: every 1740 minutes / ~29 hours), regardless of activity. A recycle is a full stop-then-restart of the process — anything the app was listening on or tracking in memory (device connections, in-flight test sessions) drops for the few seconds the restart takes, and any UDP packet that lands in that window is lost the same as if the OS had slept.

Since test programs here can run unattended for months, **both of these must be disabled**, not just tuned:

```powershell
# Disable the idle timeout entirely — the app pool (and its background listener) stays running
# even with zero HTTP traffic. Default is 20 minutes; this sets it to 0 (never).
Set-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name "processModel.idleTimeout" -Value ([TimeSpan]::FromMinutes(0))

# Disable periodic (time-based) recycling — default is ~29 hours. Setting this to 0 disables it.
Set-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name "recycling.periodicRestart.time" -Value ([TimeSpan]::FromMinutes(0))

# Clear any specific recycling times that may already be configured (belt-and-suspenders)
Clear-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name "recycling.periodicRestart.schedule"

# Ensure the pool is always running rather than starting lazily on first request (already set in §3.4,
# confirming here since it matters for this section specifically)
Set-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name "startMode" -Value "AlwaysRunning"

# Preload the site so it starts immediately with the app pool (relevant after any restart/reboot)
Set-ItemProperty "IIS:\Sites\BatteryTestingSystem" -Name "applicationDefaults.preloadEnabled" -Value $true
```

**Equivalent via the IIS Manager GUI** if you prefer clicking through it:
1. **Application Pools** → select `BatteryTestingSystemPool` → **Advanced Settings…**
2. Under **Process Model**: set **Idle Time-out (minutes)** to `0`
3. Under **Recycling**: set **Regular Time Interval (minutes)** to `0`, and clear anything under **Specific Times**

```
🖼️ [SCREENSHOT — IIS Manager "Advanced Settings" dialog for the app pool, with "Idle Time-out (minutes)" set to 0 and "Regular Time Interval (minutes)" set to 0, both fields visible in the same shot]
```

**Verify it took effect:**
```powershell
Get-ItemProperty "IIS:\AppPools\BatteryTestingSystemPool" -Name processModel.idleTimeout, recycling.periodicRestart.time
```
Both values should show `00:00:00`.

```
🖼️ [SCREENSHOT — terminal output of the Get-ItemProperty command above, confirming both values are 00:00:00]
```

> **Trade-off note for the customer:** disabling recycling means the process runs indefinitely without IIS's usual "self-healing" restart, which is normally there to paper over slow memory leaks. If the customer's IT is uncomfortable with zero recycling, the safer compromise is a **specific scheduled time** (e.g. 3:00 AM) rather than "never" — but confirm with the customer first whether that low-traffic window still risks dropping an in-progress test or a UDP registration, since for this application there may be no truly "safe" window if hardware runs continuously. Default to fully disabled unless the customer explicitly wants scheduled recycling and accepts the risk.

---
← [2. Power & Uptime Settings](02-power-and-uptime-settings.md) | Next: [5. Application Deployment →](05-application-deployment.md) (or [4. Docker Installation](04-docker-installation.md) if using that path instead)
