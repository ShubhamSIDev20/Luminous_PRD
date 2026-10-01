/* step_engine.c - see step_engine.h. */
#include "step_engine.h"

#include <string.h>

#include "../proto/program_chain.h"
#include "../proto/proto_defs.h"
#include "../proto/realtime_frame.h"
#include "../util/log.h"

const char *me_exec_state_name(me_exec_state_t s)
{
    switch (s) {
    case ME_EXEC_IDLE:            return "IDLE";
    case ME_EXEC_RUNNING:         return "RUNNING";
    case ME_EXEC_STOPPED:         return "STOPPED";
    case ME_EXEC_CHANNEL_OFFLINE: return "CHANNEL_OFFLINE";
    case ME_EXEC_DECODE_ERROR:    return "DECODE_ERROR";
    default:                      return "UNKNOWN";
    }
}

static void enqueue(me_exec_ctx_t *ctx, const me_exec_output_t *o)
{
    if (ctx->pending_count >= ME_EXEC_OUTPUT_QUEUE_LEN) {
        ME_LOGE("step_engine: circuit 0x%02X - output queue full, dropping "
                "an output (kind %d)", ctx->circuit_id, (int)o->kind);
        return;
    }
    const uint8_t slot = (uint8_t)((ctx->pending_head + ctx->pending_count) %
                                   ME_EXEC_OUTPUT_QUEUE_LEN);
    ctx->pending[slot] = *o;
    ctx->pending_count++;
}

static bool dequeue(me_exec_ctx_t *ctx, me_exec_output_t *out)
{
    if (ctx->pending_count == 0u) { return false; }
    *out = ctx->pending[ctx->pending_head];
    ctx->pending_head = (uint8_t)((ctx->pending_head + 1u) % ME_EXEC_OUTPUT_QUEUE_LEN);
    ctx->pending_count--;
    return true;
}

static void build_can_output(me_exec_ctx_t *ctx, me_exec_output_kind_t kind,
                             uint8_t function5, const uint8_t frame64[ME_CAN_FRAME_LEN],
                             uint32_t now_ms)
{
    me_exec_output_t out;
    memset(&out, 0, sizeof(out));
    out.kind   = kind;
    out.can_id = me_can_id(ME_CIRCUIT_SECONDARY(ctx->circuit_id), function5);
    memcpy(out.can_frame, frame64, ME_CAN_FRAME_LEN);
    enqueue(ctx, &out);

    ctx->response_outstanding = true;
    ctx->outstanding_kind     = kind;
    ctx->response_deadline_ms = now_ms + ME_EXEC_RESPONSE_TIMEOUT_MS;
}

static void send_setpoint(me_exec_ctx_t *ctx, uint8_t command, float current_a,
                          uint32_t now_ms)
{
    me_can_setpoint_t sp;
    sp.channel_num = ME_CIRCUIT_CHANNEL(ctx->circuit_id);
    sp.command     = command;
    sp.set_voltage = 0.0f;
    sp.set_current = current_a;

    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, frame);
    build_can_output(ctx, ME_EXEC_OUT_CAN_SET, ME_CAN_FUNC_SET, frame, now_ms);
}

static void send_poll(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_read(ME_CIRCUIT_CHANNEL(ctx->circuit_id), frame);
    build_can_output(ctx, ME_EXEC_OUT_CAN_READ, ME_CAN_FUNC_READ, frame, now_ms);
}

static void enqueue_realtime(me_exec_ctx_t *ctx, bool final_idle)
{
    me_exec_output_t out;
    memset(&out, 0, sizeof(out));
    out.kind            = ME_EXEC_OUT_REALTIME;
    out.step_number     = ctx->current_step.step_number;
    out.program_running = final_idle ? ME_RT_PROGRAM_IDLE : ME_RT_PROGRAM_RUNNING;
    out.circuit_status  = final_idle ? ME_RT_CIRCUIT_IDLE  : ME_RT_CIRCUIT_CHARGE;
    out.step_run_ms     = ctx->step_run_ms;
    out.program_run_ms  = ctx->program_run_ms;
    out.current         = final_idle ? 0.0f : ctx->last_feedback_current;
    out.voltage         = final_idle ? 0.0f : ctx->last_feedback_voltage;
    out.operator_code   = (uint8_t)ctx->current_step.operator;
    enqueue(ctx, &out);
}

void me_exec_force_stop(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    if (ctx->state == ME_EXEC_STOPPED) { return; } /* idempotent */
    send_setpoint(ctx, ME_CAN_CMD_STO, 0.0f, now_ms);
    enqueue_realtime(ctx, true);
    ctx->state = ME_EXEC_STOPPED;
}

/* Fetches and decodes the step at ctx->step_index, then dispatches on its
 * operator. Recurses (bounded by program size) through zero-duration SET
 * steps so the caller always sees the effect of entering the next
 * CAN-producing or terminal step. Returns false on any failure. */
static bool enter_step(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    const uint8_t *step_ptr = NULL;
    uint32_t       step_len = 0;
    const me_chain_result_t cr = me_chain_fetch_step(
        ctx->program, ctx->program_len, ctx->step_index, &step_ptr, &step_len);
    if (cr != ME_CHAIN_OK) {
        ME_LOGE("step_engine: circuit 0x%02X - cannot fetch step %u: %s",
                ctx->circuit_id, (unsigned)ctx->step_index, me_chain_result_name(cr));
        return false;
    }

    me_step_t decoded;
    const me_step_decode_result_t dr = me_step_decode(step_ptr, step_len, &decoded);
    if (dr != ME_STEP_DECODE_OK) {
        ME_LOGE("step_engine: circuit 0x%02X - step %u decode failed: %s",
                ctx->circuit_id, (unsigned)ctx->step_index,
                me_step_decode_result_name(dr));
        me_hex_dump(ME_LOG_INFO, "rejected step", step_ptr, step_len);
        return false;
    }

    ctx->current_step = decoded;
    ctx->step_run_ms  = 0u;
    ctx->last_tick_ms = now_ms;

    switch (decoded.operator) {
    case ME_OP_SET:
        ctx->step_index++;
        return enter_step(ctx, now_ms);

    case ME_OP_CCCHG:
        send_setpoint(ctx, ME_CAN_CMD_CHA, decoded.nominal_current_a, now_ms);
        ctx->next_poll_ms     = now_ms + ME_EXEC_POLL_PERIOD_MS;
        ctx->next_realtime_ms = now_ms + ME_EXEC_REALTIME_PERIOD_MS;
        ctx->state            = ME_EXEC_RUNNING;
        return true;

    case ME_OP_STOP:
        me_exec_force_stop(ctx, now_ms);
        return true;

    default:
        ME_LOGE("step_engine: circuit 0x%02X - step %u decoded to an "
                "unhandled operator (bug)", ctx->circuit_id,
                (unsigned)ctx->step_index);
        return false;
    }
}

void me_exec_start(me_exec_ctx_t *ctx, uint8_t circuit_id, const uint8_t *program,
                   uint32_t program_len, uint32_t now_ms)
{
    memset(ctx, 0, sizeof(*ctx));
    ctx->circuit_id   = circuit_id;
    ctx->program      = program;
    ctx->program_len  = program_len;
    ctx->step_index   = 1u;
    ctx->last_tick_ms = now_ms;

    if (!enter_step(ctx, now_ms)) {
        ctx->state = ME_EXEC_DECODE_ERROR;
    }
}

static bool cutoff_due(const me_step_t *step, uint32_t step_run_ms)
{
    if (!step->has_cutoff) { return false; }
    return step->cutoff_inclusive ? (step_run_ms >= step->cutoff_time_ms)
                                  : (step_run_ms >  step->cutoff_time_ms);
}

/*
 * Accumulate wall-clock time into the step and program run counters.
 *
 * UNCONDITIONAL BY DESIGN (ADR-36). This used to skip the accumulation while
 * a CAN response was outstanding, which made step duration a function of bus
 * latency instead of elapsed time: every missed response cost the channel a
 * full ME_EXEC_RESPONSE_TIMEOUT_MS of step time, so channels that missed
 * responses ran their programs measurably slower than channels that did not,
 * and four channels on one bus drifted apart from each other. The design doc
 * (Docs/specs/2026-08-14-step-execution-design.md) rules both ways:
 * step_run_ms is "frozen unless RUNNING" - which me_exec_tick's own
 * state guard already enforces, no response check needed - and a TIME cutoff
 * "needs only the local clock, not CAN feedback, so its resolution is bounded
 * by the 10 ms tick regardless of poll rate", against a documented
 * T_CUTOFF_LATENCY <= 50 ms budget that a 200 ms-per-miss stall blows.
 *
 * This function is the ONLY writer of last_tick_ms outside step entry:
 * me_exec_on_response()/me_exec_on_response_timeout() must NOT touch it, or
 * the delta since the previous tick is discarded instead of counted - which
 * is the same lost-time bug by another route.
 */
static void advance_time(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    const uint32_t delta = now_ms - ctx->last_tick_ms;
    ctx->step_run_ms    += delta;
    ctx->program_run_ms += delta;
    ctx->last_tick_ms    = now_ms;
}

void me_exec_tick(me_exec_ctx_t *ctx, uint32_t now_ms, me_exec_output_t *out)
{
    memset(out, 0, sizeof(*out));
    if (dequeue(ctx, out)) { return; }

    me_exec_on_response_timeout(ctx, now_ms); /* no-op unless a deadline just passed */
    if (dequeue(ctx, out)) { return; }         /* a retry may have just enqueued output */

    if (ctx->state != ME_EXEC_RUNNING) { return; }

    advance_time(ctx, now_ms);

    if (cutoff_due(&ctx->current_step, ctx->step_run_ms)) {
        ctx->step_index++;
        if (!enter_step(ctx, now_ms)) {
            ctx->state = ME_EXEC_DECODE_ERROR;
        }
        (void)dequeue(ctx, out);
        return;
    }

    if (!ctx->response_outstanding && now_ms >= ctx->next_poll_ms) {
        send_poll(ctx, now_ms);
        ctx->next_poll_ms += ME_EXEC_POLL_PERIOD_MS;
    }
    if (now_ms >= ctx->next_realtime_ms) {
        enqueue_realtime(ctx, false);
        ctx->next_realtime_ms += ME_EXEC_REALTIME_PERIOD_MS;
    }

    (void)dequeue(ctx, out);
}

void me_exec_on_response(me_exec_ctx_t *ctx, const me_can_feedback_t *fb, uint32_t now_ms)
{
    if (ctx->state != ME_EXEC_RUNNING) { return; }

    ctx->last_feedback_voltage = fb->feedback_voltage;
    ctx->last_feedback_current = fb->feedback_current;
    ctx->response_outstanding  = false;
    ctx->missed_responses      = 0u;
    /* Deliberately does NOT touch last_tick_ms - see advance_time(). */
    (void)now_ms;
}

void me_exec_on_response_timeout(me_exec_ctx_t *ctx, uint32_t now_ms)
{
    if (ctx->state != ME_EXEC_RUNNING)  { return; }
    if (!ctx->response_outstanding)     { return; }
    if (now_ms < ctx->response_deadline_ms) { return; }

    ctx->missed_responses++;
    ctx->response_outstanding = false; /* the retry below re-arms it */
    /* Deliberately does NOT touch last_tick_ms - see advance_time(). A missed
     * response must not cost the channel its step time; that was the drift
     * bug ADR-36 fixed. */

    if (ctx->missed_responses >= ME_EXEC_RETRY_LIMIT) {
        ME_LOGE("step_engine: circuit 0x%02X - %u consecutive missed CAN "
                "responses, marking channel offline", ctx->circuit_id,
                (unsigned)ctx->missed_responses);
        ctx->state = ME_EXEC_CHANNEL_OFFLINE;
        return;
    }

    ME_LOGW("step_engine: circuit 0x%02X - missed CAN response (%u/%u), "
            "retrying", ctx->circuit_id, (unsigned)ctx->missed_responses,
            (unsigned)ME_EXEC_RETRY_LIMIT);

    /*
     * Always retry with READ_VALUES, never SET_VALUES - regardless of which
     * one timed out. Per master_slave_can_v1.0.md (Set_Cmd): "The Secondary
     * LATCHES the setpoint... No keep-alive re-send is required, and the
     * Master must not re-issue a setpoint merely to obtain feedback." A
     * SET_VALUES that gets no response was still applied - the Secondary
     * latches on receipt, before it replies - so the setpoint is already in
     * effect; a READ_VALUES elicits the same feedback response without
     * re-commanding anything. Re-sending SET_VALUES here (the previous
     * behaviour) put a live command back on the wire for every miss on top
     * of the one that already landed, which is exactly what ADR-37 removes.
     */
    send_poll(ctx, now_ms);
}
