# ADR-7: `proto/` stays free of platform headers; the native build runs unit tests
> Date: 2026-08-07 | Session: #2 | Status: Accepted
> File: `DECISIONS/2026-08-07_proto-platform-free-native-build-tests.md`

---

**Context:** Verification against real hardware is slow and developer-gated
(ADR-3). Byte-layout errors — a shifted offset, a reversed IP — produce
well-formed frames that fail only inside the server's parser, which is the
worst kind of bug to chase over a socket.

**Decision:** `src/proto/` (`proto_defs.h`, `crc16.c`, `reg_frame.c`) contains
pure logic with no socket, thread or platform header. `build-native.ps1` no
longer builds a demo `.exe`; it compiles `proto/` and `util/` against a unit
test suite and runs it, failing the build on any failed assertion.

**Reason:** This converts the highest-risk part of the protocol from
"verifiable only on hardware" to "verifiable in one second on the laptop". It
repurposes the MinGW toolchain ADR-3 already justified. Tests assert every field
offset individually so a shifted field names itself rather than appearing as an
opaque buffer diff.

**Impact:**
- ✅ 46 assertions covering offsets, CRC, name padding, response parsing
- ✅ A mutation test confirmed the suite catches a reversed IP field
- ⚠️ `proto/` must stay platform-free. Adding `<sys/socket.h>` to it silently
  breaks the native build and removes this safety net.
- ⚠️ Still proves nothing about sockets, aarch64, Torizon or Docker (ADR-3)

**Related**
- Supersedes: none
- Relates to: ADR-3
