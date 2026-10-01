/*
 * crc16.c - CRC-16/Modbus implementation.
 *
 * Bitwise rather than table-driven: the largest frame in this protocol is
 * 33 bytes, so a 512-byte lookup table would cost more RAM than the loop
 * costs cycles, and the bitwise form is easier to check against the spec.
 */
#include "crc16.h"

#define ME_CRC16_POLY 0xA001u /* reflected 0x8005 */
#define ME_CRC16_INIT 0xFFFFu

uint16_t me_crc16_modbus(const uint8_t *data, size_t len)
{
    uint16_t crc = ME_CRC16_INIT;

    for (size_t i = 0; i < len; i++) {
        crc ^= (uint16_t)data[i];
        for (int bit = 0; bit < 8; bit++) {
            if (crc & 1u) {
                crc = (uint16_t)((crc >> 1) ^ ME_CRC16_POLY);
            } else {
                crc = (uint16_t)(crc >> 1);
            }
        }
    }
    return crc;
}

void me_crc16_append(uint8_t *frame, size_t body_len, me_crc_order_t order)
{
    const uint16_t crc = me_crc16_modbus(frame, body_len);
    const uint8_t lo = (uint8_t)(crc & 0xFFu);
    const uint8_t hi = (uint8_t)((crc >> 8) & 0xFFu);

    if (order == ME_CRC_ORDER_BE) {
        frame[body_len]     = hi;
        frame[body_len + 1] = lo;
    } else {
        frame[body_len]     = lo;
        frame[body_len + 1] = hi;
    }
}

uint16_t me_crc16_read(const uint8_t *frame, size_t total_len, me_crc_order_t order)
{
    const uint8_t a = frame[total_len - 2];
    const uint8_t b = frame[total_len - 1];

    if (order == ME_CRC_ORDER_BE) {
        return (uint16_t)(((uint16_t)a << 8) | b);
    }
    return (uint16_t)(((uint16_t)b << 8) | a);
}

bool me_crc16_verify(const uint8_t *frame, size_t total_len, me_crc_order_t order)
{
    if (total_len < 3u) {
        return false;
    }
    const uint16_t expected = me_crc16_modbus(frame, total_len - 2u);
    return me_crc16_read(frame, total_len, order) == expected;
}

static char lower_ascii(char c)
{
    return (c >= 'A' && c <= 'Z') ? (char)(c - 'A' + 'a') : c;
}

bool me_crc_order_parse(const char *s, me_crc_order_t *out)
{
    if (s == NULL || out == NULL) {
        return false;
    }
    if (lower_ascii(s[0]) == 'l' && lower_ascii(s[1]) == 'e' && s[2] == '\0') {
        *out = ME_CRC_ORDER_LE;
        return true;
    }
    if (lower_ascii(s[0]) == 'b' && lower_ascii(s[1]) == 'e' && s[2] == '\0') {
        *out = ME_CRC_ORDER_BE;
        return true;
    }
    return false;
}

const char *me_crc_order_name(me_crc_order_t order)
{
    return (order == ME_CRC_ORDER_BE) ? "big-endian (CRC_HI CRC_LO)"
                                      : "little-endian (CRC_LO CRC_HI)";
}
