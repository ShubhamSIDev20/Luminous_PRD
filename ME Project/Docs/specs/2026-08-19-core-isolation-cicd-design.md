# Dedicated CPU Core for Core Logic + Production CI/CD (design)

| | |
|---|---|
| Date | 2026-08-19 |
| Status | Design — awaiting hardware-in-the-loop verification |
| Source research | `Downloads/imx8mp-core-isolation-docker-guide.md` (developer-authored) |
| Touches | `me-primary/src/threads/core_logic.c`, `me-primary/src/main.c`, `me-primary/src/sys_init.h`, new `me-primary/Dockerfile`, new `me-primary/docker-compose.yml`, new `.github/workflows/release.yml`, new board-provisioning runbook |

⚠ **HARDWARE-CRITICAL — human review required.** This design changes
real-time thread scheduling / core assignment for the Core Logic thread. It
must not be implemented without explicit developer approval of this document,
and the affinity behavior itself can only be confirmed on real hardware — no
part of it is provable from a host build or CI.

---

## 1. Problem

By default all four `me_primary` threads (Communication, Core Logic, CAN Data
Manager, Data Manager) share the iMX8MP's four Cortex-A53 cores as one pool.
The Linux CFS scheduler can migrate Core Logic between cores at any time and
interrupt it for IRQs, timer ticks, and RCU callbacks — undermining the timing
determinism the step-execution engine needs.

Separately, `me_primary`'s only production deployment path today
(`deploy.bat`) works by `docker cp`-ing the binary into an **already-running**
container (`qflex-backend`) owned by another team's compose setup and
`docker exec`-ing it. That path cannot grant new Docker capabilities/limits to
`qflex-backend` (those are fixed at container-creation time), and it isn't a
repeatable, versioned release process. This design replaces it with a
purpose-built image, Compose file, and a manually-triggered GitHub Actions
release pipeline — folding in the CPU-isolation work, since the Compose file's
`cpuset` and the code's pinned core index are the same decision.

## 2. Decisions made (with the developer, 2026-08-19)

| Decision | Choice | Why |
|---|---|---|
| Runtime image base | `scratch` | `me_primary` is fully static (`-static -pthread`, see project ADR) with zero runtime deps. Smallest image, no attack surface. Trade-off accepted: `docker exec -it <ctr> bash` no longer works for on-device debugging — diagnosis is log-based going forward. |
| Registry | GitHub Container Registry (`ghcr.io`) | Private repo (`Quench-EV-Charger/ME_PRD`) → image inherits private visibility automatically. Auth is the built-in `GITHUB_TOKEN`, no new secret. |
| Release workflow scope | Build + tag + push only | `workflow_dispatch` with a manual `version` input, **never** on push/commit (explicit developer instruction). Getting a new image onto the board stays a separate manual `docker compose pull && up -d` step — same trust boundary as today's manual deploys. |
| Thread scheduling | CPU affinity only, no `SCHED_FIFO` / `mlockall` | Pinning alone gets the main benefit (no migration, no core-sharing) without the failure mode of a hung `SCHED_FIFO` thread wedging an isolated (`isolcpus`+`nohz_full`) core with nothing able to preempt it. `core_logic`'s loop isn't watchdog-supervised today, so this keeps the blast radius of a future bug in that path bounded to "runs on a busy core," not "that core is gone until the process is killed." |
| CI cross-compiler | Debian's `crossbuild-essential-arm64` (`aarch64-linux-gnu-gcc`) | **Developer-accepted trade-off**, not the recommended option. This is a *different* toolchain than the pinned `aarch64-none-linux-gnu` 15.2.rel1 used by `build.ps1` for the board target — the project has hit toolchain-divergence bugs before (MinGW vs. Arm GNU Toolchain disagreeing on an implicit `NULL`, 2026-08-11). Chosen anyway for CI simplicity (one `apt-get` line, no pinned tarball URL to maintain). If a `-Werror` pass/fail ever disagrees between `build.ps1` and this Dockerfile, that divergence is the first thing to suspect. |
| Isolated core | CPU3 (last of 4) | Matches the developer's research doc exactly. Maps cleanly onto the existing 4-thread architecture: Core Logic gets CPU3 to itself; Communication, CAN Data Manager, and Data Manager share CPU0–2 along with the kernel/Docker daemon overhead. |

## 3. Out of scope (flagged, not built)

- Auto-deploying to the board from CI (would need a self-hosted runner or a
  reachable jump host into the board's private network — not requested).
- `SCHED_FIFO` / real-time priority / `mlockall()` (see decision above).
- Any change to the `qflex-backend` container or whoever manages it.
- Multi-board / multi-core-count support — this targets exactly the iMX8MP
  quad-core Verdin, CPU3 isolated, as one physical layout.

---

## 4. Component 1 — Application-level thread pinning

**Files:** `me-primary/src/sys_init.h`, `me-primary/src/main.c`,
`me-primary/src/threads/core_logic.c`

Add one field to the existing config struct, following the same pattern as
`secondary`/`channel`:

```c
/* sys_init.h */
typedef struct {
    ...
    uint8_t        channel;
    int            core_affinity;   /* CPU index for the Core Logic thread; -1 = don't pin */
    ...
} me_config_t;
```

New CLI option in `main.c`, parsed alongside `--secondary`/`--channel`:

```
--core <n>          Dedicated CPU index for Core Logic   (default 3, -1 disables)
```

`me_config_defaults()` sets `core_affinity = 3`.

`me_core_logic_start()` already receives `const me_system_t *sys`, which
carries `sys->cfg`, so no function signature changes are needed — it reads
`sys->cfg.core_affinity` directly:

```c
bool me_core_logic_start(const me_system_t *sys)
{
    s_sys = sys;
    memset(s_cl, 0, sizeof(s_cl));

    pthread_attr_t attr;
    pthread_attr_init(&attr);

    const int core = sys->cfg.core_affinity;
    if (core >= 0) {
        cpu_set_t cpuset;
        CPU_ZERO(&cpuset);
        CPU_SET(core, &cpuset);
        const int arc = pthread_attr_setaffinity_np(&attr, sizeof(cpuset), &cpuset);
        if (arc != 0) {
            ME_LOGE("core logic thread: setaffinity to CPU%d failed (%d) - "
                     "thread will run unpinned", core, arc);
        }
    }

    const int rc = pthread_create(&s_thread, &attr, core_logic_main, NULL);
    pthread_attr_destroy(&attr);
    if (rc != 0) {
        ME_LOGE("core logic thread: pthread_create failed (%d)", rc);
        return false;
    }
    s_started = true;
    return true;
}
```

A failed `setaffinity` is logged as an error but is **not** fatal to startup —
this is a determinism improvement, not a correctness dependency, so the app
should keep running (unpinned) rather than refuse to start.

**New: isolation self-check.** At the top of `core_logic_main()`, before the
main loop, read `/sys/devices/system/cpu/isolated` and log a **warning** if
the configured core isn't listed there:

```c
static void warn_if_core_not_isolated(int core)
{
    FILE *f = fopen("/sys/devices/system/cpu/isolated", "r");
    if (!f) { return; /* file absent on some kernels - not fatal */ }
    char buf[64] = {0};
    (void)fgets(buf, sizeof(buf), f);
    fclose(f);
    /* buf is a cpu list like "3" or "2-3"; a full parser is unnecessary -
     * this is a best-effort operator warning, not a safety gate. */
    char needle[8];
    snprintf(needle, sizeof(needle), "%d", core);
    if (strstr(buf, needle) == NULL) {
        ME_LOGW("CPU%d is pinned but NOT isolated by the kernel "
                 "(isolcpus missing?) - determinism is not guaranteed", core);
    }
}
```

This turns "board was never provisioned per the runbook" from a silent loss
of the isolation guarantee into a loud log line at every startup.

Also log the confirmed landing core right after thread start
(`sched_getcpu()` from inside `core_logic_main`) so a run's log shows exactly
where the thread executed, matching the verification pattern in the
developer's research doc.

---

## 5. Component 2 — Host-level board provisioning (runbook, not code)

New doc: `Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md`.
One-time, per physical board, done directly over SSH — this is kernel boot
configuration and cannot live in a Dockerfile, Compose file, or CI:

1. `sudo fw_setenv bootargs "$(sudo fw_printenv -n bootargs) isolcpus=3 nohz_full=3 rcu_nocbs=3 irqaffinity=0,1,2"`
2. `sudo reboot`
3. Verify: `cat /proc/cmdline | grep isolcpus`, `cat /sys/devices/system/cpu/isolated` (expect `3`).
4. Install `redirect_irqs.sh` + `redirect-irqs.service` (systemd, `WantedBy=multi-user.target`) exactly as in the developer's research doc, so late-registering driver IRQs don't creep back onto CPU3.
5. One-time `docker login ghcr.io` on the board (PAT with `read:packages`), since the image is private.

Survives OTA updates because `fw_setenv` writes to the U-Boot environment
partition, not the OSTree-managed rootfs.

---

## 6. Component 3 — Production Dockerfile

New file: `me-primary/Dockerfile`

```dockerfile
# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM debian:bookworm AS builder
RUN apt-get update && \
    apt-get install -y --no-install-recommends crossbuild-essential-arm64 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /build
COPY src ./src

# Mirrors build.ps1's flags and source list exactly - keep both in sync by
# hand; this is deliberately not templated to avoid pulling in test sources.
RUN aarch64-linux-gnu-gcc -std=gnu11 -O2 -Wall -Wextra -Werror \
      -Isrc -static -pthread -o /build/me_primary \
      src/main.c \
      src/sys_init.c \
      src/app_queues.c \
      src/msg.c \
      src/threads/comm_thread.c \
      src/threads/core_logic.c \
      src/threads/data_mgr.c \
      src/threads/can_mgr.c \
      src/threads/demo_realtime.c \
      src/store/circuit_store.c \
      src/store/circuit_registry.c \
      src/proto/crc16.c \
      src/proto/reg_frame.c \
      src/proto/program_chain.c \
      src/proto/frame_router.c \
      src/proto/ack_frame.c \
      src/proto/program_frame.c \
      src/proto/control_frame.c \
      src/proto/battery_frame.c \
      src/proto/realtime_frame.c \
      src/net/tcp_client.c \
      src/net/udp_sock.c \
      src/platform/netinfo.c \
      src/util/log.c \
      src/util/msgq.c

FROM scratch
COPY --from=builder /build/me_primary /me_primary
ENTRYPOINT ["/me_primary"]
```

Built with `docker buildx build --platform linux/arm64 -t <image>:<tag> .` —
no QEMU needed: the builder stage cross-compiles natively on the `amd64`
runner, and `scratch` has no platform-specific content to emulate.

## 7. Component 4 — `docker-compose.yml`

New file: `me-primary/docker-compose.yml`, run on the board:

```yaml
services:
  me-primary:
    image: ghcr.io/quench-ev-charger/me-primary:${ME_PRIMARY_VERSION:-latest}
    container_name: me-primary
    network_mode: host   # required: registration frame carries this IP/MAC as payload
    cpuset: "0-3"         # must include CPU3 or pthread_attr_setaffinity_np returns EINVAL
    restart: unless-stopped
    command:
      - "--server"
      - "${ME_SERVER_IP}"
      - "--core"
      - "3"
      - "--device"
      - "1"
      - "--secondary"
      - "1"
      - "--channel"
      - "1"
```

`ME_PRIMARY_VERSION` and `ME_SERVER_IP` come from a board-local `.env` file
(not committed — board- and deployment-specific).

## 8. Component 5 — GitHub Actions release workflow

New file: `.github/workflows/release.yml`

```yaml
name: Release me-primary image

on:
  workflow_dispatch:
    inputs:
      version:
        description: "Version to tag (e.g. 1.2.3)"
        required: true
        type: string

permissions:
  contents: read
  packages: write

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: docker/setup-buildx-action@v3

      - uses: docker/login-action@v3
        with:
          registry: ghcr.io
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - uses: docker/build-push-action@v6
        with:
          context: ./me-primary
          platforms: linux/arm64
          push: true
          tags: |
            ghcr.io/quench-ev-charger/me-primary:${{ inputs.version }}
            ghcr.io/quench-ev-charger/me-primary:latest
```

Never triggers on push/commit, per explicit instruction — `workflow_dispatch`
only, run manually from the Actions tab with a version string.

---

## 9. Verification plan

| Layer | How | What it proves |
|---|---|---|
| Host unit tests | `.\build-native.ps1` | Unaffected — no threading/scheduling logic lives under `src/proto/` or `src/util/`. Expect the existing 220 checks green. |
| Cross-build | `.\build.ps1` | Still compiles clean under `-Werror` with the new `core_affinity` field and CLI option. |
| CI image build | Manually trigger `release.yml` with a test version | Image builds and pushes to `ghcr.io` successfully; **does not** prove affinity behavior. |
| **Hardware-in-the-loop** | On the provisioned board: `taskset -p <tid-of-core-logic>` (expect mask `8`), `mpstat -P ALL 1` (CPU3 idle except Core Logic's own work), check the new startup log line for the isolation self-check warning | **The only proof the isolation actually works.** ⚠ This suggestion requires hardware-in-the-loop testing to verify — nothing above it can confirm real scheduling behavior. |

## 10. Risks / open items

- **Toolchain divergence (accepted risk, see §2):** CI's `aarch64-linux-gnu-gcc`
  vs. local `aarch64-none-linux-gnu-gcc` could disagree on a warning under
  `-Werror`. If CI ever fails to build something `build.ps1` builds fine (or
  vice versa), check the compiler/version difference first before assuming a
  code bug.
- **Debugging without a shell:** the `scratch` image means `docker exec -it
  me-primary bash` no longer works on the board. Diagnosis moves to
  `docker logs me-primary` and the isolation self-check warning. If on-device
  interactive debugging turns out to be needed often, revisit this trade-off.
- **Board provisioning is manual and per-board:** a newly imaged or replaced
  board that skips the runbook will run correctly (thread still gets pinned)
  but silently loses the determinism guarantee, until the new self-check
  warning is read in the log.
- **`cpuset: "0-3"` must never be narrowed** by a future Compose edit done for
  unrelated reasons (e.g., someone capping CPU usage) — that would make
  `pthread_attr_setaffinity_np` fail `EINVAL` for CPU3. Worth a comment in the
  Compose file itself.

## 11. File manifest

| File | Change |
|---|---|
| `me-primary/src/sys_init.h` | add `int core_affinity` to `me_config_t` |
| `me-primary/src/main.c` | add `--core <n>` CLI option, default in `me_config_defaults()` |
| `me-primary/src/threads/core_logic.c` | pin thread via `pthread_attr_setaffinity_np`; add isolation self-check warning |
| `me-primary/Dockerfile` | new |
| `me-primary/docker-compose.yml` | new |
| `.github/workflows/release.yml` | new |
| `Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md` | new |
