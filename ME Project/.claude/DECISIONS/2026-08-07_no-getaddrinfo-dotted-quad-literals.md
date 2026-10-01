# ADR-8: No `getaddrinfo` — server addresses are dotted-quad literals
> Date: 2026-08-07 | Session: #2 | Status: Accepted
> File: `DECISIONS/2026-08-07_no-getaddrinfo-dotted-quad-literals.md`

---

**Context:** ADR-2 links board binaries `-static` and explicitly warned that
"static glibc + `dlopen`/NSS (DNS, user lookup) can misbehave". The registration
module is the first code to need a remote address, so the warning came due.

**Decision:** The Web Application address is supplied as a dotted-quad literal
(`--server 192.168.0.10`) and parsed with `inet_pton`. No hostname resolution.

**Reason:** `getaddrinfo` in a statically linked glibc binary emits a link-time
warning and carries a runtime dependency on the NSS shared objects of the glibc
version used for linking — which will not match Torizon's. `inet_pton` is pure
computation with no NSS involvement. On a local factory network the Web
Application has a fixed address anyway, so nothing is lost.

**Impact:**
- ✅ ADR-2's static linking stays intact with no runtime surprise
- ⚠️ Hostnames and mDNS are unavailable. If a future module needs them, this
  ADR and ADR-2 must be reconsidered together, not separately.

**Related**
- Supersedes: none
- Relates to: ADR-2
