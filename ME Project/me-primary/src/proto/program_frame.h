/*
 * program_frame.h - the 0xBB-specific parts of the program handshake.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers (ADR-7).
 *
 * Layout source: Ref Docs/bm_program_v3.0.md.
 *
 * THE REPLY LAYOUT IS NOT HERE. It is shared with 0xAA and 0xEE and lives in
 * ack_frame.h - one packer for the whole protocol rather than three
 * near-identical ones. What remains here is what only 0xBB knows: which of its
 * queries the communication thread may answer by itself, and the one field it
 * reads without storing.
 */
#ifndef ME_PROGRAM_FRAME_H
#define ME_PROGRAM_FRAME_H

#include <stdbool.h>
#include <stddef.h> /* NULL */
#include <stdint.h>

#include "crc16.h"
#include "proto_defs.h"

/*
 * True for a 0xBB query the communication thread can answer immediately, by
 * itself, with no side effect anywhere else in the program: Q1 (is-ready) and
 * Q3 (packet count).
 *
 * Q4 is deliberately NOT in this set. It carries program data that has to reach
 * the Data Manager, so its acknowledgement means "queued", which only the
 * routing path can honestly assert - see route_frame().
 *
 * The predicate lives here, in pure code, so the "which queries do we answer"
 * decision is host-testable instead of buried in a Linux-only switch.
 */
bool me_prg_query_is_handshake(uint8_t query_id);

/*
 * Read the packet count out of a 0xBB Q3 frame.
 *
 * Returns false unless the frame really is a Q3 of at least ME_PRG_Q3_LEN
 * bytes. Does NOT verify the CRC - the caller does that once for the whole
 * frame before trusting any field.
 *
 * The count is not acted on (completion comes from the chain terminator,
 * ADR-14). It is read so the log can state what the Web Application expected
 * to send, which is the difference between "3 packets announced, 3 stored" and
 * "3 packets announced, 1 stored" being visible at all.
 */
bool me_prg_parse_packet_count(const uint8_t *frame, uint32_t len,
                               uint16_t *out_count);

#endif /* ME_PROGRAM_FRAME_H */
