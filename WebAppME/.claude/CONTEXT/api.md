# CONTEXT/api.md — API Index
> Updated: 2026-09-04T00:00:00Z
> Base URL: `/api` (ASP.NET Core Web API, Swagger enabled in dev) | Auth: `Authorization: Bearer {token}` (partial — see auth.md)
> Full endpoint detail for any resource lives in `CONTEXT/api/{filename}` — this file only indexes.

---

## Authentication
JWT is issued by `/api/auth/login`, but bearer-token validation is **not confirmed wired** into the middleware pipeline (`AddJwtAuthentication` defined, not observed called in `Program.cs`). `[Authorize]` is enforced via ASP.NET Core Identity's cookie auth where present. See `CONTEXT/auth.md`.

---

## Controllers / Resources
| Resource | File | Base Path | Controller Source |
|----------|------|-----------|-------------------|
| Auth | [api/auth.md](api/auth.md) | `/api/auth` | `Controllers/AuthController.cs` |
| Device | [api/device.md](api/device.md) | `/api/device` | `Controllers/DeviceController.cs` |
| Export | [api/export.md](api/export.md) | `/api/export` | `Controllers/ExportController.cs` |
| MCP Tools | [api/mcp.md](api/mcp.md) | `/mcp` | `MCP/DeviceMcpTools.cs` |

---

## Key Features & Models
- **Bulk Channel Operations (`CommonRequest`)**: `ChannelList` (e.g., `[1, 2, 5, 10]`) is the only channel field now — `ChannelNumber`/`ChannelRange` were removed 2026-09-04; single-channel callers (incl. MCP tools) send a one-element list. See [api/device.md](api/device.md#bulk-channel-operations).
- **DBC File Management**: Full REST & MCP support for DBC files via `POST /api/device/GetDbcFiles` and `IDbcService.GetAllAsync()`.
- **MCP Server**: Stateless HTTP MCP tools under `/mcp` sharing core controller logic with zero duplication. See [api/mcp.md](api/mcp.md).