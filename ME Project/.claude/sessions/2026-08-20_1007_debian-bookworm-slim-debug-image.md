# Session #13: Debug image — switch Dockerfile to debian:bookworm-slim
> Date: 2026-08-20T10:07+05:30 | Agent: Claude Code (Sonnet 5) | Status: ✅ Complete (source change only)
> Prev session: [sessions/2026-08-19_1512_secondary1-multi-channel-support.md](2026-08-19_1512_secondary1-multi-channel-support.md) — Secondary 1 multi-channel support, ADR-32.

---

## 🎯 Goal
Developer reported the `me-primary` Docker image doesn't support `docker exec -it` or `apt-get`, and asked to add apt-get support with a repository.

## ✅ Done
- Found the cause: `me-primary/Dockerfile`'s final stage is `FROM scratch`
  (ADR-30), deliberately chosen with `-it`/`apt-get` named as the accepted
  trade-off — the design doc itself flagged this as worth revisiting later.
- Flagged as ⚠️ hardware-critical (this file is covered by ADR-30) and asked
  the developer to confirm the approach before editing, per project rule.
- Developer chose: switch the production image itself to
  `debian:bookworm-slim` (not a separate debug-only build target), for now —
  "later we will prepare the production docker image." Default Debian
  bookworm apt repos only (no contrib/non-free needed).
- Edited `me-primary/Dockerfile`: final stage `FROM scratch` →
  `FROM debian:bookworm-slim`, with a dated comment noting this is temporary
  and pointing at ADR-30/ADR-33. No other lines changed — builder stage,
  binary list, `ENTRYPOINT` untouched.
- Logged **ADR-33** (temporary reversal of ADR-30's runtime-image-base
  choice) and opened **T-63** (revert to `scratch` before the next real
  production release).

## Verification
- Not build/deploy tested — no Docker/WSL2 on this Windows laptop
  (project-standing constraint). This Dockerfile is built by CI/board only;
  next GHCR release build or a manual `docker buildx build` on/for the board
  is what will actually exercise it.
- `docker-compose.yml` was not touched — same image reference, same
  `cpuset`/`network_mode`/command args.

## Files Changed
- `me-primary/Dockerfile` (final stage base image)
- `.claude/DECISIONS/2026-08-20_debian-bookworm-slim-debug-image.md` (new, ADR-33)
- `.claude/DECISIONS.md` (index row added)
- `.claude/tasks/2026-08-20_revert-debug-image-to-scratch-before-release.md` (new, T-63)
- `.claude/TASKS.md` (index row added)
- `.claude/AGENT.md`, `.claude/INDEX.md` (live state)

## Next Agent Should Do
- Nothing blocking. T-63 is backlog: before the next GHCR release, decide
  whether to revert to `scratch` or keep a debug image under a separate tag/
  build target.
- If the developer reports the image still lacks a package after this
  change, check whether they need `contrib`/`non-free` components added to
  `/etc/apt/sources.list.d/debian.sources` (declined for now, default-only).
