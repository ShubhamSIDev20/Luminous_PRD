# T-58: CPU3 core isolation + production Dockerfile/docker-compose.yml/GitHub Actions GHCR release
> Created: 2026-08-19 | Status: Done (2026-08-19, Session #11)
> File: `tasks/2026-08-19_cpu3-core-isolation-cicd-release.md`

---

## Description
`--core`, `pthread_attr_setaffinity_np()` + production `Dockerfile`/
`docker-compose.yml`/GitHub Actions GHCR release, hardened. ADR-30. PR #6
merged.

## Progress Log
- **2026-08-19** (Session #11): Hardware-verified — pin works, kernel
  isolcpus still pending (T-60).

## Related
- Relates to: ADR-30, T-60
