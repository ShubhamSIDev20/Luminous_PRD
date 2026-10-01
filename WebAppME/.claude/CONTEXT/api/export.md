# Export API
> Updated: 2026-08-10T00:00:00Z
> Controller: `Controllers/ExportController.cs`

---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| POST | `/api/export/request` | Enqueue a Hangfire export job | `[Authorize]` |
| GET | `/api/export/status/{id}` | Get export job status | `[Authorize]` |
| GET | `/api/export/list` | List export records | `[Authorize]` |
| GET | `/api/export/download/{id}` | Download completed export file | `[Authorize]` |
| DELETE | `/api/export/{id}` | Delete export record (optionally the file) | `[Authorize]` |

**POST `/api/export/request` body (`ExportRequestDto`):**
```json
{ "sessionFilePath": "string", "displayName": "string | null", "forceRegenerate": false }
```

**Notes:**
- Async pattern: `request` enqueues via `IBackgroundJobClient` (Hangfire) → `ExportJobService` generates the file (ClosedXML/OpenXML) → poll `status/{id}` → `download/{id}` once ready.
- Backed by `IExportRepository` and the `ExportRecord` entity in `AppDbContext`.
- This is the only controller in the codebase with confirmed `[Authorize]` enforcement — see `.claude/ARCHITECTURE.md` "Known Gaps / Risks".