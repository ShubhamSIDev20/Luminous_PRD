# Session #3: CRC byte order + 0x02-is-success fix
> Date: 2026-08-10T11:15+05:30 | Agent: Claude Code (Opus 5) | Status: ✅ Complete
> Prev session: [sessions/2026-08-07_1615_registration-milestone.md](2026-08-07_1615_registration-milestone.md) — registration implemented and verified end to end against the real Web Application.

---

## 🎯 Goal This Session
Two protocol corrections raised by the developer from live hardware logs:
send the CRC high byte first, and stop re-registering when the server answers
`0x02` (Already Registered).

## ✅ Done This Session — both HARDWARE-VERIFIED at 09:38 UTC

**1. CRC byte order → big-endian (ADR-9, supersedes ADR-5)**
- Root cause was not the CRC maths but **three copies of the default**:
  `sys_init.c`, the `--crc-order` help text, and `deploy.ps1` each spelled out
  `le` independently.
- Introduced `ME_CRC_ORDER_DEFAULT` in `src/proto/crc16.h` as the single source
  of truth; all three now resolve through it.
- Fixed a latent defect in `comm_thread.c`: the CRC-mismatch diagnostic
  hardcoded *"re-run with `--crc-order be`"*, which would have advised switching
  to the order already in use. It now derives the suggestion from the active
  order.
- Overturned ADR-5's "confirmed little-endian both directions" finding. The
  request half of that argument was never sound — a successful registration
  proves nothing about the request CRC if the server does not validate it.

**2. Response `0x02` is success; board idles (ADR-10)**
- `reg_frame.c` gated on `value == 0x01`, so `0x02` fell into the failure path,
  which **closes the socket** and reconnects — the "TCP keeps disconnecting"
  symptom the developer reported was entirely self-inflicted.
- Added `ME_REG_VALUE_IS_SUCCESS()` to `proto_defs.h`; `registered` now means
  "the server considers this device registered".
- No "already registered?" flag was added. `do_registration()` is called once
  per connection and `idle_loop()` never registers, so the rule is structural.
- Wired up the dead `me_comm_state_t` enum: `ME_COMM_REGISTERED` and
  `ME_COMM_MONITORING` (both unreferenced) became one `ME_COMM_IDLE`, and
  `monitor()` became `idle_loop()`.

**3. Tests: 46 → 53 checks**, all passing, plus a clean `-Werror` cross-build.
Two golden vectors now pin real captured frames byte-for-byte.

## 🔬 Hardware verification — 2026-08-10 09:38 UTC

```
registration: sending 33-byte 0xDD frame (CRC big-endian (CRC_HI CRC_LO))
  ... AC 10 0E CE 00 14 2D EF 86 E2 36 9E     <- request CRC 0x369E, high byte first
registration response (7 bytes)
  DD 01 01 11 02 BE 15                        <- response CRC 0xBE15, high byte first
  DEVICE REGISTERED   Server response : 0x02 (Already Registered)
state: IDLE - registered, holding connection, watching TCP and UDP 10000/10001
```

Both CRCs independently recomputed and confirmed. **The Web Application team's
server-side CRC change is live** — it now emits `BE 15` where it sent `15 BE`
earlier the same day. No board-side TX/RX split was needed.

## 💡 Discoveries / Gotchas
- **A "disconnecting TCP connection" was the board closing its own socket.**
  `me_tcp_close()` runs unconditionally after `do_registration()` fails. Any
  future "connection drops" report should first check whether registration
  is being classified as a failure.
- **Pinning the client source port was proposed and rejected.** The board does
  the active close, so its socket enters `TIME_WAIT`; a fixed local port would
  fail `bind()` on the 1-second retry. The changing ephemeral port was a
  symptom, not a cause. Revisit only if the Web App team confirms it keys
  sessions on source port.
- **Both IPs are DHCP and have moved every session.** Board `.209` →
  `172.16.18.238` → `172.16.14.206`; server `172.16.14.244` → `172.16.15.230`.
- **The Docker-bridge warning fired as a false positive again** on
  `172.16.14.206` (see T-19). The MAC is the genuine Toradex OUI and
  `--network host` was in use; the warning is noise on this site's LAN.
- ADR-5 is a worked example of a plausible-but-unsound hardware conclusion.
  Its reasoning is preserved in `DECISIONS.md`/`DECISIONS/` with the flaw annotated.

## 🔄 In Progress
- *(none)*

## 🚫 Blocked
- *(none)*

## 🔜 Next Agent Should Do
1. Protocol work is unblocked and the foundation is hardware-proven. Pick up
   **T-10** (remaining `0xDD` queries: Q2 delete, Q3 discovery, Q4 IP config)
   or **T-11** (UDP live data on 10000, session store on 10001).
2. Inbound command frames now dispatch from `idle_loop()` in `comm_thread.c`
   — that is where **T-12** (`0xAA`/`0xBB`/`0xEE`/`0xA0`) hooks in.
3. **T-17** (dedicated Docker image) is still with the developer.
4. Low-cost cleanups if wanted: **T-18** (`ethernet0` as default `--iface`) and
   **T-19** (the false-positive bridge warning — *ask first*, it was reverted
   once already).
