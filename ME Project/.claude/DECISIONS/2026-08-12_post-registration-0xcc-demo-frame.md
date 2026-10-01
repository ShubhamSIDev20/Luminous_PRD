# ADR-23: One `0xCC` frame per successful registration — temporary demo
> Date: 2026-08-12 | Session: #9 | Status: Accepted — **TEMPORARY** — not hardware-verified
> File: `DECISIONS/2026-08-12_post-registration-0xcc-demo-frame.md`

---

**Context:** The 1 Hz demo emitter (ADR-16) only arms on an `0xEE` Q1 Start, which
requires a program to have been transferred first. The CAN Manager produces nothing
(T-30). So between registration and a Start, the live UDP path is completely silent
and the Web Application has nothing to display for a circuit that demonstrably
exists. The developer asked for one dummy frame at registration to bridge that gap.

**Decision:** After `do_registration()` succeeds, send exactly **one** 86-byte
`0xCC` frame for that circuit to **UDP 10000**, the documented live-data
destination. Contents fixed: `step_number = 1`, `temperature = 25.0 °C`, everything
else zero, both status bytes **Idle**. Bytes supplied by the developer; for device
`0x01` / circuit `0x11` the output is byte-identical to them, CRC `0xF261`.

Four sub-decisions, each with a reason:

1. **Built from `me_realtime_t`, not a hardcoded byte array.** It must address
   whatever `--secondary`/`--channel` the board was started with. The `0x01`/`0x11`
   case is pinned to the developer's exact bytes by test; a second test runs device
   `0x07` / circuit `0x32` to prove it is genuinely built.
2. **The byte layout lives in `realtime_frame.c`, not `demo_realtime.c`.** That
   module is PURE and in the host build; `demo_realtime.c` is Linux-only and
   excluded from `build-native.ps1`. Putting it there is the only way the exact
   frame could be proven on the laptop rather than discovered on hardware. Its two
   constants are defined in `realtime_frame.h` and deliberately **not** taken from
   `demo_realtime.h`'s `ME_DEMO_TEMPERATURE`, which would break purity.
3. **Through `g_q_comm`, not a direct `me_udp_send_to()`.** Every `0xCC` frame in
   this program leaves via `ME_MSG_REALTIME_DATA` → `drain_outbound()`, so exactly
   one place decides which socket and destination live data uses.
4. **Fires on every successful registration**, including reconnects. That is the
   literal reading of the request and the more useful behaviour for a demo.

**Thread-safety, which is the one real hazard here.** Every other function in
`demo_realtime.*` runs on the **Core Logic** thread — the header even says so, and
`s_tx` is a single shared buffer justified by that. `me_demo_send_post_registration()`
runs on the **Communication** thread. It therefore has its **own** buffer,
`s_tx_postreg`. Two owners, two buffers, no sharing. Sharing `s_tx` would be a
silent data race against `me_demo_service()` that would appear as occasional
corrupt live frames.

It returns `void` and logs its own outcome because a queue-full drop must not make
a successful registration look failed.

**Consequences:**
- ✅ The Web Application sees a live frame for a circuit as soon as it registers
- ✅ Exercises the whole UDP 10000 path — pack, queue, eventfd wake, drain, send —
  without needing a program transfer, which is a genuinely useful smoke test
- ⚠️ **Scaffolding.** Delete with `demo_realtime.*`. **Three** references then fail
  to compile, deliberately: the call and the `#include` in `comm_thread.c`, and
  `me_realtime_pack_post_registration()` in `realtime_frame.c/.h`
- ⚠️ A reconnect sends another frame. If the Web Application finds that noisy, the
  single call site is where to gate it
- ⚠️ Reports `temperature = 25.0 °C` for hardware that measured nothing. It is
  demo data on a demo path, but it is still a number a person could believe

**Alternatives rejected:**
- *Send on TCP 9999* — guaranteed to reach a listener, but puts unsolicited live
  data on the command/response channel, which no document describes.
- *Send on both* — doubles the chance the demo works first try at the cost of a
  duplicated frame; the developer chose UDP alone.
- *Keep the whole thing in `comm_thread.c`* — trivially correct on threading, but
  scatters temporary code into a permanent file where it outlives its purpose.

**Related**
- Supersedes: none
- Relates to: ADR-16, T-30
