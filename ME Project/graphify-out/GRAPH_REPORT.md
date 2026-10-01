# Graph Report - C:\Users\NNEVREKAR\OneDrive - Ador Powertron Ltd\Documents\Projects\MicroME\ME_PRD\ME Project  (2026-08-17)

## Corpus Check
- 106 files · ~146,747 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 772 nodes · 1935 edges · 44 communities (35 shown, 9 thin omitted)
- Extraction: 72% EXTRACTED · 28% INFERRED · 0% AMBIGUOUS · INFERRED: 546 edges (avg confidence: 0.81)
- Token cost: 62,000 input · 9,500 output

## Community Hubs (Navigation)
- Core Logic & Circuit Store
- CRC16 & Frame Router
- System Bootstrap & Thread Lifecycle
- Registration Frame Protocol (0xDD)
- Message Queue & Comm Thread State Machine
- ACK Frame & Program Handshake
- Control Frame Protocol (0xEE)
- Comm Thread Routing & Logging
- Step Execution Engine
- Design Concepts & Rationale Notes
- Step Decode (Program Op Parsing)
- Program Chain Walking (0xAA55)
- CAN Frame Protocol
- Battery Config Frame (0xAA)
- ADRs: Protocol & Frame Decisions
- Circuit Registry (Admission Control)
- ADRs: Step Execution & CAN Simulation
- Registration Design Docs & ICD
- Realtime Frame Tests
- .claude Docs & Step Execution Design
- BM Reference Doc: Frame Headers
- Realtime/Post-Registration Frame Packing
- BM Calibration Reference: Presets
- BM Calibration Reference: Voltage/Temp
- BM Reference: Factory & Session Fields
- Design Plans & Verification Ladder
- Legacy stepData.c Cross-Reference
- BM Reference: Broadcast Read/Write
- CRC Byte-Order Design Notes
- ADRs: Queue Rationale
- ICD Response Table Issues
- BM Reference: Manufacturing Data
- README: Deploy & Verification
- BM Reference: CAN/UDP Realtime
- ADR-1: Arm GNU Toolchain
- ADR-2: Static Linking
- ADR-24: Git Workflow Adoption
- ADR-3: No Local aarch64 Execution
- ADR-6: --network host
- ADR-8: No getaddrinfo
- README: proto/ Purity

## God Nodes (most connected - your core abstractions)
1. `me_crc16_append()` - 34 edges
2. `run_reg_frame_tests()` - 30 edges
3. `me_circuit_slot()` - 27 edges
4. `me_crc16_verify()` - 26 edges
5. `me_step_decode()` - 26 edges
6. `run_frame_router_tests()` - 26 edges
7. `ME Primary Implementation Reference` - 26 edges
8. `me_msgq_send()` - 25 edges
9. `main()` - 23 edges
10. `me_reg_parse_response()` - 23 edges

## Surprising Connections (you probably didn't know these)
- `me_circuit_slot()` --semantically_similar_to--> `ME_CIRCUIT_ID`  [INFERRED] [semantically similar]
  me-primary/src/store/circuit_store.c → Docs/specs/2026-08-07-me-primary-registration-design.md
- `STORE_CONFIG Frames Are Deliberately Not Acknowledged (ADR-18)` --references--> `me_frame_classify()`  [INFERRED]
  Ref Docs/bm_config_v6.0.md → me-primary/src/proto/frame_router.c
- `Program Q4 — Send Program Step Data (16-bit length prefix)` --implements--> `me_chain_fetch_step()`  [INFERRED]
  Ref Docs/bm_program_v3.0.md → me-primary/src/proto/program_chain.c
- `Registration Q1 — Register Device (length-prefixed payload)` --implements--> `me_reg_pack_request()`  [INFERRED]
  Ref Docs/bm_device_registration_v5.0.md → me-primary/src/proto/reg_frame.c
- `0xA0 Calibration Data Frame (V4.2)` --references--> `me_crc16_modbus()`  [INFERRED]
  Ref Docs/bm_calibration_v4.2.md → me-primary/src/proto/crc16.c

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Four-Thread Message-Passing Architecture** — concept_four_thread_architecture [EXTRACTED 1.00]
- **Program Load Pipeline (0xBB Q4 to Chain Completion)** — concept_program_chain_format [EXTRACTED 1.00]
- **Battery Configuration Write Pipeline (0xAA Q5)** — concept_battery_payload_v6 [EXTRACTED 1.00]
- **Host-testable pure logic layer under src/proto, src/util, src/store (ADR-7)** — me_primary_src_proto_proto_defs_me_reg_value_is_success, me_primary_src_store_circuit_store_me_circuit_slot, me_primary_src_proto_program_chain_me_chain_fetch_step, me_primary_src_proto_frame_router_me_frame_classify, me_primary_src_proto_realtime_frame_me_realtime_pack, docs_specs_2026_08_07_me_primary_registration_design_adr_7_pure_proto_layer [EXTRACTED 1.00]
- **BTS four-thread base joined by in-process message queues** — me_primary_src_comm_thread_idle_loop, me_primary_src_threads_core_logic_me_core_logic_start, me_primary_src_threads_data_mgr_me_data_mgr_start, me_primary_src_threads_can_mgr_me_can_mgr_start, me_primary_src_app_queues_me_queues_init [EXTRACTED 1.00]
- **Registration success classification and the IDLE state flow** — me_primary_src_proto_proto_defs_me_reg_value_is_success, me_primary_src_proto_reg_frame_me_reg_parse_response, me_primary_src_comm_thread_do_registration, me_primary_src_comm_thread_idle_loop, me_primary_src_comm_thread_me_comm_state_t, docs_specs_2026_08_10_registration_idle_state_design_adr_10 [EXTRACTED 1.00]
- **Two-Point Current Calibration Flow (preset → ADC count → gain/offset, per Range)** — ref_docs_bm_calibration_v4_2_q1_hw_ready, ref_docs_bm_calibration_v4_2_q2_live_data, ref_docs_bm_calibration_v4_2_q3_q4_charge_current_presets, ref_docs_bm_calibration_v4_2_q5_charge_gain_offset, ref_docs_bm_calibration_v4_2_calibration_sequence, ref_docs_bm_calibration_v4_2_range_byte [EXTRACTED 1.00]
- **BM Wire Protocol Frame Family (six start bytes, shared 4-byte header and CRC-16/Modbus trailer)** — ref_docs_bm_device_registration_v5_0_registration_frame_0xdd, ref_docs_bm_config_v6_0_config_frame_0xaa, ref_docs_bm_program_v3_0_program_frame_0xbb, ref_docs_bm_measured_param_v5_2_realtime_frame_0xcc, ref_docs_bm_control_v3_0_control_frame_0xee, ref_docs_bm_calibration_v4_2_calibration_frame_0xa0, ref_docs_bm_config_v6_0_crc16_modbus_big_endian, me_primary_src_proto_frame_router_me_frame_classify [EXTRACTED 1.00]
- **Q5 Battery Configuration: Spec Layout → Captured Frame → Parser Confirmation** — ref_docs_bm_config_v6_0_q5_write_battery_config, ref_docs_bm_config_v6_0_battery_payload_byte_map, ref_docs_bm_config_v6_0_id_byte_not_transmitted, ref_docs_bm_config_v6_0_captured_q5_frame, ref_docs_bm_config_v6_0_impedance_energy_density_float, me_primary_src_proto_battery_frame_me_battery_parse, me_primary_src_store_circuit_store_me_store_battery_set [EXTRACTED 1.00]
- **Per-Circuit Admission Control Chokepoint** — _claude_decisions_adr_17, mod_comm_thread, mod_frame_router, mod_circuit_registry [INFERRED 0.85]
- **CAN-FD Step Execution Pipeline** — mod_core_logic, mod_step_engine, mod_can_frame, mod_can_mgr, _claude_decisions_adr_27 [INFERRED 0.80]
- **Frame-Length-Table / CRC-Backstop Regression Saga** — _claude_decisions_adr_19, _claude_decisions_adr_22, mod_frame_router, proto_0xaa_config_frame [INFERRED 0.80]

## Communities (44 total, 9 thin omitted)

### Community 0 - "Core Logic & Circuit Store"
Cohesion: 0.07
Nodes (63): Absolute-deadline demo ticks avoid accumulated drift, No dynamic allocation on any path, CircuitID nibble encoding (Secondary | Channel), me_circuit_config_t, me_msg_type_t, me_msg_type_name(), ME_CIRCUIT_ID, me_battery_t (+55 more)

### Community 1 - "CRC16 & Frame Router"
Cohesion: 0.08
Nodes (61): CRC Byte Order as a Runtime Switch (-CrcOrder), me_crc_order_t, lower_ascii(), me_crc16_append(), me_crc16_modbus(), me_crc16_read(), me_crc16_verify(), me_crc_order_name() (+53 more)

### Community 2 - "System Bootstrap & Thread Lifecycle"
Cohesion: 0.06
Nodes (41): One inbox queue per thread, Consumers-before-producers thread start order, me_netinfo_t, me_app_request_stop(), me_queues_destroy(), me_queues_init(), me_queues_report(), me_config_t (+33 more)

### Community 3 - "Registration Frame Protocol (0xDD)"
Cohesion: 0.09
Nodes (52): Spec: Registration Success Semantics and the IDLE State, ADR-10: Value 0x02 is success; board idles after registering, ME_COMM_IDLE state (collapses REGISTERED + MONITORING), 30-second re-registration loop defect, Response 0x02 (Already Registered) Means Success, Golden Byte-for-Byte Registration Frame Vector, do_registration, me_comm_is_registered (+44 more)

### Community 4 - "Message Queue & Comm Thread State Machine"
Cohesion: 0.10
Nodes (45): Bounded queue with drop-on-timeout sends, eventfd-backed queue joined into a single poll() set, Capped exponential backoff (1s to 30s), never exit, Register exactly once per TCP connection, me_msgq_t, report_one(), comm_thread_main, idle_loop (formerly monitor) (+37 more)

### Community 5 - "ACK Frame & Program Handshake"
Cohesion: 0.10
Nodes (30): me_crc_order_t, me_ack_pack(), me_prg_parse_packet_count(), me_prg_query_is_handshake(), me_hex_line(), run_ack_frame_tests(), test_ack_can_report_failure(), test_ack_crc_covers_every_body_byte() (+22 more)

### Community 6 - "Control Frame Protocol (0xEE)"
Cohesion: 0.12
Nodes (31): me_control_t, me_ctrl_parse_result_t, me_crc_order_t, me_control_parse(), me_control_query_name(), me_ctrl_parse_result_name(), query_is_known(), build() (+23 more)

### Community 7 - "Comm Thread Routing & Logging"
Cohesion: 0.14
Nodes (29): me_comm_state_t, me_log_level_t, me_app_stop_requested(), me_tcp_close(), me_tcp_connect(), me_tcp_recv_response(), me_tcp_send_all(), set_nonblocking() (+21 more)

### Community 8 - "Step Execution Engine"
Cohesion: 0.18
Nodes (31): me_exec_ctx_t, me_exec_output_kind_t, me_exec_state_t, advance_time(), build_can_output(), me_can_feedback_t, me_exec_output_t, me_step_t (+23 more)

### Community 9 - "Design Concepts & Rationale Notes"
Cohesion: 0.11
Nodes (31): Shared 7-byte acknowledgement shape across 0xAA/0xBB/0xEE, Admission control: drop frames for unregistered circuits before they reach a queue, Impedance and energy-density resolved as float, not integer, 40-byte 0xAA Q5 battery-configuration payload (12 fields, bm_config_v6.0.md), CircuitID to storage-slot mapping (1-based Secondary/Channel nibbles), CRC-16/Modbus algorithm (poly 0xA001, init 0xFFFF, no final XOR), 1 Hz / 60 s demo real-time emitter (temporary scaffolding), Four-Thread Battery Testing Architecture (+23 more)

### Community 10 - "Step Decode (Program Op Parsing)"
Cohesion: 0.20
Nodes (28): action_value_width(), me_step_t, decode_cutoffs(), decode_reg_params(), get_f32_be(), get_u16_be(), get_u32_be(), me_step_decode() (+20 more)

### Community 11 - "Program Chain Walking (0xAA55)"
Cohesion: 0.19
Nodes (26): AA 55 ... 55 AA program step chain with 0xFFFFFFFF terminator, me_chain_result_t, me_chain_count_steps(), me_chain_fetch_step(), me_chain_is_complete(), me_chain_result_name(), me_chain_step_number(), me_chain_step_operator() (+18 more)

### Community 12 - "CAN Frame Protocol"
Cohesion: 0.17
Nodes (20): me_can_block_t, me_can_setpoint_t, me_can_feedback_t, get_f32_le(), me_can_block_for_channel(), me_can_id(), me_can_pack_feedback(), me_can_pack_read() (+12 more)

### Community 13 - "Battery Config Frame (0xAA)"
Cohesion: 0.17
Nodes (21): storeBatteryData() (legacy BTS_Primary_SOM firmware), 0xAA battery field offsets rest on old firmware, not a spec, me_battery_t, get_u16_be(), me_battery_parse(), build_payload(), run_battery_frame_tests(), test_captured_web_app_frame() (+13 more)

### Community 14 - "ADRs: Protocol & Frame Decisions"
Cohesion: 0.16
Nodes (19): ADR-10: Response 0x02 is success; board idles, ADR-17: Per-circuit admission gate in route_frame(), ADR-18: Board answers 0xBB Q1/Q3/Q4 behind admission gate, ADR-19: CRC is the frame delimiter of last resort, ADR-20: Transcribe config workbook into bm_config_v6.0.md, ADR-21: Battery record is 40 bytes; short payload refused, ADR-22: 0xAA gets real frame-length entries; CRC backstop, ADR-5: CRC byte order runtime-selectable (superseded) (+11 more)

### Community 15 - "Circuit Registry (Admission Control)"
Cohesion: 0.38
Nodes (16): me_registry_init(), me_registry_is_registered(), me_registry_mark_registered(), run_circuit_registry_tests(), test_every_circuit_registers_independently(), test_fresh_circuit_is_not_registered(), test_init_clears_every_slot(), test_init_clears_previously_registered_circuits() (+8 more)

### Community 16 - "ADRs: Step Execution & CAN Simulation"
Cohesion: 0.16
Nodes (18): ADR-14: Program completion via chain terminator, ADR-15: 1 Hz cadence from queue receive timeout, ADR-16: Demo real-time emitter quarantined, ADR-23: One 0xCC frame per successful registration (temporary demo), ADR-25: CAN-FD SET_VALUES/READ_VALUES two-phase exchange, ADR-27: Real program-step execution; demo_realtime deleted, ADR-28: Zero-fill unaddressed CAN slots; block selection deferred, can_frame module (+10 more)

### Community 17 - "Registration Design Docs & ICD"
Cohesion: 0.23
Nodes (11): ADR-13: Malformed CircuitID rejected, never folded to slot 0, ADR-4: bm_device_registration_v5.0 authoritative; ICD wrong, bm_device_registration_v5.0 (authoritative legacy protocol doc), ME Primary Comm Block Diagram (documentation deliverable), ME Primary Implementation Reference (file-by-file), Spec: ME Primary TCP/UDP Communication & Device Registration, ADR-6: docker run --network host is mandatory, 0xDD Registration Frame (+3 more)

### Community 18 - "Realtime Frame Tests"
Cohesion: 0.31
Nodes (13): me_get_f32_be(), me_realtime_t, fill_known(), run_realtime_frame_tests(), test_crc_is_big_endian_over_the_body(), test_every_payload_offset(), test_float_encoding_matches_the_spec_example(), test_float_round_trip() (+5 more)

### Community 19 - ".claude Docs & Step Execution Design"
Cohesion: 0.33
Nodes (8): Step Execution Implementation Plan, Step Execution Design, Program Packet V0.7, Program Packet Operator/Cutoff/Logic/Registration Op-Codes, TABLE step operator (0x12), BM Config v6.0, Master-Slave CAN-FD Protocol v1.0, Program Packet V0.12

### Community 20 - "BM Reference Doc: Frame Headers"
Cohesion: 0.18
Nodes (11): me-primary Source Layout (threads / proto / net / store / util), 2-Byte Broadcast Frame Header (Q7–Q10), Broadcast Frames Mis-Parse Under the 4-Byte Header, 4-Byte Circuit-Scoped Frame Header, 0xAA Configuration Data Frame (v6.0), Config Q1 — Is HW Ready to Read/Write Configuration, Registration Q1 — Register Device (length-prefixed payload), Registration Q2 — Delete Device (+3 more)

### Community 21 - "Realtime/Post-Registration Frame Packing"
Cohesion: 0.31
Nodes (10): me_crc_order_t, me_realtime_t, me_put_f32_be(), me_realtime_pack(), me_realtime_pack_post_registration(), put_u16_be(), put_u32_be(), me_post_reg_send() (+2 more)

### Community 22 - "BM Calibration Reference: Presets"
Cohesion: 0.22
Nodes (10): Two-Point Gain/Offset Calibration Sequence, Calibration Q3/Q4 — Charging Current Low/High Point Presets, Calibration Q5 — Current Gain and Offset (Charge Mode), Calibration Q6/Q7 — Discharging Current Low/High Point Presets, Calibration Q8 — Current Gain and Offset (Discharge Mode), Calibration Range Byte, Calibration Range Table (0xFF, 0x01–0x04), Power-Fail Resume Context via EEPROM Real-Time Block (+2 more)

### Community 23 - "BM Calibration Reference: Voltage/Temp"
Cohesion: 0.22
Nodes (9): CALIB_DATA_MAX_LENGTH = 31, 0xA0 Calibration Data Frame (V4.2), Live Temperature Field (Float + Integer, new in V4.2), Calibration Q11 — Voltage Gain and Offset (Charge Mode), Calibration Q12–Q14 — Discharge Voltage Low/High/Gain+Offset, Calibration Q15–Q23 — Cancel/Stop/Verify (layouts unverified), Calibration Q1 — Is HW Ready for Calibration, Calibration Q2 — Live Current/Voltage/Temperature Packet (31 bytes) (+1 more)

### Community 24 - "BM Reference: Factory & Session Fields"
Cohesion: 0.22
Nodes (9): Duplicate Factory Parameter ID 0x05 (two UDP port fields), Factory Data Parameters (IDs 0x01–0x23, 131 bytes), 18 PID Gains — {Kp,Ki,Kd} x {CC/CV/CP Charge & Discharge}, Config Q2 — Read All Factory Configuration Data, STORE_CONFIG Frames Are Deliberately Not Acknowledged (ADR-18), Registration Q3 — Discovery Broadcast (full network info response), Operator Code Field (CCChg, CCDchg, CVChg, …), 4-Byte Session ID Prefix on the Port 10001 Payload (+1 more)

### Community 25 - "Design Plans & Verification Ladder"
Cohesion: 0.29
Nodes (7): ME Primary BTS Block Diagram (source spec), Plan: Registration Success Semantics and IDLE State, Build-and-verify checkpoint (replaces commits; not a git repo), Plan: BTS 4-Thread Base, ADR-3: a native build never proves the board target works, ADR-7: src/proto/ stays free of socket and platform headers, Three-layer verification ladder

### Community 26 - "Legacy stepData.c Cross-Reference"
Cohesion: 0.40
Nodes (6): ADR-26: CCChg registration-parameters block always present, step_decode module, CCChg step operator (0x01), SET step operator (0x0A), STOP/STO step operator (0x0B), stepData.c (legacy Secondary firmware)

### Community 27 - "BM Reference: Broadcast Read/Write"
Cohesion: 0.33
Nodes (6): Config Q7/Q8 — Broadcast Write Factory / Manufacturing Data, Config Q9/Q10 — Broadcast Read Factory / Manufacturing Data, 4-Byte Unique Number (broadcast target identifier), Wrong-CRC Response Zeroes the Query ID, Registration Q4 — Device IP Configuration Broadcast, 8-Byte Unique ID (MAC-derived device identifier)

### Community 28 - "CRC Byte-Order Design Notes"
Cohesion: 0.50
Nodes (5): Golden response vector DD 01 01 11 02 15 BE, ADR-9: CRC-16 transmitted big-endian (CRC_HI CRC_LO), CRC byte-order ambiguity resolved as a runtime parameter, Golden 33-byte registration request vector (ends AD BE), ME_CRC_ORDER_DEFAULT

### Community 29 - "ADRs: Queue Rationale"
Cohesion: 0.67
Nodes (3): ADR-11: In-process message queues, not POSIX mq_open, ADR-12: Every queue send has a finite timeout, app_queues / msgq (in-process queues)

### Community 30 - "ICD Response Table Issues"
Cohesion: 0.67
Nodes (3): ICD §3.4 response table is wrong (5 bytes, no CRC), Length byte settles the header field order, WebAppDocs ICD.md (Web Application interface control document)

### Community 31 - "BM Reference: Manufacturing Data"
Cohesion: 0.67
Nodes (3): Manufacturing Data Parameters (IDs 0x97–0x9F, 57 bytes), Config Q3 — Read All Manufacturing Information Data, Read-Only SW Version Fields (0x97–0x99, 11-byte ASCII, no NUL)

## Ambiguous Edges - Review These
- `0xA0 Calibration Data Frame (V4.2)` → `Calibration Q15–Q23 — Cancel/Stop/Verify (layouts unverified)`  [AMBIGUOUS]
  Ref Docs/bm_calibration_v4.2.md · relation: references
- `Q5 Battery Configuration 40-Byte Payload Byte Map` → `Battery ID / Charge Factor ID Collision at 0x6B`  [AMBIGUOUS]
  Ref Docs/bm_config_v6.0.md · relation: references
- `Factory Data Parameters (IDs 0x01–0x23, 131 bytes)` → `Duplicate Factory Parameter ID 0x05 (two UDP port fields)`  [AMBIGUOUS]
  Ref Docs/bm_config_v6.0.md · relation: references

## Knowledge Gaps
- **48 isolated node(s):** `networkDataHandler.c (legacy BTS_Primary_SOM firmware)`, `.claude/DECISIONS.md (ADR log)`, `bm_control_v3.0.md`, `bm_program_v3.0.md`, `TCP stream reassembly (rxbuf accumulation and frame-boundary resolution)` (+43 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `0xA0 Calibration Data Frame (V4.2)` and `Calibration Q15–Q23 — Cancel/Stop/Verify (layouts unverified)`?**
  _Edge tagged AMBIGUOUS (relation: references) - confidence is low._
- **What is the exact relationship between `Q5 Battery Configuration 40-Byte Payload Byte Map` and `Battery ID / Charge Factor ID Collision at 0x6B`?**
  _Edge tagged AMBIGUOUS (relation: references) - confidence is low._
- **What is the exact relationship between `Factory Data Parameters (IDs 0x01–0x23, 131 bytes)` and `Duplicate Factory Parameter ID 0x05 (two UDP port fields)`?**
  _Edge tagged AMBIGUOUS (relation: references) - confidence is low._
- **Why does `Plan: BTS 4-Thread Base` connect `Design Plans & Verification Ladder` to `Core Logic & Circuit Store`, `Registration Design Docs & ICD`, `Message Queue & Comm Thread State Machine`?**
  _High betweenness centrality (0.127) - this node is a cross-community bridge._
- **Why does `me_msgq_send()` connect `Message Queue & Comm Thread State Machine` to `Core Logic & Circuit Store`, `System Bootstrap & Thread Lifecycle`, `Comm Thread Routing & Logging`, `Realtime/Post-Registration Frame Packing`, `Design Plans & Verification Ladder`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **Why does `me_core_logic_start()` connect `Core Logic & Circuit Store` to `Design Plans & Verification Ladder`, `System Bootstrap & Thread Lifecycle`, `Program Chain Walking (0xAA55)`, `Message Queue & Comm Thread State Machine`?**
  _High betweenness centrality (0.080) - this node is a cross-community bridge._
- **Are the 31 inferred relationships involving `me_crc16_append()` (e.g. with `CRC Byte Order as a Runtime Switch (-CrcOrder)` and `me_ack_pack()`) actually correct?**
  _`me_crc16_append()` has 31 INFERRED edges - model-reasoned connections that need verification._