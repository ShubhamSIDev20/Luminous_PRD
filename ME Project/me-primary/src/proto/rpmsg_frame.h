/*
 * rpmsg_frame.h - RPMsg wire codec for the A53 <-> M7 CAN exchange.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. All device access
 * lives in platform/rpmsg_link.c.
 *
 * Layout source: Ref Docs/RPMSG_PROTOCOL.md v3.0.
 *
 * TWO LAYERS, OPPOSITE ENDIANNESS - the easiest thing in this file to get
 * wrong:
 *
 *   RPMsg header (11 bytes)   command and length are BIG-endian
 *   can_frame_msg_t (80 B)    timestamp_ms and can_id are LITTLE-endian
 *
 * The header is a wire format defined by the protocol document; the payload
 * is the M7's native struct copied verbatim. Do not "tidy" these into one
 * byte order and do not share a helper between them.
 */
#ifndef ME_RPMSG_FRAME_H
#define ME_RPMSG_FRAME_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_RPMSG_HDR_LEN      11u
#define ME_RPMSG_PAYLOAD_MAX  80u
#define ME_RPMSG_FRAME_MAX    (ME_RPMSG_HDR_LEN + ME_RPMSG_PAYLOAD_MAX) /* 91 */

#define ME_RPMSG_SYNC_BYTE        0xAAu

#define ME_RPMSG_ACTION_WRITE_SET 0x01u
#define ME_RPMSG_ACTION_READ      0x02u

#define ME_RPMSG_RESULT_REQUEST   0x00u
#define ME_RPMSG_RESULT_SUCCESS   0x01u
#define ME_RPMSG_RESULT_FAILURE   0xFFu

#define ME_RPMSG_CMD_CAN_SET_FRAME 0x00100001u
#define ME_RPMSG_CMD_CAN_GET_FRAME 0x00100002u

/* can_frame_msg_t is exactly 80 bytes with data[64] at offset 16. */
#define ME_RPMSG_CAN_MSG_LEN   80u
#define ME_RPMSG_CAN_DATA_OFF  16u
#define ME_RPMSG_CAN_DATA_MAX  64u

typedef struct {
    uint8_t  action;
    uint32_t command;
    uint8_t  result;
    uint32_t length;
} me_rpmsg_hdr_t;

/*
 * Build one complete frame into `out`. Returns the byte count written
 * (ME_RPMSG_HDR_LEN + payload_len), or 0 if payload_len exceeds
 * ME_RPMSG_PAYLOAD_MAX or `out` is too small. `payload` may be NULL when
 * payload_len is 0.
 */
size_t me_rpmsg_wrap(uint8_t action, uint32_t command, uint8_t result,
                     const uint8_t *payload, size_t payload_len,
                     uint8_t *out, size_t out_sz);

/*
 * Decode the 11-byte header at the start of `buf`. Returns false if `len` is
 * short, the sync byte is wrong, or `length` exceeds ME_RPMSG_PAYLOAD_MAX -
 * the last of which is the recovery trigger the stream parser needs
 * (RPMSG_PROTOCOL.md section 5 step 4).
 */
bool me_rpmsg_parse_header(const uint8_t *buf, size_t len,
                           me_rpmsg_hdr_t *out);

/* The CAN-FD payload size this project always sends: one 64-byte block
 * frame per Secondary (Ref Docs/master_slave_can_v1.0.md). */
#define ME_CAN_PAYLOAD_BYTES 64u

/*
 * The M7's native can_frame_msg_t, unpacked into host fields.
 *
 * This is NOT the wire struct - do not memcpy it onto the wire. The wire
 * form is produced by me_rpmsg_pack_can(), which writes the 80 bytes with
 * explicit little-endian stores. A packed-struct memcpy would be a silent
 * trap the day this builds for a big-endian host, and would depend on
 * compiler padding rules besides.
 */
typedef struct {
    uint32_t timestamp_ms;
    uint32_t can_id;
    uint8_t  is_extended;
    uint8_t  is_fd;
    uint8_t  brs;
    uint8_t  esi;
    uint8_t  dlc;
    uint8_t  data_len;
    uint8_t  data[ME_RPMSG_CAN_DATA_MAX];
} me_rpmsg_can_t;

/*
 * Write the 80-byte can_frame_msg_t. All multi-byte fields little-endian.
 * Bytes past in->data_len are zero-filled. Returns false if data_len
 * exceeds 64.
 */
bool me_rpmsg_pack_can(const me_rpmsg_can_t *in,
                       uint8_t out80[ME_RPMSG_CAN_MSG_LEN]);

/*
 * Read the 80-byte can_frame_msg_t back. Returns false if the encoded
 * data_len exceeds 64 - a frame the M7 should never send, so it is treated
 * as corruption rather than clamped.
 */
bool me_rpmsg_parse_can(const uint8_t in80[ME_RPMSG_CAN_MSG_LEN],
                        me_rpmsg_can_t *out);

/*
 * CAN-FD DLC code for a real byte count, padding up to the next valid
 * length (RPMSG_PROTOCOL.md section 3 table). data_len over 64 returns 15.
 */
uint8_t me_rpmsg_dlc_for_len(uint8_t data_len);

/* Real byte count for a DLC code. Returns 0 for dlc > 15. */
uint8_t me_rpmsg_len_for_dlc(uint8_t dlc);

/*
 * Reassembly buffer. One read() from the RPMsg device can deliver up to the
 * 496-byte buffer payload, and a partial frame may already be held, so the
 * buffer is 496 + 91 rounded up. Sized as a compile-time constant because
 * there is no dynamic allocation anywhere in this codebase.
 */
#define ME_RPMSG_STREAM_BUF_SZ 640u

typedef struct {
    uint8_t buf[ME_RPMSG_STREAM_BUF_SZ];
    size_t  used;
    /*
     * Size of the frame handed out by the last me_rpmsg_stream_next(), still
     * sitting at the front of buf[]. It is dropped at the START of the next
     * call rather than at the end of this one, because the payload pointer
     * returned to the caller points into buf[] - consuming immediately would
     * hand back a pointer to bytes already slid away.
     */
    size_t  pending;
} me_rpmsg_stream_t;

void me_rpmsg_stream_init(me_rpmsg_stream_t *s);

/*
 * Append raw bytes from one read(). Returns how many were accepted - fewer
 * than `len` means the buffer was full and the excess was dropped, which the
 * caller should log. Dropping is deliberate: growing without bound, or
 * discarding the whole buffer, both turn a transient overrun into a
 * permanent desync.
 */
size_t me_rpmsg_stream_push(me_rpmsg_stream_t *s, const uint8_t *data,
                            size_t len);

/*
 * Extract the next complete frame. Returns false when the buffer holds no
 * whole frame yet. On true, *payload points INTO the stream buffer and stays
 * valid only until the next push or next call - copy it if you need to keep
 * it. *payload is NULL when hdr->length is 0.
 *
 * Call in a loop until it returns false: one push can contain several frames.
 */
bool me_rpmsg_stream_next(me_rpmsg_stream_t *s, me_rpmsg_hdr_t *hdr,
                          const uint8_t **payload);

#endif /* ME_RPMSG_FRAME_H */
