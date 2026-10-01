/* rpmsg_frame.c - see rpmsg_frame.h for the format source. */
#include "rpmsg_frame.h"

#include <string.h>

/* Header fields only. The payload is little-endian and must never come
 * through here. */
static void put_u32_be(uint8_t *dst, uint32_t v)
{
    dst[0] = (uint8_t)(v >> 24);
    dst[1] = (uint8_t)(v >> 16);
    dst[2] = (uint8_t)(v >> 8);
    dst[3] = (uint8_t)(v);
}

static uint32_t get_u32_be(const uint8_t *src)
{
    return ((uint32_t)src[0] << 24) | ((uint32_t)src[1] << 16)
         | ((uint32_t)src[2] << 8)  | (uint32_t)src[3];
}

size_t me_rpmsg_wrap(uint8_t action, uint32_t command, uint8_t result,
                     const uint8_t *payload, size_t payload_len,
                     uint8_t *out, size_t out_sz)
{
    if (payload_len > ME_RPMSG_PAYLOAD_MAX) {
        return 0u;
    }
    const size_t total = ME_RPMSG_HDR_LEN + payload_len;
    if (out == NULL || out_sz < total) {
        return 0u;
    }
    if (payload_len > 0u && payload == NULL) {
        return 0u;
    }

    out[0] = ME_RPMSG_SYNC_BYTE;
    out[1] = action;
    put_u32_be(&out[2], command);
    out[6] = result;
    put_u32_be(&out[7], (uint32_t)payload_len);
    if (payload_len > 0u) {
        memcpy(&out[ME_RPMSG_HDR_LEN], payload, payload_len);
    }
    return total;
}

bool me_rpmsg_parse_header(const uint8_t *buf, size_t len,
                           me_rpmsg_hdr_t *out)
{
    if (buf == NULL || out == NULL || len < ME_RPMSG_HDR_LEN) {
        return false;
    }
    if (buf[0] != ME_RPMSG_SYNC_BYTE) {
        return false;
    }
    const uint32_t length = get_u32_be(&buf[7]);
    if (length > ME_RPMSG_PAYLOAD_MAX) {
        return false;
    }

    out->action  = buf[1];
    out->command = get_u32_be(&buf[2]);
    out->result  = buf[6];
    out->length  = length;
    return true;
}

/* Payload fields only - little-endian, the M7's native order. Deliberately
 * separate from the big-endian header helpers above. */
static void put_u32_le(uint8_t *dst, uint32_t v)
{
    dst[0] = (uint8_t)(v);
    dst[1] = (uint8_t)(v >> 8);
    dst[2] = (uint8_t)(v >> 16);
    dst[3] = (uint8_t)(v >> 24);
}

static uint32_t get_u32_le(const uint8_t *src)
{
    return (uint32_t)src[0] | ((uint32_t)src[1] << 8)
         | ((uint32_t)src[2] << 16) | ((uint32_t)src[3] << 24);
}

/* Index = DLC - 9; DLC 0-8 map 1:1 and are handled arithmetically. */
static const uint8_t k_dlc_len[7] = { 12u, 16u, 20u, 24u, 32u, 48u, 64u };

uint8_t me_rpmsg_dlc_for_len(uint8_t data_len)
{
    uint8_t i;

    if (data_len <= 8u) {
        return data_len;
    }
    for (i = 0u; i < 7u; i++) {
        if (data_len <= k_dlc_len[i]) {
            return (uint8_t)(9u + i);
        }
    }
    return 15u;
}

uint8_t me_rpmsg_len_for_dlc(uint8_t dlc)
{
    if (dlc <= 8u) {
        return dlc;
    }
    if (dlc <= 15u) {
        return k_dlc_len[dlc - 9u];
    }
    return 0u;
}

bool me_rpmsg_pack_can(const me_rpmsg_can_t *in,
                       uint8_t out80[ME_RPMSG_CAN_MSG_LEN])
{
    if (in == NULL || out80 == NULL) {
        return false;
    }
    if (in->data_len > ME_RPMSG_CAN_DATA_MAX) {
        return false;
    }

    memset(out80, 0, ME_RPMSG_CAN_MSG_LEN);
    put_u32_le(&out80[0], in->timestamp_ms);
    put_u32_le(&out80[4], in->can_id);
    out80[8]  = in->is_extended;
    out80[9]  = in->is_fd;
    out80[10] = in->brs;
    out80[11] = in->esi;
    out80[12] = in->dlc;
    out80[13] = in->data_len;
    /* out80[14..15] stay zero: the reserved field. */
    if (in->data_len > 0u) {
        memcpy(&out80[ME_RPMSG_CAN_DATA_OFF], in->data, in->data_len);
    }
    return true;
}

bool me_rpmsg_parse_can(const uint8_t in80[ME_RPMSG_CAN_MSG_LEN],
                        me_rpmsg_can_t *out)
{
    if (in80 == NULL || out == NULL) {
        return false;
    }
    if (in80[13] > ME_RPMSG_CAN_DATA_MAX) {
        return false;
    }

    out->timestamp_ms = get_u32_le(&in80[0]);
    out->can_id       = get_u32_le(&in80[4]);
    out->is_extended  = in80[8];
    out->is_fd        = in80[9];
    out->brs          = in80[10];
    out->esi          = in80[11];
    out->dlc          = in80[12];
    out->data_len     = in80[13];
    memcpy(out->data, &in80[ME_RPMSG_CAN_DATA_OFF], ME_RPMSG_CAN_DATA_MAX);
    return true;
}

void me_rpmsg_stream_init(me_rpmsg_stream_t *s)
{
    if (s != NULL) {
        s->used    = 0u;
        s->pending = 0u;
    }
}

size_t me_rpmsg_stream_push(me_rpmsg_stream_t *s, const uint8_t *data,
                            size_t len)
{
    size_t space;

    if (s == NULL || data == NULL || len == 0u) {
        return 0u;
    }
    space = ME_RPMSG_STREAM_BUF_SZ - s->used;
    if (len > space) {
        len = space;
    }
    if (len > 0u) {
        memcpy(&s->buf[s->used], data, len);
        s->used += len;
    }
    return len;
}

/* Drop the first `n` bytes, sliding the remainder down. */
static void stream_consume(me_rpmsg_stream_t *s, size_t n)
{
    if (n >= s->used) {
        s->used = 0u;
        return;
    }
    memmove(&s->buf[0], &s->buf[n], s->used - n);
    s->used -= n;
}

bool me_rpmsg_stream_next(me_rpmsg_stream_t *s, me_rpmsg_hdr_t *hdr,
                          const uint8_t **payload)
{
    if (s == NULL || hdr == NULL || payload == NULL) {
        return false;
    }

    /* Drop the frame handed out last call, now that the caller has finished
     * reading it. See the `pending` comment in the header. */
    if (s->pending > 0u) {
        stream_consume(s, s->pending);
        s->pending = 0u;
    }

    for (;;) {
        size_t i;
        size_t total;

        /* 1. Scan to the sync byte, discarding anything before it. */
        for (i = 0u; i < s->used; i++) {
            if (s->buf[i] == ME_RPMSG_SYNC_BYTE) {
                break;
            }
        }
        if (i > 0u) {
            stream_consume(s, i);
        }

        /* 2. Need a whole header before the length field can be trusted. */
        if (s->used < ME_RPMSG_HDR_LEN) {
            return false;
        }

        /* 3. A header that will not parse - wrong sync, or a length over 80 -
         *    means this 0xAA was payload data, not a real frame start. Skip
         *    exactly one byte and rescan (RPMSG_PROTOCOL.md section 5 step 4).
         *    Discarding more would swallow the genuine frame behind it. */
        if (!me_rpmsg_parse_header(s->buf, s->used, hdr)) {
            stream_consume(s, 1u);
            continue;
        }

        /* 4. Wait for the payload to arrive. */
        total = ME_RPMSG_HDR_LEN + (size_t)hdr->length;
        if (s->used < total) {
            return false;
        }

        /* 5. One whole frame. The payload pointer aliases the buffer, which
         *    is why the contract says it dies on the next call. */
        *payload = (hdr->length > 0u) ? &s->buf[ME_RPMSG_HDR_LEN] : NULL;

        /* The caller reads *payload before touching the stream again, so
         * consuming here would invalidate it. Instead record the frame size
         * and drop it at the START of the next call. */
        s->pending = total;
        return true;
    }
}
