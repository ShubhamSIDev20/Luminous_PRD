# ADR-33: Production Dockerfile temporarily switched `scratch` → `debian:bookworm-slim`
> Date: 2026-08-20 | Session: #13 | Status: Accepted — ⚠️ hardware-critical, TEMPORARY, developer-approved
> File: `DECISIONS/2026-08-20_debian-bookworm-slim-debug-image.md`

---

**Decision:** `me-primary/Dockerfile`'s final runtime stage is changed from
`FROM scratch` to `FROM debian:bookworm-slim`, no other lines changed. This
restores `docker exec -it me-primary bash` and `apt-get` on the board, using
`bookworm-slim`'s default `main`/`security`/`updates` apt sources (no extra
repos added — none were requested beyond the defaults).

**Reason:** ADR-30 deliberately chose `scratch` for the production image
(smallest size, zero attack surface) and explicitly accepted the loss of
`-it`/`apt-get` as the trade-off — see
`Docs/specs/2026-08-19-core-isolation-cicd-design.md` §2 and §10, which named
this exact scenario: *"if on-device interactive debugging turns out to be
needed often, revisit this trade-off."* Developer asked for `-it`/`apt-get`
back on 2026-08-20 and confirmed (when offered a debug-only alternate build
target instead) to change the production image directly, for now.

**Impact:**
- ✅ `docker exec -it me-primary bash` and `apt-get install ...` now work on
  the board.
- ⚠️ Reverses ADR-30's stated rationale: the shipped image is no longer
  `scratch` — larger image, real (if minimal) attack surface, a real
  filesystem/package manager on-device.
- ⚠️ **Explicitly temporary** — developer said "later we will prepare the
  production docker image." Nothing else in ADR-30 (CPU3 `--core` pin,
  `cpuset: 0-3`, GHCR release workflow) changed.
- Not yet rebuilt/redeployed to the board — this is a source change only,
  same as every other change in this pipeline (no Docker/WSL2 on this
  laptop; CI or the board builds it).

**Related**
- Supersedes: ADR-30's "Runtime image base: `scratch`" line (temporarily —
  T-63 tracks reverting it)
- Relates to: T-63
