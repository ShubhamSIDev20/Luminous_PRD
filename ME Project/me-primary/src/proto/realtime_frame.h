/*
 * realtime_frame.h - the 86-byte 0xCC measured-parameter frame.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers.
 *
 * Layout source: Docs/Ref Docs/bm_measured_param_v5.2.md.
 *   4 header + 80 payload + 2 CRC = 86 bytes.
 *   Header: CC | DeviceNumber | CircuitNumber | QueryID(0x01)
 *
 * The same frame goes to UDP 10000 (live data). The UDP 10001 session variant
 * inserts a 4-byte Session ID ahead of the payload and is not built here yet.
 *
 * Floats are IEEE 754 single-precision, BIG-ENDIAN: the spec's own worked
 * example is 42 BF 33 33 = 95.6 A.
 */
#ifndef ME_REALTIME_FRAME_H
#define ME_REALTIME_FRAME_H

#include <stddef.h>
#include <stdint.h>

#include "crc16.h"
#include "proto_defs.h"

#define ME_RT_PAYLOAD_LEN 80u
#define ME_RT_HEADER_LEN  ME_HDR_LEN
#define ME_RT_FRAME_LEN   (ME_RT_HEADER_LEN + ME_RT_PAYLOAD_LEN + ME_REG_CRC_LEN)

/* Payload offsets. Frame index = payload offset + ME_RT_HEADER_LEN. */
#define ME_RT_OFF_STEP_NUMBER      0u
#define ME_RT_OFF_PROGRAM_RUNNING  2u
#define ME_RT_OFF_CIRCUIT_STATUS   3u
#define ME_RT_OFF_USER_MSG         4u
#define ME_RT_OFF_ERROR_ID         5u
#define ME_RT_OFF_STEP_RUN_MS      9u
#define ME_RT_OFF_PROGRAM_RUN_MS  13u
#define ME_RT_OFF_CURRENT         17u
#define ME_RT_OFF_VOLTAGE         21u
#define ME_RT_OFF_TEMPERATURE     25u
#define ME_RT_OFF_POWER           29u
#define ME_RT_OFF_ACCUM_CAPACITY  33u
#define ME_RT_OFF_CHG_CAPACITY    37u
#define ME_RT_OFF_DIS_CAPACITY    41u
#define ME_RT_OFF_STEP_CAPACITY   45u
#define ME_RT_OFF_ACCUM_ENERGY    49u
#define ME_RT_OFF_CHG_ENERGY      53u
#define ME_RT_OFF_DIS_ENERGY      57u
#define ME_RT_OFF_STEP_ENERGY     61u
#define ME_RT_OFF_OPERATOR        65u
#define ME_RT_OFF_CYCLE_STATUS    66u
#define ME_RT_OFF_CYCLE_START     67u
#define ME_RT_OFF_CYCLE_ITER      69u
#define ME_RT_OFF_TABLE_STEP      71u
#define ME_RT_OFF_TABLE_ROWS      73u
#define ME_RT_OFF_REG_TYPE        75u
#define ME_RT_OFF_DIGITAL_IN      77u
#define ME_RT_OFF_DIGITAL_OUT_SEC 78u
#define ME_RT_OFF_DIGITAL_OUT_PRI 79u

/* Circuit Status values (payload offset 3). */
#define ME_RT_CIRCUIT_IDLE      0x00u
#define ME_RT_CIRCUIT_CHARGE    0x01u
#define ME_RT_CIRCUIT_DISCHARGE 0x02u
#define ME_RT_CIRCUIT_PAUSE     0x03u
#define ME_RT_CIRCUIT_CONTINUE  0x04u
#define ME_RT_CIRCUIT_INT       0x05u
#define ME_RT_CIRCUIT_ERROR     0x06u
#define ME_RT_CIRCUIT_MSG       0x07u

/* Program Running Status values (payload offset 2). */
#define ME_RT_PROGRAM_IDLE    0x00u
#define ME_RT_PROGRAM_RUNNING 0x01u

typedef struct {
    uint16_t step_number;
    uint8_t  program_running;
    uint8_t  circuit_status;
    uint8_t  user_msg;
    uint32_t error_id;
    uint32_t step_run_ms;
    uint32_t program_run_ms;
    float    current;
    float    voltage;
    float    temperature;
    float    power;
    float    accum_capacity;
    float    charge_capacity;
    float    discharge_capacity;
    float    step_capacity;
    float    accum_energy;
    float    charge_energy;
    float    discharge_energy;
    float    step_energy;
    uint8_t  operator_code;
    uint8_t  cycle_status;
    uint16_t cycle_start_step;
    uint16_t cycle_iteration;
    uint16_t table_step;
    uint16_t table_rows;
    uint16_t registration_type;
    uint8_t  digital_inputs;
    uint8_t  digital_out_secondary;
    uint8_t  digital_out_primary;
} me_realtime_t;

/* IEEE 754 single-precision, big-endian. Shared with battery_frame.c, which
 * decodes the same encoding. */
void  me_put_f32_be(uint8_t *dst, float v);
float me_get_f32_be(const uint8_t *src);

/*
 * Build the frame into out[], which must hold at least ME_RT_FRAME_LEN bytes.
 * Returns the number of bytes written (always ME_RT_FRAME_LEN).
 */
size_t me_realtime_pack(const me_realtime_t *rt, uint8_t device_id,
                        uint8_t circuit_id, uint8_t *out, me_crc_order_t order);

/*
 * Builds the one-shot frame sent after a successful device registration, so
 * the Web Application has live data to display before a Start command has
 * been issued and real step execution has produced anything of its own.
 * Called from src/threads/post_reg.c.
 *
 * Contents fixed by the developer, 2026-08-12: step_number = 1, temperature =
 * 25.0 C, every other field zero, both status bytes Idle. For device 0x01 and
 * circuit 0x11 the output is byte-for-byte the 86-byte frame the Web Application
 * is known to accept, CRC 0xF261 - asserted in
 * test_post_registration_frame_matches_the_developer_bytes().
 *
 * It lives HERE, in a pure and therefore host-testable module, rather than in
 * the Linux-only thread that calls it - so the exact frame can be proven on
 * the laptop instead of discovered on hardware.
 *
 * out[] must hold at least ME_RT_FRAME_LEN bytes. Returns bytes written.
 */

/* The two fields the frame actually carries. Defined HERE, not in the
 * Linux-only caller, so this module stays pure and host-testable. */
#define ME_RT_POST_REG_STEP_NUMBER  1u
#define ME_RT_POST_REG_TEMPERATURE 25.0f

size_t me_realtime_pack_post_registration(uint8_t device_id, uint8_t circuit_id,
                                          uint8_t *out, me_crc_order_t order);

#endif /* ME_REALTIME_FRAME_H */
