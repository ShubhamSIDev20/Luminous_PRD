# Auth System
> Updated: 2026-08-10T00:00:00Z

## Flow
1. `POST /api/auth/login` with `{ email, password }` → `AuthController.Login`
2. Validated via `UserManager<ApplicationUser>` / `SignInManager<ApplicationUser>` (ASP.NET Core Identity)
3. On success, `AuthController.GenerateToken` builds a JWT from `JwtSettings` (issuer, audience, expiry)
4. Client is expected to send `Authorization: Bearer {token}` on subsequent requests
5. **Gap:** `AddJwtAuthentication` (bearer-token validation middleware) is defined in the codebase but was not observed being called in `Program.cs` — so JWT validation on protected endpoints is unconfirmed. Verify before assuming bearer auth is enforced anywhere.
6. Separately, ASP.NET Core Identity cookie/session auth is wired in `Program.cs` and backs `[Authorize]` attributes (e.g. on `ExportController`)

## Tokens
| Token | Expiry | Config Source |
|-------|--------|----------------|
| Access token | `JwtSettings:AccessTokenExpirationMinutes` (60 by default) | `appsettings.json` |
| Refresh token | `JwtSettings:RefreshTokenExpirationDays` (30 by default) | `appsettings.json` |
| ID token | `JwtSettings:IdTokenExpirationMinutes` (60 by default) | `appsettings.json` |
| Clock skew | `JwtSettings:ClockSkewMinutes` (5 by default) | `appsettings.json` |

`JwtSettings:Issuer` / `Audience` are also configured in `appsettings.json` — do not hardcode these; read from config.

## Identity Model
- `ApplicationUser` — extends `IdentityUser` (see `Models/Entities/ApplicationUser.cs`)
- `ApplicationRole` — extends `IdentityRole` (see `Models/Entities/ApplicationRole.cs`)
- `AppDbContext` inherits `IdentityDbContext<ApplicationUser, ApplicationRole, string>`
- Default authorization/registration seeding happens at startup via `InitializeDataSeeder` and `ServiceLocator`

## Authorization Coverage (as scanned)
| Controller | `[Authorize]`? |
|------------|-----------------|
| `AuthController` | `/login` is `[AllowAnonymous]` (expected) |
| `ExportController` | ✅ Yes |
| `DeviceController` | ❌ No — known gap, `.claude/TASKS.md` T-2 |

## Key Files
| File | Role |
|------|------|
| `Controllers/AuthController.cs` | Login endpoint + JWT generation |
| `Models/Entities/ApplicationUser.cs` | Identity user entity |
| `Models/Entities/ApplicationRole.cs` | Identity role entity |
| `Data/AppDbContext.cs` | `IdentityDbContext` — Identity table configuration |
| `appsettings.json` (`JwtSettings` section) | JWT issuer/audience/expiry config |

## Secrets Handling
`appsettings.json` currently contains a DB encryption key and password under `AppSettings`. These are secret-shaped — never copy their actual values into documentation or commits; treat as sensitive even in development, and prefer environment variables / user secrets for production.