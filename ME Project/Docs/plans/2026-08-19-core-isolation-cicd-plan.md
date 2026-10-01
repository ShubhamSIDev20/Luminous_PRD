# Core Isolation + Production CI/CD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dedicate CPU3 to the Core Logic thread via affinity-only pinning, and replace the ad-hoc `qflex-backend` docker-exec deploy path with a versioned Dockerfile + Compose + manually-triggered GitHub Actions release pipeline.

**Architecture:** A new `--core` CLI option (default 3) flows through `me_config_t` into `me_core_logic_start()`, which pins the thread with `pthread_attr_setaffinity_np()` before creation and logs a warning if the kernel hasn't actually isolated that core. Separately, a multi-stage Dockerfile cross-compiles the existing source tree to a `scratch`-based image, a Compose file runs it with the cpuset the pinning needs, and a `workflow_dispatch`-only GitHub Actions workflow builds and pushes that image to GHCR.

**Tech Stack:** C (gnu11, glibc pthread/sched extensions), Docker multi-stage build (`crossbuild-essential-arm64`), Docker Compose, GitHub Actions + GHCR.

**Spec:** `ME Project/Docs/specs/2026-08-19-core-isolation-cicd-design.md`

## Global Constraints

- Binaries are statically linked (`-static -pthread`) — do not introduce any dynamic dependency (project ADR, `ME Project/CLAUDE.md`).
- Build flags on both host and CI: `-std=gnu11 -O2 -Wall -Wextra -Werror` (`ME Project/CLAUDE.md`).
- No hostname resolution (`getaddrinfo`) — server addresses are dotted-quad only (`ME Project/CLAUDE.md`).
- `--network`/`network_mode: host` is required wherever `me_primary` runs — the registration frame carries the board's real IP/MAC as payload (`ME Project/CLAUDE.md`, spec §7).
- **Standing rule:** any change under `me-primary/src/` (or `tests/`) must be verified by running **both** `.\build.ps1` and `.\build-native.ps1`, reporting the native check count. Cross-build compiling clean is never "tested" — only `.\deploy.ps1` against real hardware proves runtime behavior (`ME Project/CLAUDE.md`).
- No Docker Desktop/WSL2 on the Windows dev laptop — Docker only runs on the board and in CI (`ME Project/CLAUDE.md`). Dockerfile/Compose/workflow changes cannot be build-tested locally; verification for those is YAML/syntax-level plus manual review, with real proof deferred to a manually-triggered CI run.
- `docker-compose.yml`'s `cpuset` must always include CPU3, or `pthread_attr_setaffinity_np` fails `EINVAL` (spec §7, §10).
- The release workflow triggers **only** on `workflow_dispatch` — never on push/commit (explicit developer instruction, spec §2).
- ⚠ **Hardware-critical:** Task 2 changes real-time thread scheduling. Present the diff for explicit developer approval before committing, per `ME Project/CLAUDE.md`'s "Hardware-Critical Code" rule.

---

### Task 1: `--core` CLI option and config field

**Files:**
- Modify: `ME Project/me-primary/src/sys_init.h`
- Modify: `ME Project/me-primary/src/sys_init.c`
- Modify: `ME Project/me-primary/src/main.c`

**Interfaces:**
- Produces: `me_config_t.core_affinity` (`int`, default `3`, `-1` disables pinning) — Task 2 reads this as `sys->cfg.core_affinity`.

- [ ] **Step 1: Add the field to `me_config_t`**

Edit `ME Project/me-primary/src/sys_init.h`:

```c
typedef struct {
    char           server_ip[16];
    uint16_t       server_port;
    char           iface[ME_IFNAME_MAX];
    uint8_t        device_id;
    uint8_t        secondary;
    uint8_t        channel;
    char           device_name[ME_REG_NAME_LEN + 1];
    me_crc_order_t crc_order;
    int            connect_timeout_ms;
    int            response_timeout_ms;
    /* CPU index the Core Logic thread is pinned to; -1 disables pinning.
     * See Docs/specs/2026-08-19-core-isolation-cicd-design.md. */
    int            core_affinity;
    bool           verbose;
} me_config_t;
```

- [ ] **Step 2: Set the default**

Edit `ME Project/me-primary/src/sys_init.c`, in `me_config_defaults()`:

```c
void me_config_defaults(me_config_t *cfg)
{
    memset(cfg, 0, sizeof(*cfg));
    cfg->server_port         = ME_PORT_TCP_CMD;
    cfg->device_id           = 0x01;
    cfg->secondary           = 1;
    cfg->channel             = 1;
    cfg->crc_order           = ME_CRC_ORDER_DEFAULT;
    cfg->connect_timeout_ms  = 5000;
    cfg->response_timeout_ms = 5000;
    cfg->core_affinity       = 3;     /* dedicate CPU3 to Core Logic by default */
    cfg->verbose             = false;
    snprintf(cfg->iface, sizeof(cfg->iface), "eth0");
    snprintf(cfg->device_name, sizeof(cfg->device_name), "BTS-600");
}
```

- [ ] **Step 3: Add the CLI option**

Edit `ME Project/me-primary/src/main.c`. In `usage()`, insert a line between `--channel` and `--name`:

```c
        "  --channel <n>       CircuitID lower nibble     (default 1)\n"
        "  --core <n>          Dedicated CPU for Core Logic (default 3, -1 disables)\n"
        "  --name <str>        Device name, max 16 chars  (default BTS-600)\n"
```

In `parse_args()`, insert a new branch between `--channel` and `--name`:

```c
        } else if (strcmp(a, "--channel") == 0) {
            NEED_VALUE();
            cfg->channel = (uint8_t)atoi(argv[++i]);
        } else if (strcmp(a, "--core") == 0) {
            NEED_VALUE();
            cfg->core_affinity = atoi(argv[++i]);
        } else if (strcmp(a, "--name") == 0) {
```

No range validation is added here deliberately — whether a given CPU index exists is a runtime hardware fact the OS itself is the authority on. `pthread_attr_setaffinity_np()` in Task 2 already turns an invalid index into a logged error, not a crash.

- [ ] **Step 4: Verify — both builds, per the standing rule**

```powershell
cd "ME Project\me-primary"
.\build.ps1
.\build-native.ps1
```

Expected: `build.ps1` compiles clean (`-Werror`) and prints the `ELF64`/`AArch64` header; `build-native.ps1` passes with the same check count as before this change (this task adds no new host-testable logic, so the count must be unchanged — report it). Say "compiles clean," not "tested" — this is a Linux-only code path with no host test coverage.

- [ ] **Step 5: Commit**

```bash
git add "ME Project/me-primary/src/sys_init.h" "ME Project/me-primary/src/sys_init.c" "ME Project/me-primary/src/main.c"
git commit -m "Add --core CLI option for Core Logic thread affinity"
```

---

### Task 2: Pin the Core Logic thread + isolation self-check

⚠ **HARDWARE-CRITICAL — human review required.** Present this diff for explicit developer approval before committing; do not proceed to Step 5 without it.

**Files:**
- Modify: `ME Project/me-primary/src/threads/core_logic.c`

**Interfaces:**
- Consumes: `sys->cfg.core_affinity` (`int`, from Task 1) via the existing `const me_system_t *sys` parameter of `me_core_logic_start()`.

- [ ] **Step 1: Add `_GNU_SOURCE` and `<sched.h>`**

`pthread_attr_setaffinity_np()` and `sched_getcpu()` are glibc extensions gated behind `_GNU_SOURCE`, which nothing in this codebase defines yet (`-std=gnu11` alone does not enable it — it only turns on `_DEFAULT_SOURCE`, a different macro, per the existing note in `ME Project/CLAUDE.md` about `struct ifreq`). The macro must be defined before any `#include`, so it goes above the file's existing header comment's `#include`:

```c
/*
 * core_logic.c - the Core Logic thread.
 */
#define _GNU_SOURCE
#include "core_logic.h"

#include <pthread.h>
#include <sched.h>
#include <stdio.h>
#include <string.h>
```

- [ ] **Step 2: Add the isolation self-check helper**

Insert this new static function immediately before `static void *core_logic_main(void *arg)`:

```c
/*
 * Best-effort operator warning, not a safety gate: if the board was never
 * provisioned per Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md,
 * pinning still succeeds (the thread really does run only on `core`) but the
 * kernel can still schedule other work, IRQs, and RCU callbacks onto it too -
 * the determinism guarantee is silently gone. This turns that into a loud
 * log line instead.
 */
static void warn_if_core_not_isolated(int core)
{
    FILE *f = fopen("/sys/devices/system/cpu/isolated", "r");
    if (!f) {
        return; /* file absent on some kernels - nothing to compare against */
    }
    char buf[64] = {0};
    (void)fgets(buf, sizeof(buf), f);
    fclose(f);

    /* buf is a cpu list like "3" or "2-3" - a full range parser is
     * unnecessary for a warning that is advisory, not load-bearing. */
    char needle[8];
    snprintf(needle, sizeof(needle), "%d", core);
    if (strstr(buf, needle) == NULL) {
        ME_LOGW("core logic thread: CPU%d is pinned but NOT isolated by the "
                "kernel (isolcpus missing?) - determinism is not guaranteed",
                core);
    }
}
```

- [ ] **Step 3: Log the landing core and run the self-check at thread start**

Edit `core_logic_main()`:

```c
static void *core_logic_main(void *arg)
{
    (void)arg;
    ME_LOGI("core logic thread: started");

    if (s_sys->cfg.core_affinity >= 0) {
        ME_LOGI("core logic thread: running on CPU%d", sched_getcpu());
        warn_if_core_not_isolated(s_sys->cfg.core_affinity);
    }

    while (!me_app_stop_requested()) {
```

(the rest of the loop body is unchanged)

- [ ] **Step 4: Pin the thread before creating it**

Edit `me_core_logic_start()`:

```c
bool me_core_logic_start(const me_system_t *sys)
{
    s_sys = sys;
    memset(s_cl, 0, sizeof(s_cl));
    memset(s_exec, 0, sizeof(s_exec));

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

A failed `setaffinity` is logged but not fatal — this is a determinism
improvement, not a correctness dependency, so the app keeps running
(unpinned) rather than refusing to start.

- [ ] **Step 5: Verify — both builds, per the standing rule**

```powershell
cd "ME Project\me-primary"
.\build.ps1
.\build-native.ps1
```

Expected: `build.ps1` compiles clean under `-Werror` (this is the step that
would catch a missing `_GNU_SOURCE` — an implicit-declaration warning on
`pthread_attr_setaffinity_np`/`sched_getcpu`/`CPU_SET` becomes a hard error).
`build-native.ps1`'s check count is unchanged (this file is Linux-only, not
part of the host build). State plainly that this only proves the code
compiles — pinning behavior itself requires the board.

- [ ] **Step 6: Get developer sign-off, then commit**

Show the diff to the developer per the hardware-critical gate above. Only
after explicit approval:

```bash
git add "ME Project/me-primary/src/threads/core_logic.c"
git commit -m "Pin Core Logic thread to its dedicated CPU with an isolation self-check"
```

---

### Task 3: Board provisioning runbook

**Files:**
- Create: `ME Project/Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md`

**Interfaces:** none — this is documentation, consumed by whoever provisions a physical board, not by any other task.

- [ ] **Step 1: Write the runbook**

```markdown
# iMX8MP Core Isolation — Board Provisioning Runbook

One-time, per physical Verdin iMX8M Plus board. Kernel boot configuration
cannot live in a Dockerfile, Compose file, or CI — it must be applied
directly on the board over SSH. Survives OTA updates because `fw_setenv`
writes to the U-Boot environment partition, not the OSTree-managed rootfs.

Companion design doc: `../specs/2026-08-19-core-isolation-cicd-design.md`.

## 1. Check current boot arguments

```bash
ssh torizon@<board-ip>
cat /proc/cmdline
sudo fw_printenv bootargs
```

## 2. Append isolation parameters

```bash
sudo fw_setenv bootargs "$(sudo fw_printenv -n bootargs) isolcpus=3 nohz_full=3 rcu_nocbs=3 irqaffinity=0,1,2"
```

| Flag | Meaning |
|---|---|
| `isolcpus=3` | Scheduler never auto-assigns any thread to CPU3 |
| `nohz_full=3` | Disables the periodic timer-tick interrupt on CPU3 |
| `rcu_nocbs=3` | Kernel RCU callbacks run elsewhere, not on CPU3 |
| `irqaffinity=0,1,2` | Hardware interrupts default to CPU0-2, stay off CPU3 |

## 3. Reboot and verify

```bash
sudo reboot
```

After reboot:

```bash
cat /proc/cmdline | grep isolcpus
cat /sys/devices/system/cpu/isolated        # expect: 3
cat /sys/devices/system/cpu/nohz_full        # expect: 3
```

## 4. Keep late-registering IRQs off CPU3

Some drivers set their own IRQ affinity after boot, overriding the
`irqaffinity` default.

```bash
sudo tee /usr/local/bin/redirect_irqs.sh > /dev/null <<'EOF'
#!/bin/bash
for irq_path in /proc/irq/*/smp_affinity_list; do
    echo "0-2" > "$irq_path" 2>/dev/null
done
echo "[INFO] All IRQs redirected away from CPU3"
EOF
sudo chmod +x /usr/local/bin/redirect_irqs.sh
sudo /usr/local/bin/redirect_irqs.sh
```

Make it persistent:

```bash
sudo tee /etc/systemd/system/redirect-irqs.service > /dev/null <<'EOF'
[Unit]
Description=Redirect IRQs away from isolated CPU3
After=multi-user.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/redirect_irqs.sh

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable redirect-irqs.service
sudo systemctl start redirect-irqs.service
```

## 5. Log in to the private image registry

The `me-primary` image is private (GHCR, inherits the repo's visibility).
One-time login with a PAT that has `read:packages`:

```bash
docker login ghcr.io -u <github-username>
```

## 6. Confirm after deploying `me_primary`

Once `docker compose up -d` is running the new image (see
`me-primary/docker-compose.yml`):

```bash
docker logs me-primary | grep -E "running on CPU|NOT isolated"
```

Expect a line like `core logic thread: running on CPU3` and the absence of
the `NOT isolated` warning. If the warning appears, re-check step 3.

The image is `scratch`-based (no shell), so `docker exec -it ... bash` for
`taskset`/`ps` does not work anymore - run those from the **host** instead,
which is the correct place for them regardless (they inspect the process as
the host scheduler sees it):

```bash
# PID of the container's process, as the host sees it
docker inspect -f '{{.State.Pid}}' me-primary

# Thread IDs inside that process (host-side ps, not docker exec)
ps -eLf | grep <PID-from-above>

# Confirm the Core Logic thread's TID is pinned to CPU3 only (expect mask "8")
taskset -p <TID-of-core-logic-thread>

# Confirm CPU3 stays otherwise idle - only Core Logic's own work should show
mpstat -P ALL 1
```


- [ ] **Step 2: Commit**

```bash
git add "ME Project/Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md"
git commit -m "Add board provisioning runbook for iMX8MP core isolation"
```

---

### Task 4: Production Dockerfile

**Files:**
- Create: `ME Project/me-primary/Dockerfile`

**Interfaces:**
- Consumes: `ME Project/me-primary/src/**` (the same source tree `build.ps1` compiles).
- Produces: a `scratch`-based image whose sole content is `/me_primary` — consumed by Task 5's Compose file.

- [ ] **Step 1: Write the Dockerfile**

```dockerfile
# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM debian:bookworm AS builder
RUN apt-get update && \
    apt-get install -y --no-install-recommends crossbuild-essential-arm64 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /build
COPY src ./src

# Mirrors build.ps1's flags and explicit source list exactly - keep both in
# sync by hand; this is deliberately not templated to avoid pulling in test
# sources. See Docs/specs/2026-08-19-core-isolation-cicd-design.md §2 for
# why this is a different cross-compiler than build.ps1's pinned toolchain
# (accepted trade-off, not an oversight).
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

If a future task adds a new `.c` file under `me-primary/src/`, add it to
**both** this `RUN` command's list and `build.ps1`'s `$sources` array — they
must be kept identical by hand (noted explicitly so it isn't missed).

- [ ] **Step 2: Verify — no Docker locally, so verify what's actually checkable**

This machine has no Docker Engine (standing project rule — cross-compilation
happens via a native toolchain, not Docker, on the dev laptop). Two things
*can* be checked without Docker:

1. **The source list matches `build.ps1` exactly.** Diff the two lists by eye
   — both must contain the same 25 files in `src/`.
2. **The compiler invocation matches the flags in `ME Project/CLAUDE.md`** —
   `-std=gnu11 -O2 -Wall -Wextra -Werror -static -pthread`.

Real build verification happens in Task 6, when the developer manually
triggers the release workflow on a real Linux runner with Docker available.

- [ ] **Step 3: Commit**

```bash
git add "ME Project/me-primary/Dockerfile"
git commit -m "Add production Dockerfile for me-primary (multi-stage, scratch runtime)"
```

---

### Task 5: `docker-compose.yml`

**Files:**
- Create: `ME Project/me-primary/docker-compose.yml`

**Interfaces:**
- Consumes: the image Task 4's Dockerfile produces, published as `ghcr.io/quench-ev-charger/me-primary:<tag>` by Task 6.
- Consumes: `ME_PRIMARY_VERSION` and `ME_SERVER_IP`, read from a board-local `.env` file (not committed — board- and deployment-specific).

- [ ] **Step 1: Write the Compose file**

```yaml
services:
  me-primary:
    image: ghcr.io/quench-ev-charger/me-primary:${ME_PRIMARY_VERSION:-latest}
    container_name: me-primary
    network_mode: host   # required: registration frame carries this IP/MAC as payload - do not remove
    # Must include CPU3 (or whatever --core is set to) or
    # pthread_attr_setaffinity_np() fails EINVAL. Do not narrow this range
    # for unrelated CPU-limiting reasons without re-checking the core
    # isolation design (Docs/specs/2026-08-19-core-isolation-cicd-design.md).
    cpuset: "0-3"
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

- [ ] **Step 2: Verify — YAML syntax check**

No Docker locally to run `docker compose config`, but the YAML itself can be
parsed:

```powershell
pip install --user pyyaml
python -c "import yaml; yaml.safe_load(open('ME Project/me-primary/docker-compose.yml').read()); print('valid YAML')"
```

Expected: `valid YAML`. This confirms syntax only, not Compose semantics —
semantic validation happens on the board when `docker compose up` actually
runs it.

- [ ] **Step 3: Commit**

```bash
git add "ME Project/me-primary/docker-compose.yml"
git commit -m "Add production docker-compose.yml for me-primary"
```

---

### Task 6: GitHub Actions release workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: `ME Project/me-primary/Dockerfile` (Task 4) as the build context.
- Produces: `ghcr.io/quench-ev-charger/me-primary:<version>` and `:latest`, consumed by Task 5's Compose file on the board.

- [ ] **Step 1: Write the workflow**

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
          context: ./ME Project/me-primary
          platforms: linux/arm64
          push: true
          tags: |
            ghcr.io/quench-ev-charger/me-primary:${{ inputs.version }}
            ghcr.io/quench-ev-charger/me-primary:latest
```

Note the `context: ./ME Project/me-primary` path — the repo root is
`ME_PRD`, and `me-primary/` lives under the `ME Project/` subfolder, not at
the repo root. This never triggers on push/commit, per explicit developer
instruction — `workflow_dispatch` only.

- [ ] **Step 2: Verify — YAML syntax check**

```powershell
python -c "import yaml; yaml.safe_load(open('.github/workflows/release.yml').read()); print('valid YAML')"
```

Expected: `valid YAML`.

- [ ] **Step 3: Commit**

```bash
git add ".github/workflows/release.yml"
git commit -m "Add manually-triggered GitHub Actions release workflow for me-primary"
```

- [ ] **Step 4: Real verification — ask the developer before triggering CI**

This step pushes to the remote and consumes GitHub Actions minutes on a
private repo — do not do this unilaterally. Ask the developer whether to
push the branch and manually trigger the workflow now (`gh workflow run
release.yml -f version=0.0.1-test`) to get the first real, non-local proof
that Task 4's Dockerfile actually builds. This is the first point in the
whole plan where that becomes possible to check at all.
