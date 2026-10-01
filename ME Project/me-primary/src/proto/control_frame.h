/*
 * control_frame.h - 0xEE control frame parsing.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers.
 *
 * Layout source: Docs/Ref Docs/bm_control_v3.0.md.
 *
 *   EE | DeviceNumber | CircuitNumber | QueryID | [payload] | CRC[2]
 *
 * Q2 Stop, Q3 Pause, Q4 Continue and Q6 Reset carry no payload (6-byte frame).
 * Q1 Start carries a 4-byte big-endian **Session ID** and Q5 Sync Time a 4-byte
 * big-endian **epoch** - both 10-byte frames, same offset, different meanings.
 *
 * ⚠️ The Session ID is NOT in bm_control_v3.0.md's original table. It was found
 * on hardware 2026-08-12, when a valid 10-byte Start frame was split at 6 bytes
 * and its CRC checked over the wrong span, producing a BAD_CRC on a frame whose
 * checksum was perfectly correct. See test_control_frame.c for the captured
 * bytes.
 */
#ifndef ME_CONTROL_FRAME_H
#define ME_CONTROL_FRAME_H

#include <stdbool.h>
#include <stddef.h> /* NULL */
#include <stdint.h>

#include "crc16.h"
#include "proto_defs.h"

typedef struct {
    uint8_t  device_id;
    uint8_t  circuit_id;  /* upper nibble Secondary, lower nibble Channel  */
    uint8_t  query_id;
    uint32_t epoch;       /* Q5 only; valid only when has_epoch is true    */
    bool     has_epoch;
    uint32_t session_id;  /* Q1 only; valid only when has_session_id true  */
    bool     has_session_id;
} me_control_t;

typedef enum {
    ME_CTRL_PARSE_OK = 0,
    ME_CTRL_PARSE_SHORT,     /* fewer than ME_FRAME_MIN_LEN bytes          */
    ME_CTRL_PARSE_BAD_START, /* first byte is not 0xEE                     */
    ME_CTRL_PARSE_BAD_CRC,
    ME_CTRL_PARSE_BAD_QUERY  /* query ID is not one of the six documented  */
} me_ctrl_parse_result_t;

const char *me_ctrl_parse_result_name(me_ctrl_parse_result_t r);

/* Parse a whole 0xEE frame including its CRC. */
me_ctrl_parse_result_t me_control_parse(const uint8_t *buf, uint32_t len,
                                        me_crc_order_t order,
                                        me_control_t *out);

/* "Start", "Stop", ... or "Unknown". */
const char *me_control_query_name(uint8_t query_id);

#endif /* ME_CONTROL_FRAME_H */
