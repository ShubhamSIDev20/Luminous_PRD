# ME Primary — Implementation Reference

> **Read this instead of the codebase.** One entry per source file: what it is
> responsible for, its public interface, how it actually works inside, what it
> depends on, and what will bite you.
>
> Companion documents:
> - `ME_Primary_Comm_Block_Diagram.md` — registration over TCP 9999
> - `ME_Primary_BTS_Block_Diagram.md` — the 4-thread design this implements
> - `.claude/DECISIONS.md` — the ADR log; read before changing frame layout,
>   CRC, toolchain, linking or the `docker run` line
>
> Last updated: 2026-08-12, after the 40-byte battery record, the `0xAA`
> frame-length table and the post-registration demo frame (#9). Previously: the
> `0xEE` Session ID fix and CRC-derived frame boundaries (#8).
> Verified: 158 host unit checks pass; static `ELF64 AArch64` builds clean under
> `-Werror`. **Not yet run on hardware.**

---

## 1. How to read this document

Each entry has the same five headings:

| Heading | What it tells you |
|---|---|
| **Does** | The one responsibility. If you cannot state it in a sentence, the file is wrong. |
| **Interface** | What other files may call. Everything else is `static`. |
| **Inside** | The logic that is not obvious from the signatures. |
| **Depends on** | What it includes. Cycles here are a design smell. |
| **Gotchas** | What has already gone wrong, or would. |

Two classifications appear throughout:

- **PURE** — no sockets, no threads, no platform headers. Built by
  `build-native.ps1` and covered by host unit tests.
- **LINUX-ONLY** — built only by `build.ps1`, proven only on hardware.

---

## 2. Build and run

```powershell
.\build-native.ps1    # 158 host unit checks - protocol logic only
.\build.ps1           # static aarch64 ELF -> bin\me_primary
.\deploy.ps1 -BoardIP <board-ip> -ServerIP <web-app-ip>
```

**Only `deploy.ps1` proves the system works.** A passing native build exercises
zero aarch64 codegen, zero Torizon, zero Docker and zero sockets (ADR-3).

Toolchain paths are resolved from `.embedded-override.json`, never from `PATH`.

---

## 3. The map

```
   main.c
     |
     +-- sys_init.c ....... board identity, 3 sockets, 2 UDP destinations
     +-- app_queues.c ..... 4 queues + the stop flag
     +-- circuit_store.c .. 128 MB of per-circuit storage
     +-- circuit_registry.c  which circuits may be handled at all
     +-- ack_frame.c ...... the one 7-byte reply shape (0xAA/0xBB/0xEE)
     +-- demo_realtime.c .. the temporary emitter
     |
     +-- 4 threads, started consumers-first:
           data_mgr.c  ->  can_mgr.c  ->  core_logic.c  ->  comm_thread.c
                                                            (LAST, on purpose)

   Queues (one inbox per thread):
     g_q_data  <- comm            STORE_PROGRAM/BATTERY/CONFIG
               <- core            REQ_PROGRAM/REQ_BATTERY, SESSION_DATA
     g_q_core  <- comm            CONTROL
               <- data            RSP_PROGRAM_CHUNK/RSP_BATTERY/RSP_NOT_FOUND
     g_q_comm  <- core            REALTIME_DATA         (POLLABLE: eventfd)
               <- data            SESSION_DATA
     g_q_can   <- core            CAN_TX
```

---

## 4. Entry and runtime

### `src/main.c` — LINUX-ONLY

**Does.** Parse the command line, install signal handlers, initialise
everything in order, start four threads, join them, tear down.

**Interface.** `main()` only.

**Inside.** The ordering is the whole point of this file.

```
  parse args -> log level -> signal handlers
  me_sys_init()      board IP/MAC, UDP sockets, UDP destinations, reg request
  me_queues_init()   4 queues; g_q_comm gets an eventfd
  me_store_init()    metadata only - NOT the 128 MB
  me_demo_init()     device id + CRC order for the emitter
  start: data_mgr -> can_mgr -> core_logic -> comm_thread   <-- comm LAST
  join:  comm -> core_logic -> can_mgr -> data_mgr
  me_queues_report() -> me_queues_destroy() -> me_sys_shutdown()
```

**Depends on.** `app_queues.h`, `sys_init.h`, `store/circuit_store.h`, and all
four thread headers under `threads/`.

**Gotchas.**
- **The communication thread starts LAST.** It is the only source of inbound
  frames. Starting it first means a frame can be routed to a queue nobody is
  draining yet.
- `me_system_t sys` is `static`, not a local. Four threads hold a pointer to it.
- The signal handler calls `me_app_request_stop()` and nothing else. Every
  thread polls that flag, so one write stops the process.
- `SIGPIPE` is ignored: a peer vanishing mid-send must not kill PID 1.

---

### `src/sys_init.c/.h` — LINUX-ONLY

**Does.** Everything that happens once at power-on: read the board's own
network identity, open both UDP sockets, build both UDP destinations, and build
the registration request.

**Interface.**
```c
typedef struct { char server_ip[16]; uint16_t server_port; char iface[];
                 uint8_t device_id, secondary, channel; char device_name[];
                 me_crc_order_t crc_order; int connect_timeout_ms,
                 response_timeout_ms; bool verbose; } me_config_t;

typedef struct { me_config_t cfg; me_netinfo_t net;
                 int udp_live_fd, udp_session_fd;
                 struct sockaddr_in udp_live_dest, udp_session_dest;
                 me_reg_request_t reg_request; } me_system_t;

void me_config_defaults(me_config_t *cfg);
bool me_sys_init(me_system_t *sys, const me_config_t *cfg);
void me_sys_shutdown(me_system_t *sys);
```

**Inside.** The TCP socket is deliberately **not** opened here — the
communication thread owns it, because it must be able to reconnect.

**Depends on.** `net/udp_sock.h`, `platform/netinfo.h`, `proto/reg_frame.h`,
`util/log.h`.

**Gotchas.**
- **`me_config_t` is the COMMAND-LINE config.** The per-circuit stored
  configuration is `me_circuit_config_t` in `store/circuit_store.h`. Two
  different things; do not merge the names.
- `udp_live_dest` and `udp_session_dest` were added in this milestone. Before
  it, both sockets were open but had nowhere to send — invisible while nothing
  was sent, and the first thing that would have failed on the demo.
- Both UDP destinations use `cfg.server_ip`, the same host as TCP. If the Web
  Application ever listens for UDP elsewhere, that needs a new argument.

---

### `src/app_queues.c/.h` — LINUX-ONLY

**Does.** Owns the four queue instances and the process-wide stop flag.

**Interface.**
```c
extern me_msgq_t g_q_comm, g_q_core, g_q_data, g_q_can;

#define ME_SEND_TIMEOUT_CTRL_MS  50
#define ME_SEND_TIMEOUT_BULK_MS 500
#define ME_RECV_TIMEOUT_MS      200

bool me_queues_init(void);
void me_queues_destroy(void);
void me_queues_report(void);
void me_app_request_stop(void);
bool me_app_stop_requested(void);
```

**Inside.** `g_q_comm` is the only queue initialised `pollable`, because its
consumer is already blocked in `poll()` on three sockets.

**Depends on.** `util/msgq.h`, `util/log.h`. Nothing else — deliberately, so
every thread can depend on this without a cycle.

**Gotchas.**
- The stop flag lives here, not in `comm_thread.c`, so that the Data Manager
  does not have to include the communication thread's header to learn it should
  exit. `me_comm_thread_request_stop()` still exists as a thin wrapper.
- `me_queues_report()` at shutdown is the first place to look when a frame went
  missing: a non-zero drop count is logged at WARN.

---

### `src/msg.h`, `src/msg.c` — PURE

**Does.** Defines the single message struct that crosses every thread boundary,
plus printable names for its enums.

**Interface.**
```c
#define ME_MSG_PAYLOAD_MAX (64u * 1024u)
#define ME_MSG_FLAG_LAST   0x01u

typedef enum { ME_MSG_NONE, ME_MSG_STORE_PROGRAM, ME_MSG_STORE_BATTERY,
               ME_MSG_STORE_CONFIG, ME_MSG_CONTROL, ME_MSG_REQ_PROGRAM,
               ME_MSG_REQ_BATTERY, ME_MSG_RSP_PROGRAM_CHUNK, ME_MSG_RSP_BATTERY,
               ME_MSG_RSP_NOT_FOUND, ME_MSG_REALTIME_DATA, ME_MSG_SESSION_DATA,
               ME_MSG_CAN_TX, ME_MSG_CAN_DATA, ME_MSG_TYPE_COUNT } me_msg_type_t;

typedef enum { ME_STATUS_OK, ME_STATUS_NO_PROGRAM, ME_STATUS_PROGRAM_INCOMPLETE,
               ME_STATUS_PROGRAM_INVALID, ME_STATUS_NO_BATTERY,
               ME_STATUS_BAD_CIRCUIT, ME_STATUS_COUNT } me_msg_status_t;

typedef struct { me_msg_type_t type; uint8_t circuit_id, flags; uint16_t status;
                 uint32_t offset, len; uint8_t payload[ME_MSG_PAYLOAD_MAX]; }
        me_msg_t;

const char *me_msg_type_name(me_msg_type_t t);
const char *me_msg_status_name(me_msg_status_t s);
```

**Gotchas.**
- One struct, discriminated by `type` — not a union per direction. A fixed slot
  size is what lets every queue be a static array with no allocation.
- `status` is overloaded on `STORE_CONFIG`: it carries the **query ID**, so the
  raw config payload can be stored against the query that delivered it.
- `offset` is overloaded on `RSP_NOT_FOUND`: it carries the **message type that
  failed**, so Core Logic can tell a missing program from a missing battery.

---

## 5. Threads

### `src/threads/comm_thread.c/.h` — LINUX-ONLY

**Does.** Owns all three sockets. Connects, registers, then holds the
connection and moves frames in both directions.

**Interface.**
```c
typedef enum { ME_COMM_CONNECTING, ME_COMM_REGISTERING, ME_COMM_IDLE,
               ME_COMM_STOPPED } me_comm_state_t;
const char *me_comm_state_name(me_comm_state_t s);
bool me_comm_thread_start(me_system_t *sys);
void me_comm_thread_request_stop(void);
bool me_comm_thread_stop_requested(void);
void me_comm_thread_join(void);
bool me_comm_is_registered(void);
```

**Inside.** Seven internal pieces beyond the state machine:

| Function | Role |
|---|---|
| `do_registration()` | Sends the 33-byte `0xDD` frame, evaluates the reply. Called **once per connection**. |
| `consume_rx_buffer()` | Peels complete frames off the reassembly buffer. |
| `route_frame()` | Classifies one frame, **drops it unless its circuit is registered**, answers it or sends it to `g_q_core` / `g_q_data`. |
| `frame_crc_ok()` | Verifies a whole inbound frame's CRC. Prints the computed-vs-carried arithmetic on mismatch, like `do_registration()` does. |
| `send_prg_ack()` | Puts a 7-byte `0xBB` acknowledgement on the TCP socket. |
| `handle_program_handshake()` | Answers `0xBB` Q1 (is-ready) and Q3 (packet count). |
| `drain_outbound()` | Acks the eventfd and sends every queued frame on its UDP port. |

**`route_frame()` takes the socket fd**, and `consume_rx_buffer()` threads it
through. That is new as of session #7 and it is the structural consequence of the
board answering frames rather than only consuming them: until then, the
registration request was the *only* thing this program ever wrote to TCP.

`idle_loop()` polls **four** descriptors: TCP, UDP 10000, UDP 10001, and
`me_msgq_fd(&g_q_comm)`.

**TCP reassembly.** TCP is a stream: two frames can arrive in one `read()` and
one frame can arrive split across two. Bytes accumulate in `s_rxbuf` and
`me_frame_expected_len()` decides where each frame ends. When it returns 0 —
which it does for `0xAA`, because that frame type **carries no length field
anywhere in the protocol** — the remainder of the read is treated as one frame,
which is what the old firmware did for every type.

**Admission control.** `route_frame()` drops any frame whose CircuitID is not
in the registry, **before** it reaches a queue or earns a reply — so no consumer
thread repeats the check and no future message type can bypass it.
`do_registration()` is the only writer: on a success reply (`0x01` Registered or
`0x02` Already Registered) it marks its own configured Secondary/Channel.
Because the `0xDD` frame carries **one** CircuitID and is sent **once per
connection**, exactly 1 of the 64 circuits is registered today and the other 63
are refused. See `store/circuit_registry.h` and Open Item 5.

Order inside `route_frame()`, and each step's reason:

| # | Step | Why here |
|---|---|---|
| 1 | classify | a frame that cannot be parsed has no CircuitID to gate on |
| 2 | `0xDD` filtered out | the one valid frame with **no CircuitNumber field**; gating it would report a nonexistent "circuit 0x00" |
| 3 | **admission control** | ahead of step 4 on purpose: answering "ready" for an unregistered circuit would be a false claim about hardware this board has no record of |
| 4 | `0xBB` Q1/Q3 answered | no side effect anywhere else, so this thread owns them outright |
| 5 | `ME_MSG_NONE` dropped | recognised, unimplemented; logged by name |
| 6 | CRC verified for anything acked | an ack asserts the bytes were good, so they get checked — **`0xEE` included since #8**, see below |
| 7 | queued, then acked | the ack means **queued to the owning thread** — see below |

**Answering the Web Application (sessions #7–#8).** All replies are the same
7-byte frame from `me_ack_pack()`, which echoes the inbound start byte:

| Query | Reply | Meaning |
|---|---|---|
| `0xBB` Q1 is-ready | `BB dev ckt 01 01` + CRC | unconditional today: the Data Manager accepts a program at any time and a new one replaces the old (ADR-14) |
| `0xBB` Q3 packet count | `BB dev ckt 03 01` + CRC | count is **logged, then discarded** — completion comes from the chain terminator |
| `0xBB` Q4 program packet | `BB dev ckt 04 01/00` + CRC | `0x01` = queued to the Data Manager; `0x00` = bad CRC or queue full |
| `0xAA` Q5 battery write | `AA dev ckt 05 01/00` + CRC | the data really is stored, so success is honest. ⚠️ **shape inferred — there is no `0xAA` document** |
| `0xEE` all six commands | `EE dev ckt qq 01/00` + CRC | documented at `bm_control_v3.0.md`'s "Response (OK)" |

**Not acked: `STORE_CONFIG`.** The `0xAA` read queries (Q2 factory, Q3
manufacturing, Q4 battery) owe the Web Application actual *data* back, not a
yes/no, and this milestone only stashes their bytes unparsed. A bare "success"
would claim a read that never happened.

The ack's meaning has a real limit: **"queued", not "done".** The store or the
command executes on another thread and the comm thread never learns its outcome, so
a full program buffer is reported by the Data Manager's own log line and whether a
test actually started by Core Logic's.

**Why `0xEE` is CRC-checked here even though Core Logic re-verifies.** ADR-18
originally exempted it, reasoning that `me_control_parse()` would catch a bad CRC.
Code review found that wrong: the ack is sent from `route_frame()` *synchronously*,
while Core Logic parses later on its own thread and has **no path back to the
socket** — so a corrupt `0xEE` frame was told `0x01` OK and then silently dropped.
Two CRCs over the same bytes cannot disagree. Residual gap: an unrecognised QueryID
is still acked before Core Logic rejects it (Open Item 13).

**Depends on.** `app_queues.h`, `net/tcp_client.h`, `net/udp_sock.h`,
`proto/frame_router.h`, `proto/program_frame.h`, `store/circuit_registry.h`,
`sys_init.h`.

**Gotchas.**
- **The registration frame is never re-sent from `idle_loop()`.** "Register once
  per connection" is structural, not flag-based (ADR-10).
- **The registry is NOT cleared on disconnect.** Deliberate: no frame can
  arrive without a connection, and `idle_loop()` is only entered after
  registration succeeds, so there is no window where an unregistered frame is
  accepted. Clearing would also wipe CAN-registered circuits on an unrelated
  TCP blip once that writer exists.
- `drain_outbound()` runs on **every** poll iteration including the timeout
  path. A send that raced the `poll()` must not wait for the next wake-up, or a
  1 Hz emitter stutters.
- `s_rxlen` is reset on every new connection. Leftover bytes from a dropped
  connection would corrupt the first frame of the next one.
- **"The TCP connection keeps disconnecting" is usually this board closing it.**
  `me_tcp_close()` runs unconditionally after `do_registration()` returns false.

---

### `src/threads/core_logic.c/.h` — LINUX-ONLY

**Does.** Owns test state. Handles control commands, assembles programs, and
extracts step 1.

**Interface.** `bool me_core_logic_start(const me_system_t *sys);`
`void me_core_logic_join(void);`

**Inside.** The main loop is three lines and they carry the design:

```c
const int timeout = me_demo_ms_until_next_tick();   /* this IS the 1 Hz timer */
if (me_msgq_recv(&g_q_core, &s_rx, timeout)) handle(&s_rx);
me_demo_service();
```

The queue receive already had to be bounded so a stop is noticed promptly.
Shortening that bound when a frame is due turns the same wait into the emitter's
clock — no timer thread, no `timerfd`.

**Start flow.** `CONTROL`/Start → `REQ_PROGRAM` + `REQ_BATTERY` → chunks
assembled into `g_cl_program[slot]` with every `offset` verified → step 1
hex-dumped with a banner → `me_execute_program()`.

**Depends on.** `app_queues.h`, `proto/control_frame.h`,
`proto/program_chain.h`, `store/circuit_store.h`, `demo_realtime.h`,
`sys_init.h`.

**Gotchas.**
- **`me_execute_program()` is the seam.** Its entire body is
  `me_demo_arm(circuit_id)`. When real execution lands, that call is replaced
  and `demo_realtime.c` is deleted. Nothing else in Core Logic knows the demo
  exists.
- `g_cl_program` is a second 128 MB, mirroring the Data Manager. That is
  deliberate — the step walker runs against a buffer this thread owns.
- **Battery data is optional.** `RSP_NOT_FOUND` for a battery logs a warning and
  continues; only a missing program abandons the Start.
- A chunk with an unexpected `offset` abandons the transfer and zeroes the
  length. The alternative is running a program with a hole in it.
- A fresh Start **restarts** a stopped circuit, on purpose, so the flow can be
  re-demonstrated without restarting the application.

---

### `src/threads/data_mgr.c/.h` — LINUX-ONLY

**Does.** Owns all persistent storage. Nothing else calls `circuit_store.c`.

**Interface.** `bool me_data_mgr_start(void);` `void me_data_mgr_join(void);`
`ME_SESSION_RING_DEPTH` 64, `ME_SESSION_FRAME_MAX` 256.

**Inside.**

| Handler | Behaviour |
|---|---|
| `STORE_PROGRAM` | Append contiguously; completion re-checked after every packet. |
| `STORE_BATTERY` | Parse all 12 fields then store per circuit. **A payload under 40 bytes is REJECTED** with a WARN naming both lengths and the document; one over 40 stores what it knows and WARNs that the protocol moved. All 12 stored fields are logged over two lines. |
| `STORE_CONFIG` | Stored raw against its query ID; nothing parses it yet. |
| `REQ_PROGRAM` | Serve in `ME_MSG_PAYLOAD_MAX` chunks, last flagged `ME_MSG_FLAG_LAST`. |
| `REQ_BATTERY` | Serve, or `RSP_NOT_FOUND`. |
| `SESSION_DATA` | Push to the ring; `ring_flush()` forwards it to `g_q_comm`. |
| `CAN_DATA` | Logged at DEBUG; no handler yet. |

`ring_flush()` runs every iteration including the timeout path, so a record that
could not be forwarded is retried without needing a new message to arrive.

**Depends on.** `app_queues.h`, `proto/battery_frame.h`,
`store/circuit_store.h`.

**Gotchas.**
- The session ring stores **fixed 256-byte records**, not `me_msg_t`. A ring of
  64 messages would be 4 MB for frames that are ~90 bytes.
- Ring overflow drops the **oldest**. Session data is a running log; losing the
  oldest beats refusing the newest.
- An incomplete program logs both possible causes explicitly — still arriving,
  or the final step is missing its terminator — because the second is invisible
  otherwise.
- **This is where battery data becomes per-circuit state.** `me_store_battery_set()`
  keys on the same CircuitID→slot mapping the program buffers use, so battery and
  program for a Secondary/Channel always land in the same slot. The store holds
  `me_battery_t` **by value**, which is why widening it from 7 to 12 fields on
  2026-08-12 required no change in `circuit_store.c` at all.
- Rejecting a short battery payload is a **behaviour change** as of 2026-08-12: it
  used to log and discard, which read the same in the log but is now paired with a
  `0x00` ack to the Web Application from `route_frame()`.

---

### `src/threads/can_mgr.c/.h` — LINUX-ONLY

**Does.** Transports CAN-FD frames between Core Logic and the M7 core over
RPMsg (`Ref Docs/RPMSG_PROTOCOL.md`). Wraps outbound `ME_MSG_CAN_TX` into an
RPMsg SET frame and writes it to `/dev/ttyRPMSG30`; turns the M7's streamed
GET_FRAME replies back into `ME_MSG_CAN_DATA` for Core Logic. No CAN
controller is touched here — that is the M7 firmware's job.

**Interface.** `bool me_can_mgr_start(void);` `void me_can_mgr_join(void);`

**Inside.** One `poll()` loop over two sources: `g_q_can`'s eventfd and the
RPMsg device fd. Queue side wraps and writes; device side reassembles the byte
stream (`rpmsg_frame.c`) and dispatches zero-length ACKs to counters or
80-byte `GET_FRAME` payloads to `handle_stream_frame()`. An
`s_active_ch[secondary]` bitmask, set on successful TX, is what lets an
inbound frame's CAN ID (Secondary only) be attributed to a full CircuitID
(Secondary + channel) on the way back.

**Gotchas.**
- **The active-channel bitmask is the only channel-attribution mechanism.**
  Block 1 (channels 1-4) and Block 2 (5-8) share one CAN ID
  (`master_slave_can_v1.0.md`); if a Secondary ever has channels active in
  both blocks, an inbound frame becomes ambiguous by ID alone. Unreachable
  while only channel 1 runs — `can_mgr.c` logs a warning rather than
  misattributing silently if that changes.
- **ACKs are asynchronous.** A SET is written and the thread moves on
  immediately; the ACK arrives later in the same read loop. Blocking on a
  round trip per frame does not fit the timing budget at 8 Secondaries.
- **A missing `/dev/ttyRPMSG30` degrades, it does not abort.** The thread logs
  once, keeps draining `g_q_can`, and counts every dropped TX — registration
  and Web Application traffic stay up so the board is still diagnosable with
  the M7 down.
- This suggestion requires hardware-in-the-loop testing to verify — see
  `Docs/specs/2026-08-18-rpmsg-can-transport-design.md` section 8.

---

### `src/threads/demo_realtime.c/.h` — LINUX-ONLY — **TEMPORARY**

**Does.** Two unrelated pieces of scaffolding, both stand-ins for the CAN Manager:

1. **The 1 Hz emitter** — one synthetic `0xCC` frame per second for 60 seconds
   after an `0xEE` Q1 Start, so the whole loop can be seen working.
2. **The post-registration frame** (added 2026-08-12) — **one** `0xCC` frame
   immediately after a circuit's device registration succeeds, because the emitter
   above needs a Start and until then the live path is silent.

**Interface.**
```c
#define ME_DEMO_REALTIME 1        /* set to 0 to compile it out entirely */
#define ME_DEMO_TICK_MS       1000u
#define ME_DEMO_DURATION_MS  60000u
#define ME_DEMO_CURRENT_STEP    1.0f
#define ME_DEMO_VOLTAGE        48.0f
#define ME_DEMO_TEMPERATURE    25.0f

typedef enum { ME_TEST_IDLE, ME_TEST_RUNNING, ME_TEST_STOPPED } me_test_state_t;

void me_demo_init(uint8_t device_id, me_crc_order_t crc_order);
void me_demo_arm(uint8_t circuit_id);
void me_demo_disarm(uint8_t circuit_id, bool send_idle_frame);
me_test_state_t me_demo_state(uint8_t circuit_id);
int  me_demo_ms_until_next_tick(void);
void me_demo_service(void);
void me_demo_send_post_registration(uint8_t circuit_id);   /* <- COMM thread */
```

**Inside.** `Current` ramps `tick * 1.0` A; `Power` is `48.0 * Current`;
everything else is fixed or zero. Tick 60 sends `program_running = 0` and
`circuit_status = 0`, then the circuit becomes `ME_TEST_STOPPED`.

**Gotchas.**
- **DELETE THIS FILE** when real execution lands. It is one file behind one
  macro precisely so removal is a deletion, not an untangling. Two references
  will then fail to compile, which is intended: the call + `#include` in
  `comm_thread.c`, and `me_realtime_pack_post_registration()` in
  `realtime_frame.c/.h`.
- ⚠️ **`me_demo_send_post_registration()` runs on the COMMUNICATION thread.**
  Every other function here runs on Core Logic's. That is why it has its **own**
  message buffer, `s_tx_postreg`; sharing the emitter's `s_tx` would be a data
  race against `me_demo_service()`. **Nothing else in this module is safe to call
  from two threads.**
- It returns `void` and logs its own outcome deliberately: it is called at the end
  of `do_registration()`, and a queue-full drop must not make a successful
  registration look failed.
- It fires on **every** successful registration, so a reconnect sends another
  frame. Its byte layout is in `realtime_frame.c` (pure, host-tested), not here.
- `s_device_id` and `s_crc_order` come from `me_demo_init()`, which `main()` calls
  at `main.c:195` — **before any thread starts**, so there is no initialisation
  race with the Communication thread's first registration.
- Deadlines are **absolute** (`next_tick_ms += ME_DEMO_TICK_MS`), not
  `now + 1000`. The second form accumulates scheduling delay across 60 ticks.
- Frames use the **control** send timeout, not the bulk one. Real-time data is
  only meaningful while fresh; dropping is better than queueing behind stale
  frames.
- `Current` was chosen as the moving field because it is payload offset 17 of a
  spec the Web Application already renders — a moving number needs no change on
  their side to observe.

---

## 6. Protocol — all PURE, all host-tested

### `src/proto/proto_defs.h`

**Does.** The single source of truth for every offset, size, port and
identifier. **No protocol magic number may appear anywhere else.**

**Contains.** Ports; command groups (`0xDD 0xAA 0xBB 0xCC 0xEE 0xA0`); the
common 4-byte header offsets; query IDs for every group; registration request
and response layouts; `ME_CIRCUIT_ID`/`_SECONDARY`/`_CHANNEL`; the step-chain
constants; and the capacity macros.

**Gotchas.**
- **`0xDD` does not use the common header.** Its QueryID is byte 1 and it
  carries a Length byte. Every other group is
  `Start | Device | Circuit | QueryID`.
- **`ME_STEP_START_1` is `0xAA`, the same value as `ME_START_CONFIG`.** Separate
  namespaces — a step packet never appears on the wire alone, a config frame
  never appears inside a program buffer — but keep the names distinct.
- `ME_PROGRAM_BUF_SIZE` is `#ifndef`-guarded so `build-native.ps1` can override
  it to 64 KB.

---

### `src/proto/crc16.c/.h`

**Does.** CRC-16/Modbus, poly `0xA001`, init `0xFFFF`, no final XOR.

**Interface.** `me_crc16_modbus`, `me_crc16_append`, `me_crc16_verify`,
`me_crc16_read`, `me_crc_order_parse`, `me_crc_order_name`,
`ME_CRC_ORDER_DEFAULT`.

**Gotchas.**
- **`ME_CRC_ORDER_DEFAULT` is the ONLY place the byte order is decided.**
  `sys_init.c`, `main.c`'s help text and `deploy.ps1` all trace back to it. It
  is `ME_CRC_ORDER_BE` — high byte first — and that is hardware-verified in both
  directions (ADR-9).
- A wrong order still produces a well-formed frame. It fails only inside the
  peer's checksum test, never in parsing.

---

### `src/proto/reg_frame.c/.h`

**Does.** Pack the 33-byte `0xDD` request; parse the 7-byte response.

**Interface.** `me_reg_pack_request`, `me_reg_parse_response`,
`me_reg_value_name`, `me_reg_parse_result_name`.

**Gotchas.**
- **`0x01` and `0x02` are BOTH success** — `ME_REG_VALUE_IS_SUCCESS`. Treating
  `0x02` as a rejection made the board re-register every 30 seconds, observed on
  hardware 2026-08-10 (ADR-10).
- The Length byte is **computed** from field sizes, never hardcoded as `0x1C`.
- Device and Circuit in the response are echoes: a mismatch warns, never blocks.

---

### `src/proto/program_chain.c/.h`

**Does.** Walk the step chain: a singly-linked list embedded in a byte array.

**Interface.**
```c
typedef enum { ME_CHAIN_OK, ME_CHAIN_NOT_FOUND, ME_CHAIN_BAD_NEXT_INDEX,
               ME_CHAIN_BAD_END_SEQ, ME_CHAIN_TRUNCATED } me_chain_result_t;
me_chain_result_t me_chain_fetch_step(const uint8_t *buf, uint32_t buf_len,
                                      uint32_t step_no, const uint8_t **step_out,
                                      uint32_t *step_len_out);
bool     me_chain_is_complete(const uint8_t *buf, uint32_t buf_len);
uint32_t me_chain_count_steps(const uint8_t *buf, uint32_t buf_len);
uint16_t me_chain_step_number(const uint8_t *step);
uint8_t  me_chain_step_operator(const uint8_t *step);
```

**Format.**
```
 off  0     1     2  3  4  5     6  7     8      ...     n-2  n-1
     +-----+-----+-----------+---------+-----+-----------+----+----+
     | AA  | 55  | nextIndex | stepNo  | op  | step data | 55 | AA |
     +-----+-----+-----------+---------+-----+-----------+----+----+
                  absolute offset of the next step; FFFFFFFF = end
```

**Inside.** One private `walk()` serves all three public functions — `want == 0`
walks to the end, which is how completeness and counting are computed. There are
not three subtly different walkers.

**Gotchas.**
- **Steps are counted POSITIONALLY, not read from the `stepNo` field.** Ported
  from the old firmware on purpose: a program with mislabelled numbers still
  executes in buffer order.
- **`nextIndex` is ABSOLUTE.** The Data Manager must append packets contiguously
  from offset 0; anything inserted between them invalidates every offset.
- A backward or self-referencing `nextIndex` is rejected. The old walker looped
  forever on one — a hung thread, not a rejected program.
- `buf`/`buf_len` are parameters, not globals. That is what makes it testable
  and lets both threads share one implementation.

---

### `src/proto/frame_router.c/.h`

**Does.** Decide which thread owns an inbound frame, and where a frame ends.

**Interface.**
```c
typedef struct { bool valid; me_msg_type_t msg_type; uint8_t start, device_id,
                 circuit_id, query_id; uint32_t body_off, body_len;
                 const char *reject; } me_frame_info_t;
bool     me_frame_classify(const uint8_t *buf, uint32_t len, me_frame_info_t *out);
uint32_t me_frame_expected_len(const uint8_t *buf, uint32_t len);
```

**Routing.**

| Start | Query | → | body |
|---|---|---|---|
| `0xBB` | `0x04` | `STORE_PROGRAM` | step bytes, past the 2-byte length |
| `0xBB` | `0x01`/`0x03` | `ME_MSG_NONE` | **answered by the comm thread itself** — no other thread is involved, so there is no message type to assign |
| `0xBB` | other | `ME_MSG_NONE` | recognised, not handled |
| `0xAA` | `0x05` | `STORE_BATTERY` | payload, minus header and CRC |
| `0xAA` | other | `STORE_CONFIG` | payload, minus header and CRC |
| `0xEE` | any | `CONTROL` | **the whole frame** |
| `0xDD` | any | `ME_MSG_NONE` | handled synchronously by the comm thread |
| `0xA0` | any | `ME_MSG_NONE` | recognised, not implemented |

**Gotchas.**
- **Does not verify the CRC.** Each consumer re-verifies with its own parser, and
  the router must be able to name a corrupt frame's start byte for the log.
- `0xEE` forwards the **whole** frame including its CRC, because Core Logic
  re-parses rather than trusting that the router already did.
- The declared `0xBB` length is checked against what actually arrived. Trusting
  it blindly is how the old firmware's `memcpy` could run past its buffer.
- `me_frame_expected_len()` is **a table of documented assumptions, not a parse** —
  this protocol carries no length field anywhere. Current entries:

  | Start | Length | Source |
  |---|---|---|
  | `0xDD` | 7 | fixed-size registration response |
  | `0xBB` Q4 | `6 + declared + 2` | the frame states its own payload length |
  | `0xBB` Q1 / Q3 | 6 / 8 | session #7 |
  | `0xBB` Q2 / Q5 / Q6 | **0** | not implemented, no opinion |
  | `0xEE` Q1 Start | **10, not 6** | carries a Session ID — session #8; this entry being wrong broke a hardware run |
  | `0xEE` Q5 Sync | 10 | 4-byte epoch |
  | `0xEE` others | 6 | header + CRC |
  | **`0xAA` Q1–Q4** | **6** | `bm_config_v6.0.md` §3 — added 2026-08-12 |
  | **`0xAA` Q5** | **46** | 4 + 40 + 2, `bm_config_v6.0.md` §5.3/§5.4 |
  | **`0xAA` Q6** | **10** | 4-byte epoch, §3.6 |
  | `0xAA` Q7–Q10 | **0** | broadcast, **2-byte header** — not derivable by this arithmetic (§9.1) |
  | `0xA0` | **0** | calibration encodes nothing |

**`me_frame_resolve_len()` — the CRC as delimiter of last resort (ADR-19).**
Every inbound frame goes through this, and `consume_rx_buffer()` calls it instead
of `me_frame_expected_len()` directly:

| Order | Step | When |
|---|---|---|
| 1 | layout length, if it fits *and* checksums | the normal path |
| 2 | declared-length frames returned as-is | `0xDD`, `0xBB` Q4 |
| 3 | **the whole read**, if it checksums — for *every* start byte | the documented one-frame-per-write case |
| 4 | shortest-first CRC scan over `[6, avail]` | the table's entry is wrong, or frames coalesced |
| 5 | layout length unchanged | nothing checksums: corrupt or incomplete |

- **Steps 1 and 3 exist to keep step 4 off the normal path.** A first version went
  straight from 1 to 4, which made the scan the *primary* route for every `0xAA`
  frame — where a coincidental short CRC match inside its own payload truncates the
  frame and orphans the rest, recreating the desync this function prevents. Caught
  in review; regression test
  `test_resolve_len_does_not_truncate_on_a_planted_short_crc`.
- ⚠️ **Step 3 was gated on `layout == 0` until 2026-08-12, and that gate was a
  bug.** The reasoning had been that "no entry" and "an entry that failed to
  verify" are different states. But the moment `0xAA` Q5 gained its correct
  46-byte entry, a *short* `0xAA` frame had a layout of 46 — larger than what had
  arrived — so step 1 could not use it, the gate excluded it from step 3, and it
  fell into the scan, which truncated it. **Adding a correct table entry made a
  different frame parse worse.** A layout that overshoots what arrived says nothing
  about where the frame ends, so it must not forfeit the whole-read check. Step 3
  is now unconditional; regression test
  `test_resolve_len_does_not_truncate_when_the_layout_overshoots`.
- **`length_is_declared()` frames are never scanned** — `0xDD` is fixed-size and
  `0xBB` Q4 states its own length, whose payload is step data in which a
  coincidental match could land *inside* a step.
- **`*out_scanned` is `(layout != 0 && resolved != layout)` in both steps 3 and 4**
  — a real entry that was really wrong, nothing else. `consume_rx_buffer()` logs it
  at WARN naming both lengths, because silent self-healing leaves the table wrong
  forever.
- Shortest-first: a longer coincidental match swallows the *next* frame; a shorter
  one can only truncate this one.
- Includes `battery_frame.h` for `ME_BATTERY_FRAME_LEN` — pure module including a
  pure module, so the 40 stays defined once rather than restated as a literal.

---

### `src/proto/ack_frame.c/.h` — PURE

**Does.** Build the one 7-byte acknowledgement shape the whole protocol uses.

**Interface.**
```c
size_t me_ack_pack(uint8_t start, uint8_t device_id, uint8_t circuit_id,
                   uint8_t query_id, uint8_t value, me_crc_order_t order,
                   uint8_t *out);
```

**Layout.** `Start | DeviceNumber | CircuitNumber | QueryID | Value | CRC[2]`.
`ME_ACK_VALUE_OK` = `0x01`, `ME_ACK_VALUE_FAIL` = `0x00`. Sources:
`bm_program_v3.0.md` ("`BB 01 01 04 01 -- --`") and `bm_control_v3.0.md`
("Response (OK): `EE 01 01 01 01 -- --`").

**Why it exists.** Until session #7 the registration request was the only frame
this board ever *wrote*, so the reply layout gets the same treatment as every
inbound layout: pure, host-tested, one field per assertion. It is **one** packer
because `0xAA`, `0xBB` and `0xEE` genuinely share the shape — the first version
hardcoded `0xBB` and had to be generalised in #8 when `0xAA`/`0xEE` needed it.

**Gotchas.**
- **Every identifying field is echoed from the inbound frame** — start byte,
  device, circuit and QueryID. None comes from this board's configuration. The Web
  Application correlates on exactly these bytes, so substituting our own would
  answer a question nobody asked, and a wrong start byte or QueryID reads as a
  reply to a *different* query rather than as a malformed frame.
- ✅ **The `0xAA` shape is now documented, not inferred (2026-08-12).**
  `bm_config_v6.0.md` §3.5 gives it verbatim: Write successful
  `AA 01 01 05 01 -- --`, Write Failed `AA 01 01 05 00 -- --`. This was previously
  flagged as "the weakest claim in this file, question it first if the Web
  Application rejects a battery ack" — the guess turned out to be right.
- The broadcast queries Q7–Q10 use a **different** 5-byte reply
  (`Start | QueryID | Value | CRC[2]`, no device or circuit) and additionally
  **zero the QueryID** to signal a CRC failure, distinguishing it from a write
  failure. `me_ack_pack()` cannot produce that shape and nothing needs it yet.
  `bm_config_v6.0.md` §4.1.
- Deliberately **does not** reuse `ME_REG_VALUE_*`. Those belong to `0xDD`, where
  `0x02` also means success — no other group carries that meaning.
- Does not verify anything inbound. `frame_crc_ok()` in `comm_thread.c` does that
  before any ack is sent.

---

### `src/proto/program_frame.c/.h` — PURE

**Does.** The `0xBB`-specific parts of the program handshake. **Not the reply
layout** — that moved to `ack_frame.c` in session #8.

**Interface.**
```c
bool me_prg_query_is_handshake(uint8_t query_id);
bool me_prg_parse_packet_count(const uint8_t *frame, uint32_t len,
                               uint16_t *out_count);
```

**Gotchas.**
- **`me_prg_query_is_handshake()` excludes Q4 deliberately.** Q1 and Q3 have no
  side effect anywhere else, so the comm thread answers them alone. Q4 carries
  data that must reach the Data Manager, so its ack means "queued" and can only
  be sent from the routing path.
- The Q3 count is read and **logged, never stored** — completion comes from the
  chain terminator (ADR-14). Logging it is what makes "3 announced, 1 stored"
  visible.
- Does not verify the CRC. The caller does that once for the whole frame, before
  trusting any field.

---

### `src/proto/control_frame.c/.h`

**Does.** Parse `0xEE` control frames.

**Interface.** `me_control_parse`, `me_control_query_name`,
`me_ctrl_parse_result_name`, `me_control_t`.

**Inside.** Check order is deliberate: `SHORT` → `BAD_START` → `BAD_CRC` →
`BAD_QUERY`. A frame from the wrong command group should say so rather than be
reported as corruption.

**Gotchas.** Only Q5 Sync Time sets `has_epoch`. The epoch is big-endian.

---

### `src/proto/battery_frame.c/.h`

**Does.** Parse the `0xAA` **Q5** battery-configuration payload — all 12 fields.

**Interface.** `me_battery_parse`, `me_battery_t`, `ME_BATTERY_PAYLOAD_LEN` **40**,
`ME_BATTERY_FRAME_LEN` **46**, one `ME_BAT_OFF_*` per field.

**Layout.** `bm_config_v6.0.md` §5.3:

| off | size | field | | off | size | field |
|---|---|---|---|---|---|---|
| 0 | 4 | nominal capacity `f32` | | 21 | 1 | charge factor `u8` |
| 4 | 1 | number of cells `u8` | | 22 | 4 | **impedance** `f32` |
| 5 | 4 | gassing voltage `f32` | | 26 | 4 | **break voltage** `f32` |
| 9 | 4 | maximum voltage `f32` | | 30 | 4 | **nominal voltage** `f32` |
| 13 | 4 | nominal current `f32` | | 34 | 4 | **energy density** `f32` |
| 17 | 4 | cold cranking `f32` | | 38 | 2 | **battery ID** `u16` |

**Gotchas.**
- ✅ **Specification-backed AND wire-confirmed since 2026-08-12.**
  `Ref Docs/bm_config_v6.0.md` §5.3 names every byte, and §5.4 verifies the layout
  against a frame captured from the ME Web Application — all twelve boundaries
  land correctly and the CRC checks out. This file previously warned that no
  configuration document existed.
- **`ME_BATTERY_PAYLOAD_LEN` was 22 and is now 40.** The legacy
  `storeBatteryData()` layout stopped after charge factor; the first seven offsets
  were right, they were merely incomplete. Because the parser deliberately
  "tolerated a longer payload", the bolded five fields above — 18 bytes — were
  **silently discarded on every battery packet**.
- **A 22-byte payload is now REFUSED**, not half-parsed (developer decision). A
  record with `valid = true` but a zeroed nominal voltage would let a test run
  against nonsense. `data_mgr.c` WARNs naming both lengths and Q5 is acked `0x00`.
- A payload longer than 40 is still accepted with the extra ignored, but now
  WARNs — it means the protocol moved.
- ✅ **Impedance and energy density are `float`** — confirmed by the developer
  2026-08-12, so the source spreadsheet's integer-looking samples are simply an
  error in the spreadsheet. They were the only two 4-byte battery fields it does
  not annotate "It will be in float"; observed traffic already agreed (the Web
  Application sent `3F 80 00 00` for a field set to `1`), so **no code change was
  needed**. Both encodings are 4 bytes wide, so a wrong choice would have yielded
  a nonsense value rather than a parse error — which is why it was raised rather
  than assumed. `bm_config_v6.0.md` §9.3; pinned by
  `test_impedance_and_energy_density_are_floats()`.
- Has a private `get_u16_be` for battery ID — the only 16-bit field here.
  `realtime_frame.c`'s `put_u16_be` stays private; one small reader is cheaper
  than widening that interface for a single caller.

---

### `src/proto/realtime_frame.c/.h`

**Does.** Build the 86-byte `0xCC` frame, and own the big-endian float codec.

**Interface.** `me_put_f32_be`, `me_get_f32_be`, `me_realtime_pack`,
`me_realtime_t`, `ME_RT_FRAME_LEN` 86, one `ME_RT_OFF_*` per field, and
**`me_realtime_pack_post_registration`** (temporary — see below).

**Layout.** 4 header + 80 payload + 2 CRC. Header
`CC | Device | Circuit | 0x01`.

**Gotchas.**
- Floats are **IEEE 754 single-precision, BIG-endian**. The golden test is the
  spec's own worked example: `95.6f → 42 BF 33 33`.
- `me_put_f32_be` uses `memcpy` through a `uint32_t`, not a pointer cast.
  Type-punning `float*` to `uint32_t*` is undefined behaviour and `-O2` is
  entitled to miscompile it.
- `battery_frame.c` reuses `me_get_f32_be` — one decoder for the whole protocol.
- ⚠️ **`me_realtime_pack_post_registration()` is TEMPORARY demo scaffolding**
  living in a permanent module. It builds the one frame sent after a successful
  registration: `step_number = 1`, `temperature = 25.0 °C`, everything else zero,
  both status bytes Idle. For device `0x01` / circuit `0x11` the output is
  byte-identical to the 86 bytes the developer supplied, CRC `0xF261`.

  It lives **here rather than in `demo_realtime.c`** on purpose: this module is
  PURE and therefore in the host build, while `demo_realtime.c` is Linux-only and
  excluded from `build-native.ps1`. Putting the byte layout here is what lets the
  exact frame be proven on the laptop. **Delete it together with
  `demo_realtime.*`** — see "Delete the demo scaffolding" in §12.
- `ME_RT_POST_REG_STEP_NUMBER` and `ME_RT_POST_REG_TEMPERATURE` are defined in
  this header, deliberately **not** taken from `demo_realtime.h`'s
  `ME_DEMO_TEMPERATURE`: that header is Linux-only and including it would break
  both this module's purity and the host test build.

---

### `src/proto/rpmsg_frame.c/.h` — PURE

**Does.** The wire codec for the A53↔M7 CAN exchange over RPMsg
(`Ref Docs/RPMSG_PROTOCOL.md` v3.0): the 11-byte header, the 80-byte
`can_frame_msg_t` payload, the CAN-FD DLC↔length table, and a byte-stream
reassembler for `/dev/ttyRPMSG30`. No sockets, no I/O — `can_mgr.c` owns the fd.

**Interface.** `me_rpmsg_wrap`, `me_rpmsg_parse_header`, `me_rpmsg_hdr_t`;
`me_rpmsg_pack_can`, `me_rpmsg_parse_can`, `me_rpmsg_can_t`;
`me_rpmsg_dlc_for_len`, `me_rpmsg_len_for_dlc`; `me_rpmsg_stream_init`,
`me_rpmsg_stream_push`, `me_rpmsg_stream_next`, `me_rpmsg_stream_t`.

**Gotchas.**
- **Two layers, opposite endianness.** The 11-byte header (`command`,
  `length`) is **big-endian**; the 80-byte `can_frame_msg_t` (`timestamp_ms`,
  `can_id`) is **little-endian**, the M7's native order. Do not unify them or
  share a helper between the two — separate `put_u32_be`/`put_u32_le` exist on
  purpose.
- `me_rpmsg_stream_next()`'s `*payload` **aliases the stream buffer** and is
  only valid until the next call. The frame it points to is not consumed
  immediately: consumption is deferred to the *start* of the following call
  (`s->pending`), because consuming first would slide the very bytes the
  caller is still reading.
- An implausible header (`length > 80`) makes the parser skip exactly **one**
  byte and rescan, not discard the whole buffer — discarding more would
  swallow a genuine frame sitting right behind the bad one
  (`RPMSG_PROTOCOL.md` §5 step 4).
- The golden test vectors are `RPMSG_PROTOCOL.md` §8's exact 91-byte SET frame
  for CAN ID `0x18F` — the contract with the M7 firmware. If that test ever
  fails, the two cores disagree about the wire.

---

## 7. Storage

### `src/store/circuit_store.c/.h` — PURE

**Does.** Map CircuitID to a slot, and hold every circuit's program, battery
data and configuration.

**Interface.**
```c
#define ME_SLOT_INVALID (-1)
int     me_circuit_slot(uint8_t circuit_id);
uint8_t me_circuit_from_slot(int slot);
void me_store_init(void);
bool me_store_program_append(uint8_t circuit_id, const uint8_t *data, uint32_t len);
void me_store_program_reset(uint8_t circuit_id);
bool me_store_program_is_complete(uint8_t circuit_id);
uint32_t me_store_program_len(uint8_t circuit_id);
const uint8_t *me_store_program_ptr(uint8_t circuit_id);
bool me_store_battery_set/get(...);
bool me_store_config_set/get(...);
```

**Mapping.** `slot = (secondary - 1) * 8 + (channel - 1)`. Both nibbles are
**1-based**: `0x11 → 0`, `0x88 → 63`.

**Gotchas.**
- **A malformed CircuitID returns `ME_SLOT_INVALID` and NEVER folds to slot 0.**
  `0x00`, `0x01`, `0x10`, `0x09`, `0x90`, `0xFF` are all rejected. A bad ID
  quietly overwriting Secondary 1 Channel 1's program is a corruption with no
  error, no log and no symptom until the wrong test runs on real cells.
- **`me_store_init()` does not clear the 128 MB.** Touching every page would
  turn reserved address space into committed memory for nothing.
- **A packet arriving for a completed program starts a new one.** Concatenating
  would leave the new chain's absolute offsets pointing into the old.
- **NOT thread-safe, deliberately.** Only the Data Manager calls it. A mutex
  would invite a second caller.

### `src/store/circuit_registry.c/.h` — PURE

**Does.** Answers one question: *may this circuit be handled at all?* A 64-slot
table of "has this Secondary/Channel registered".

**Interface.**
```c
void me_registry_init(void);
bool me_registry_mark_registered(uint8_t circuit_id);  /* false on malformed ID */
bool me_registry_is_registered(uint8_t circuit_id);
```

**Why not part of `circuit_store`.** `circuit_store` answers *what has this
circuit sent*; this answers *may we act on it*. One is a data question, the
other access control — folding them together would put admission logic in a
file whose header says it is about storage.

**Who calls it.** `main.c` inits it. `comm_thread.c` is the only other caller:
`do_registration()` marks, `route_frame()` reads. Nothing else touches it.

**Gotchas.**
- **NOT thread-safe, deliberately.** The **communication thread** is the single
  owner — that is what makes a plain `bool` array safe with no atomics.
- **A future CAN-side registration must NOT call `mark_registered()` directly.**
  `can_mgr` is a different thread; a direct call makes this an unsynchronised
  cross-thread write, and `-O2` may hoist the read in the routing loop. Send a
  message to the communication thread instead — the same rule that keeps
  `circuit_store` single-owner.
- **`is_registered()` is false for malformed AND for never-registered IDs.**
  The caller does not need to tell them apart: both mean "ignore this frame".
- **Only 1 of 64 circuits can register today.** See Open Item 5.

---

## 8. Transport and platform — LINUX-ONLY

### `src/net/tcp_client.c/.h`
`me_tcp_connect`, `me_tcp_send_all`, `me_tcp_recv_response`, `me_tcp_close`.
`me_tcp_recv_response` waits one extra short interval after the expected byte
count, so an over-long reply is detected rather than silently truncated.
**Dotted-quad only** — no `getaddrinfo` (ADR-8).

### `src/net/udp_sock.c/.h`
`me_udp_open`, `me_udp_close`, `me_udp_dest_init`, `me_udp_send_to`.
A send failure logs `errno` and returns false; the caller drops the frame and
carries on. A transient `ENETUNREACH` must not end a 60-second run.

### `src/platform/netinfo.c/.h`
`me_netinfo_read`, `me_netinfo_format_ip`, `me_netinfo_format_mac`.
Falls back to the first non-loopback interface that is up, because Torizon may
present the NIC as `eth0` or `end0`.
**These values are protocol payload, not diagnostics** — hence `--network host`
(ADR-6).

### `src/platform/rpmsg_link.c/.h` — LINUX-ONLY
`me_rpmsg_device_path` (`ME_RPMSG_DEV` env var, default `/dev/ttyRPMSG30`),
`me_rpmsg_open`, `me_rpmsg_write_all`, `me_rpmsg_close`. Deliberately thin —
`open`/`termios`/`write`/`close` and nothing else; every byte-layout decision
lives in `proto/rpmsg_frame.c` so it stays host-testable.
**Raw mode is mandatory** (`cfmakeraw`, `VMIN`/`VTIME` = 0): a 64-byte CAN
payload contains arbitrary bytes, and the default line discipline's CR/LF and
control-byte translation would silently corrupt frames. `me_rpmsg_open`
returning -1 is not fatal to the caller — `can_mgr.c` degrades rather than
aborting.

---

## 9. Utilities

### `src/util/log.c/.h` — PURE
`ME_LOGD/I/W/E`, `me_hex_line`, `me_hex_dump`, `me_log_set_level`.
The hex dump is the primary field diagnostic: there is no decoder on the board
side, so a rejected frame must be diagnosable from the console alone.

### `src/util/msgq.c/.h` — PURE (builds on both targets)

**Interface.**
```c
#define ME_MSGQ_DEPTH 16u
bool me_msgq_init(me_msgq_t *q, const char *name, bool pollable);
void me_msgq_destroy(me_msgq_t *q);
bool me_msgq_send(me_msgq_t *q, const me_msg_t *m, int timeout_ms);
bool me_msgq_recv(me_msgq_t *q, me_msg_t *out, int timeout_ms);
int  me_msgq_fd(const me_msgq_t *q);
void me_msgq_ack_event(me_msgq_t *q);
unsigned long me_msgq_dropped/sent/received(const me_msgq_t *q);
```

**Gotchas.**
- **Every send has a finite timeout and DROPS on expiry.** No thread ever blocks
  forever, so congestion degrades into a counted, logged loss rather than a
  hang. This is what makes deadlock impossible by construction.
- `eventfd` is behind `#ifdef __linux__`; on the host `pollable` is accepted and
  ignored.
- Timed waits use **`CLOCK_REALTIME`**, because `pthread_condattr_setclock` does
  not exist in winpthreads and the queue must build on the host for its tests. A
  clock step could make one 50–500 ms timeout fire early or late. Acceptable
  there; **do not reuse this pattern anywhere timing affects the wire.**
- `send`/`recv` copy only `len` payload bytes, so a 6-byte control frame does
  not cost a 64 KB `memcpy`.
- **`me_msgq_t` is ~1 MB.** Never declare one as a local — it overflows a
  default thread stack. All four are file-scope in `app_queues.c`.

---

## 10. Tests — `tests/`

`build-native.ps1` compiles and runs all of these. **158 checks.**

| File | Covers |
|---|---|
| `test_crc16.c` | Reference vector `"123456789" → 0x4B37`, byte order, append/verify |
| `test_reg_frame.c` | Every `0xDD` field offset individually, golden hardware vectors |
| `test_log.c` | Hex dump formatting |
| `test_msgq.c` | FIFO, timeouts, full-queue drop, `len`-bounded copy, 40-message producer/consumer |
| `test_program_chain.c` | Fetch by step, terminator, and every malformed case |
| `test_frame_router.c` | Every start byte, declared-length overrun, expected length, `0xBB` Q1/Q3 and **all six `0xAA`** sizes, coalesced splits, **both truncation guards** |
| `test_ack_frame.c` | Every ack field at its own offset, **both** documents' literal example bytes, start-byte and QueryID echo, both CRC orders, CRC span |
| `test_program_frame.c` | The handshake predicate, Q3 count parse and its rejections |
| `test_control_frame.c` | All six commands, epoch, and each rejection code |
| `test_battery_frame.c` | All 12 offsets, field independence, 40-byte length, legacy 22 refused, **the captured 46-byte Web App frame**, float-vs-u32 |
| `test_realtime_frame.c` | `95.6f → 42 BF 33 33`, all 80 payload offsets, CRC, **the exact 86-byte post-registration frame** |
| `test_circuit_store.c` | Slot mapping, append, reset-on-resend, capacity refusal |
| `test_circuit_registry.c` | Mark/query round-trip, 64-circuit independence sweep, nibble-swap and row-boundary aliasing, malformed IDs refused, init clears all slots |

**Not covered, and no test pretends otherwise:** thread creation, `eventfd`
wake-ups, `poll()`, socket I/O, the real `.bss` footprint. Those are proven by
`deploy.ps1` on hardware.

**Also not covered:** the *ordering* inside `route_frame()`. The pieces are
tested — `test_circuit_registry.c` proves the table, `test_program_frame.c`
proves the reply bytes and the handshake predicate — but the seven-step sequence
itself (that `0xDD` is filtered before the gate reads `circuit_id`, that the gate
precedes the reply, that a Q4 ack follows a successful queue send) lives in
`comm_thread.c`, which is LINUX-ONLY and excluded from the host build. It is
proven by reading and by hardware, nothing else. Session #7 moved the *decisions*
into pure code where it could; the sequence is what remains.

---

## 11. Cross-cutting rules

1. **No protocol magic number outside `proto_defs.h`.**
2. **No socket or platform header inside `src/proto/`.** It silently drops out
   of the host test build and the byte-layout safety net is lost (ADR-7).
3. **No dynamic allocation.** Every buffer is static or stack.
4. **Test-first.** Write the failing test, watch it fail for the right reason,
   then implement. New protocol fields get a test asserting their offset
   individually.
5. **No file over 2000 lines.** The largest here is ~380.
6. **Never remove `--network host`** (ADR-6).
7. **Never claim the board works from a native build** (ADR-3).

---

## 12. Common tasks

**Add a new inbound frame type**
1. Constants → `proto_defs.h`
2. Parser + tests → `src/proto/<name>_frame.c`
3. Route it → `frame_router.c` (`me_frame_classify`, and
   `me_frame_expected_len` if the frame carries a length)
4. Message type → `msg.h` + `msg.c`
5. Handle it → the owning thread's `handle()`
6. Both build scripts

**Answer a frame the board must reply to** — `me_ack_pack()` in `ack_frame.c`
already builds the 7-byte reply for any group; pass the inbound start byte. The
model to follow: the packer is pure and host-tested, the *decision* of which
queries to answer is a pure predicate (`me_prg_query_is_handshake()`), and only
the socket call lives in `comm_thread.c`. Two rules, both learned the hard way:
put the reply **behind** the admission gate in `route_frame()` (answering for an
unregistered circuit claims something about hardware this board has no record of),
and **verify the CRC before acking** — an ack is a claim about the bytes, and a
downstream thread cannot retract it.

**Add or correct a frame length** — `me_frame_expected_len()` in
`frame_router.c`, and read ADR-19 first. Never add an entry without a captured
frame or a document behind it: a wrong length does not fail locally, it desyncs
every following frame on the connection. If a hardware log shows
`RECOVERED, but the length table … needs fixing`, that WARN names the entry to
correct.

⚠️ **Adding a *correct* entry can still break a different frame.** When the
`0xAA` entries were added on 2026-08-12, a short `0xAA` frame suddenly had a
layout of 46 — larger than what had arrived — which excluded it from
`me_frame_resolve_len()`'s whole-read step (then gated on `layout == 0`) and
dropped it into the scan, which truncated it. After adding any entry, check that
a frame *shorter* than it still resolves; that is what
`test_resolve_len_does_not_truncate_when_the_layout_overshoots` now guards.

**Change the CRC byte order** — `ME_CRC_ORDER_DEFAULT` in `crc16.h`. One place.
Read ADR-9 first.

**Add a circuit or grow a program** — `ME_MAX_SECONDARIES`, `ME_MAX_CHANNELS`,
`ME_PROGRAM_BUF_SIZE` in `proto_defs.h`. Check `.bss` against the Docker memory
limit afterwards.

**Replace the demo with real execution** — put the body in
`me_execute_program()` in `core_logic.c`, then delete `demo_realtime.c/.h` and
its two build-script lines. **Three** further references then break the build,
which is deliberate so none can be forgotten:

| Reference | Where |
|---|---|
| `me_demo_send_post_registration()` call | `comm_thread.c`, end of `do_registration()` |
| `#include "demo_realtime.h"` | `comm_thread.c` include block |
| `me_realtime_pack_post_registration()` + its two `ME_RT_POST_REG_*` constants | `realtime_frame.c/.h` — delete the function and its test |

**Diagnose a missing frame** — read the shutdown `queues:` lines first. A
non-zero drop count names the queue and is logged at WARN.

---

## 13. Open items

1. ~~**`0xAA` layouts are not specification-backed.**~~ **RESOLVED 2026-08-12** by
   `Ref Docs/bm_config_v6.0.md`, transcribed from the developer's
   `Config Data Frame Format V6.0.xlsx`. The battery payload is **40 bytes, not
   22**, and the missing 18 were being silently discarded. Superseded by Open
   Items 15–18 below, which are what the document did *not* settle.
2. **`0xBB` Q1 and Q3 ARE sent — observed on hardware 2026-08-12**, which
   corrected the earlier reading that Q3 was gone. Both are answered as of
   session #7. **Q2 (program metadata) is still unknown**: it is logged and
   dropped, and if the Web Application waits for a reply to it the handshake
   stalls there instead. **Q1's frame length is the one unverified constant** —
   `ME_PRG_Q1_LEN` is 6 on the strength of the layout (no payload, same shape as
   a payload-less `0xEE`), not on the strength of a captured frame. If it is
   really some other length, the symptom is a CRC-mismatch log with a hex dump,
   then a byte-offset stream desync producing "unrecognised start byte" WARNs.
   Check the `inbound TCP data` dump on the next run.
3. **Verify `.bss` against the Docker memory limit.** `readelf` reports
   **261 MB** of `.bss` (`NOBITS`); the binary is 4.1 MB. Linux reserves rather
   than commits, but Docker limits count RSS — run `docker stats` with several
   circuits loaded.
4. **`0xAA` frames cannot be split from a coalesced TCP read.** The protocol
   encodes no length. Fine if the Web Application sends one frame per write.
   *Now also means:* two frames for different circuits in one read are gated on
   the **first** frame's CircuitID.
5. **Only 1 of the 64 circuits can ever register — known and accepted.** The
   `0xDD` frame carries one CircuitID and is sent once per connection, so
   admission control is currently 63/64ths closed while the rest of the system
   (`g_program[64]`, `g_cl_program[64]`, `g_demo[64]`) is built for 64. Deliberate
   and temporary — the CAN-side per-Secondary handshake in `can_mgr.c` is the
   intended writer for the other slots — but until it lands **this board is
   functionally single-circuit**, working for the circuit passed via
   `--secondary`/`--channel` (`deploy.ps1` defaults to `0x11`).

   **Not a risk (developer decision, 2026-08-12):** the Web Application conforms
   to this board's CircuitID rather than the reverse. Read this before touching
   `circuit_registry.c` or writing the CAN-side registration; do **not** re-raise
   it as a blocker on the hardware run. If a mismatch ever does occur the symptom
   is "Start arrives, nothing happens" plus a WARN naming the circuit.
6. **Device-scoped commands are gated as if circuit-scoped.** `ME_CTRL_SYNC_TIME`
   (`0xEE` Q5) and `ME_CTRL_RESET` (Q6) plausibly concern the board, not one
   circuit, but `route_frame()` gates every `0xEE`/`0xAA` frame on its CircuitID.
   Harmless today — Sync Time is already a logged no-op — but it will look like a
   clock that never syncs once implemented. Confirm with the Web Application team
   whether every `0xEE`/`0xAA` frame carries a real per-circuit CircuitID.
7. **The demo emitter is scaffolding.** Delete it when execution lands.
8. **Q1 is-ready is answered unconditionally.** Readiness genuinely is
   unconditional today: the Data Manager takes a program at any time and a new
   one replaces the old (ADR-14). Once step execution lands, a circuit already
   running a test is the case that needs `ME_PRG_VALUE_FAIL` —
   `handle_program_handshake()` in `comm_thread.c` is the single place to change.
9. **`0xBB` Q4 frames are now CRC-verified before storage.** New in session #7,
   because the per-packet ack asserts the bytes were good. Previously the CRC was
   never checked on this path at all. If the Web Application computes the Q4 CRC
   over a different span than the whole frame, **every program packet is refused
   with a `0x00` ack** — the log prints computed-vs-carried in both byte orders,
   so it is a one-look diagnosis rather than a mystery.
10. **The Q4 ack means "queued", not "stored".** The store happens on the Data
    Manager thread and the comm thread never learns its outcome, so a full
    program buffer is reported only by the Data Manager's own log line. The same
    limit applies to the `0xEE` ack: it means the command was queued to Core
    Logic, not that a test started.
11. **`0xEE` Q1 Start carries an undocumented 4-byte Session ID** (found on
    hardware 2026-08-12; `Ref Docs/bm_control_v3.0.md` now amended). It is parsed
    and **logged, not stored** — the `0xCC` live frame has no field for it. The UDP
    10001 session variant does, which is where it belongs once session records
    exist (Open Item / T-29).
12. **Every other entry in the length table is still unverified against a captured
    frame** — `0xEE` Q2/Q3/Q4/Q6 = 6, `0xBB` Q1 = 6, Q3 = 8. **`0xAA` Q5 = 46 is
    now confirmed** against a captured frame (`bm_config_v6.0.md` §5.4); Q1–Q4 = 6
    and Q6 = 10 come from the document but no capture. ADR-19 makes a wrong one
    recoverable rather than fatal, and each occurrence logs `RECOVERED, but the
    length table … needs fixing`. **Grep the next hardware log for it** (T-44).
13. **An unrecognised `0xEE` QueryID is acked before Core Logic rejects it.** The
    CRC half is fixed — a corrupt frame gets `0x00` — but a well-formed frame with
    a QueryID outside the six documented gets `0x01`, then `me_control_parse()`
    returns `BAD_QUERY` on the Core Logic thread, which has no path back to the
    socket. Zero impact while only the six are sent (T-45).
14. ~~**The `0xAA` ack shape is inferred.**~~ **RESOLVED 2026-08-12** —
    `bm_config_v6.0.md` §3.5 documents it verbatim and the inferred shape was
    right. The broadcast queries Q7–Q10 use a *different* 5-byte reply that also
    zeroes the QueryID on a CRC failure; nothing needs it yet (§4.1).
15. ~~**Battery Impedance and Energy Density: float or integer?**~~ **RESOLVED
    2026-08-12 — both are `float`** (developer decision; the Web Application team
    will be told to send float only). They were the only two 4-byte battery fields
    the source spreadsheet does not annotate "It will be in float", and its samples
    (`00 00 00 64` = "100 Ohm") decode sensibly only as `uint32` — so the
    spreadsheet samples are wrong. Observed traffic already said float, so **the
    parser needed no change**; it is pinned by
    `test_impedance_and_energy_density_are_floats()`. Worth remembering *why* this
    was the highest-consequence item: both encodings are 4 bytes wide, so the wrong
    choice would have produced a nonsense value and never a parse error.
    `bm_config_v6.0.md` §9.3 (T-47 closed).
16. **`0xAA` Q7–Q10 are broadcast frames with a 2-byte header** — no DeviceNumber,
    no CircuitNumber. Every `0xAA` path here assumes 4 bytes, so one is read as
    device `0x07` / circuit `0x01`, which `me_circuit_slot()` rejects as malformed,
    so it is dropped. **Safe by accident.** Deliberately unnamed in `proto_defs.h`
    and returning 0 from the length table. §9.1 (T-48).
17. **A battery payload under 40 bytes is REJECTED**, not half-parsed (developer
    decision). If the Web Application sends the legacy 22-byte form, Q5 is answered
    `0x00` and the WARN names both lengths. Watch the first hardware log for
    `shorter than the 40 that bm_config_v6.0.md Q5 defines` (T-46).
18. **The post-registration `0xCC` frame is temporary demo scaffolding** and now
    fires on **every** successful registration, including reconnects. If the Web
    Application logs one live frame per reconnect and that is unwelcome, the single
    call site at the end of `do_registration()` is where to gate it.
19. **Five `0xAA` queries are fully specified but unanswered.** Q1–Q4 and Q6 now
    have documented request and response layouts (§3, §6–§8) and known lengths, but
    no handler. They are deliberately **not** acknowledged, because each owes real
    data or a real status back and a bare "success" would claim a read that never
    happened (ADR-18). Only Q5 write is implemented.
