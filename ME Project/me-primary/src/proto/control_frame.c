/*
 * control_frame.c - 0xEE control frame parsing.
 */
#include "control_frame.h"

#include <string.h>

const char *me_ctrl_parse_result_name(me_ctrl_parse_result_t r)
{
    switch (r) {
    case ME_CTRL_PARSE_OK:        return "OK";
    case ME_CTRL_PARSE_SHORT:     return "SHORT";
    case ME_CTRL_PARSE_BAD_START: return "BAD_START";
    case ME_CTRL_PARSE_BAD_CRC:   return "BAD_CRC";
    case ME_CTRL_PARSE_BAD_QUERY: return "BAD_QUERY";
    default:                      return "UNKNOWN";
    }
}

const char *me_control_query_name(uint8_t query_id)
{
    switch (query_id) {
    case ME_CTRL_START:     return "Start";
    case ME_CTRL_STOP:      return "Stop";
    case ME_CTRL_PAUSE:     return "Pause";
    case ME_CTRL_CONTINUE:  return "Continue";
    case ME_CTRL_SYNC_TIME: return "SyncTime";
    case ME_CTRL_RESET:     return "Reset";
    default:                return "Unknown";
    }
}

static bool query_is_known(uint8_t q)
{
    return q == ME_CTRL_START || q == ME_CTRL_STOP || q == ME_CTRL_PAUSE
        || q == ME_CTRL_CONTINUE || q == ME_CTRL_SYNC_TIME
        || q == ME_CTRL_RESET;
}

me_ctrl_parse_result_t me_control_parse(const uint8_t *buf, uint32_t len,
                                        me_crc_order_t order,
                                        me_control_t *out)
{
    memset(out, 0, sizeof(*out));

    if (buf == NULL || len < ME_FRAME_MIN_LEN) {
        return ME_CTRL_PARSE_SHORT;
    }

    /* Start byte before CRC: a frame from the wrong command group should say
     * so, not be reported as corruption. */
    if (buf[ME_HDR_OFF_START] != ME_START_CONTROL) {
        return ME_CTRL_PARSE_BAD_START;
    }
    if (!me_crc16_verify(buf, len, order)) {
        return ME_CTRL_PARSE_BAD_CRC;
    }

    const uint8_t qid = buf[ME_HDR_OFF_QUERY_ID];
    if (!query_is_known(qid)) {
        return ME_CTRL_PARSE_BAD_QUERY;
    }

    out->device_id  = buf[ME_HDR_OFF_DEVICE];
    out->circuit_id = buf[ME_HDR_OFF_CIRCUIT];
    out->query_id   = qid;

    /*
     * Two commands carry a 4-byte big-endian field at the same offset, meaning
     * different things. Q5's is a clock; Q1's identifies the test session.
     *
     * They are read into SEPARATE struct members on purpose. One shared "arg"
     * field would make a mis-parsed Sync Time frame present as a plausible
     * session ID, or the reverse - and both are just four opaque bytes on the
     * wire, so nothing downstream could tell.
     */
    if (qid == ME_CTRL_SYNC_TIME && len >= ME_CTRL_SYNC_LEN) {
        const uint8_t *e = &buf[ME_CTRL_OFF_EPOCH];
        out->epoch = ((uint32_t)e[0] << 24) | ((uint32_t)e[1] << 16)
                   | ((uint32_t)e[2] << 8)  | (uint32_t)e[3];
        out->has_epoch = true;
    } else if (qid == ME_CTRL_START && len >= ME_CTRL_START_LEN) {
        const uint8_t *s = &buf[ME_CTRL_OFF_SESSION_ID];
        out->session_id = ((uint32_t)s[0] << 24) | ((uint32_t)s[1] << 16)
                        | ((uint32_t)s[2] << 8)  | (uint32_t)s[3];
        out->has_session_id = true;
    }

    return ME_CTRL_PARSE_OK;
}
