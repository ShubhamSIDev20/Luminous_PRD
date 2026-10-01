# CAN-FD Transport Analysis — A53 (Linux/SocketCAN) vs M7 (FreeRTOS/RPMsg)

**Date:** 2026-08-18 (revised same day — confirmed topology and frame count)
**Target:** NXP i.MX8M Plus (MIMX8ML8CVNKZAB) on Toradex Verdin iMX8M Plus, Torizon OS
**Question:** How do we send/receive real CAN-FD on the physical CAN port, and should the
1 ms cycle to the Secondary boards run on the Cortex-A53 or the Cortex-M7?
**Status:** Analysis / recommendation only. No implementation approved. No code written.

**Revision note (3rd pass):** Section 2 has been updated with the
developer-confirmed topology: **up to 8 Secondary boards, 8 channels each.**
Reading all 8 channels of one Secondary is **2 TX query frames + 2 RX response
frames = 4 CAN-FD frames total**, all 64 bytes as currently implemented
(`can_frame.c:53`, `me_can_pack_read()`). **The 1 ms budget applies only when
a single Secondary is connected.** The bitrate is now confirmed as **flat
2 Mbps, no BRS** (arbitration and data phase both at 2 Mbps — see §2.2).

⚠ **At flat 2 Mbps, 4 × 64-byte frames cost ~1.16 ms of wire time alone — this
is still 16% over the 1 ms budget before any turnaround or processing delay is
added.** See §2.2 for the full arithmetic and the mitigation options. This
does not change the A53-vs-M7 recommendation (§1) — the shortfall is a
bus-bandwidth problem, and an M7 does not run the bus any faster — but it is a
real risk that needs a decision before implementation.

*(Correction: an earlier pass of this document mis-computed the 8-Secondary
full-system figure — it read "~2.7 ms" where the arithmetic actually gives
~10.7 ms at 500 k/2 M. §2.2 below has been corrected.)*

---

## 1. Executive Summary

**One-line summary: use SocketCAN on the A53, not RPMsg on the M7 — the transport bottleneck is bus bandwidth, not CPU jitter, and an M7 does not fix that while adding weeks of effort and a new failure mode.**

**Recommendation: Cortex-A53 with Linux SocketCAN. The M7 is not justified.**

Three findings drive this:

1. **The bus is the bottleneck, not the CPU.** A 64-byte CAN-FD frame at the
   confirmed flat 2 Mbps bitrate costs ~290 µs of wire time. Linux scheduler
   jitter on this SoC is roughly an order of magnitude smaller. Adding an MCU
   to shave jitter that is already dominated by transaction cost buys nothing.
2. **1 ms is a single-Secondary read cycle, not a system-wide hard deadline —
   and at today's frame count, it does not quite fit.** Reading all 8
   channels of one Secondary is 2 TX query + 2 RX response = **4 CAN-FD
   frames**. At the confirmed flat 2 Mbps and today's 64-byte-per-frame
   implementation, that is **~1.16 ms of wire time — 16% over the 1 ms
   budget**, before any turnaround delay. Closing this gap needs either
   shrinking the query frames or a bitrate/protocol adjustment, not a CPU
   change — see §2.2. With up to 8 Secondaries sharing the bus, a full-system
   read scales to 32 frames ≈ **9.3 ms** minimum wire time, since only one
   frame is ever on the wire at a time.
3. **The real blocker is `me_msg_t`, and it is not a CAN problem.** Every 64-byte
   CAN frame is copied as a ~64 KB struct through two message queues. This would
   defeat a 1 ms cycle regardless of which core owns the CAN controller — and
   moving to the M7 would inherit the same defect over RPMsg.

---

## 2. Bus Bandwidth — The Governing Constraint

### 2.1 Frame timing arithmetic

A CAN-FD frame with an 11-bit identifier, BRS enabled, and 64 data bytes costs
approximately:

- **~30 bits at the nominal (arbitration) bitrate** — SOF, 11-bit ID, RRS, IDE,
  FDF, res, BRS, then ACK slot, ACK delimiter, EOF (7), IFS (3).
- **~550 bits at the data bitrate** — ESI, DLC (4), data (512), stuff bits,
  CRC-21, delimiters.

| Nominal / Data bitrate | Arbitration phase | Data phase | **Total per frame** | Frames per 1 ms |
|---|---|---|---|---|
| **Flat 2 Mbit/s, no BRS** ← confirmed config | ~15 µs | ~275 µs | **~290 µs** | ~3 |
| 500 kbit/s / 2 Mbit/s (BRS) | ~60 µs | ~275 µs | ~335 µs | ~2–3 |
| 1 Mbit/s / 5 Mbit/s (BRS) | ~30 µs | ~110 µs | ~140 µs | ~6–7 |
| 1 Mbit/s / 8 Mbit/s (BRS) | ~30 µs | ~69 µs | ~99 µs | ~9–10 |

> **Note on BRS:** the bitrate switch only accelerates the data phase.
> Arbitration, ACK, EOF and IFS always run at the nominal rate. Raising the
> *nominal* bitrate therefore helps short frames more than intuition suggests
> — which is why flat 2 Mbps (nominal = data = 2 Mbps) beats 500 k/2 M split
> BRS per-frame, despite both having a 2 Mbps data phase: the arbitration
> phase is faster too. The BRS rows are kept here only as reference points for
> the mitigation discussion in §2.2, not as the chosen configuration.

### 2.2 Confirmed topology and per-Secondary frame count (2026-08-18, updated)

Developer-confirmed scope:

- **Up to 8 Secondary boards** on the bus.
- **Each Secondary controls 8 channels.**
- **Reading all 8 channels of one Secondary is 2 TX query frames + 2 RX
  response frames = 4 CAN-FD frames total** (developer-confirmed, answering
  Open Question 5 from the first revision). The 2-and-2 split matches the
  wire layout already implemented in `can_frame.c:37-92` — 4 channel slots
  packed per 64-byte frame, so 2 response frames × 4 slots = 8 channels, and
  the query side mirrors it 1:1 (one query frame per response frame).
- **The 1 ms budget applies only when a single Secondary is connected to the
  Primary.** It is not a per-Secondary guarantee that holds regardless of how
  many boards share the bus.

**Frame size as currently implemented.** `me_can_pack_read()`
(`can_frame.c:53`) writes a full `out64[ME_CAN_FRAME_LEN]` — 64 bytes — for
the query frame, identical in size to the response frame produced by
`me_can_pack_set()` / the feedback packer. There is no smaller query format in
the current code; every frame in both directions is 64 bytes.

**Confirmed bitrate: flat 2 Mbps, no BRS** — nominal (arbitration) and data
phase both run at 2 Mbps; the bitrate-switch flag is not used.

**Recomputed wire time for the single-Secondary case:**

```
4 frames × ~290 µs/frame  ≈  1.16 ms
```

⚠ **This is ~16% over the 1 ms budget from wire time alone** — before adding
the Secondary's processing/turnaround delay between receiving a query and
transmitting its response, which only makes the total worse. Plain `CAN_RAW`
on the A53 (§3) does not change this: it is a bus-bandwidth ceiling, not a
scheduling problem, so it would apply identically to an M7-based transport.

**Physical-layer caveat specific to flat 2 Mbps (flag, don't assume):** CAN
arbitration relies on every node observing the dominant/recessive result of
each bit within that bit's time slot, which requires the bit time to exceed
roughly twice the worst-case signal propagation delay across the *entire* bus
(all 8 Secondary stubs included). Running arbitration at 2 Mbps — not just the
data phase, which has no such constraint — is materially more restrictive on
total bus length and stub length than the 500 kbps/1 Mbps arbitration rates
conventionally used with CAN-FD. **This needs hardware-in-the-loop
verification against the actual Verdin-to-Secondary cabling and stub lengths
before flat 2 Mbps is finalized** — it is a physical signal integrity
question, not something this analysis can certify from timing arithmetic
alone.

**Mitigation options for the residual 16% gap, in order of preference:**

| Option | Effect | Cost |
|---|---|---|
| **Shrink the query frame.** `master_slave_can_v1.0.md:56-59` already defines a `0x09` = 12-byte DLC alongside `0x0F` = 64 bytes — a read query plausibly only needs a command + channel selector, not a full 64-byte payload | 2 × 64 B response (~290 µs each) + 2 × 12 B query (~85 µs each) ≈ **~750 µs** — fits with ~25% margin at the confirmed flat 2 Mbps | Requires Secondary firmware to accept a 12-byte query frame instead of 64 bytes — a protocol change coordinated with whoever owns Secondary firmware, not a Primary-side-only fix |
| **Use BRS with a slower arbitration rate instead of flat 2 Mbps** — e.g. 1 Mbps arbitration / 5 Mbps data | 4 × ~140 µs ≈ **~560 µs** — 44% margin | Worth noting: this is *faster per frame than flat 2 Mbps* **and** relaxes the arbitration bus-length constraint above (1 Mbps arbitration tolerates a longer bus than 2 Mbps arbitration does), at the cost of needing a data-phase-capable (5 Mbps) transceiver on every node and re-enabling BRS in the controller config |
| Relax the target | N/A | Only viable if 1 ms is a soft throughput goal rather than a hard deadline (Open Question 4, §9) |

**Recommendation on this point:** the BRS alternative (1 Mbps arbitration /
5 Mbps data) is worth serious consideration alongside the confirmed flat
2 Mbps — it is both faster per frame and physically safer on bus length, at
the cost of confirming 5 Mbps transceiver capability across all 9 nodes
(Primary + 8 Secondaries). Shrinking the query frame is a good complementary
optimization either way, but depends on a cross-team protocol decision. This
is presented as input to your decision, not a change to the confirmed flat
2 Mbps config — that choice is yours to make (Open Question 7, §9).

**Multi-Secondary scaling.** CAN is a single shared bus — only one frame is
ever on the wire at a time, so frame counts add rather than run in parallel.
A full-system read across all 8 Secondaries is 4 × 8 = **32 frames ≈ 9.3 ms**
at flat 2 Mbps, minimum wire time before request/response turnaround
overhead. The 1 ms figure is a *best-case, single-board* number;
system-level cycle time must be budgeted per the actual number of boards
connected — it does not stay at 1 ms as boards are added.

### 2.3 Cross-check against the general protocol reference

`ME Project/Ref Docs/master_slave_can_v1.0.md:56-59` specifies:

- 500 kbps arbitration phase, 2 Mbps data phase
- 11-bit standard identifier
- Intel (little-endian) byte order
- DLC `0x0F` = 64 bytes, `0x09` = 12 bytes

`master_slave_can_v1.0.md:122-138` gives a general bus timing table (channel
counts here are **not** expressed in the confirmed 8-boards × 8-channels
terms of §2.2, so treat it as an older/coarser scaling reference rather than
the authoritative figure):

| Scope | Specified cycle |
|---|---|
| 1 module / 4 channels | 1 ms |
| 20 channels | 5 ms |
| 40 channels | 10 ms |
| 200 channels | 50 ms |

Order of magnitude does not closely match §2.2's 32-frame/~9.3 ms (at flat
2 Mbps) full-system estimate — this reference table's numbers were based on
an older/coarser channel-counting convention (§2.3 heading note above) and
predate the confirmed 4-frames-per-Secondary figure. **§2.2 is the figure to
design against; treat this table as historical context only.**

### 2.4 Hardware caveat

The Verdin carrier board's CAN transceiver must be rated for the 2 Mbit/s data
phase. Many common transceivers are 2 Mbit/s or 5 Mbit/s class; some are slower.
**This must be verified against the carrier board schematic before bring-up.**

---

## 3. Method A — Cortex-A53 + Linux SocketCAN

### 3.1 Hardware availability

The i.MX8M Plus has **two native FlexCAN controllers, both CAN-FD capable**,
exposed on the Verdin as `can0` / `can1` (alt-names `verdin-can1` /
`verdin-can2`).

Verify on the actual board before assuming — the Verdin iMX8M **Mini** has no
FlexCAN and uses an SPI-attached MCP251xFD instead, which has materially
different latency characteristics:

```bash
ip -details link show can0     # expect "flexcan" and FD capability
```

### 3.2 Interface bring-up

```bash
ip link set can0 down
ip link set can0 type can \
    bitrate  500000 sample-point  0.8 \
    dbitrate 2000000 dsample-point 0.75 \
    fd on restart-ms 100
ip link set can0 txqueuelen 1000
ip link set can0 up
```

### 3.3 Torizon container requirements

CAN interfaces live in the host network namespace. `deploy.ps1` already uses
`--network host`; the following must be added:

| Flag | Purpose |
|---|---|
| `--net=host` | already present — host network namespace |
| `--cap-add=NET_ADMIN` | configure the CAN link from inside the container |
| `--cap-add=SYS_NICE` | set `SCHED_FIFO` on the CAN thread (only if RT tuning is adopted) |

Container packages for debugging: `iproute2`, `can-utils`.

### 3.4 Socket API shape

```c
int s = socket(PF_CAN, SOCK_RAW, CAN_RAW);

int enable = 1;
setsockopt(s, SOL_CAN_RAW, CAN_RAW_FD_FRAMES, &enable, sizeof(enable));

struct ifreq ifr = {0};
strncpy(ifr.ifr_name, "can0", IFNAMSIZ - 1);
ioctl(s, SIOCGIFINDEX, &ifr);

struct sockaddr_can addr = { .can_family  = AF_CAN,
                             .can_ifindex = ifr.ifr_ifindex };
bind(s, (struct sockaddr *)&addr, sizeof(addr));

struct canfd_frame f = { .can_id = 0x123,
                         .len    = 64,           /* plain length, NOT a DLC code */
                         .flags  = CANFD_BRS };
write(s, &f, CANFD_MTU);                          /* CANFD_MTU == 72 */
```

Kernel `struct canfd_frame`:

```c
struct canfd_frame {
    canid_t can_id;  /* 32 bit CAN_ID + EFF/RTR/ERR flags */
    __u8    len;     /* frame payload length in byte (0 .. 64) */
    __u8    flags;   /* CANFD_BRS, CANFD_ESI */
    __u8    __res0;
    __u8    __res1;
    __u8    data[64] __attribute__((aligned(8)));
};
```

**RX discrimination:** the read length distinguishes frame type — `CAN_MTU` (16)
means `struct can_frame`, `CANFD_MTU` (72) means `struct canfd_frame`.

**Note:** `len` is a plain byte count (0–64), *not* a DLC code. The kernel
handles the `0x0F` ↔ 64 mapping. Nothing in the current codebase encodes DLC
codes, so this aligns.

Use `epoll`/`poll` with a per-request timeout in the `can_mgr` thread. Do not
busy-loop.

### 3.5 Determinism tuning (only if measurement shows it is needed)

In increasing order of cost:

1. **Torizon OS PREEMPT_RT variant.** Toradex ships a real-time kernel build.
   Single biggest win — moves worst-case scheduling latency from milliseconds to
   tens of microseconds.
2. **`SCHED_FIFO` priority ~80** on the CAN thread, plus
   `mlockall(MCL_CURRENT | MCL_FUTURE)` to eliminate page-fault stalls.
3. **CPU core isolation** — `isolcpus=3 nohz_full=3 rcu_nocbs=3` on the kernel
   command line; pin the CAN thread to that core and set the FlexCAN IRQ
   affinity to match.
4. **CPU governor to `performance`** — `ondemand` alone can add 50–200 µs.

### 3.6 Rejected option — `CAN_BCM` kernel-space cyclic transmission

The Linux Broadcast Manager (`CAN_BCM`) performs cyclic CAN-FD transmission
entirely in kernel space (`TX_SETUP` with `SETTIMER | STARTTIMER | CAN_FD_FRAME`),
immune to userspace scheduling. This was initially considered attractive.

**It was rejected.** `master_slave_can_v1.0.md:115-120` defines a **polled
master/slave request–response** discipline: send, await reply, resend up to 3
times, then mark the slave permanently offline. BCM is fire-and-forget cyclic
transmission with no concept of awaiting a reply before proceeding. Using it
would fight the protocol.

Plain `CAN_RAW` with `epoll` and per-request timeouts is the correct fit.

*(BCM's `RX_SETUP`/`RX_TIMEOUT` could still be useful later for silent-slave
detection, but that is not the primary transport.)*

---

## 4. Method B — Cortex-M7 + FreeRTOS + RPMsg

### 4.1 How it would work

- FlexCAN2 is **removed from the Linux device tree** and assigned to the M7
  domain in the U-Boot device tree. On Torizon this means applying the HMP
  device tree overlay plus a custom overlay disabling `flexcan2` for Linux.
- M7 firmware is built with the **MCUXpresso SDK** (part `MIMX8ML8xxxKZ` for the
  Verdin iMX8MP Quad) using the `arm-none-eabi` Arm GNU Toolchain.
- Firmware is loaded from **U-Boot via `bootaux`** (Toradex's recommended path),
  or from Linux via `remoteproc`.
- A53 ↔ M7 communication uses **RPMsg** over the Messaging Unit with a reserved
  DDR region, via `imx_rpmsg_tty` or `rpmsg_char`.

Required kernel command line argument, otherwise Linux gates the M7 root clock
and the core dies:

```
clk-imx8mp.mcore_booted=1
```

U-Boot loading sequence:

```
setenv cm_boot "${load_cm_image}; cp.b ${loadaddr} 0x7e0000; dcache flush; bootaux 0x7e0000"
```

### 4.2 The decisive objection

The step execution engine and `core_logic` thread run in **Linux userspace on
the A53**. If the A53 must originate the content of each cycle, moving CAN to
the M7 does not remove Linux jitter — it **adds RPMsg latency on top of it**.
The bottleneck moves; it does not disappear.

The M7 only pays off if the *cyclic control loop itself* moves down:

```
A53 (Linux)                          M7 (FreeRTOS)
step engine  ──setpoints @ 20ms──▶   1 ms cyclic CAN-FD TX/RX to secondaries
             ◀──telemetry @ 20ms──   aggregation, fault detection, safety trip
```

That is a different product architecture, not a transport swap. It is the right
answer only if hard real-time is required — guaranteed deadlines with safety
consequences on a miss.

### 4.3 Known caveats

- **NXP AN5317:** on i.MX8M platforms, `remoteproc` stops only the Cortex-M
  *CPU*, not its bus masters. In-flight transactions hang and require a full SoC
  reset. NXP states it is **not recommended to stop the M7 in a production
  system.** This has produced kernel panics for Toradex users.
- Debugging requires JTAG or a dedicated M7 UART console.
- The existing host-based unit test suite (192 checks) cannot cover M7 firmware.
- Two firmware artifacts must be versioned, built, and field-updated in lockstep.
- The codebase currently has **zero** M7/AMP infrastructure — no `remoteproc`,
  `rpmsg`, `OpenAMP`, or `flexcan` references anywhere. `ME Project/CLAUDE.md`
  states explicitly: *"This is **not** a bare-metal/RTOS target."*

---

## 5. Side-by-Side Comparison

| | **A53 / SocketCAN** | **M7 / FreeRTOS** |
|---|---|---|
| Typical TX jitter, stock kernel | 100 µs – several ms | — |
| Typical TX jitter, PREEMPT_RT + isolated core | ~20–100 µs, worst case a few hundred µs | — |
| M7 ISR-driven jitter | — | **< 10 µs, hard-deterministic** |
| Jitter vs. 335 µs frame wire time | Comfortably below | Far below (over-provisioned) |
| 1 ms per-module cycle achievable? | **Yes**, soft real-time | Yes, hard real-time |
| Effort to first working frames | **~1–2 days** | ~2–4 weeks |
| Reuses existing `can_mgr` thread + queues | **Yes, directly** | No — new IPC layer + M7 application |
| Debuggability | `candump`, `gdb`, existing host test suite | JTAG, M7 UART, no host tests |
| Failure blast radius | Container restart | SoC reset (per AN5317) |
| Fixes the 64 KB `me_msg_t` copy problem | No (fixed separately) | **No — inherits it over RPMsg** |
| Helps if decision logic stays on A53 | **Yes** | **No — adds RPMsg latency** |

All latency figures are typical published values and are **not trustworthy until
measured on the actual board and carrier.**

---

## 6. Current Codebase State

### 6.1 The CAN manager is a wired stub

`ME Project/me-primary/src/threads/can_mgr.c` (174 lines).
`can_mgr.h:1-14` states the contract: *"A WIRED STUB. No CAN interface is
opened, because none exists on this board yet."*

- Thread body at `can_mgr.c:120-154` loops on
  `me_msgq_recv(&g_q_can, &s_rx, ME_RECV_TIMEOUT_MS)`. It never opens a socket.
- `ME_MSG_CAN_TX` messages are dispatched at `can_mgr.c:132-144` on the low 5
  bits of `s_rx.offset` (the function code) to `handle_set_values()` /
  `handle_read_values()`. **The 64-byte payload is decoded, then discarded —
  nothing is ever written to a bus.**
- Simulated responses are fabricated at `can_mgr.c:29-39` and `:96-118` (a slow
  voltage drift from 12.0 V toward 14.4 V in 0.001 V steps per READ poll).
- Producer side is `core_logic.c:102-112` (`dispatch_output()`), which carries
  the 11-bit CAN ID in `s_tx.offset`.
- Tracked as open task **T-30** in `.claude/TASKS.md:97`.
- `ME Project/me-primary/src/proto/can_frame.c:37-92` already implements the
  wire layout (4 channel slots × 16 bytes = 64; per slot: `+0` f32 voltage LE,
  `+4` f32 current LE, `+8` command/state, `+9` channel number, `+10..15` zero)
  and ID construction at `can_frame.c:13-16`:
  `(circuit6 & 0x3F) << 5 | (func5 & 0x1F)`.

**No SocketCAN usage exists anywhere in the repo** — zero hits for `PF_CAN`,
`AF_CAN`, `SIOCGIFINDEX`, `canfd_frame`, `CAN_RAW`, or `<linux/can*>`.

### 6.2 BLOCKER — `me_msg_t` is ~64 KB per message

`ME Project/me-primary/src/msg.h:78-86`:

```c
typedef struct {
    me_msg_type_t type;
    uint8_t       circuit_id;
    uint8_t       flags;
    uint16_t      status;
    uint32_t      offset;     /* 11-bit CAN ID smuggled through here */
    uint32_t      len;
    uint8_t       payload[ME_MSG_PAYLOAD_MAX];   /* 64 KB (msg.h:29) */
} me_msg_t;
```

Every CAN frame — **64 bytes of actual data** — is copied as a ~64 KB struct
through `g_q_can` and again through `g_q_core`. A depth-16 queue is ~1 MB.

At the current 100 ms poll this is invisible. At a 1 ms cycle across several
modules it becomes **tens of megabytes per second of pure `memcpy`**, thrashing
the A53's L2 cache far more destructively than any scheduler jitter.

**This is the actual obstacle to a 1 ms cycle, and it is orthogonal to the
A53-vs-M7 decision.** The M7 path would inherit the same defect across RPMsg.

Structural notes:
- There is **no CAN-specific queue struct**. No FD flag, no BRS bit, no DLC
  field, no CAN ID field. The ID is smuggled through `offset`.
- Length is `len`, always the constant `ME_CAN_FRAME_LEN` = 64
  (`can_frame.h:19`) — never a DLC code.
- `can_frame.h:7-10` warns these floats are **little-endian, opposite** of every
  WebApp-facing frame.

### 6.3 Timing constants are two orders of magnitude off

`ME Project/me-primary/src/exec/step_engine.h:51-55`:

```c
#define ME_EXEC_POLL_PERIOD_MS       100u   /* vs 1 ms bus capability */
#define ME_EXEC_REALTIME_PERIOD_MS  1000u
#define ME_EXEC_RESPONSE_TIMEOUT_MS  200u   /* 3 strikes = 600 ms to detect a dead board */
#define ME_EXEC_RETRY_LIMIT            3u
```

`ME Project/me-primary/src/app_queues.h:32-35`:

```c
#define ME_SEND_TIMEOUT_CTRL_MS  50
#define ME_SEND_TIMEOUT_BULK_MS 500
#define ME_RECV_TIMEOUT_MS      200   /* the can_mgr loop's wake-up period */
```

For a 1 ms cycle, `ME_EXEC_RESPONSE_TIMEOUT_MS` needs to fall to roughly 2–5 ms,
and `ME_RECV_TIMEOUT_MS` must not gate the CAN loop.

ADR-27 (`.claude/DECISIONS.md:38-40`) records the current design intent: *"10 ms
engine tick (cutoffs) · 100 ms CAN poll · 1000 ms `0xCC` emit."*
`.claude/ARCHITECTURE.md:178` marks IF-B (CAN-FD to Secondary boards) as
**Simulated**.

### 6.4 Build and test implications

- No Makefile or CMake. PowerShell only:
  `build.ps1` (cross, target triple **`aarch64-none-linux-gnu`**, flags
  `-std=gnu11 -O2 -Wall -Wextra -Werror -I src -static -pthread`),
  `build-native.ps1` (MinGW-w64 host, protocol unit tests),
  `deploy.ps1` (scp + `docker run --network host`).
- `can_mgr.c` is **deliberately excluded** from `build-native.ps1` because it is
  Linux-only (`can_mgr.h:13`). A real SocketCAN implementation must keep the
  socket syscalls in a thin separate file so the dispatch logic remains covered
  by the host test suite.
- Static linking (`-static`) is compatible with SocketCAN — `<linux/can.h>` is a
  kernel UAPI header with no glibc NSS dependency.

---

## 7. Recommended Work Sequence

Three independent pieces, in this order:

| # | Work | Verify |
|---|---|---|
| 1 | **Shrink the CAN message path.** Either add a dedicated small CAN queue type (~80 bytes: ID, len, flags, 64-byte payload), or make `me_msgq` copy only `len` bytes. Size queue depth for up to **8 Secondaries × 2 frames = 16 frames in flight** (§2.2), not the single-frame case. | Host unit tests; measure per-message copy cost before/after |
| 2 | **Replace the `can_mgr.c` stub with real SocketCAN.** `CAN_RAW` + `CAN_RAW_FD_FRAMES` bound to `can0`; `epoll` with per-request timeout driving the existing 3-strikes logic. Socket syscalls isolated in a new `src/platform/can_socket.c`. | `candump` on the board; loopback test; existing dispatch tests still green |
| 3 | **Retune timing constants and instrument.** Add TX/RX timestamp histograms (`SO_TIMESTAMPING`) so the achieved cycle is measured, not assumed. | 24 h run under realistic load; inspect p99.9 and max |

Item 1 is a **prerequisite** for 1 ms and is entirely host-testable.

Estimated effort: roughly 1–2 days AI-assisted for all three, versus 2–4 weeks
for the M7 path — which would not fix item 1 anyway.

---

## 8. Hardware-Critical Notice

> ⚠ **HUMAN REVIEW REQUIRED — hardware-critical change.**
>
> CAN bit-timing (`bitrate`, `dbitrate`, sample points) and the retry/offline
> timing constants directly affect real-time behaviour on physical hardware.
> These require **hardware-in-the-loop testing** on the Verdin board with the
> actual Secondary boards attached. No latency or jitter figure in this document
> is trustworthy until measured there.
>
> Additionally, confirm the carrier board's CAN transceiver is rated for the
> 2 Mbit/s data phase before committing to the specified bitrates.

---

## 9. Open Questions

1. **Message struct fix:** a dedicated small CAN message type (cleaner), or a
   variable-length copy in `me_msgq` (touches fewer call sites)?
2. **Wiring:** is `can0` or `can1` connected to the Secondary boards on the
   carrier, and is the other free for a bus-analyser tap during bring-up?
3. **Transceiver rating:** confirmed 2 Mbit/s-capable?
4. **Deadline class:** is the 1 ms single-Secondary read cycle a hard deadline
   with safety consequences, or a soft throughput target? (Scope is now
   confirmed per §2.2 — this question is about the *consequence of missing it*,
   not the frame count.)
5. ~~Request-side frame cost~~ — **Answered (2026-08-18):** 2 TX query frames +
   2 RX response frames = 4 frames total per single-Secondary 8-channel read
   (§2.2).
6. **Multi-Secondary cycle target:** with up to 8 Secondaries connected, what
   is the required full-system read cycle time? §2.2 estimates ~9.3 ms
   minimum wire time for 8 boards at the confirmed flat 2 Mbps — is that
   acceptable, or does the system need a smaller payload / different bitrate
   scheme to hit a tighter number?
7. ~~Bitrate decision~~ — **Partially answered (2026-08-18):** bitrate is
   confirmed as **flat 2 Mbps, no BRS**. This still leaves the 4-frame
   single-Secondary read at ~1.16 ms — **16% over the 1 ms budget** from wire
   time alone (§2.2). **Still open:** which mitigation closes this gap —
   shrink the query frame to 12 bytes (needs a Secondary-firmware protocol
   change), switch to BRS with 1 Mbps arbitration/5 Mbps data (faster per
   frame *and* relaxes the arbitration bus-length constraint, but needs
   5 Mbps-capable transceivers confirmed on all 9 nodes), or relax the 1 ms
   target?
8. **Bus physical layer (new, follows from §2.2):** has the arbitration-phase
   bus-length constraint at flat 2 Mbps been checked against the actual
   Verdin-to-Secondary cabling and stub lengths? This is a hardware-in-the-loop
   item, not something derivable from this document.

---

## 10. References

### External

- [Linux SocketCAN documentation](https://docs.kernel.org/networking/can.html) — `CAN_RAW_FD_FRAMES`, `struct canfd_frame`, `CAN_BCM` / `CAN_FD_FRAME`
- [Toradex — How to Use CAN on Torizon OS](https://developer.toradex.com/torizon/application-development/use-cases/peripheral-access/how-to-use-can-on-torizoncore/)
- [Toradex — CAN (Linux BSP)](https://developer.toradex.com/linux-bsp/application-development/peripheral-access/can-linux/)
- [Toradex — FreeRTOS on the Cortex-M7 of a Verdin iMX8M Plus](https://developer.toradex.com/software/real-time/freertos/freertos-on-the-cortex-m7-of-a-verdin-imx8mp/)
- [Toradex — HMP RPMsg Guide](https://developer.toradex.com/software/hmp/hmp-nxp/cortexm-rpmsg-guide/)
- [NXP Community — Linux Remoteproc on i.MX8MP](https://community.nxp.com/t5/i-MX-Processors/Linux-Remoteproc-on-i-MX8MP/td-p/1585828)
- NXP AN5317 — i.MX8M heterogeneous multicore, `remoteproc` stop limitation

### Internal

| Path | Relevance |
|---|---|
| `ME Project/Ref Docs/master_slave_can_v1.0.md:56-59, :115-120, :122-138` | Bitrates, retry discipline, bus timing table |
| `ME Project/me-primary/src/threads/can_mgr.c` | The stub to replace |
| `ME Project/me-primary/src/threads/can_mgr.h:1-14` | Stub contract, Linux-only note |
| `ME Project/me-primary/src/threads/core_logic.c:102-112` | `dispatch_output()` producer |
| `ME Project/me-primary/src/proto/can_frame.c:13-16, :37-92` | ID construction and wire layout |
| `ME Project/me-primary/src/proto/can_frame.h:7-10, :19, :43-56` | Endianness warning, frame length, logical structs |
| `ME Project/me-primary/src/msg.h:29, :78-86` | The 64 KB message blocker |
| `ME Project/me-primary/src/exec/step_engine.h:51-55` | Timing constants to retune |
| `ME Project/me-primary/src/app_queues.h:32-35` | Queue timeouts |
| `ME Project/me-primary/build.ps1`, `build-native.ps1`, `deploy.ps1` | Build and deploy |
| `.claude/TASKS.md:97` | T-30 — open task for a real CAN interface |
| `.claude/DECISIONS.md:38-40` | ADR-27 — current timing design |
| `.claude/ARCHITECTURE.md:178` | IF-B marked Simulated |

---

## 11. Confirmed Board State — CAN-FD Is Currently Owned by the M7

**Date: 2026-08-18. Status: documented procedure only — nothing executed, no files
changed on the board or in this repo.**

Diagnostics run directly on the board host (SSH as `torizon`, not inside a
container) confirm the M7 core is live and holding the CAN peripheral, not a
document artifact as first suspected:

```
$ cat /proc/cmdline | grep -o 'mcore_booted=.'
mcore_booted=1
$ ls /sys/class/remoteproc/ && cat /sys/class/remoteproc/remoteproc0/state
remoteproc0
running
```

This means, contrary to the initial hope in §1 that D-02 ("which core owns
CAN") might be an undecided-on-paper-only question: **on this specific board,
right now, an M7 firmware image is loaded and running, and `ip -details link
show` on the host shows no `can0`/`can1`.** The recommendation in §1 (A53 +
SocketCAN) is unchanged — this section only documents *how* to carry it out
if and when the decision is made to reassign the peripheral.

**Developer note (2026-08-18): no move approved yet.** This section is a
reference procedure for later, not a plan being executed now.

### 11.1 Reversal procedure — M7 → A53

**1. Stop the M7 firmware from loading at boot.**
Check which path launched it:
```bash
cat /sys/class/remoteproc/remoteproc0/firmware
```
- If Linux/`remoteproc`-launched: disable whatever service or udev rule
  auto-loads it (commonly a systemd unit or early-boot script writing to
  `/sys/class/remoteproc/remoteproc0/state`).
- If U-Boot-launched (`bootaux`): clear the `cm_boot`/autostart U-Boot env
  variable so `bootaux` is never invoked.

**2. Re-enable the FlexCAN node(s) for Linux in the devicetree.**
Toradex documents CAN1/CAN2 as "Verdin Reserved" interfaces, **enabled by
default with no overlay needed** on a stock Verdin iMX8M Plus — so on this
board, something has explicitly disabled Linux's view of the node(s), most
likely an HMP/M7-reservation overlay setting `status = "disabled"`. Remove
that overlay's entry from:
```
/boot/ostree/<deployment>/dtb/overlays.txt
```
or flip the node's `status` back to `"okay"` if the change lives in a base
devicetree edit rather than an overlay.

**3. Check for an RDC (Resource Domain Controller) lock.**
If the M7 firmware programmed the RDC to hard-assign FlexCAN's memory region
to the M7 domain (rather than only a devicetree flag), a devicetree edit
alone will not be enough — Linux will fault on access even after step 2. This
cannot be ruled out until the M7 is stopped and the interface is retested.

**4. Reboot and verify.**
```bash
ip -details link show      # expect can0 / can1, "flexcan", FD-capable
```

**5. Bring the interface up and confirm on the wire.**
```bash
ip link set can0 up type can bitrate 2000000 dbitrate 2000000 fd on
candump -x can0
```

**6. Expose it to the Docker container.**
Add to `deploy.ps1`'s `docker run`, alongside the existing `--network host`:
```
--cap-add=NET_ADMIN -v /run/udev/:/run/udev/
```
Add `iproute2` and `can-utils` to the container image for debugging
(`candump`, `cansend`).

**7. Replace the `can_mgr.c` stub** with the real `SOCK_RAW` /
`CAN_RAW_FD_FRAMES` socket implementation per §3.4.

### 11.2 Still unverified

- Whether step 2 alone suffices, or an RDC lock (step 3) also needs clearing
  — undetermined until the M7 is actually stopped and retested on hardware.
- What firmware is currently running on the M7 (own team's, or a Toradex demo
  image) — `cat /sys/class/remoteproc/remoteproc0/firmware` was requested but
  not yet captured.
- Whether `can0` or `can1` is the one wired to the Secondary boards on the
  carrier (Open Question 2, §9 — still open).
