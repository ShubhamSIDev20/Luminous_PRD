# ADR-3: No local execution of the aarch64 binary; MinGW native build for logic checks only
> Date: 2026-08-06 | Session: #1 | Status: Accepted
> File: `DECISIONS/2026-08-06_no-local-aarch64-execution-mingw-native-build.md`

---

**Context:** Developer asked whether Hello World could be seen/run on the Windows
laptop itself, not just on the board. The board binary is a static aarch64 Linux
ELF — Windows cannot execute it (wrong CPU architecture *and* wrong OS ABI).

**Decision:** Accept that the aarch64 artifact cannot run locally. Add a second,
parallel build (`build-native.ps1`) that compiles the same `main.c` with MinGW-w64
into a native Windows `.exe` purely as a fast logic sanity-check.

**Reason:** QEMU **user-mode** emulation (`qemu-aarch64`), which would have run the
real artifact under emulation, ships only on Linux hosts. QEMU's Windows builds
contain only `qemu-system-*` full-machine emulation, which would require booting a
complete Linux kernel + disk image — disproportionate for this. QEMU was installed
during this session, found unusable for this purpose, and **uninstalled**
(package + leftover `C:\Program Files\qemu`, 5.7 MB).

**Impact:**
- ✅ Developer gets a sub-second local edit → compile → run loop
- ✅ Same single `main.c` feeds both targets — no source duplication
- ⚠️ A passing native build proves **nothing** about aarch64 codegen, Torizon, or
  Docker. Only `deploy.ps1` against real hardware verifies the target path.
- ⚠️ Do not re-attempt QEMU user-mode on this Windows host — already ruled out.

**Related**
- Supersedes: none
- Relates to: ADR-7
