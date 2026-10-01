/*
 * program_frame.c - 0xBB program-group responses.
 */
#include "program_frame.h"

bool me_prg_query_is_handshake(uint8_t query_id)
{
    return (query_id == ME_QID_PRG_IS_READY)
           || (query_id == ME_QID_PRG_PACKET_COUNT);
}

bool me_prg_parse_packet_count(const uint8_t *frame, uint32_t len,
                               uint16_t *out_count)
{
    if (frame == NULL || out_count == NULL || len < ME_PRG_Q3_LEN) {
        return false;
    }
    if (frame[ME_HDR_OFF_START] != ME_START_PROGRAM) {
        return false;
    }
    if (frame[ME_HDR_OFF_QUERY_ID] != ME_QID_PRG_PACKET_COUNT) {
        return false;
    }

    /* Big-endian, matching every other multi-byte field in this protocol. */
    *out_count = (uint16_t)(((uint16_t)frame[ME_PRG_OFF_PKT_COUNT] << 8)
                            | (uint16_t)frame[ME_PRG_OFF_PKT_COUNT + 1u]);
    return true;
}
