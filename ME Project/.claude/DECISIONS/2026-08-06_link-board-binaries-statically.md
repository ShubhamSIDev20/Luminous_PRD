# ADR-2: Link board binaries statically
> Date: 2026-08-06 | Session: #1 | Status: Accepted
> File: `DECISIONS/2026-08-06_link-board-binaries-statically.md`

---

**Context:** The binary is cross-compiled on Windows against the Arm toolchain's
bundled sysroot glibc, but executes inside a Docker container on the board whose
base image ships an unrelated glibc version.

**Decision:** Build board binaries with `-static` (set in `build.ps1`).

**Reason:** A dynamically-linked binary would depend on the exact glibc present in
the runtime container, risking `GLIBC_2.XX not found` at launch and coupling the
build to a specific base image. Static linking makes the artifact runnable in any
`linux/arm64` container unmodified.

**Impact:**
- ✅ Container base image can change freely without rebuilding concerns
- ✅ `-DockerImage` in `deploy.ps1` is a free parameter
- ⚠️ Larger binaries (~700 KB vs ~20 KB) — irrelevant at current scale, revisit only
  if the ME project ships many binaries or hits image-size limits
- ⚠️ Static glibc + `dlopen`/NSS (DNS, user lookup) can misbehave; if the ME project
  needs those, reconsider dynamic linking against a pinned base image

**Related**
- Supersedes: none
- Relates to: ADR-8
