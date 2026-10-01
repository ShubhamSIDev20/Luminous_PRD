# T-64: Cut a new GHCR release including the periodic registration retry fix (ADR-34)
> Created: 2026-08-20 | Status: Done
> File: `tasks/2026-08-20_release-ghcr-version-with-registration-retry-fix.md`

---

## Description
ADR-34 (periodic re-registration retry for a circuit still awaiting web-app
approval) is hardware-verified but lives in `develop`/`main` source only.
The published `ghcr.io/quench-ev-charger/me-primary:latest`/`0.0.2` image
does not contain it. Cut a new version (e.g. `0.0.3`) via
`.github/workflows/release.yml` once this work is merged.

## Why / Context
Verified 2026-08-20 by side-loading a freshly built binary directly onto
board `172.16.18.167`, bypassing the production `docker-compose.yml` stack,
specifically to avoid needing a release just to test. That was deliberate —
don't treat the hardware verification as equivalent to "shipped."

## Progress Log
- **2026-08-20**: Task opened alongside ADR-34/session #14.
- **2026-08-21**: Now also covers ADR-39 (SET_VALUES transmission never
  coalesced) and this session's diagnostic-logging tuning, session #16.
- **2026-08-21**: Released `ghcr.io/quench-ev-charger/me-primary:0.0.3` via
  `release.yml` (run 32452548756) from `main` (PR #14 merged). 0.0.3 is now
  the highest published semver, so `:latest` moved to it too. Developer
  explicitly chose to keep the `debian:bookworm-slim` debug base image for
  this release (T-63 deliberately deferred again — "always create debug
  image, once the project work is done I will tell you to create scratch
  first"), so `:latest`/`0.0.3` is still the debug variant, not `scratch`.
  Closed: ADR-34/35/36/37/38/39 are now all in a published image.

## Related
- Depends on: ADR-34 merged to `main`
- Relates to: T-63 (also pending release — the two could ship in the same
  version bump, or T-63 could be resolved separately since it's a Dockerfile
  base-image change with no interaction with this one)
