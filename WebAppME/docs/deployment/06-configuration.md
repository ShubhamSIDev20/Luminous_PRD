# 6. Post-Installation Configuration

← [5. Application Deployment](05-application-deployment.md) | Next: [7. Database Setup →](07-database-setup.md)

---

Location: `C:\BTS\App\appsettings.json` (IIS) or passed as environment variables into the container (Docker).

**Never leave the values shipped in a dev/sample `appsettings.json` in production.** Review/replace these before go-live:

| Key | Purpose | Action at install time |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite DB file path | Must point at the **separate data folder** — `D:\BTS\Data\BtsAppdb.db` — never inside `C:\BTS\App`. See [§1.5](01-prerequisites.md#folder-layout-decision-read-this-first). |
| `AppSettings:Data` | Root folder for app-managed files (exports, uploads) | `D:\BTS\Data` — same separation rule applies |
| `AppSettings:DbEncryption`, `AppSettings:Password` | Database-level encryption key/password (SQLCipher) | **Generate new, unique, random values for this customer.** Never reuse a value seen in source control or another customer's install. Store in the customer's password manager, not in a ticket or this document. |
| `JwtSettings:Issuer`, `JwtSettings:Audience` | Token issuer/audience URLs | Set to the customer's actual hostname/domain, not a placeholder like `yourdomain.com` |
| `Cors:AllowedOrigins` | Which origins may call the API | Replace `localhost`/placeholder entries with the real operator-facing URL(s); remove dev entries |

```
🖼️ [SCREENSHOT — appsettings.json open in a text editor with ConnectionStrings and AppSettings:Data highlighted, showing the D:\BTS\Data path]
```

## Preferred method: environment variables, not the JSON file

Overriding secrets via environment variables (rather than editing the JSON file directly) avoids plaintext secrets sitting in a file that might get copied into a support bundle or screenshot.

- **IIS:** set via the Application Pool / Site environment variables, or in `web.config`'s `<environmentVariables>` block under `<aspNetCore>`:
  ```xml
  <aspNetCore processPath=".\BatteryTestingSystem.exe" ... >
    <environmentVariables>
      <environmentVariable name="ConnectionStrings__DefaultConnection" value="Data Source=D:\BTS\Data\BtsAppdb.db" />
      <environmentVariable name="AppSettings__Data" value="D:\BTS\Data" />
    </environmentVariables>
  </aspNetCore>
  ```
- **Docker:** `-e` flags, using double-underscore for nested keys — see [§4.4](04-docker-installation.md#44-run-the-container)

```
🖼️ [SCREENSHOT — IIS Manager, Application settings / environment variables editor for the site]
```

## After any config change

Restart the app — ASP.NET Core reads most config only at startup:
```powershell
Restart-WebSite -Name "BatteryTestingSystem"     # IIS
# or
docker restart batterytestingsystem              # Docker
```

> Restarting the app pool is exactly the kind of event [§3.9](03-iis-installation.md#39-prevent-iis-from-idling-out-the-application-pool-critical) warns about — a config change is a legitimate reason to restart, but confirm you're not doing it during an active test run if avoidable.

---
← [5. Application Deployment](05-application-deployment.md) | Next: [7. Database Setup →](07-database-setup.md)
