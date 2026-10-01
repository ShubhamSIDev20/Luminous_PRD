/*
 * frame_router.h - classify an inbound frame and decide which thread owns it.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. It includes ../msg.h,
 * which is itself pure - the ADR-7 rule bans socket and platform headers from
 * proto/, not plain data definitions.
 *
 * The router is the fork in the road for every byte the Web Application sends.
 * Misrouting is silent: a program packet delivered to Core Logic, or a Start
 * command delivered to the Data Manager, is simply ignored at the far end with
 * nothing logged. That is why this is pure and tested rather than a switch
 * buried in the communication thread.
 */
#ifndef ME_FRAME_ROUTER_H
#define ME_FRAME_ROUTER_H

#include <stdbool.h>
#include <stddef.h> /* NULL */
#include <stdint.h>

#include "../msg.h"
#include "crc16.h"
#include "proto_defs.h"

typedef struct {
    bool          valid;
    me_msg_type_t msg_type;  /* ME_MSG_NONE when valid but not routable    */
    uint8_t       start;
    uint8_t       device_id;
    uint8_t       circuit_id;
    uint8_t       query_id;
    uint32_t      body_off;  /* first byte to forward                       */
    uint32_t      body_len;  /* how many bytes to forward                   */
    const char   *reject;    /* NULL when valid; a reason otherwise         */
} me_frame_info_t;

/*
 * Classify buf[0..len-1]. Returns out->valid.
 *
 * Does NOT verify the CRC: each consumer re-verifies with its own parser, and
 * the router must be able to report a corrupt frame's start byte for the log.
 *
 * body_off/body_len describe what to copy into the message payload:
 *   0xBB Q4  - the step bytes only, past the 2-byte length field
 *   0xAA     - the payload, past the header, excluding the CRC
 *   0xEE     - the WHOLE frame, because Core Logic re-parses and re-verifies it
 */
bool me_frame_classify(const uint8_t *buf, uint32_t len, me_frame_info_t *out);

/*
 * Total length of the frame starting at buf[0], or 0 when it cannot be
 * determined from the bytes available.
 *
 * TCP is a stream: two frames can arrive in one read() and one frame can arrive
 * split across two. This is what lets the communication thread split a
 * coalesced read into individual frames.
 *
 * It returns 0 for 0xAA and for a 0xBB query other than Q4, because THOSE
 * FRAMES CARRY NO LENGTH FIELD - the protocol simply does not encode it. The
 * caller must then fall back to treating the remainder of the read as one
 * frame, which is what the old firmware did for every frame type. Returning a
 * guess here would silently mis-split.
 */
uint32_t me_frame_expected_len(const uint8_t *buf, uint32_t len);

/*
 * The frame's REAL length, using its CRC as the delimiter.
 *
 * WHY THIS EXISTS. me_frame_expected_len() above is a table of layout knowledge,
 * because this protocol carries no length field in most frames. On 2026-08-12 one
 * entry was wrong - 0xEE Start was believed to have no payload when it carries a
 * 4-byte Session ID - and the cost was not one rejected frame. The frame was
 * split at 6 bytes, its CRC computed over the wrong span and reported as
 * corruption, and the 4 leftover bytes then desynced EVERY following frame on the
 * connection. A wrong entry here is a non-local failure.
 *
 * The CRC is the only reliable delimiter the protocol actually provides, so:
 *
 *   1. take the layout length if it verifies                (the normal case)
 *   2. otherwise scan for the first length in
 *      [ME_FRAME_MIN_LEN, avail] whose CRC verifies         (recovery)
 *   3. otherwise return the layout length unchanged         (old behaviour)
 *
 * Step 2 also fixes the coalesced-0xAA case, which previously had no boundary at
 * all - see the old note on me_frame_expected_len() returning 0.
 *
 * *out_scanned is set true only when step 2 found a length the table did not
 * predict. That means the table is WRONG and should be corrected; the caller logs
 * it loudly rather than quietly self-healing forever.
 *
 * A DECLARED length is never second-guessed: 0xDD is fixed-size and 0xBB Q4
 * states its own payload length, so scanning their step data for a coincidental
 * CRC match could pick a boundary inside a program step. For those, a checksum
 * failure is reported honestly instead of being papered over.
 */
uint32_t me_frame_resolve_len(const uint8_t *buf, uint32_t avail,
                              me_crc_order_t order, bool *out_scanned);

#endif /* ME_FRAME_ROUTER_H */
