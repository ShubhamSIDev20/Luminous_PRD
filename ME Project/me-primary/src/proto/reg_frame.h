/*
 * reg_frame.h - device registration frame (0xDD / Q1) pack and parse.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. This is what makes the
 * byte layout testable on the Windows dev laptop, where an offset error costs
 * seconds instead of a board round-trip.
 */
#ifndef ME_REG_FRAME_H
#define ME_REG_FRAME_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "crc16.h"
#include "proto_defs.h"

typedef struct {
    uint8_t device_id;
    uint8_t circuit_id; /* build with ME_CIRCUIT_ID(secondary, channel) */
    char    device_name[ME_REG_NAME_LEN + 1]; /* NUL-terminated; packed truncated/padded */
    uint8_t ip[ME_REG_IP_LEN];
    uint8_t mac[ME_REG_MAC_LEN];
} me_reg_request_t;

/*
 * Pack req into out, which must have room for ME_REG_REQUEST_LEN bytes.
 * Returns the number of bytes written (always ME_REG_REQUEST_LEN).
 */
size_t me_reg_pack_request(const me_reg_request_t *req,
                           uint8_t *out,
                           me_crc_order_t order);

typedef enum {
    ME_REG_PARSE_OK = 0,
    ME_REG_PARSE_BAD_LENGTH,
    ME_REG_PARSE_BAD_START,
    ME_REG_PARSE_BAD_QUERY,
    ME_REG_PARSE_BAD_CRC
} me_reg_parse_result_t;

typedef struct {
    uint8_t device_id;  /* echoed by the server */
    uint8_t circuit_id; /* echoed by the server */
    uint8_t value;
    bool    registered;    /* server considers the device registered:
                            * value is 0x01 or 0x02 (ME_REG_VALUE_IS_SUCCESS).
                            * Read `value` to tell the two apart. */
    bool    echo_mismatch; /* echo differs from what was sent - informational */
} me_reg_response_t;

/*
 * Parse a registration response.
 *
 * sent may be NULL, in which case echo_mismatch is always false. Echo
 * mismatches never cause a parse failure: only the Value byte decides
 * registration.
 */
me_reg_parse_result_t me_reg_parse_response(const uint8_t *buf,
                                            size_t len,
                                            const me_reg_request_t *sent,
                                            me_crc_order_t order,
                                            me_reg_response_t *out);

const char *me_reg_parse_result_name(me_reg_parse_result_t r);
const char *me_reg_value_name(uint8_t value);

#endif /* ME_REG_FRAME_H */
