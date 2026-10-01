/*
 * step_engine.h - per-circuit battery-testing program execution.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers, no clock reads.
 * `now_ms` is a PARAMETER everywhere - this is what makes a 10-second
 * cutoff provable in microseconds of host test time instead of discovered
 * on the board. See Docs/specs/2026-08-14-step-execution-design.md §3.3.
 */
#ifndef ME_STEP_ENGINE_H
#define ME_STEP_ENGINE_H

#include <stdbool.h>
#include <stdint.h>

#include "../proto/can_frame.h"
#include "../proto/step_decode.h"

typedef enum {
    ME_EXEC_IDLE = 0,
    ME_EXEC_RUNNING,
    ME_EXEC_STOPPED,
    ME_EXEC_CHANNEL_OFFLINE,
    ME_EXEC_DECODE_ERROR,
} me_exec_state_t;

typedef enum {
    ME_EXEC_OUT_NONE = 0,
    ME_EXEC_OUT_CAN_SET,  /* send a SET_VALUES frame, expect one response  */
    ME_EXEC_OUT_CAN_READ, /* send a READ_VALUES frame, expect one response */
    ME_EXEC_OUT_REALTIME, /* emit one 0xCC frame with the fields below     */
} me_exec_output_kind_t;

typedef struct {
    me_exec_output_kind_t kind;

    /* ME_EXEC_OUT_CAN_SET / ME_EXEC_OUT_CAN_READ */
    uint16_t can_id;
    uint8_t  can_frame[ME_CAN_FRAME_LEN];

    /* ME_EXEC_OUT_REALTIME */
    uint16_t step_number;
    uint8_t  program_running; /* ME_RT_PROGRAM_*, see realtime_frame.h      */
    uint8_t  circuit_status;  /* ME_RT_CIRCUIT_*, see realtime_frame.h     */
    uint32_t step_run_ms;
    uint32_t program_run_ms;
    float    current;
    float    voltage;
    uint8_t  operator_code;
} me_exec_output_t;

#define ME_EXEC_POLL_PERIOD_MS       100u
#define ME_EXEC_REALTIME_PERIOD_MS  1000u
#define ME_EXEC_RESPONSE_TIMEOUT_MS  200u
#define ME_EXEC_RETRY_LIMIT            3u
#define ME_EXEC_OUTPUT_QUEUE_LEN       4u

typedef struct {
    me_exec_state_t state;
    uint8_t         circuit_id; /* ME CircuitID: secondary<<4 | channel */

    const uint8_t  *program;    /* caller-owned; never copied           */
    uint32_t        program_len;
    uint32_t        step_index; /* 1-based, matches me_chain_fetch_step() */

    me_step_t       current_step;
    uint32_t        last_tick_ms;
    uint32_t        step_run_ms;
    uint32_t        program_run_ms;

    bool                   response_outstanding;
    me_exec_output_kind_t  outstanding_kind;
    uint32_t               response_deadline_ms;
    uint8_t                missed_responses;

    uint32_t        next_poll_ms;
    uint32_t        next_realtime_ms;
    float           last_feedback_voltage;
    float           last_feedback_current;

    me_exec_output_t pending[ME_EXEC_OUTPUT_QUEUE_LEN];
    uint8_t          pending_head;
    uint8_t          pending_count;
} me_exec_ctx_t;

/* Starts execution of `program` (already validated complete by the chain
 * walker) on `ctx`, from step 1, at engine time now_ms. `program` must
 * outlive `ctx`. */
void me_exec_start(me_exec_ctx_t *ctx, uint8_t circuit_id, const uint8_t *program,
                   uint32_t program_len, uint32_t now_ms);

/*
 * Advances the engine to now_ms and fills *out with at most ONE thing to
 * do. Call in a loop, draining ME_EXEC_OUT_NONE, until nothing is left -
 * two of this iteration's own outputs (a poll and a realtime emit) can
 * legitimately be due in the same millisecond.
 */
void me_exec_tick(me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out);

/* Response to whichever CAN frame is currently outstanding (SET or READ -
 * both share the same retry counter, per design doc §2.1). */
void me_exec_on_response(me_exec_ctx_t *ctx, const me_can_feedback_t *fb, uint32_t now_ms);

/*
 * The outstanding CAN frame got no response within ME_EXEC_RESPONSE_TIMEOUT_MS.
 * me_exec_tick() calls this internally once the deadline passes, so callers
 * do not need their own timeout bookkeeping - it is exposed publicly only
 * so a host test can trigger it directly without stepping through many
 * 10 ms ticks.
 */
void me_exec_on_response_timeout(me_exec_ctx_t *ctx, uint32_t now_ms);

/* Sends a CMD_STO SET_VALUES frame and transitions to ME_EXEC_STOPPED
 * immediately, regardless of program position. Used both by the program's
 * own STOP operator and by an external STOP command (0xEE Q2). Idempotent. */
void me_exec_force_stop(me_exec_ctx_t *ctx, uint32_t now_ms);

const char *me_exec_state_name(me_exec_state_t s);

#endif /* ME_STEP_ENGINE_H */
