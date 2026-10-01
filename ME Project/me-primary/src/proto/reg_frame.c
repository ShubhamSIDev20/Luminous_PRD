/*
 * reg_frame.c - device registration frame (0xDD / Q1) pack and parse.
 */
#include "reg_frame.h"

#include <string.h>

/* Length of a NUL-terminated string, capped at max. Avoids strnlen, which is
 * not uniformly available across the two toolchains this builds under. */
static size_t bounded_len(const char *s, size_t max)
{
    size_t n = 0;
    while (n < max && s[n] != '\0') {
        n++;
    }
    return n;
}

size_t me_reg_pack_request(const me_reg_request_t *req,
                           uint8_t *out,
                           me_crc_order_t order)
{
    memset(out, 0, ME_REG_REQUEST_LEN);

    out[ME_REG_OFF_START]      = ME_START_REGISTRATION;
    out[ME_REG_OFF_QUERY_ID]   = ME_QID_REGISTER;
    out[ME_REG_OFF_LENGTH]     = (uint8_t)ME_REG_PAYLOAD_LEN;
    out[ME_REG_OFF_DEVICE_ID]  = req->device_id;
    out[ME_REG_OFF_CIRCUIT_ID] = req->circuit_id;

    /* Name: copy what fits, leave the rest as the zeros memset already wrote.
     * A name of exactly 16 characters therefore fills the field with no NUL
     * spilling into the IP address at offset 21. */
    const size_t name_len = bounded_len(req->device_name, ME_REG_NAME_LEN);
    memcpy(&out[ME_REG_OFF_NAME], req->device_name, name_len);

    memcpy(&out[ME_REG_OFF_IP], req->ip, ME_REG_IP_LEN);
    memcpy(&out[ME_REG_OFF_MAC], req->mac, ME_REG_MAC_LEN);

    me_crc16_append(out, ME_REG_OFF_CRC, order);

    return (size_t)ME_REG_REQUEST_LEN;
}

me_reg_parse_result_t me_reg_parse_response(const uint8_t *buf,
                                            size_t len,
                                            const me_reg_request_t *sent,
                                            me_crc_order_t order,
                                            me_reg_response_t *out)
{
    memset(out, 0, sizeof(*out));

    if (len != (size_t)ME_REG_RESPONSE_LEN) {
        return ME_REG_PARSE_BAD_LENGTH;
    }
    if (buf[ME_RSP_OFF_START] != ME_START_REGISTRATION) {
        return ME_REG_PARSE_BAD_START;
    }
    if (buf[ME_RSP_OFF_QUERY_ID] != ME_QID_REGISTER) {
        return ME_REG_PARSE_BAD_QUERY;
    }
    if (!me_crc16_verify(buf, len, order)) {
        return ME_REG_PARSE_BAD_CRC;
    }

    out->device_id  = buf[ME_RSP_OFF_DEVICE_ID];
    out->circuit_id = buf[ME_RSP_OFF_CIRCUIT_ID];
    out->value      = buf[ME_RSP_OFF_VALUE];
    out->registered = ME_REG_VALUE_IS_SUCCESS(out->value);

    /* Informational only: an echo mismatch never blocks registration.
     * Only the Value byte decides. */
    if (sent != NULL) {
        out->echo_mismatch = (out->device_id != sent->device_id) ||
                             (out->circuit_id != sent->circuit_id);
    }

    return ME_REG_PARSE_OK;
}

const char *me_reg_parse_result_name(me_reg_parse_result_t r)
{
    switch (r) {
    case ME_REG_PARSE_OK:         return "ok";
    case ME_REG_PARSE_BAD_LENGTH: return "wrong length (expected 7 bytes)";
    case ME_REG_PARSE_BAD_START:  return "wrong start byte (expected 0xDD)";
    case ME_REG_PARSE_BAD_QUERY:  return "wrong query ID (expected 0x01)";
    case ME_REG_PARSE_BAD_CRC:    return "CRC mismatch";
    default:                      return "unknown";
    }
}

const char *me_reg_value_name(uint8_t value)
{
    switch (value) {
    case ME_REG_VALUE_FAILED:             return "Failed";
    case ME_REG_VALUE_REGISTERED:         return "Registered";
    case ME_REG_VALUE_ALREADY_REGISTERED: return "Already Registered";
    default:                              return "unrecognised";
    }
}
