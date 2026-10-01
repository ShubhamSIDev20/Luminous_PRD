# CODEBASE_MAP.md
> Updated: 2026-08-19T17:15+05:30

> 📘 For per-file detail — responsibility, interface, internal logic,
> dependencies, gotchas — see `../Docs/ME_Primary_Implementation_Reference.md`.
> This map is the tree and the index; that document is the reference.

---

## Annotated File Tree

```
ME Project/
├── me-primary/                    # THE application (was hello-world-bringup)
│   ├── src/
│   │   ├── main.c                 # Entry: arg parsing, signals, starts 4 threads.
│   │   │                          #   COMM THREAD STARTS LAST (consumers first).
│   │   ├── sys_init.[ch]          # Power-on: netinfo + UDP sockets + UDP
│   │   │                          #   DESTINATIONS + registration request.
│   │   │                          #   NOTE: me_config_t here is the CLI config.
│   │   ├── app_queues.[ch]        # The 4 queues + send/recv timeouts + stop flag.
│   │   │                          #   Depends on nothing but msgq - no cycles.
│   │   ├── msg.[ch]               # The ONE message struct crossing thread bounds
│   │   ├── threads/               # <-- LINUX-ONLY. Proven on hardware only.
│   │   │   │                      #     ALL FOUR threads live here. Nothing that
│   │   │   │                      #     runs as a thread belongs outside it.
│   │   │   ├── comm_thread.[ch]   # Owns all 3 sockets. poll() over TCP + UDP x2
│   │   │   │                      #   + q_comm eventfd. Reassembles + routes.
│   │   │   ├── core_logic.[ch]    # Test state, program assembly, step-1 extract.
│   │   │   │                      #   me_execute_program() is the execution SEAM.
│   │   │   ├── data_mgr.[ch]      # ONLY caller of circuit_store. Chunked serving
│   │   │   │                      #   + 64-slot session ring (drops oldest).
│   │   │   ├── can_mgr.[ch]       # RPMsg CAN transport: ONE poll() over g_q_can's
│   │   │   │                      #   eventfd + /dev/ttyRPMSG30. Wraps CAN_TX, turns
│   │   │   │                      #   M7's streamed replies into CAN_DATA. Active-
│   │   │   │                      #   channel bitmask attributes reply -> circuit_id.
│   │   │   │                      #   Per-Secondary/per-block SHADOW BUFFER merges
│   │   │   │                      #   each SET frame before TX (ADR-32) so channel
│   │   │   │                      #   N's setpoint never zero-stomps channel M's
│   │   │   │                      #   slot in the same 64-byte block.
│   │   │   └── post_reg.[ch]      # One-shot 0xCC after registration (COMM thread).
│   │   │                          #   Replaces demo_realtime.* (deleted, ADR-27).
│   │   ├── exec/
│   │   │   └── step_engine.[ch]   # PURE per-circuit program-step state machine.
│   │   │                          #   Clock is a PARAMETER (now_ms), never read
│   │   │                          #   internally - host-testable cutoffs (ADR-27).
│   │   ├── store/
│   │   │   ├── circuit_store.[ch] # CircuitID->slot + per-circuit program/battery/
│   │   │   │                      #   config. 1-BASED nibbles. NOT thread-safe by
│   │   │   │                      #   design - only the Data Manager calls it.
│   │   │   └── circuit_registry.[ch] # "May this circuit be handled at all?"
│   │   │                          #   64 bools. Written by comm_thread on 0xDD
│   │   │                          #   success, read by route_frame. Single-owner:
│   │   │                          #   the COMM thread. A future CAN writer must
│   │   │                          #   send a message, not call it (ADR-17).
│   │   ├── proto/                 # <-- PURE LOGIC. No sockets, no platform headers.
│   │   │   │                      #     Keep it that way (ADR-7) or host tests break.
│   │   │   ├── proto_defs.h       # SINGLE SOURCE OF TRUTH: every offset, size,
│   │   │   │                      #   port, start byte, query ID, step-chain const,
│   │   │   │                      #   capacity macro, CircuitID macros.
│   │   │   ├── crc16.[ch]         # CRC-16/Modbus. ME_CRC_ORDER_DEFAULT lives here.
│   │   │   ├── reg_frame.[ch]     # Pack 33-byte request / parse 7-byte response
│   │   │   ├── program_chain.[ch] # The AA 55 .. 55 AA walker + completion check
│   │   │   ├── frame_router.[ch]  # Inbound frame -> which thread + where it ends
│   │   │   ├── ack_frame.[ch]     # THE 7-byte ACK shape, shared by 0xAA/0xBB/0xEE.
│   │   │   │                      #   Start byte is echoed, so one packer serves all.
│   │   │   ├── program_frame.[ch] # 0xBB-only bits: which queries comm answers
│   │   │   │                      #   alone, + the Q3 count it reads but discards.
│   │   │   ├── control_frame.[ch] # 0xEE parse (Start/Stop/Pause/Continue/Sync/Reset)
│   │   │   ├── battery_frame.[ch] # 0xAA Q5 battery, 40 bytes / 12 fields. ✅ spec-
│   │   │   ├── realtime_frame.[ch]# 86-byte 0xCC build + BIG-endian float codec.
│   │   │   │                      #   Also builds the post-reg frame (permanent).
│   │   │   ├── step_decode.[ch]   # SET/CCChg/STOP step-body decode. BIG-endian.
│   │   │   │                      #   Source: old Secondary's stepData.c, cross-
│   │   │   │                      #   checked vs Program Packet V0.12 (ADR-26).
│   │   │   ├── can_frame.[ch]     # CAN-FD SET/READ_VALUES 64-byte frame pack/parse.
│   │   │   │                      #   LITTLE-endian floats - OPPOSITE of every
│   │   │   │                      #   WebApp frame above. Never share a float
│   │   │   │                      #   pack fn between this file and the others.
│   │   │   │                      #   me_can_merge_slot() (ADR-32): merges one
│   │   │   │                      #   channel's slot into a block buffer - the
│   │   │   │                      #   shadow-merge primitive can_mgr.c uses.
│   │   │   └── rpmsg_frame.[ch]   # A53<->M7 RPMsg wire codec: 11-byte header (BIG-
│   │   │                          #   endian) + 80-byte can_frame_msg_t (LITTLE-
│   │   │                          #   endian) + stream reassembler for the byte-
│   │   │                          #   stream device. Golden vectors: RPMSG_PROTOCOL.md §8.
│   │   ├── net/
│   │   │   ├── tcp_client.[ch]    # Non-blocking connect w/ timeout, full-send,
│   │   │   │                      #   timed-recv with over-long detection
│   │   │   └── udp_sock.[ch]      # UDP 10000/10001 open/bind + dest + send_to
│   │   ├── platform/
│   │   │   ├── netinfo.[ch]       # Board's own IP + MAC via SIOCGIFADDR/HWADDR.
│   │   │   │                      #   Auto-detects iface (Torizon uses ethernet0).
│   │   │   │                      #   Warns on 172.16-31.x.x (Docker bridge).
│   │   │   └── rpmsg_link.[ch]    # open/write/close on /dev/ttyRPMSGxx, raw mode.
│   │   │                          #   ME_RPMSG_DEV env var overrides the path.
│   │   │                          #   Degrades (-1), never aborts the app.
│   │   └── util/
│   │       ├── log.[ch]           # Timestamped stderr log + hex dump.
│   │       │                      #   The hex dump is the field debugging tool.
│   │       ├── msgq.[ch]          # The queue: mutex + 2 condvars + optional
│   │       │                      #   eventfd. Built by BOTH build scripts.
│   │       └── channel_list.[ch]  # PURE: parses --channels "1,2,3,4" into a
│   │                              #   validated array (ADR-32). Built by BOTH.
│   ├── tests/                     # Host-only. Built and run by build-native.ps1.
│   │   ├── test_util.h            # CHECK / CHECK_EQ_U / CHECK_BYTE / CHECK_STR
│   │   ├── test_main.c            # Runner; exits non-zero on any failure
│   │   ├── test_crc16.c           # Reference vectors + byte-order behaviour
│   │   ├── test_reg_frame.c       # EVERY field offset asserted individually
│   │   ├── test_log.c             # Hex dump format + column alignment
│   │   ├── test_msgq.c            # FIFO, timeouts, drops, 40-msg producer/consumer
│   │   ├── test_program_chain.c   # Fetch by step + EVERY malformed chain case
│   │   ├── test_frame_router.c    # Every start byte, all six 0xAA lengths, both
│   │   │                          # truncation guards, coalesced splits
│   │   │                          #   + Q1/Q3 expected lengths (6 / 8)
│   │   ├── test_ack_frame.c       # Every ACK field at its own offset, both docs'
│   │   │                          #   literal example bytes, start-byte + QueryID
│   │   │                          #   echo, both CRC orders, CRC span
│   │   ├── test_program_frame.c   # Handshake predicate + Q3 count parse/reject
│   │   ├── test_control_frame.c   # All 6 commands + each rejection code
│   │   ├── test_battery_frame.c   # All 12 offsets + the captured 46-byte frame
│   │   ├── test_realtime_frame.c  # 95.6f -> 42 BF 33 33, 80 offsets, CRC, and the
│   │   │                          # exact 86-byte post-registration frame
│   │   ├── test_circuit_store.c   # Slot mapping + append/reset/capacity rules
│   │   ├── test_circuit_registry.c# Mark/query, 64-circuit independence sweep,
│   │   │                          #   nibble-swap + row-boundary aliasing,
│   │   │                          #   malformed IDs refused, init clears all
│   │   ├── test_step_decode.c     # SET mask, CCChg+cutoff+reg-params, STOP,
│   │   │                          #   >15 cutoffs/params rejected, bad comparators
│   │   ├── test_can_frame.c       # Exact 64 bytes, LE floats, block/slot mapping
│   │   ├── test_step_engine.c     # Cutoff at exact ms, 3-strike offline, full
│   │   │                          #   SET->CCChg->STOP sequence
│   │   ├── test_rpmsg_frame.c     # RPMSG_PROTOCOL.md §8 golden vectors, DLC table,
│   │   │                          #   stream reassembly: split reads, concatenated
│   │   │                          #   frames, resync on garbage/bad length, overflow
│   │   └── test_channel_list.c    # --channels list parsing: order, out-of-range,
│   │                              #   too-many, duplicate, malformed (ADR-32)
│   ├── bin/                       # Build output (artifacts, not source)
│   ├── build.ps1                  # aarch64 static cross-build, -std=gnu11 -Werror
│   ├── build-native.ps1           # Builds AND RUNS 229 unit checks (MinGW).
│   │                              #   Passes -DME_PROGRAM_BUF_SIZE=65536.
│   ├── deploy.ps1                 # scp + docker run --network host
│   ├── deploy.bat                 # LEGACY: docker cp + exec into another team's
│   │                              #   already-running qflex-backend (ADR-30)
│   ├── Dockerfile                 # scratch, ARM64 cross-build (ADR-30)
│   ├── docker-compose.yml         # cpuset: 0-3 for CPU3 isolation (ADR-30)
│   └── README.md                  # Build/deploy/troubleshooting
│
├── .github/workflows/release.yml  # workflow_dispatch-only GHCR release (ADR-30)
├── Docs/
│   ├── ME_Primary_Implementation_Reference.md  # FILE-BY-FILE code reference
│   ├── ME_Primary_BTS_Block_Diagram.md         # The 4-thread design
│   ├── ME_Primary_Comm_Block_Diagram.md        # Registration/comms diagram
│   ├── bm_device_registration_v5.0.md          # AUTHORITATIVE frame source (ADR-4)
│   ├── specs/2026-08-07-...-registration-design.md
│   ├── specs/2026-08-14-step-execution-design.md
│   ├── plans/2026-08-11-bts-4-thread-base-plan.md
│   └── plans/2026-08-14-step-execution-plan.md
│
├── Ref Docs/                       # TOP-LEVEL, sibling of Docs/ - not nested.
│   │                               #   0xAA-0xEE, 0xA0 specs + CAN-FD
│   ├── master_slave_can_v1.0.md    #   (corrected: READ_VALUES is Master-issued too)
│   └── program_packet_v0.12.md     #   step opcodes, cross-checked vs firmware
│
├── .claude/                       # AI memory - do not reference from source
├── .vscode/tasks.json             # 3 tasks: test / build / deploy
├── .embedded-override.json        # Toolchain paths (resolve compilers from HERE)
└── CLAUDE.md                      # Project guidance
```

All Hello World bring-up leftovers (`main.c`, the `hello_world` binaries and
`hello-world-bringup.tar`) were deleted on 2026-08-07 at the developer's
direction. Nothing in the tree refers to them.

Reference documents live outside this project, in
`../ME Workspace/Reference Documents/` and `../ME_PRD/`.

> ⚠️ **`WebAppDocs/ICD.md` §3.4 is wrong** about the registration response — it
> documents 5 bytes with no CRC; the real frame is 7 bytes with a CRC.
> The developer has decided **not** to correct that file. Treat
> `Docs/bm_device_registration_v5.0.md` as authoritative and do not re-raise
> fixing the ICD. (ADR-4)

---

## Key Functions

| Name | File | What It Does |
|------|------|-------------|
| `me_crc16_modbus()` | `src/proto/crc16.c` | CRC-16/Modbus, poly `0xA001`, init `0xFFFF` |
| `me_crc16_verify()` | `src/proto/crc16.c` | Validate a frame whose last 2 bytes are its CRC |
| `me_reg_pack_request()` | `src/proto/reg_frame.c` | Build the 33-byte `0xDD` frame. Length byte computed, never hardcoded. |
| `me_reg_parse_response()` | `src/proto/reg_frame.c` | Validate the 7-byte reply. **`0x01` AND `0x02` both register** — use `ME_REG_VALUE_IS_SUCCESS()` (ADR-10). Echo mismatch warns but never blocks. |
| `me_netinfo_read()` | `src/platform/netinfo.c` | Board's real IP + MAC, with interface fallback |
| `me_sys_init()` | `src/sys_init.c` | Power-on init: identity + both UDP sockets + **both UDP destinations** + request template (circuit_id filled in per-attempt by `do_registration()`, ADR-32) |
| `me_channel_list_parse()` | `src/util/channel_list.c` | Parses `--channels "1,2,3,4"` into a validated, order-preserving array. Rejects out-of-range/duplicate/malformed rather than truncating (ADR-32) |
| `comm_thread_main()` | `src/threads/comm_thread.c` | The connection state machine loop. Registration loop now iterates every configured channel independently (ADR-32) |
| `do_registration()` | `src/threads/comm_thread.c` | Send, receive, validate, print the banner **for one explicit `circuit_id` parameter**. Diagnoses CRC-order mismatches explicitly. Called **once per configured channel, per connection** (ADR-32). |
| `idle_loop()` | `src/threads/comm_thread.c` | `poll()` 3 sockets + the queue eventfd until disconnect or stop (was `monitor()` before session #3) |
| `consume_rx_buffer()` | `src/threads/comm_thread.c` | Peel whole frames off the TCP reassembly buffer |
| `route_frame()` | `src/threads/comm_thread.c` | Classify one frame, **drop it unless its circuit is registered** (ADR-17), then answer it or send it to `q_core`/`q_data`. The single admission-control chokepoint. **Takes the socket fd** since #7 (ADR-18) — the inbound path now replies. |
| `handle_program_handshake()` | `src/threads/comm_thread.c` | Answers `0xBB` Q1 is-ready and Q3 packet count. Behind the gate (ADR-18) |
| `send_ack()` | `src/threads/comm_thread.c` | Puts an ack on the socket for `0xAA` Q5, `0xBB` Q1/Q3/Q4 or any `0xEE`. **Means QUEUED, never "done"** |
| `frame_crc_ok()` | `src/threads/comm_thread.c` | Whole-frame CRC check + computed-vs-carried diagnostic. Gates every ack |
| `drain_outbound()` | `src/threads/comm_thread.c` | Ack the eventfd, then `sendto()` each queued frame on its UDP port |
| `me_msgq_send()` / `_recv()` | `src/util/msgq.c` | Bounded queue ops. **Finite timeout, drops on expiry** (ADR-12) |
| `me_circuit_slot()` | `src/store/circuit_store.c` | CircuitID → slot. Returns `ME_SLOT_INVALID` for malformed; **never folds to 0** (ADR-13) |
| `me_registry_mark_registered()` | `src/store/circuit_registry.c` | Admit a circuit. Only caller is `do_registration()` on a `0x01`/`0x02` reply |
| `me_registry_is_registered()` | `src/store/circuit_registry.c` | The gate `route_frame()` consults. **False for malformed AND never-registered** — the caller need not distinguish |
| `me_store_program_append()` | `src/store/circuit_store.c` | Append contiguously; resets first if the previous program was complete |
| `me_chain_fetch_step()` | `src/proto/program_chain.c` | Walk the chain to step N. Counts **positionally**, not by the `stepNo` field |
| `me_chain_is_complete()` | `src/proto/program_chain.c` | True once a step carries `nextIndex == 0xFFFFFFFF` (ADR-14) |
| `me_frame_classify()` | `src/proto/frame_router.c` | Start byte → message type + circuit + body slice |
| `me_frame_expected_len()` | `src/proto/frame_router.c` | Frame length **from the layout table** — a table of assumptions, not parsing; 0 when the protocol encodes none. `0xEE` Q1 = **10** since #8 (Session ID); **all six `0xAA` queries** since #9 (ADR-22) |
| `me_frame_resolve_len()` | `src/proto/frame_router.c` | **The real length, via the CRC** when the table is wrong (ADR-19). Every inbound frame goes through this. Sets `*out_scanned` so a wrong table entry logs itself. ⚠️ Its whole-read step must stay **ungated** — see the gotchas |
| `me_ack_pack()` | `src/proto/ack_frame.c` | Build the 7-byte ack for **any** group. Start byte, QueryID, device and circuit all **echoed from the inbound frame** |
| `me_prg_query_is_handshake()` | `src/proto/program_frame.c` | Which `0xBB` queries the comm thread answers alone (Q1, Q3 — **not** Q4) |
| `me_realtime_pack()` | `src/proto/realtime_frame.c` | Build the 86-byte `0xCC` frame |
| `me_realtime_pack_post_registration()` | `src/proto/realtime_frame.c` | The one frame sent after registration: step 1, 25.0 °C, rest zero. **Permanent** — called from `post_reg.c` (ADR-27) |
| `me_battery_parse()` | `src/proto/battery_frame.c` | The **40-byte** `0xAA` Q5 payload → all 12 fields. Refuses anything under 40 |
| `me_store_battery_set()` / `_get()` | `src/store/circuit_store.c` | The per-circuit battery record, same CircuitID→slot mapping as the program buffers |
| `me_post_reg_send()` | `src/threads/post_reg.c` | The only demo-era function still called, now permanent (ADR-27) |
| `me_put_f32_be()` | `src/proto/realtime_frame.c` | IEEE-754 **big**-endian float. `95.6f → 42 BF 33 33`. WebApp-facing frames only |
| `me_step_decode()` | `src/proto/step_decode.c` | SET/CCChg/STOP step body → `me_step_t`. Big-endian. Named error per rejection, never half-parses (ADR-26) |
| `me_can_pack_set()` / `_read()` / `_pack_feedback()` | `src/proto/can_frame.c` | Build a 64-byte CAN-FD frame for one channel. **Little-endian** floats — opposite of everything above |
| `me_can_block_for_channel()` | `src/proto/can_frame.c` | Channel 1-8 → which 64-byte block + slot (1-4→Block1, 5-8→Block2) |
| `me_can_merge_slot()` | `src/proto/can_frame.c` | Merges one channel's 16-byte slot into a caller-owned block buffer, leaving other channels' slots untouched. The shadow-merge primitive `can_mgr.c` calls for every SET frame (ADR-32) |
| `me_rpmsg_wrap()` / `me_rpmsg_parse_header()` | `src/proto/rpmsg_frame.c` | 11-byte RPMsg header, **big-endian** `command`/`length` |
| `me_rpmsg_pack_can()` / `me_rpmsg_parse_can()` | `src/proto/rpmsg_frame.c` | 80-byte `can_frame_msg_t`, **little-endian** — the M7's native struct |
| `me_rpmsg_stream_push()` / `me_rpmsg_stream_next()` | `src/proto/rpmsg_frame.c` | Byte-stream reassembly for `/dev/ttyRPMSG30`; `*payload` aliases the buffer, valid only until the next call |
| `me_rpmsg_open()` / `me_rpmsg_write_all()` | `src/platform/rpmsg_link.c` | Raw-mode fd I/O; a -1 open degrades `can_mgr.c`, never aborts the app |
| `handle_can_tx()` / `handle_stream_frame()` | `src/threads/can_mgr.c` | TX to M7 / validated RX from M7; also stamp+log RPMsg RTT in `s_tx_us[]` (ADR-31). For SET frames, merges into `s_set_shadow[secondary][block]` via `me_can_merge_slot()` before transmitting the merged buffer (ADR-32) |
| `me_core_logic_start()` | `src/threads/core_logic.c` | Pins the thread to `cfg.core_affinity` (default CPU3) via `pthread_attr_setaffinity_np()`; warns if the kernel hasn't isolated that CPU (ADR-30) |
| `me_exec_start()` / `me_exec_tick()` | `src/exec/step_engine.c` | The pure per-circuit state machine. `now_ms` is a PARAMETER — never reads the clock itself (ADR-27) |
| `me_execute_program()` | `src/threads/core_logic.c` | **THE EXECUTION SEAM**, now real: starts a `step_engine` instance per circuit (ADR-27) |
| `service_engines()` | `src/threads/core_logic.c` | Ticks every circuit's engine each 10ms loop iteration, dispatches CAN_TX/REALTIME_DATA |
| `me_hex_dump()` | `src/util/log.c` | The primary field-debugging output |
| `ME_CIRCUIT_ID(sec, ch)` | `src/proto/proto_defs.h` | Pack secondary/channel into the two nibbles |

---

## Naming Conventions

| Thing | Convention | Example |
|-------|-----------|---------|
| Files | snake_case | `reg_frame.c` |
| Public functions | `me_` prefix, snake_case | `me_reg_pack_request` |
| Static functions | snake_case, no prefix | `do_registration` |
| Types | `me_` prefix, `_t` suffix | `me_reg_request_t` |
| Constants / macros | `ME_` prefix, SCREAMING_SNAKE | `ME_REG_OFF_DEVICE_ID` |
| File-scope statics | `s_` prefix | `s_stop_requested` |
| Test cases | `test_<behaviour_in_words>` | `test_pack_writes_ip_in_dotted_quad_order` |

---

## Gotchas

- **Never add socket or platform headers to `src/proto/`** — it silently drops
  out of the host test build and you lose the byte-layout safety net (ADR-7).
- **Two float endiannesses coexist on purpose.** `can_frame.c` is little-endian
  (CAN-FD); everything else (`step_decode.c`, `realtime_frame.c`) is big-endian
  (WebApp wire). Never share a pack/unpack function between the two (ADR-27).
- **A CAN-FD slot's `Command`/`STATE` byte `0x00` is an ACTIVE stop, not
  "no change".** `can_mgr.c` no longer transmits a partially zero-filled
  block frame — `handle_can_tx()` merges every SET frame into a persistent
  per-Secondary/per-block shadow buffer first (ADR-32, resolves the
  "revisit before channel 2+" item from ADR-28). If you ever bypass
  `me_can_merge_slot()` and transmit a raw `me_can_pack_set()` frame
  directly to the bus with more than one channel live, this bug returns.
- **Never hardcode a protocol constant outside `proto_defs.h`.** The length byte
  in particular is computed from field sizes so it cannot drift.
- **`--network host` is part of protocol correctness**, not a runtime detail
  (ADR-6). Removing it produces valid frames carrying a useless address.
- **No ports need publishing.** The board is a TCP *client* — outbound
  connections never require `-p`/`EXPOSE` — and `-p` is ignored under
  `--network host` regardless. Host networking is needed for the IP/MAC
  payload, not for ports. (UDP 10002/10003 discovery will one day need inbound
  broadcast, which is a further argument for host networking, not publishing.)
- **Torizon names the NIC `ethernet0`** — confirmed on hardware 2026-08-07, not
  `eth0` and not `end0`. The auto-detect fallback handles it; `--iface
  ethernet0` skips two warning lines.
- **The Docker-bridge IP warning fires on `172.16.x.x` LANs** — a known false
  positive on this site's network. A narrower MAC-prefix check was written and
  **reverted at the developer's request**. Do not re-fix without asking.
- **Read connect errors precisely.** `Connection refused` = an RST came back, so
  the path works and nothing is listening. A genuinely broken link gives
  `Network is unreachable`, `No route to host`, or a timeout. Confusing these
  wastes time debugging the board when the server is at fault.
- **Use `-std=gnu11`.** `-std=c11` hides `struct ifreq`, `IFNAMSIZ`, `IFF_UP`.
- **No `getaddrinfo`** — static glibc + NSS (ADR-8). Dotted-quad + `inet_pton`.
- **CRC byte order is BIG-ENDIAN — high byte first.** ✅ Hardware-verified in
  **both directions** 2026-08-10 (ADR-9): request `0x369E` → `36 9E`, response
  `0xBE15` → `BE 15`. Supersedes ADR-5's little-endian reading. The single
  source of truth is `ME_CRC_ORDER_DEFAULT` in `src/proto/crc16.h` — never
  spell `le`/`be` as a default anywhere else; that three-copy drift *was* the
  original bug. The `--crc-order` flag is kept as a diagnostic.
- **Response Value `0x02` is SUCCESS**, not a rejection (ADR-10). Use
  `ME_REG_VALUE_IS_SUCCESS()`; never compare against
  `ME_REG_VALUE_REGISTERED` directly. Getting this wrong caused a 30-second
  re-registration loop on 2026-08-10.
- **Registration is sent once per TCP connection, structurally.** There is no
  "already registered" flag — `do_registration()` is called once per
  connection and `idle_loop()` never registers. Do not add a guard flag; it
  would be a second source of truth for something the control flow already
  guarantees.
- **"The TCP connection keeps disconnecting" is usually the board closing it.**
  `me_tcp_close()` runs *unconditionally* after `do_registration()` returns
  false, then the backoff loop reconnects. Before suspecting the network,
  check whether the response is being classified as a failure. This exact
  symptom on 2026-08-10 was the `0x02` bug, not a network fault.
- **Do not pin the client source port.** Proposed on 2026-08-10 and rejected:
  the board performs the active close, so its socket enters `TIME_WAIT` and a
  fixed local port would fail `bind()` on the 1-second retry. The changing
  ephemeral port is a symptom of reconnecting, never a cause.
- **A passing `build-native.ps1` proves protocol logic only** — nothing about
  aarch64, Torizon, Docker or sockets (ADR-3). Proven again on 2026-08-11: the
  host build was green while the cross-build failed on undeclared `NULL`, which
  MinGW's headers happened to pull in transitively and the ARM toolchain's did
  not. **Run both builds before claiming anything compiles.**
- **Never declare a `me_msgq_t` as a local.** It is ~1 MB (16 slots × 64 KB) and
  overflows a default thread stack. All four live at file scope in
  `app_queues.c` (ADR-11).
- **CircuitID nibbles are 1-BASED.** `0x11` is the first circuit and maps to
  slot 0. `0x00`, `0x01`, `0x10`, `0x09`, `0x90`, `0xFF` are malformed and are
  rejected — they must never fold to slot 0, or a bad ID silently overwrites
  Secondary 1 Channel 1's program (ADR-13).
- **`ME_STEP_START_1` is `0xAA` — the same byte as `ME_START_CONFIG`.** Separate
  namespaces: a step packet never appears on the wire alone, a config frame never
  appears inside a program buffer. Keep the constant names distinct.
- **`nextIndex` in a program step is an ABSOLUTE offset** into the program
  buffer. The Data Manager must append packets contiguously from offset 0;
  anything inserted between them invalidates every offset in the chain (ADR-14).
- **`me_config_t` is the COMMAND-LINE config** (`sys_init.h`). The per-circuit
  stored configuration is `me_circuit_config_t` (`store/circuit_store.h`).
  Since ADR-32, `me_config_t` carries `channels[]`/`channel_count` (from
  `--channels`), not a single `channel` scalar — check both sites if you see
  old code still reading `cfg.channel`.
- **Registration now loops per configured channel, independently** (ADR-32).
  `do_registration()` takes an explicit `circuit_id` parameter rather than
  reading `sys->reg_request.circuit_id` — `sys->reg_request` is a template
  only (device_id/name/ip/mac shared across channels); `circuit_id` is filled
  in per attempt. A rejection on one channel does not stop the others.
- **`0xAA` frames carry no length field anywhere** — but the *layout* fixes each
  query's size, and since ADR-22 the table knows them (Q1–Q4 = 6, Q5 = 46, Q6 = 10),
  so coalesced reads split correctly. Q7–Q10 stay 0: they are **broadcast frames
  with a 2-byte header** and every `0xAA` path here assumes 4 (T-48).
- ⚠️ **Never gate `me_frame_resolve_len()`'s whole-read step on `layout == 0`.** It
  was, and adding the correct 46-byte Q5 entry then made a *short* `0xAA` frame
  truncate at a coincidental CRC match — a correct table entry breaking an
  unrelated frame. See the ADR-19 amendment and
  `test_resolve_len_does_not_truncate_when_the_layout_overshoots`.
- **The `0xAA` battery layout IS specification-backed** since 2026-08-12:
  `Ref Docs/bm_config_v6.0.md` §5.3, confirmed against a captured frame in §5.4.
  The payload is **40 bytes / 12 fields**; it was 22 and the remaining 18 bytes were
  being silently discarded (ADR-21). Impedance and Energy Density are `float` —
  developer-confirmed 2026-08-12, so the source spreadsheet's `uint32`-looking
  samples are an error in the spreadsheet, not an alternative encoding (T-47 closed).
- **A battery payload under 40 bytes is REFUSED**, not half-parsed, and Q5 is
  NACKed. Deliberate — a record with `valid = true` and a zeroed nominal voltage
  would let a test run against nonsense (ADR-21, T-46).
- **`demo_realtime.c/.h` is DELETED (ADR-27).** `me_execute_program()` in
  `core_logic.c` now starts a real `step_engine` instance per circuit. The
  post-registration frame survives as `post_reg.c`, called from
  `comm_thread.c` — same single-caller, own-buffer pattern as before.
- **Every queue send has a finite timeout and drops on expiry** (ADR-12). When a
  frame "never arrived", read the `queues:` lines logged at shutdown first — a
  non-zero drop count names the queue and is logged at WARN.
- **Git repo since session #9/#10** (ADR-24). `main` = stable; `develop` =
  ongoing work — commit there, not to `main` directly.
