# T-63: Revert `me-primary/Dockerfile` to `scratch` before the next production release
> Created: 2026-08-20 | Status: Backlog
> File: `tasks/2026-08-20_revert-debug-image-to-scratch-before-release.md`

---

## Description
`me-primary/Dockerfile`'s final stage is temporarily `debian:bookworm-slim`
(ADR-33) so `docker exec -it` and `apt-get` work for on-device debugging.
Before the "production docker image" the developer mentioned preparing later
is actually released via `.github/workflows/release.yml`, decide whether to
revert to `FROM scratch` (ADR-30's original choice) or keep the debug image
as `latest` and add a separate minimal target for releases.

## Why / Context
ADR-30 chose `scratch` specifically for minimal size / zero attack surface in
the GHCR-released production image. ADR-33 reverses that for debugging
convenience and is explicitly marked temporary by the developer ("later we
will prepare the production docker image").

## Progress Log
- **2026-08-20**: Task opened alongside ADR-33.
- **2026-08-20**: PR #9 (ADR-33) and PR #10 (fixed pre-existing Dockerfile
  source-list drift — `src/util/channel_list.c` was missing, unrelated to
  ADR-33, broke the release build) merged to `main`. Developer then triggered
  `release.yml` for `v0.0.2`, which **succeeded and moved `:latest`** —
  `ghcr.io/quench-ev-charger/me-primary:latest` now points at the
  `debian:bookworm-slim` debug image, not a `scratch` image. This raises the
  priority: `:latest` in production is currently the debug variant.
- **2026-08-21**: Asked explicitly before cutting 0.0.3 (T-64) whether to
  revert to `scratch` first. Developer's answer: keep the debug image for
  now — "always create debug image, once the project work is done I will
  tell you to create scratch first." `0.0.3`/`:latest` is therefore also the
  debug variant. This is a deliberate, standing decision for the ongoing
  hardware-verification phase, not an oversight — don't re-raise it as a
  blocker on future releases; wait for the developer to say the project work
  is done.

## Related
- Depends on: none
- Relates to: ADR-30, ADR-33
