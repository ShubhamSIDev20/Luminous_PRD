# T-3: Decide whether the ME app ships as a Docker image rather than a bare binary
> Created: 2026-08-06 | Status: Backlog (🟡 Med)
> File: `tasks/2026-08-06_decide-docker-image-vs-bare-binary.md`

---

## Description
Current flow scp's a static binary and bind-mounts it. Note ADR-1: images
cannot be built on the laptop.

## Progress Log
- **2026-08-06** (Session #1): Task opened during toolchain decisions.
- **2026-08-19** (Session #11): Effectively superseded by T-17/T-58's
  production Dockerfile + docker-compose.yml + CI release pipeline (ADR-30).

## Related
- Relates to: ADR-1, T-17
