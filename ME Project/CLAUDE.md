# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ME Project — embedded work targeting an NXP MIMX8ML8CVNKZAB SoC (i.MX8M Plus)
on a Toradex Verdin iMX8M Plus module, running **Torizon OS** with Docker
Engine on-board.

`me-primary/` is the ME Primary Board application. The initial bring-up
milestone (Hello World, cross-compile → scp → `docker run`) is **complete and
verified on hardware**; that directory was renamed from `hello-world-bringup/`
and now holds the real application.

**Device registration over TCP 9999 is complete and hardware-verified**
(2026-08-10). The board is the TCP **client**, the Web Application is the server.
See `Docs/specs/2026-08-07-me-primary-registration-design.md` and
`Docs/ME_Primary_Comm_Block_Diagram.md`.

**Current milestone:** the **4-thread base for the Battery Testing
Application** — Communication, Core Logic, Data Manager and CAN Data Manager,
joined by in-process message queues. Built 2026-08-11; **not yet run on
hardware.** Design: `Docs/ME_Primary_BTS_Block_Diagram.md`.

> 📘 **Before opening any source file, read
> `Docs/ME_Primary_Implementation_Reference.md`.** It documents every file —
> responsibility, public interface, internal logic, dependencies and gotchas —
> so you can orient from one document instead of reading the whole codebase.

The threading rules that are easy to violate:

- **Never declare a `me_msgq_t` as a local.** It is ~1 MB and overflows a
  default thread stack. All four live at file scope in `src/app_queues.c`.
- **Never add an unbounded queue send.** Every send takes a finite timeout and
  drops on expiry; that is what makes deadlock impossible by construction.
- **CircuitID nibbles are 1-based.** `0x11` is the first circuit → slot 0. A
  malformed ID must be rejected, never folded to slot 0.
- **`src/threads/demo_realtime.c` is scaffolding.** It emits a synthetic `0xCC`
  frame per second for 60 seconds so the loop can be seen working. Delete it
  when real step execution lands; `me_execute_program()` in `core_logic.c` is
  the seam.

## Target architecture (read this before touching build/deploy code)

This is **not** a bare-metal/RTOS target — it's a Cortex-A53 application
processor running embedded Linux. The workflow is cross-compile-then-deploy,
not flash-and-debug:

```
Windows laptop (x86_64)                    Board (Verdin iMX8MP, aarch64, Torizon OS)
  aarch64-none-linux-gnu-gcc   --static-->   scp   -->   docker run -v ...:/bin  <image>  /bin
  (no Docker/WSL on the PC)                            (uses the board's existing Docker Engine)
```

Key decisions already made for this project (don't relitigate without asking):

- **No Docker Desktop / WSL2 on the Windows laptop.** Cross-compilation uses a
  standalone Arm GNU Toolchain instead, to avoid the VM/reboot footprint of
  Docker Desktop or WSL2 on this machine.
- **Binaries are statically linked** (`-static`). This is deliberate: it
  decouples the compiled binary from whatever glibc version ships in the
  Docker image on the board, so any `linux/arm64` container can run it
  unmodified.
- Docker itself only runs on the **board** side — it's the execution
  environment there, not the build environment on the PC.

## Toolchain

Board target (aarch64 Linux) cross-compiler: Arm GNU Toolchain
`aarch64-none-linux-gnu` 15.2.rel1 at
`C:\ArmGNUToolchain\aarch64-none-linux-gnu-15.2.rel1`.

Per the global embedded toolchain-lookup convention, this path is recorded in
`.embedded-override.json` at the project root under `toolchains.aarch64-linux-gnu`
— a project-specific addition, since the global
`~/.claude/embedded-toolchain.json` schema only covers bare-metal MCU
toolchains (Cortex-M GCC, MPLAB, XC8/16/32) and has no slot for an aarch64
Linux userspace cross-compiler. Resolve this path from the project override
file, not the global one.

> ⛔ **STANDING RULE (developer, 2026-08-12).** The **iMX8MP board is the only
> deliverable** — `.\build.ps1`. A Windows binary is never an output of the
> work. When it compiles clean under `-Werror`, say "compiles clean" — never
> "tested"; only `deploy.ps1` against real hardware proves the board target
> works.

## Commands

All scripts live in `me-primary/`.

Build for the board (static aarch64 ELF → `bin\me_primary`):

```powershell
.\build.ps1
```

Deploy and run on the board:

```powershell
.\deploy.ps1 -BoardIP <board-ip> -ServerIP <web-app-ip>
```

Defaults: `-BoardUser torizon` (Torizon OS's standard default user),
`-RemotePath /home/torizon/me_primary`, `-DockerImage debian:bookworm-slim`,
`-Port 9999`, `-CrcOrder be`. See `me-primary/README.md` for the full
parameter list and troubleshooting.

**`deploy.ps1` runs the container with `--network host`, and that is not
cosmetic.** The registration frame carries the board's IP and MAC as *payload*.
On Docker's default bridge the program reads the container's virtual interface
(`172.17.x.x`, synthetic MAC) and registers the board at an unreachable
address — with a valid CRC, so nothing appears wrong. Do not remove that flag.

**Full deployment guide (both `deploy.ps1` dev iteration and the persistent
`docker compose` production path on the board, SSH/password-auth fallback,
and the RPMsg-device-race failure mode and recovery):**
`Docs/runbooks/2026-08-21-imx8mp-deployment-guide.md`. Read it before any
board deploy/restart — `deploy.ps1`'s CLI flags have drifted from what the
board's production `docker-compose.yml` actually runs.

## Build flags worth knowing

- `-std=gnu11`, not `-std=c11`: `struct ifreq`, `IFNAMSIZ` and `IFF_UP` live
  behind `_DEFAULT_SOURCE`, which strict ISO mode switches off.
- `-static` (see ADR): consequently the code avoids `getaddrinfo`, which carries
  an NSS runtime dependency in a static glibc binary. Server addresses are
  dotted-quad literals parsed with `inet_pton`. Do not introduce hostname
  resolution without revisiting this.
- `-Werror` on the build.

## Verification

Two layers, and it matters which claim each one supports:

| Layer | Command | What it actually proves |
|---|---|---|
| Cross-build | `.\build.ps1` | Compiles clean under `-Werror` and produces a static `ELF64 AArch64` binary with no dynamic section. |
| **End-to-end registration** | `.\deploy.ps1` | **The only proof the system works.** Must be run by the developer. |

The developer must run the last row: Claude Code has no network path to the
hardware from this machine. QEMU user-mode emulation (`qemu-aarch64`) is
**not** available on Windows hosts, so the aarch64 binary cannot be executed
locally at all; only `qemu-system-*` full-machine emulation ships on Windows,
which would require booting a complete Linux image and isn't worth it for this.

## Requirement traceability — update it with every code change

> ⛔ **STANDING RULE (developer, 2026-08-26).** `../Requirements/Code_Tracebility/`
> is the live record of which `ME_Primary_SRS_V0.1` requirements this code
> meets, what proves it, and what the rest will cost. **It is updated as part of
> every change to `me-primary`, not afterwards.**

The markdown in that folder is generated, never hand-edited. After a change
that moves a requirement forward:

1. Update the verdict and evidence for the requirements it touched, in
   `../Requirements/Code_Tracebility/tools/trace_status.py`. Evidence names a
   file and a symbol, not a feeling.
2. If a work package closed or changed size, update
   `../Requirements/Code_Tracebility/tools/trace_packages.py`.
3. `python ../Requirements/Code_Tracebility/tools/build_traceability.py`
4. Commit the source module and the regenerated markdown in the same commit.

The bar for `DONE` is the same as the bar in the Verification table above: a
host test can prove protocol logic, but anything involving a socket, a thread,
the RPMsg link or real hardware needs a `deploy.ps1` run. **Do not promote a
verdict on the strength of `build-native.ps1`** — that is ADR-3.

`Requirements/Code_Tracebility/README.md` is the entry point and explains how
the folder relates to `.claude/TASKS.md` (work in flight) and
`.claude/DECISIONS.md` (why the code is shaped this way).

## Graphify Skill Usage

**Important:** Whenever a task involves a graph, flowchart, diagram, architecture visualization, dependency graph, state machine, sequence diagram, or similar visual representation, **use the Graphify skill whenever applicable**.

Before completing such tasks, evaluate whether the Graphify skill is appropriate and invoke it when it can produce a better result than plain text or manually generated output.