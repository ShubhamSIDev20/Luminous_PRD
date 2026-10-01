# BTS 4-Thread Base — Implementation Plan

> **For agentic workers:** implement task-by-task, test-first. Steps use checkbox (`- [ ]`) syntax.
> This project is **not a git repository**, so the usual "commit" step is replaced by a
> **build-and-verify checkpoint**: `.\build-native.ps1` (unit tests) then `.\build.ps1`
> (cross-compile). Both must pass before the next task starts.

**Goal:** Build the four-thread base for the Battery Testing Application — Communication,
Core Logic, Data Manager and CAN Data Manager — connected by in-process message queues,
with per-circuit storage, program-chain walking, step-1 extraction, and a 60-second demo
real-time emitter that proves the whole loop on the Web Application.

**Architecture:** One inbox queue per thread. The Communication thread's queue carries an
`eventfd` so it can wait on three sockets and its queue in one `poll()`. All pure logic
(queue, chain walker, frame parsers, slot mapping) lives under `src/proto/`, `src/util/`
and `src/store/` and is unit-tested on the Windows host; threads and sockets are proven
only on hardware.

**Tech Stack:** C (gnu11 on target, c11 on host), pthreads, Linux `eventfd`/`poll`,
Arm GNU `aarch64-none-linux-gnu` 15.2.rel1 cross, MinGW-w64 GCC 16.1 host.

**Source spec:** `Docs/ME_Primary_BTS_Block_Diagram.md`

## Global Constraints

- No file may exceed **2000 lines**.
- **No dynamic allocation.** No `malloc`/`free` on any path. All buffers static or stack.
- `src/proto/` must contain **no socket or platform headers** — it drops out of the host
  test build otherwise (ADR-7).
- **No protocol magic number outside `proto_defs.h`** (project rule 15).
- `-Werror` on both builds. `-std=gnu11` on target, `-std=c11` on host.
- CRC is **big-endian, high byte first**, via `ME_CRC_ORDER_DEFAULT` (ADR-9).
- **Never remove `--network host`** from the deploy path (ADR-6).
- **Never claim the board target works from a native build** (ADR-3).
- Test-first: write the failing test, watch it fail for the right reason, then implement.
- `me_config_t` is **already taken** by `sys_init.h` for CLI config. The per-circuit stored
  configuration type is `me_circuit_config_t`.

---

## File Structure

### Created — pure logic, built by `build-native.ps1`

| File | Responsibility |
|---|---|
| `src/msg.h` | `me_msg_type_t`, `me_msg_status_t`, `me_msg_t`, `ME_MSG_FLAG_LAST` |
| `src/util/msgq.c/.h` | bounded queue: mutex + condvars + optional eventfd |
| `src/store/circuit_store.c/.h` | CircuitID→slot mapping and all per-circuit storage |
| `src/proto/program_chain.c/.h` | `AA 55 … 55 AA` chain walker + completion check |
| `src/proto/frame_router.c/.h` | inbound bytes → message type + circuit ID |
| `src/proto/control_frame.c/.h` | `0xEE` parse |
| `src/proto/battery_frame.c/.h` | `0xAA` Q5 battery-info parse |
| `src/proto/realtime_frame.c/.h` | build the 86-byte `0xCC` frame |

### Created — Linux-only, hardware-verified only

| File | Responsibility |
|---|---|
| `src/threads/data_mgr.c/.h` | Data Manager thread |
| `src/threads/core_logic.c/.h` | Core Logic thread |
| `src/threads/can_mgr.c/.h` | CAN Data Manager stub |
| `src/threads/demo_realtime.c/.h` | **temporary** 1 Hz / 60 s emitter |
| `src/app_queues.c/.h` | the four queue instances, one owner |

### Modified

| File | Change |
|---|---|
| `src/proto/proto_defs.h` | query IDs for `0xAA`/`0xBB`/`0xEE`/`0xCC`, step-chain constants, capacity macros |
| `src/net/udp_sock.c/.h` | add `me_udp_send_to()` and destination builder |
| `src/sys_init.c/.h` | build both UDP destination addresses at init |
| `src/comm_thread.c` | eventfd in `poll()`, inbound frame routing, outbound UDP |
| `src/main.c` | create and join four threads |
| `tests/test_util.h`, `tests/test_main.c` | register new suites |
| `build-native.ps1`, `build.ps1` | add sources |

---

## Task 1: Message types and the queue

**Files:**
- Create: `src/msg.h`, `src/util/msgq.h`, `src/util/msgq.c`, `tests/test_msgq.c`
- Modify: `tests/test_util.h`, `tests/test_main.c`, `build-native.ps1`

**Interfaces produced:**
```c
/* msg.h */
typedef enum { ME_MSG_NONE = 0, ME_MSG_STORE_PROGRAM, ME_MSG_STORE_BATTERY,
               ME_MSG_STORE_CONFIG, ME_MSG_CONTROL, ME_MSG_REQ_PROGRAM,
               ME_MSG_REQ_BATTERY, ME_MSG_RSP_PROGRAM_CHUNK, ME_MSG_RSP_BATTERY,
               ME_MSG_RSP_NOT_FOUND, ME_MSG_REALTIME_DATA, ME_MSG_SESSION_DATA,
               ME_MSG_CAN_TX, ME_MSG_CAN_DATA, ME_MSG_TYPE_COUNT } me_msg_type_t;

typedef enum { ME_STATUS_OK = 0, ME_STATUS_NO_PROGRAM, ME_STATUS_PROGRAM_INCOMPLETE,
               ME_STATUS_PROGRAM_INVALID, ME_STATUS_NO_BATTERY,
               ME_STATUS_BAD_CIRCUIT } me_msg_status_t;

#define ME_MSG_FLAG_LAST 0x01u
#define ME_MSG_PAYLOAD_MAX (64u * 1024u)

typedef struct {
    me_msg_type_t type; uint8_t circuit_id; uint8_t flags; uint16_t status;
    uint32_t offset; uint32_t len; uint8_t payload[ME_MSG_PAYLOAD_MAX];
} me_msg_t;

const char *me_msg_type_name(me_msg_type_t t);
const char *me_msg_status_name(me_msg_status_t s);

/* msgq.h */
#define ME_MSGQ_DEPTH 16u
bool me_msgq_init(me_msgq_t *q, const char *name, bool pollable);
void me_msgq_destroy(me_msgq_t *q);
bool me_msgq_send(me_msgq_t *q, const me_msg_t *m, int timeout_ms);
bool me_msgq_recv(me_msgq_t *q, me_msg_t *out, int timeout_ms);
int  me_msgq_fd(const me_msgq_t *q);
void me_msgq_ack_event(me_msgq_t *q);
unsigned long me_msgq_dropped(const me_msgq_t *q);
```

- [ ] **Step 1: Write failing tests** covering FIFO order, `len`-only payload copy, timeout
  on empty receive, timeout + drop counting on a full queue, `ME_MSGQ_DEPTH` boundary
  (fill exactly full, then one more fails), and `me_msgq_fd()` returning -1 when not pollable.
- [ ] **Step 2:** Run `.\build-native.ps1`. Expect FAIL — `msgq.h` not found.
- [ ] **Step 3:** Implement `msg.h` and `msgq.c`. `eventfd` behind `#ifdef __linux__`;
  on the host `pollable` is accepted and `evt_fd` stays -1. Timed waits use
  `pthread_cond_timedwait` against `CLOCK_REALTIME` (winpthreads has no
  `pthread_condattr_setclock`).
- [ ] **Step 4:** Run `.\build-native.ps1`. Expect PASS.
- [ ] **Step 5: Checkpoint** — `.\build-native.ps1` and `.\build.ps1` both clean.

---

## Task 2: Protocol constants and CircuitID→slot mapping

**Files:**
- Modify: `src/proto/proto_defs.h`
- Create: `src/store/circuit_store.h`, `src/store/circuit_store.c`, `tests/test_circuit_store.c`

**Interfaces produced:**
```c
/* proto_defs.h additions */
#define ME_MAX_SECONDARIES 8u
#define ME_MAX_CHANNELS    8u
#define ME_MAX_CIRCUITS    (ME_MAX_SECONDARIES * ME_MAX_CHANNELS)
#ifndef ME_PROGRAM_BUF_SIZE
#define ME_PROGRAM_BUF_SIZE (2u * 1024u * 1024u)
#endif
#define ME_QID_PROGRAM_DATA 0x04u   /* 0xBB Q4 */
#define ME_CTRL_START 0x01u ... ME_CTRL_RESET 0x06u
#define ME_QID_CFG_WRITE_BATTERY 0x05u
#define ME_QID_REALTIME 0x01u
#define ME_STEP_START_1 0xAAu / _2 0x55u / END_1 0x55u / END_2 0xAAu
#define ME_STEP_TERMINATOR 0xFFFFFFFFu
#define ME_STEP_HEADER_LEN 8u
#define ME_STEP_OFF_NEXT 2u / _STEP_NO 6u / _OPERATOR 8u

/* circuit_store.h */
#define ME_SLOT_INVALID (-1)
int me_circuit_slot(uint8_t circuit_id);
uint8_t me_circuit_from_slot(int slot);
```

- [ ] **Step 1: Write failing tests:** `0x11→0`, `0x18→7`, `0x21→8`, `0x88→63`, and
  `ME_SLOT_INVALID` for `0x00`, `0x01`, `0x10`, `0x09`, `0x90`, `0x99`, `0xFF`.
  Round-trip `me_circuit_from_slot(me_circuit_slot(id)) == id` for all 64 valid IDs.
- [ ] **Step 2:** Run `.\build-native.ps1`. Expect FAIL.
- [ ] **Step 3:** Implement. `slot = (sec-1)*ME_MAX_CHANNELS + (ch-1)`; reject
  `sec==0 || ch==0 || sec>8 || ch>8`.
- [ ] **Step 4:** Run `.\build-native.ps1`. Expect PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 3: Program chain walker

**Files:** Create `src/proto/program_chain.h/.c`, `tests/test_program_chain.c`

**Interfaces produced:**
```c
typedef enum { ME_CHAIN_OK = 0, ME_CHAIN_NOT_FOUND, ME_CHAIN_BAD_NEXT_INDEX,
               ME_CHAIN_BAD_END_SEQ, ME_CHAIN_TRUNCATED } me_chain_result_t;
const char *me_chain_result_name(me_chain_result_t r);
me_chain_result_t me_chain_fetch_step(const uint8_t *buf, uint32_t buf_len,
                                      uint32_t step_no,
                                      const uint8_t **step_out, uint32_t *step_len_out);
bool me_chain_is_complete(const uint8_t *buf, uint32_t buf_len);
uint32_t me_chain_count_steps(const uint8_t *buf, uint32_t buf_len);
uint16_t me_chain_step_number(const uint8_t *step);
uint8_t  me_chain_step_operator(const uint8_t *step);
```

- [ ] **Step 1: Write failing tests** against a synthetic 3-step chain: fetch step 1, 2, 3;
  `NOT_FOUND` for step 4 and step 0; `is_complete` true with terminator and false without;
  `count_steps` == 3; embedded `stepNo` and operator readable; and the malformed set —
  `nextIndex` beyond `buf_len` → `BAD_NEXT_INDEX`, wrong end sequence → `BAD_END_SEQ`,
  buffer shorter than one header → `TRUNCATED`, zero length → `TRUNCATED`, `nextIndex`
  pointing backwards → `BAD_NEXT_INDEX`.
- [ ] **Step 2:** Run `.\build-native.ps1`. Expect FAIL.
- [ ] **Step 3:** Implement the walk, ported from the old `fetchProgramStep()` but taking
  `buf`/`buf_len` as parameters and counting steps positionally.
- [ ] **Step 4:** Run `.\build-native.ps1`. Expect PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 4: Frame router

**Files:** Create `src/proto/frame_router.h/.c`, `tests/test_frame_router.c`

**Interfaces produced:**
```c
typedef struct {
    bool          valid;
    me_msg_type_t msg_type;    /* ME_MSG_NONE if not routable */
    uint8_t       start;
    uint8_t       device_id;
    uint8_t       circuit_id;
    uint8_t       query_id;
    uint32_t      body_off;    /* first payload byte after the header */
    uint32_t      body_len;
    const char   *reject;      /* NULL when valid */
} me_frame_info_t;

bool me_frame_classify(const uint8_t *buf, uint32_t len, me_frame_info_t *out);
```

- [ ] **Step 1: Write failing tests:** `0xBB` Q4 → `ME_MSG_STORE_PROGRAM` with `body_off`
  past the 2-byte length field; `0xAA` Q5 → `ME_MSG_STORE_BATTERY`; other `0xAA` query
  → `ME_MSG_STORE_CONFIG`; `0xEE` → `ME_MSG_CONTROL`; unknown start byte → `valid==false`
  with a non-NULL `reject`; frame shorter than a header → invalid; circuit ID extracted
  from byte 2 in every case.
- [ ] **Step 2:** Run `.\build-native.ps1`. Expect FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `.\build-native.ps1`. Expect PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 5: Control frame parser

**Files:** Create `src/proto/control_frame.h/.c`, `tests/test_control_frame.c`

**Interfaces produced:**
```c
typedef struct { uint8_t device_id, circuit_id, query_id; uint32_t epoch; bool has_epoch; }
        me_control_t;
typedef enum { ME_CTRL_PARSE_OK = 0, ME_CTRL_PARSE_SHORT, ME_CTRL_PARSE_BAD_START,
               ME_CTRL_PARSE_BAD_CRC, ME_CTRL_PARSE_BAD_QUERY } me_ctrl_parse_result_t;
me_ctrl_parse_result_t me_control_parse(const uint8_t *buf, uint32_t len,
                                        me_crc_order_t order, me_control_t *out);
const char *me_control_query_name(uint8_t query_id);
```

- [ ] **Step 1: Write failing tests:** a 6-byte `EE 01 11 01 <crc>` Start parses with
  `query_id == ME_CTRL_START`; Q5 Sync Time yields `has_epoch` and the 4-byte big-endian
  value; wrong start byte, short frame, corrupted CRC and an unknown query ID each return
  their own result code.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 6: Battery frame parser

**Files:** Create `src/proto/battery_frame.h/.c`, `tests/test_battery_frame.c`

**Interfaces produced:**
```c
typedef struct {
    bool     valid;
    float    nom_capacity, gassing_voltage, max_voltage, nom_current, cold_cranking_current;
    uint8_t  no_of_cells, charge_factor;
} me_battery_t;
bool me_battery_parse(const uint8_t *payload, uint32_t len, me_battery_t *out);
```

Field offsets are taken from `storeBatteryData()` in the old
`BTS_Primary_SOM/src/networkDataHandler.c` — **not from a specification, because none
exists.** The parser rejects a short payload rather than reading past it, and the header
comment records the provenance so the next reader knows it is unverified.

- [ ] **Step 1: Write failing tests:** `nom_capacity` at offset 0, `no_of_cells` at 4,
  `gassing_voltage` at 5, `max_voltage` at 9, `nom_current` at 13,
  `cold_cranking_current` at 17, `charge_factor` at 21 — each asserted individually with a
  known big-endian float; a payload one byte short returns false.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 7: Real-time frame builder

**Files:** Create `src/proto/realtime_frame.h/.c`, `tests/test_realtime_frame.c`

**Interfaces produced:**
```c
#define ME_RT_PAYLOAD_LEN 80u
#define ME_RT_HEADER_LEN   4u
#define ME_RT_FRAME_LEN   (ME_RT_HEADER_LEN + ME_RT_PAYLOAD_LEN + ME_REG_CRC_LEN) /* 86 */

typedef struct {
    uint16_t step_number, cycle_start_step, cycle_iteration, table_step, table_rows,
             registration_type;
    uint8_t  program_running, circuit_status, user_msg, operator_code, cycle_status,
             digital_inputs, digital_out_secondary, digital_out_primary;
    uint32_t error_id, step_run_ms, program_run_ms;
    float    current, voltage, temperature, power,
             accum_capacity, charge_capacity, discharge_capacity, step_capacity,
             accum_energy, charge_energy, discharge_energy, step_energy;
} me_realtime_t;

void   me_put_f32_be(uint8_t *dst, float v);
float  me_get_f32_be(const uint8_t *src);
size_t me_realtime_pack(const me_realtime_t *rt, uint8_t device_id, uint8_t circuit_id,
                        uint8_t *out, me_crc_order_t order);
```

- [ ] **Step 1: Write failing tests:** `me_put_f32_be(buf, 95.6f)` writes `42 BF 33 33` —
  the worked example from `bm_measured_param_v5.2` itself; round-trip
  `me_get_f32_be(me_put_f32_be(x)) == x`; packed length is exactly 86; header is
  `CC | dev | ckt | 0x01`; **every payload offset asserted individually** (step number 0,
  running status 2, circuit status 3, error id 5, step run ms 9, program run ms 13,
  current 17, voltage 21, temperature 25, power 29, operator 65, registration type 75,
  digital IO 77/78/79); CRC verifies big-endian over bytes 0–83.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 8: Circuit storage

**Files:** Modify `src/store/circuit_store.h/.c`; extend `tests/test_circuit_store.c`

**Interfaces produced:**
```c
typedef struct { bool valid; uint8_t query_id; uint32_t len; uint8_t raw[256]; }
        me_circuit_config_t;   /* NOT me_config_t - that name is taken by sys_init.h */

void me_store_init(void);
bool me_store_program_append(uint8_t circuit_id, const uint8_t *data, uint32_t len);
void me_store_program_reset(uint8_t circuit_id);
bool me_store_program_is_complete(uint8_t circuit_id);
uint32_t me_store_program_len(uint8_t circuit_id);
const uint8_t *me_store_program_ptr(uint8_t circuit_id);
bool me_store_battery_set(uint8_t circuit_id, const me_battery_t *b);
bool me_store_battery_get(uint8_t circuit_id, me_battery_t *out);
bool me_store_config_set(uint8_t circuit_id, uint8_t query_id,
                         const uint8_t *data, uint32_t len);
```

Host builds compile with `-DME_PROGRAM_BUF_SIZE=65536` so the test binary does not
commit 256 MB under MinGW.

- [ ] **Step 1: Write failing tests:** append two fragments and read back the concatenation;
  append past `ME_PROGRAM_BUF_SIZE` returns false and leaves the prior content intact;
  a terminated chain flips `is_complete`; appending after completion resets first
  (length equals only the new fragment); battery set/get round-trips; every accessor
  rejects an invalid CircuitID.
- [ ] **Step 2–4:** FAIL → implement → PASS.
- [ ] **Step 5: Checkpoint.**

---

## Task 9: Queue instances and the Data Manager thread

**Files:** Create `src/app_queues.h/.c`, `src/threads/data_mgr.h/.c`

**Interfaces produced:**
```c
/* app_queues.h */
extern me_msgq_t g_q_comm, g_q_core, g_q_data, g_q_can;
bool me_queues_init(void);
void me_queues_destroy(void);

/* data_mgr.h */
bool me_data_mgr_start(void);
void me_data_mgr_join(void);
```

Handles `STORE_PROGRAM` / `STORE_BATTERY` / `STORE_CONFIG` / `REQ_PROGRAM` /
`REQ_BATTERY` / `SESSION_DATA` / `CAN_DATA`. Serves `REQ_PROGRAM` as
`RSP_PROGRAM_CHUNK` messages of at most `ME_MSG_PAYLOAD_MAX`, last one flagged
`ME_MSG_FLAG_LAST`. Session ring is `ME_SESSION_RING_DEPTH` 64, dropping oldest on
overflow. Receive timeout 200 ms; bulk send timeout 500 ms; control send timeout 50 ms.

- [ ] **Step 1:** Implement. No host test — this is thread code, proven on hardware.
- [ ] **Step 2: Checkpoint** — `.\build.ps1` clean; `.\build-native.ps1` still passing.

---

## Task 10: CAN Data Manager stub

**Files:** Create `src/threads/can_mgr.h/.c`

**Interfaces produced:** `bool me_can_mgr_start(void); void me_can_mgr_join(void);`

Receives on `g_q_can` with a 200 ms timeout, logs any `CAN_TX` with a hex dump, honours
the stop flag. No CAN interface is opened.

- [ ] **Step 1:** Implement.
- [ ] **Step 2: Checkpoint.**

---

## Task 11: Demo real-time emitter

**Files:** Create `src/threads/demo_realtime.h/.c`

**Interfaces produced:**
```c
#ifndef ME_DEMO_REALTIME
#define ME_DEMO_REALTIME 1
#endif
#define ME_DEMO_TICK_MS 1000u
#define ME_DEMO_DURATION_MS 60000u
#define ME_DEMO_CURRENT_START 0.0f
#define ME_DEMO_CURRENT_STEP 1.0f
#define ME_DEMO_VOLTAGE 48.0f
#define ME_DEMO_TEMPERATURE 25.0f

typedef enum { ME_TEST_IDLE = 0, ME_TEST_RUNNING, ME_TEST_STOPPED } me_test_state_t;
const char *me_test_state_name(me_test_state_t s);

void            me_demo_init(uint8_t device_id);
void            me_demo_arm(uint8_t circuit_id);
void            me_demo_disarm(uint8_t circuit_id);
me_test_state_t me_demo_state(uint8_t circuit_id);
int             me_demo_ms_until_next_tick(void);   /* clamped to 200 */
void            me_demo_service(void);              /* emits every due frame */
```

Absolute deadlines (`next_tick_ms += ME_DEMO_TICK_MS`) so 60 ticks span 60 s without
accumulated drift. Tick 60 sends `program_running = 0` and `circuit_status = 0`, then the
circuit becomes `ME_TEST_STOPPED`.

- [ ] **Step 1:** Implement.
- [ ] **Step 2: Checkpoint.**

---

## Task 12: Core Logic thread

**Files:** Create `src/threads/core_logic.h/.c`

**Interfaces produced:** `bool me_core_logic_start(const me_system_t *sys); void me_core_logic_join(void);`

Main loop: `timeout = me_demo_ms_until_next_tick()`, `me_msgq_recv(&g_q_core, &m, timeout)`,
dispatch, then `me_demo_service()`. On `CONTROL` Start: `REQ_PROGRAM` + `REQ_BATTERY`,
assemble chunks into `g_cl_program[slot]` verifying each `offset`, hex-dump step 1, call
`me_execute_program()` which calls `me_demo_arm()`. On `CONTROL` Stop: `me_demo_disarm()`.
Pause/Continue for a stopped circuit are logged and ignored.

- [ ] **Step 1:** Implement.
- [ ] **Step 2: Checkpoint.**

---

## Task 13: Communication thread integration

**Files:** Modify `src/net/udp_sock.h/.c`, `src/sys_init.h/.c`, `src/comm_thread.c`

**Interfaces produced:**
```c
bool me_udp_dest_init(const char *ip, uint16_t port, struct sockaddr_in *out);
bool me_udp_send_to(int fd, const struct sockaddr_in *dest, const uint8_t *d, size_t n);
```
`me_system_t` gains `struct sockaddr_in udp_live_dest, udp_session_dest;`.

`idle_loop()` polls **four** descriptors — TCP, both UDP, and `me_msgq_fd(&g_q_comm)`.
Inbound TCP bytes go through `me_frame_classify()` and are routed to `g_q_data` or
`g_q_core`. Draining `g_q_comm` sends `REALTIME_DATA` to `udp_live_dest` and
`SESSION_DATA` to `udp_session_dest`.

- [ ] **Step 1:** Implement.
- [ ] **Step 2: Checkpoint.**

---

## Task 14: Wire up main and the build scripts

**Files:** Modify `src/main.c`, `build.ps1`, `build-native.ps1`

Start order: queues → store → demo → data manager → CAN → core logic → **communication
last**, so no frame can arrive before its consumer exists. Join all four, then destroy
queues and close sockets.

- [ ] **Step 1:** Implement.
- [ ] **Step 2: Checkpoint** — `.\build-native.ps1` all checks pass; `.\build.ps1` produces
  a static `ELF64 AArch64` with no dynamic section.

---

## Task 15: Documentation

**Files:** Create `Docs/ME_Primary_Implementation_Reference.md`; modify `CLAUDE.md`,
`me-primary/README.md`, `.claude/*`

The implementation reference is **file-by-file**: for each source file, its responsibility,
its public interface, its key internal logic, what it depends on, and the gotchas — so a
future reader consults one document instead of reading the whole codebase.

- [ ] **Step 1:** Write `Docs/ME_Primary_Implementation_Reference.md`.
- [ ] **Step 2:** Update `.claude/` memory files, `CLAUDE.md` and `README.md`.
- [ ] **Step 3: Final checkpoint** — both builds clean.

---

## Self-Review

**Spec coverage:** §2 queue → T1. §3 messages → T1. §4 addressing/storage → T2, T8.
§5 chain → T3. §6.1 program load → T9. §6.2 Start → T12. §6.3 session ring → T9.
§6.4 UDP destinations → T13. §6.5 CAN → T10. §6.6 demo → T7, T11. §7 lifecycle → T14.
§8 error handling → T9, T12, T13. §9 files → all. §10 testing → T1–T8. No gaps.

**Type consistency:** `me_circuit_config_t` (not `me_config_t`) throughout; `me_battery_t`
defined in T6 and consumed in T8/T9; `me_msg_t` from T1 used everywhere; `me_chain_result_t`
from T3 used in T9/T12; `me_realtime_t` from T7 used in T11.

**Known deliberate gap:** `0xAA` field offsets rest on old firmware, not a spec. Recorded in
the source header and in the design document's open items.
