# ADR-29: Real RPMsg CAN transport replaces the fabricated `can_mgr.c` responder
> Date: 2026-08-18/19 | Session: #11 | Status: Accepted, partially hardware-verified
> File: `DECISIONS/2026-08-18_real-rpmsg-can-transport.md`

---

**Decision:** `can_mgr.c` now exchanges CAN-FD frames with the M7 core over
the real RPMsg character device (`/dev/ttyRPMSG30`), via new
`platform/rpmsg_link.c` (I/O), `proto/rpmsg_frame.c` (wrap/parse), and a
stream reassembler with resync + overflow handling. `g_q_can` was made
pollable so the CAN manager's `poll()` loop covers both the queue and the
device fd in one wait.

**Reason:** The T-54 (session #10) responder was a same-process fabricator
standing in for real M7 hardware. Design: `Docs/specs/2026-08-18-rpmsg-can-transport-design.md`.

**Impact:** ✅ Registration + the link itself confirmed live on hardware
2026-08-19 (`can manager thread: started, M7 link on /dev/ttyRPMSG30`).
⚠️ Full battery-test execution against a real Secondary through this path
is still not proven end to end (T-24).

**Related**
- Supersedes: the T-54 fabricated `can_mgr.c` responder
- Relates to: T-57, T-24, ADR-31
