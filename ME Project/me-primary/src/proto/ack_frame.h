/*
 * ack_frame.h - the one acknowledgement shape shared by every command group.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers (ADR-7).
 *
 *   Start | DeviceNumber | CircuitNumber | QueryID | Value | CRC[2]   (7 bytes)
 *
 * Three groups need a simple yes/no reply and all three use this layout, so
 * there is one packer rather than three near-identical ones:
 *
 *   0xAA  Q5 battery write
 *   0xBB  Q1 is-ready, Q3 packet count, Q4 per-packet ack
 *   0xEE  all six control commands
 *
 * Sources: bm_program_v3.0.md ("BB 01 01 04 01 -- --") and bm_control_v3.0.md
 * ("EE 01 01 01 01 -- --"). There is NO 0xAA document in Ref Docs - that one is
 * inferred from the agreement of the other two, and is the weakest link in this
 * file. If the Web Application rejects a battery ack, this is the first thing to
 * question.
 */
#ifndef ME_ACK_FRAME_H
#define ME_ACK_FRAME_H

#include <stddef.h>
#include <stdint.h>

#include "crc16.h"
#include "proto_defs.h"

/*
 * Build an acknowledgement into out[], which must hold at least ME_ACK_LEN
 * bytes. Returns the number of bytes written (always ME_ACK_LEN).
 *
 * EVERY IDENTIFYING FIELD IS ECHOED FROM THE FRAME BEING ANSWERED - start byte,
 * device, circuit and query. None of them is taken from this board's own
 * configuration. The Web Application correlates a reply to its question on
 * exactly these bytes, so substituting our own values would answer a question
 * nobody asked.
 *
 * value is ME_ACK_VALUE_OK or ME_ACK_VALUE_FAIL.
 */
size_t me_ack_pack(uint8_t start, uint8_t device_id, uint8_t circuit_id,
                   uint8_t query_id, uint8_t value, me_crc_order_t order,
                   uint8_t *out);

#endif /* ME_ACK_FRAME_H */
