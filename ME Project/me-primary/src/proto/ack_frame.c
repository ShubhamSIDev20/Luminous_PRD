/*
 * ack_frame.c - the one acknowledgement shape shared by 0xAA, 0xBB and 0xEE.
 */
#include "ack_frame.h"

size_t me_ack_pack(uint8_t start, uint8_t device_id, uint8_t circuit_id,
                   uint8_t query_id, uint8_t value, me_crc_order_t order,
                   uint8_t *out)
{
    out[ME_HDR_OFF_START]    = start;
    out[ME_HDR_OFF_DEVICE]   = device_id;
    out[ME_HDR_OFF_CIRCUIT]  = circuit_id;
    out[ME_HDR_OFF_QUERY_ID] = query_id;
    out[ME_ACK_OFF_VALUE]    = value;

    /* The CRC span is derived from the frame length, never written as a literal
     * 5: if a field is ever added, the span follows it. */
    me_crc16_append(out, ME_ACK_LEN - ME_REG_CRC_LEN, order);

    return ME_ACK_LEN;
}
