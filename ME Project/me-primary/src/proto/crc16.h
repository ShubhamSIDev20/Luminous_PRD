/*
 * crc16.h - CRC-16/Modbus for the ME Primary Board protocol.
 *
 * Polynomial 0xA001 (reflected), init 0xFFFF, no final XOR.
 * Reference vector: "123456789" -> 0x4B37.
 *
 * Byte order on the wire: HIGH BYTE FIRST (big-endian). The two source
 * documents disagree -
 *   - bm_device_registration_v5.0 writes the trailer as "CRC_HI CRC_LO"
 *   - WebAppDocs/ICD.md section 3.2 states "appended Little Endian"
 * - and per the project rule that bm_device_registration_v5.0 wins, the
 * big-endian reading is the one implemented. A CRC of 0xADBE therefore goes
 * out as "AD BE", not "BE AD".
 *
 * Both orders produce a well-formed frame, so a wrong choice fails only inside
 * the peer's checksum test - never in parsing. The order stays a runtime
 * parameter (--crc-order) so a mismatch can be diagnosed without a rebuild,
 * but big-endian is the default and the intended wire behaviour.
 */
#ifndef ME_CRC16_H
#define ME_CRC16_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef enum {
    ME_CRC_ORDER_LE = 0, /* low byte first  - Modbus RTU convention */
    ME_CRC_ORDER_BE = 1  /* high byte first - "CRC_HI CRC_LO" reading */
} me_crc_order_t;

/*
 * The order used when nothing overrides it. SINGLE SOURCE OF TRUTH: every
 * default (sys_init, the --crc-order help text, deploy.ps1) must trace back
 * here rather than spelling "le"/"be" out again.
 */
#define ME_CRC_ORDER_DEFAULT ME_CRC_ORDER_BE

/* Compute CRC-16/Modbus over len bytes. */
uint16_t me_crc16_modbus(const uint8_t *data, size_t len);

/* Write the CRC of frame[0..body_len-1] into frame[body_len] and
 * frame[body_len+1], in the given byte order. */
void me_crc16_append(uint8_t *frame, size_t body_len, me_crc_order_t order);

/* Verify a frame whose final two bytes are its CRC.
 * total_len includes the two CRC bytes. Returns false if total_len < 3. */
bool me_crc16_verify(const uint8_t *frame, size_t total_len, me_crc_order_t order);

/* Read the CRC stored in the final two bytes of a frame, honouring order.
 * Caller guarantees total_len >= 2. */
uint16_t me_crc16_read(const uint8_t *frame, size_t total_len, me_crc_order_t order);

/* "le" / "be", case-insensitive. Returns false if the string is neither. */
bool me_crc_order_parse(const char *s, me_crc_order_t *out);

const char *me_crc_order_name(me_crc_order_t order);

#endif /* ME_CRC16_H */
