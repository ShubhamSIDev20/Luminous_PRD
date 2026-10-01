# ADR-6: The container must run with `--network host`
> Date: 2026-08-07 | Session: #2 | Status: Accepted
> File: `DECISIONS/2026-08-07_container-network-host-required.md`

---

**Context:** The registration frame carries the board's own IP and MAC address
as *payload* — the Web Application uses them to reach back to the board.

**Decision:** `deploy.ps1` always passes `--network host`. The program reads its
identity from a live interface at startup rather than from configuration, and
warns if it detects a `172.16–31.x.x` address.

**Reason:** On Docker's default bridge network `getifaddrs`/`SIOCGIFADDR` return
the *container's* virtual interface — typically `172.17.0.x` with a synthetic
MAC. The frame would transmit successfully with a valid CRC and register the
board at an address that does not exist on the network. Nothing would look
wrong. `--network host` also gives the container the real 9999/10000/10001
ports the protocol assumes.

**Impact:**
- ✅ Registration payload always carries the board's true identity
- ⚠️ This is the first place the `docker run` command line is part of protocol
  correctness, not just runtime convenience. Do not remove the flag.
- ⚠️ Host networking means no port isolation between board containers

**Related**
- Supersedes: none
- Relates to: ADR-4
