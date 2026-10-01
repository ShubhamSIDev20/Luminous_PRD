/*
 * program_chain.h - walking the battery-testing program's step chain.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. That is what lets the
 * walker be hammered with malformed chains on a Windows laptop instead of on a
 * board, and it is why both the Data Manager and Core Logic can share one
 * implementation.
 *
 * Ported from fetchProgramStep() in the old
 * BTS_Primary_SOM/src/networkDataHandler.c, with two deliberate changes:
 *
 *   1. buf and buf_len are PARAMETERS, not file-scope globals. The old version
 *      reached for programDataBuffer and totalProgramDataLength directly, which
 *      made it untestable and single-instance.
 *   2. A backward or self-referencing nextIndex is rejected. The old walker
 *      would loop forever on one - a hung thread rather than a rejected
 *      program.
 *
 * Retained on purpose: steps are counted POSITIONALLY as the walk proceeds,
 * not read from the stepNo field. A program with mislabelled step numbers still
 * executes in buffer order.
 */
#ifndef ME_PROGRAM_CHAIN_H
#define ME_PROGRAM_CHAIN_H

#include <stdbool.h>
#include <stddef.h> /* NULL - do not rely on stdint.h pulling this in */
#include <stdint.h>

#include "proto_defs.h"

typedef enum {
    ME_CHAIN_OK = 0,
    ME_CHAIN_NOT_FOUND,      /* walked the whole chain, no such step number  */
    ME_CHAIN_BAD_NEXT_INDEX, /* next offset outside the buffer, or backwards */
    ME_CHAIN_BAD_END_SEQ,    /* packet does not end with 55 AA               */
    ME_CHAIN_TRUNCATED       /* buffer too short to hold even one header     */
} me_chain_result_t;

const char *me_chain_result_name(me_chain_result_t r);

/*
 * Find step number step_no (1-based) in the chain.
 *
 * On ME_CHAIN_OK, *step_out points into buf (no copy is made) and
 * *step_len_out is the packet length including its start and end sequences.
 * On any other result both outputs are left untouched.
 */
me_chain_result_t me_chain_fetch_step(const uint8_t *buf, uint32_t buf_len,
                                      uint32_t step_no,
                                      const uint8_t **step_out,
                                      uint32_t *step_len_out);

/*
 * True when the chain reaches a step whose nextIndex is ME_STEP_TERMINATOR and
 * whose end sequence checks out - i.e. the Web Application has sent all of it.
 *
 * This replaces the old 0xBB Q3 packet-count query, which ME no longer sends.
 */
bool me_chain_is_complete(const uint8_t *buf, uint32_t buf_len);

/* Number of well-formed steps reachable from offset 0. Zero on a malformed or
 * empty buffer. */
uint32_t me_chain_count_steps(const uint8_t *buf, uint32_t buf_len);

/* Read the fields a step carries about itself. `step` must be a pointer
 * returned by me_chain_fetch_step(). */
uint16_t me_chain_step_number(const uint8_t *step);
uint8_t  me_chain_step_operator(const uint8_t *step);

#endif /* ME_PROGRAM_CHAIN_H */
