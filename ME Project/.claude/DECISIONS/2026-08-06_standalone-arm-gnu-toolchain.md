# ADR-1: Standalone Arm GNU cross-toolchain instead of Docker Desktop or WSL2
> Date: 2026-08-06 | Session: #1 | Status: Accepted
> File: `DECISIONS/2026-08-06_standalone-arm-gnu-toolchain.md`

---

**Context:** Target is a Toradex Verdin iMX8M Plus running Torizon OS with Docker
Engine on the board. Developer builds on a Windows laptop and ships artifacts over
the network. The laptop had **no** Docker Desktop, **no** WSL2 distro, and no
aarch64 cross-compiler.

**Decision:** Install Arm GNU Toolchain `aarch64-none-linux-gnu` 15.2.rel1 natively
on Windows at `C:\ArmGNUToolchain\aarch64-none-linux-gnu-15.2.rel1`. Docker is used
only on the **board** side as the execution environment, never on the laptop as a
build environment.

**Reason:** Chosen by the developer from three options. Docker Desktop + `buildx`/QEMU
would work but requires a VM backend (WSL2 or Hyper-V), typically a reboot, and
QEMU-emulated compilation is slow. WSL2 + `gcc-aarch64-linux-gnu` is the heaviest
setup. The standalone toolchain has zero emulation overhead and no VM footprint.

**Impact:**
- ✅ Fast native cross-compilation, no VM or reboot on the laptop
- ✅ Keeps the laptop free of a Docker daemon that nothing else needs
- ⚠️ Cannot build or test **container images** locally — the Dockerfile path (if the
  ME project later needs one) can only be exercised on the board
- ⚠️ MSI install requires `EULA=1`; silent install fails with exit 1603 without it

**Related**
- Supersedes: none
- Relates to: ADR-2, ADR-3
