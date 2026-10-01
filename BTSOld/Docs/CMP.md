# Configuration Management Plan (CMP)
**Document ID:** BTS-CMP-001  **Version:** 0.5  **Date:** May 2026  **Status:** Approved

---

## 1. Configuration Items

| CI ID | Item | Location | Tool |
|---|---|---|---|
| CI-001 | Source code | GitHub: sysin1/BatteryTestingSystem | Git |
| CI-002 | Docker image | ghcr.io/sysin1/battery-testing-system | ghcr.io |
| CI-003 | Release ZIP | GitHub Releases (v0.5.x) | GitHub |
| CI-004 | Database migrations | /Migrations/ | EF Core |
| CI-005 | CI/CD workflow | .github/workflows/dotnet-ci-cd.yml | GitHub Actions |
| CI-006 | Docker compose | docker-compose.yml | Docker |
| CI-007 | App configuration | appsettings.json | .NET Config |
| CI-008 | Documentation | /Docs/ | Markdown |

---

## 2. Branching Strategy

```
main ─────────────────────────────────────────▶  (production)
  │
  └── feature/xxx ──▶ PR ──▶ merge to main
```

- All production deployments from `main` branch
- CI/CD triggers on push to `main`
- Feature branches for new development work

---

## 3. Versioning Scheme

**Format:** `v{Major}.{Minor}.{BuildNumber}`

| Segment | Meaning | Example |
|---|---|---|
| Major | Breaking API/protocol changes | v**1**.0.0 |
| Minor | Feature additions | v0.**6**.0 |
| BuildNumber | Auto-incremented GitHub Actions run number | v0.5.**14** |

---

## 4. Release Process

```
1. Developer pushes to main branch
2. GitHub Actions (ubuntu-latest):
   a. dotnet restore
   b. dotnet build -c Release
   c. dotnet publish -c Release -o publish
   d. zip -r BatteryTestingSystem-release.zip publish/
   e. Create GitHub Release (v0.5.{run_number}) + ZIP attachment
   f. Build Docker image
   g. Push to ghcr.io/sysin1/battery-testing-system:latest
3. Production: docker compose pull && docker compose up -d
```

---

## 5. Change Control

| Change Type | Approval Required | Process |
|---|---|---|
| Bug fix | Developer self-approval | Commit to main |
| Feature addition | Peer review | Feature branch → PR → merge |
| Protocol change | Architect approval | PR + PROTOCOL.md update |
| DB migration | Architect approval | PR + migration test on clean DB |
| Breaking change | Full team review | Major version bump |

---

## 6. Backup & Recovery

| Item | Method | Frequency |
|---|---|---|
| Source code | GitHub distributed VCS | Every push |
| Main SQLite DB | Host volume backup (./bts-data/) | Daily |
| Session SQLite DBs | Host volume backup | Daily |
| Docker image | Immutable tags on ghcr.io | Per release |
| Documentation | Git history | Every push |

---

## 7. Audit Trail

All significant state changes in the running application are captured in:
- `AuditLogs` database table (application-level)
- Serilog rolling log files (system-level): `logs/btsservice-{date}.log`
- Git commit history (code-level)
- GitHub Actions run history (build/deploy-level)
