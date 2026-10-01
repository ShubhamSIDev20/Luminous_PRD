/*
 * can_frame.h - CAN-FD SET_VALUES/READ_VALUES frame construction and
 * feedback parsing, for ONE physical Secondary channel per call.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers.
 *
 * Layout source: Ref Docs/master_slave_can_v1.0.md. Floats are IEEE-754
 * binary32, LITTLE-ENDIAN - the opposite of every WebApp-facing frame in
 * this codebase (step_decode.c, realtime_frame.c). This asymmetry is
 * deliberate; do not share a float pack/unpack function between the two.
 */
#ifndef ME_CAN_FRAME_H
#define ME_CAN_FRAME_H

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ME_CAN_FRAME_LEN          64u
#define ME_CAN_SLOT_LEN           16u
#define ME_CAN_CHANNELS_PER_BLOCK  4u

/* Command byte (Master -> Slave, slot offset +8). */
#define ME_CAN_CMD_STO 0x00u
#define ME_CAN_CMD_CHA 0x01u
/* Outbound-only meaning of 0x04 ("reset the last error"). Deliberately a
 * DIFFERENT named constant from ME_CAN_STATE_ERR below, even though the
 * numeric value is identical - see design doc trap "0x04 is
 * direction-dependent". Unused this iteration; defined so the trap is
 * visible in code, not only in a comment. */
#define ME_CAN_CMD_RST_ERR 0x04u

/* STATE byte (Slave -> Master, slot offset +8). */
#define ME_CAN_STATE_STO 0x00u
#define ME_CAN_STATE_CHA 0x01u
/* Inbound-only meaning of 0x04 ("the slave node is in an error state"). */
#define ME_CAN_STATE_ERR 0x04u

/* CAN-FD function codes. */
#define ME_CAN_FUNC_SET  0x01u
#define ME_CAN_FUNC_READ 0x02u

typedef struct {
    uint8_t channel_num; /* 1-based, 1..8 */
    uint8_t command;     /* ME_CAN_CMD_* */
    float   set_voltage;
    float   set_current;
} me_can_setpoint_t;

typedef struct {
    uint8_t state;            /* ME_CAN_STATE_* */
    float   feedback_voltage;
    float   feedback_current;
} me_can_feedback_t;

typedef enum { ME_CAN_BLOCK_1 = 1, ME_CAN_BLOCK_2 = 2 } me_can_block_t;

/*
 * Maps a 1-based channel (1..8) to its block and 0-based slot index within
 * that block's 64-byte frame. Channels 1-4 -> Block 1 slots 0-3; 5-8 ->
 * Block 2 slots 0-3. Per the developer's decision
 * (Docs/specs/2026-08-14-step-execution-design.md §6 item 4), the caller
 * sends a block's frame only when that block has at least one active
 * channel - this iteration only ever has channel 1, so only Block 1 is
 * ever built.
 */
me_can_block_t me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out);

/*
 * 11-bit CAN identifier: circuit_num6 (6 bits) << 5 | function5 (5 bits).
 * circuit_num6 is the CAN-FD "circuit/module number" from
 * Ref Docs/master_slave_can_v1.0.md - one per physical Secondary (DC-DC)
 * module, each handling up to 8 channels. Callers pass ME's Secondary
 * number here (ME_CIRCUIT_SECONDARY(circuit_id)), NOT the full 8-bit ME
 * CircuitID.
 */
uint16_t me_can_id(uint8_t circuit_num6, uint8_t function5);

/*
 * Builds a full 64-byte SET_VALUES frame with every slot zero except the
 * one for sp->channel_num. Per the developer's decision (design doc §5
 * trap 2 / §6 item 3), the other three slots are deliberately left
 * zero-filled: this iteration has exactly one channel on the bus, so a
 * zero-filled CMD_STO in an unaddressed slot reaches nobody. Revisit before
 * a second channel is wired up.
 */
void me_can_pack_set(const me_can_setpoint_t *sp, uint8_t out64[ME_CAN_FRAME_LEN]);

/*
 * Merges the 16-byte slot for channel_num out of incoming64 into block64,
 * leaving every other channel's slot in block64 untouched. Both buffers are
 * ME_CAN_FRAME_LEN bytes. incoming64 is typically a freshly-packed
 * single-channel frame from me_can_pack_set(); block64 is the running,
 * per-Secondary-per-block shadow state the caller retransmits.
 *
 * This is what lets four independently-timed channels share one 64-byte
 * CAN-FD block frame without one channel's zero-filled slots stomping
 * another's live setpoint - see can_mgr.c handle_can_tx() and
 * Docs/specs/2026-08-19-multi-channel-secondary1-design.md section 4.
 */
void me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN],
                       const uint8_t incoming64[ME_CAN_FRAME_LEN],
                       uint8_t channel_num);

/* Builds a full 64-byte READ_VALUES request frame for one channel. Only
 * Channel # (slot offset +9) is meaningful in a request; every other byte
 * in that slot, and every other slot, is zero. */
void me_can_pack_read(uint8_t channel_num, uint8_t out64[ME_CAN_FRAME_LEN]);

/*
 * Builds a full 64-byte response frame (as if from the Secondary) for one
 * channel. Used by the CAN Data Manager's fabricator (Task 9), since no
 * physical Secondary exists yet.
 */
void me_can_pack_feedback(uint8_t channel_num, const me_can_feedback_t *fb,
                          uint8_t out64[ME_CAN_FRAME_LEN]);

/*
 * Parses the slot for channel_num out of a 64-byte frame. Works on BOTH a
 * genuine response frame (Feedback Voltage/Current, STATE) and a Master
 * request frame (Set Voltage/Current, Command) - the two share an
 * identical byte layout, only the field NAMES differ by direction. Returns
 * false if channel_num is out of range (not 1..8).
 */
bool me_can_parse_feedback(const uint8_t frame64[ME_CAN_FRAME_LEN],
                           uint8_t channel_num, me_can_feedback_t *out);

#endif /* ME_CAN_FRAME_H */
