# ARCHITECTURE.md
> Updated: 2026-08-15T23:55+05:30

## System Overview

The ME Primary Board application is a single-process, **four-thread** Linux
userspace program running on a Toradex Verdin iMX8M Plus (Cortex-A53, aarch64)
under Torizon OS, inside a Docker container on the board.

It is **not** a bare-metal/RTOS target. Build is cross-compile-then-deploy, not
flash-and-debug.

Registration over TCP 9999 is complete and hardware-verified. The board is the
TCP **client**.

Current milestone: the Battery Testing Application base — four threads joined by
in-process message queues, per-circuit storage, program-chain walking, and
step-1 extraction. Built 2026-08-11, **not yet run on hardware**.

## Component Diagram

```
  Windows laptop (x86_64)                Verdin iMX8M Plus (aarch64, Torizon)
  +----------------------+               +--------------------------------------+
  | aarch64-none-linux-  |    scp        |  Docker Engine (on-board)            |
  | gnu-gcc  -static     |-------------->|   +------------------------------+   |
  |                      |               |   | container --network host     |   |
  | MinGW gcc            |               |   |   me_primary                 |   |
  |   -> unit tests only |               |   +------------------------------+   |
  +----------------------+               +--------------------------------------+
                                                    |         |          |
                                              TCP 9999   UDP 10000   UDP 10001
                                                    v         v          v
                                         +--------------------------------------+
                                         |         WEB APPLICATION              |
                                         |  command/response · live · session   |
                                         +--------------------------------------+
```

Full functional and workflow diagrams: `Docs/ME_Primary_Comm_Block_Diagram.md`.

## Layers

| Layer | Responsibility | Location |
|-------|---------------|----------|
| Entry | Argument parsing, signal handling, thread lifecycle | `src/main.c` |
| System init | Board identity, UDP sockets + destinations, registration request | `src/sys_init.c` |
| Runtime | The four queues, send/receive timeouts, the stop flag | `src/app_queues.c` |
| Messaging | The one message struct crossing every thread boundary | `src/msg.h`, `src/util/msgq.c` |
| Communication | Owns all three sockets; `poll()` over TCP + 2×UDP + queue eventfd | `src/threads/comm_thread.c` |
| Core Logic | Test state, program assembly, step-1 extraction, execution seam | `src/threads/core_logic.c` |
| Data Manager | The only caller of storage; serves programs in chunks; session ring | `src/threads/data_mgr.c` |
| CAN Manager | **Simulated** SET/READ_VALUES responder — no physical interface (ADR-27) | `src/threads/can_mgr.c` |
| Execution engine | Pure per-circuit step state machine, clock injected (ADR-27) | `src/exec/step_engine.c` |
| Step decode | SET/CCChg/STOP step-body decode, big-endian | `src/proto/step_decode.c` |
| CAN framing | 64-byte CAN-FD frame pack/parse, **little-endian** | `src/proto/can_frame.c` |
| Post-registration | One-shot `0xCC` after registration — **permanent**, not scaffolding | `src/threads/post_reg.c` |
| Storage | CircuitID→slot mapping, per-circuit program / **40-byte battery record** / config | `src/store/circuit_store.c` |
| Admission control | Which circuits may be handled at all (ADR-17) | `src/store/circuit_registry.c` |
| Reply layout | The one 7-byte ack for `0xAA`/`0xBB`/`0xEE` — **documented, not inferred, since ADR-20** | `src/proto/ack_frame.c` |
| Frame framing | Where a frame ends — layout table incl. **`0xAA`** (ADR-22), **CRC as backstop** (ADR-19 as amended) | `src/proto/frame_router.c` |
| Transport | TCP client and UDP socket mechanics | `src/net/` |
| Protocol | Frame layout, pack/parse, CRC, chain walker — **pure, no I/O** | `src/proto/` |
| Platform | Board's own IP and MAC via ioctl | `src/platform/` |
| Utility | Timestamped logging, hex dump | `src/util/` |

## Threading Model

**Five threads: main plus four workers.** One inbox queue per worker, so each
has exactly one place to wait and its main loop is a single receive plus a
`switch`.

```
                    WEB APPLICATION
       TCP 9999      UDP 10000      UDP 10001
           ^             ^              ^
           +-------------+--------------+
                         |
              +----------+-----------+
              | COMMUNICATION THREAD |
              | poll(): tcp, udp x2, |
              |         q_comm.evtfd |
              +--+----------------+--+
                 |                |          ^
          q_data |         q_core |          | q_comm
                 v                v          |
        +--------+-----+  +-------+------+   |
        | DATA MANAGER |  |  CORE LOGIC  |---+
        | g_program[64]|<-| resident prog|
        | session ring |->| buffers      |
        +--------------+  +------+-------+
                                 | q_can
                                 v
                        +--------+-------+
                        | CAN MGR (stub) |
                        +----------------+
```

- **main** — initialises, spawns all four, blocks in `pthread_join`, then
  reports queue counters and closes sockets. Signal handlers set a flag only.
- **communication** — owns the TCP connection and both UDP sockets. `q_comm`
  carries an **`eventfd`** so this thread can wait on three sockets *and* its
  queue in one `poll()` (ADR-11).
- **core logic** — its queue receive timeout doubles as the demo emitter's 1 Hz
  clock; no timer thread, no `timerfd` (ADR-15).
- **data manager** — the only caller of `circuit_store.c`. Storage is therefore
  single-threaded by construction and needs no mutex.
- **can manager** — runs, receives, logs. No CAN interface opened.

Communication state machine: `CONNECTING → REGISTERING → IDLE`, returning to
`CONNECTING` on failure with backoff doubling from 1 s to 30 s. (`REGISTERED`
and `MONITORING` were collapsed into `IDLE` in session #3 — ADR-10.)

**Every queue send takes a finite timeout and drops on expiry** (50 ms control,
500 ms bulk, 200 ms receive). Deadlock is impossible by construction, and
congestion becomes a counted, logged loss visible in the `queues:` lines at
shutdown (ADR-12).

No dynamic allocation on any path. All buffers are fixed-size static or struct
members.

## Request Flow — registration

1. `main` parses `--server` and installs `SIGINT`/`SIGTERM` handlers
2. `sys_init` reads the board's IP and MAC, opens UDP 10000 and 10001, and
   builds `me_reg_request_t` once
3. Comm thread connects to `<server>:9999`
4. `reg_frame` packs 33 bytes; `log` hex-dumps them; `tcp_client` sends
5. `tcp_client` waits for 7 bytes, then a short settle to detect over-long replies
6. `reg_frame` validates length, start byte, query ID and CRC, then reads `Value`
7. `Value == 0x01` **or `0x02`** → circuit added to the admission registry, banner
   printed, state `IDLE` (ADR-10)
8. **Permanent (ADR-23/27):** one 86-byte `0xCC` frame for that circuit is
   queued to `g_q_comm` (`post_reg.c`) and leaves on UDP 10000 — `step 1`,
   `25.0 °C`, everything else zero. The Web Application therefore has live
   data before any program is transferred. Fires on every successful
   registration, reconnects included.

## Request Flow — Start a battery test

> **Step 0 — admission control (ADR-17).** Every inbound frame below is dropped
> by `route_frame()` unless its CircuitID is in the registry. Only the circuit
> that completed `0xDD` registration is admitted, so **the flow below runs only
> for the circuit passed via `--secondary`/`--channel`** (default `0x11`). A
> frame for any other circuit produces a WARN naming it and goes no further.

1. Web App sends `0xBB` **Q1 is-ready**; the comm thread answers it itself with a
   7-byte `0x01` ack and involves no other thread (ADR-18). A **Q3** packet count,
   if sent, is acked the same way and its count logged, then discarded
2. Web App sends `0xBB` Q4 program packets; `frame_router` classifies each, the
   comm thread verifies its CRC, routes it to `q_data`, then acks it — `0x01`
   meaning **queued to the Data Manager**, `0x00` on a bad CRC or a full queue
3. Data Manager appends **contiguously from offset 0** and walks the chain after
   every packet; `nextIndex == 0xFFFFFFFF` marks the program complete (ADR-14)
4. Web App sends `0xEE` Q1 Start — **10 bytes, carrying a 4-byte Session ID**
   (undocumented until hardware exposed it, 2026-08-12) → CRC verified, routed to
   `q_core`, acked; Core Logic logs the Session ID
5. Core Logic issues `REQ_PROGRAM` + `REQ_BATTERY` to the Data Manager
6. Data Manager replies with `RSP_PROGRAM_CHUNK` messages, last one flagged
   `ME_MSG_FLAG_LAST`; Core Logic verifies every chunk's absolute `offset`
7. Core Logic extracts step 1 with `me_chain_fetch_step()`, prints the banner and
   hex dump, then calls `me_execute_program()`
8. `me_execute_program()` starts a real `step_engine` instance for the circuit
   (ADR-27): decodes `SET`→`CCChg`→`STOP` via `step_decode`, sends a CAN-FD
   `SET_VALUES` frame (`can_frame`, little-endian) to `q_can`, polls
   `READ_VALUES` every 100ms, emits a real `0xCC` every 1000ms, evaluates the
   TIME cutoff every 10ms tick. `can_mgr.c` **fabricates** the Secondary's
   response — no physical CAN device exists yet. On cutoff: `CMD_STO` +
   final Idle `0xCC`, circuit → `ME_EXEC_STOPPED`

## External Interfaces

| Interface | Peer | Transport | Status |
|-----------|------|-----------|--------|
| IF-A | Web Application | TCP 9999 | Registration ✅ hardware-verified; `0xAA`/`0xBB`/`0xEE` parsed and routed (not hardware-verified). **Gated per-circuit — 1 of 64 admitted until CAN registration lands (ADR-17)**. **Bidirectional since #7:** `0xBB` Q1/Q3/Q4, `0xAA` Q5 and all six `0xEE` are answered (ADR-18). `0xAA` fully specified since #9 (ADR-20); Q1–Q4/Q6 recognised but **unanswered** — they owe real data, not a yes/no |
| IF-A | Web Application | UDP 10000 | **Live data now from real step execution** (ADR-27) — `step_engine` emits every 1000ms. One `0xCC` also leaves here per successful registration, now **permanent** (`post_reg.c`) |
| IF-A | Web Application | UDP 10001 | Session path built end to end; nothing produces records yet (T-29) |
| IF-B | Secondary boards | CAN-FD | **Simulated** (ADR-27) — `can_mgr.c` fabricates `SET_VALUES`/`READ_VALUES` responses; no physical interface opened (T-30) |
| IF-D | Modbus client | RS-485 / TCP | Not implemented |

## Known Constraints

- **Static linking** (ADR-2) forbids `getaddrinfo`; addresses are dotted-quad
  literals (ADR-8)
- **`--network host` is mandatory** — the frame carries the board's IP/MAC as
  payload (ADR-6)
- **`src/proto/` must stay platform-free** or the host test suite breaks (ADR-7)
- **`-std=gnu11`**, not `-std=c11` — `struct ifreq` and `IFF_*` need
  `_DEFAULT_SOURCE`
- **CRC goes out big-endian, high byte first** — `ME_CRC_ORDER_DEFAULT` in
  `src/proto/crc16.h` is the only place that decides (ADR-9, supersedes ADR-5).
  ✅ Verified against the real server in both directions, 2026-08-10.
- **Registration succeeds on `0x01` or `0x02`, then holds `ME_COMM_IDLE`**
  (ADR-10). The frame is sent once per TCP connection, structurally.
- **`.bss` is 261 MB** (`NOBITS`) — 64 circuits × 2 MB in the Data Manager plus
  the same mirrored in Core Logic, at `ME_PROGRAM_BUF_SIZE`. Linux reserves
  demand-zero pages rather than committing them and the binary stays 4.1 MB, but
  **Docker memory limits count RSS** — verify with `docker stats` (T-27).
- **CircuitID nibbles are 1-based**; a malformed ID is rejected, never folded to
  slot 0 (ADR-13).
- **`me_msgq_t` is ~1 MB** — never a local; all four are file-scope in
  `app_queues.c` (ADR-11).
- **Most frames carry no length field.** `me_frame_expected_len()` is therefore a
  table of *documented assumptions*, and one was wrong on hardware (`0xEE` Q1
  Start), which desynced every following frame rather than just failing its own.
  **The CRC is the delimiter of last resort** (ADR-19): layout → whole read →
  shortest-first scan, with a WARN when a real table entry proves wrong.
  `0xAA` gained real entries in ADR-22 (Q1–Q4 = 6, Q5 = 46, Q6 = 10), so it now
  splits by layout rather than by scan. ⚠️ **The whole-read step must not be gated
  on `layout == 0`** — it was, and adding the correct Q5 entry then made a *short*
  `0xAA` frame truncate. See the ADR-19 amendment.
- **The `0xAA` battery payload is 40 bytes, and all 12 fields are stored per
  circuit** (ADR-21). It was 22 until 2026-08-12, with the remaining 18 bytes
  silently discarded on every packet. A payload under 40 is now refused and NACKed.
  **Impedance and Energy Density are `float`** — confirmed by the developer
  2026-08-12, matching what the parser already did; the spreadsheet's
  integer-looking samples are wrong (T-47 closed).
- **`0xAA` Q7–Q10 are broadcast frames with a 2-byte header.** Every `0xAA` path
  assumes 4 bytes; such a frame is read as circuit `0x01`, which is malformed, so
  admission control drops it. Correct outcome, wrong reason (T-48).
- No file exceeds ~460 lines; the 2000-line project limit is not near
