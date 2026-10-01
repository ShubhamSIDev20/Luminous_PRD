# ADR-30: CPU3 core isolation + production Dockerfile/Compose/CI release
> Date: 2026-08-19 | Session: #11 | Status: Accepted — ⚠️ hardware-critical, developer-approved
> File: `DECISIONS/2026-08-19_cpu3-core-isolation-cicd-release.md`

---

**Decision:** Dedicate CPU3 to the Core Logic thread via a `--core` CLI
flag (default 3) and `pthread_attr_setaffinity_np()` in `core_logic.c`,
with a runtime self-check that WARNs if the kernel hasn't isolated that
CPU (`isolcpus`). Replaced the old `deploy.bat`→`docker exec` path into a
pre-existing `qflex-backend` container with a purpose-built `Dockerfile`
(scratch, ARM64 cross-build), `docker-compose.yml` (`cpuset: 0-3`), and a
manually-triggered (`workflow_dispatch` only) GitHub Actions release to
GHCR, hardened with a main-only branch guard, strict semver validation,
and a `:latest` tag that never moves backward.

**Reason:** `deploy.bat`'s target container is owned by another team and
its cgroup/capabilities are fixed at creation time — it can't be handed
CPU isolation. Kernel-level isolation (`isolcpus`/`nohz_full`/`rcu_nocbs`)
can't live in a Dockerfile/Compose file/CI at all — it's a one-time board
provisioning step (`Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md`).

**Impact:**
- ✅ Hardware-verified 2026-08-19: the pin works even through the legacy
  `qflex-backend` path (`running on CPU3`).
- ⚠️ Kernel isolation itself is NOT yet applied on the test board
  (`172.16.18.167`) — the runbook still needs a one-time run there (T-60).
  Until then the pin is real but not exclusive.

**Related**
- Supersedes: none (replaces the old `deploy.bat`→`qflex-backend` path)
- Relates to: T-58, T-60
