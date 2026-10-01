/*
 * test_step_engine.c - per-circuit battery-testing program execution.
 */
#include <string.h>

#include "test_util.h"
#include "../src/exec/step_engine.h"
#include "../src/proto/program_chain.h"
#include "../src/proto/realtime_frame.h"

/* Writes a chain-wrapped step. Mirrors test_step_decode.c's put_wrapped(). */
static uint32_t put_step(uint8_t *buf, uint32_t off, uint32_t next,
                         uint16_t step_no, const uint8_t *body, uint32_t body_len)
{
    uint32_t i = off;
    buf[i++] = ME_STEP_START_1;
    buf[i++] = ME_STEP_START_2;
    buf[i++] = (uint8_t)(next >> 24);
    buf[i++] = (uint8_t)(next >> 16);
    buf[i++] = (uint8_t)(next >> 8);
    buf[i++] = (uint8_t)(next);
    buf[i++] = (uint8_t)(step_no >> 8);
    buf[i++] = (uint8_t)(step_no);
    memcpy(&buf[i], body, body_len);
    i += body_len;
    buf[i++] = ME_STEP_END_1;
    buf[i++] = ME_STEP_END_2;
    return i - off;
}

/* A 3-step SET -> CCChg(10A, >10000ms) -> STOP program, chain-linked. */
static uint32_t build_reference_program(uint8_t *buf)
{
    uint8_t set_body[]   = { 0x0A, 0x00, 0x01, 0xFF };
    uint8_t ccchg_body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x51, 0x00, 0x00, 0x27, 0x10, 0x00,
        0x00,
    };
    uint8_t stop_body[] = { 0x0B };

    uint32_t off = 0;
    const uint32_t len1 = put_step(buf, off, 0, 1, set_body, sizeof(set_body));
    off += len1;
    const uint32_t len2 = put_step(buf, off, 0, 2, ccchg_body, sizeof(ccchg_body));
    off += len2;
    const uint32_t len3 = put_step(buf, off, ME_STEP_TERMINATOR, 3, stop_body, sizeof(stop_body));
    off += len3;

    /* Patch nextIndex now that offsets are known (put_step wrote 0 as a
     * placeholder for steps 1 and 2). */
    uint32_t o = 0;
    buf[o + 2] = (uint8_t)((len1) >> 24); buf[o + 3] = (uint8_t)((len1) >> 16);
    buf[o + 4] = (uint8_t)((len1) >> 8);  buf[o + 5] = (uint8_t)(len1);
    o += len1;
    const uint32_t next2 = len1 + len2;
    buf[o + 2] = (uint8_t)(next2 >> 24); buf[o + 3] = (uint8_t)(next2 >> 16);
    buf[o + 4] = (uint8_t)(next2 >> 8);  buf[o + 5] = (uint8_t)(next2);

    return off; /* off already includes len1+len2+len3 - do NOT add len3 again */
}

static void test_starting_on_a_set_step_immediately_enters_ccchg(void)
{
    TEST_CASE("step_engine: SET is zero-duration - start() lands directly on CCChg's CAN_SET");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);
    CHECK_BYTE(out.can_frame, 8, ME_CAN_CMD_CHA);

    /* Nothing else due at t=0. */
    me_exec_tick(&ctx, 0, &out);
    CHECK_EQ_U(ME_EXEC_OUT_NONE, out.kind);
}

static void test_missed_responses_retry_then_go_offline(void)
{
    TEST_CASE("step_engine: 3 consecutive missed responses reach CHANNEL_OFFLINE");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* drain the initial CAN_SET from entering CCChg */
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);

    /*
     * Three timeouts, 200ms apart. Every retry is CAN_READ, never CAN_SET
     * (ADR-37) - the Secondary latches a setpoint on receipt, before it
     * replies, so a SET that got no response was still applied; re-sending
     * it would violate master_slave_can_v1.0.md's "the Master must not
     * re-issue a setpoint merely to obtain feedback." The third timeout
     * goes offline instead of retrying again.
     */
    uint32_t t = 200;
    me_exec_tick(&ctx, t, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_READ, out.kind); /* retry 1 */
    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    t += 200;
    me_exec_tick(&ctx, t, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_READ, out.kind); /* retry 2 */
    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    t += 200;
    me_exec_tick(&ctx, t, &out);
    CHECK_EQ_U(ME_EXEC_CHANNEL_OFFLINE, ctx.state);
}

static void test_a_response_clears_the_missed_counter(void)
{
    TEST_CASE("step_engine: a response before the deadline clears missed_responses");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* the initial CAN_SET */

    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA,
                             .feedback_voltage = 12.0f, .feedback_current = 10.0f };
    me_exec_on_response(&ctx, &fb, 50);

    CHECK_EQ_U(0, ctx.missed_responses);
    CHECK(!ctx.response_outstanding);
}

static void test_time_does_not_accrue_while_a_response_is_outstanding(void)
{
    TEST_CASE("step_engine: step_run_ms is frozen while a CAN response is outstanding");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* CAN_SET sent, response_outstanding = true */

    /* Advance 5000 real ms with no response - step_run_ms must not move,
     * mirroring the old firmware's own step-timer freeze behaviour. */
    me_exec_tick(&ctx, 5000, &out);
    CHECK_EQ_U(0, ctx.step_run_ms);
}

static void test_cutoff_fires_at_exactly_the_configured_ms(void)
{
    TEST_CASE("step_engine: a 10000ms cutoff fires at tick 10000, not 9990 or 10010");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program); /* CCChg cutoff = 10000ms, '>' */

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA, .feedback_voltage = 12.0f,
                             .feedback_current = 10.0f };
    me_exec_output_t out;

    me_exec_tick(&ctx, 0, &out); /* drain entry CAN_SET */
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);
    me_exec_on_response(&ctx, &fb, 0);

    /*
     * Tick every 100ms up to and including 10000ms, answering every poll
     * along the way - this mirrors how Core Logic actually drives the
     * engine (a steady 10ms tick; see Task 11), not a single large jump.
     *
     * This matters structurally, not just for test realism: next_poll_ms
     * advances by a FIXED period each time a poll fires (see Task 8's
     * me_exec_tick()), it does not snap forward to "catch up" to now_ms.
     * A test - or a caller - that jumps now_ms far ahead in one call would
     * see an unexpected poll fire on that same call, because
     * now_ms >= next_poll_ms stays true from a stale, unadvanced
     * next_poll_ms. Ticking at least as often as ME_EXEC_POLL_PERIOD_MS,
     * exactly as Task 11's fixed 10ms loop does, is what keeps this from
     * happening in production.
     */
    for (uint32_t t = 100; t <= 10000; t += 100) {
        me_exec_tick(&ctx, t, &out);
        if (out.kind == ME_EXEC_OUT_CAN_READ) {
            me_exec_on_response(&ctx, &fb, t);
            me_exec_tick(&ctx, t, &out); /* drains a coincident realtime emit, if any */
        }
    }

    /* '>' means the cutoff has NOT fired at exactly 10000ms - only past it. */
    CHECK_EQ_U(ME_EXEC_RUNNING, ctx.state);

    /* One tick later, it fires: the step advances into STOP, which sends a
     * CMD_STO CAN_SET immediately. */
    me_exec_tick(&ctx, 10001, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);
    CHECK_BYTE(out.can_frame, 8, ME_CAN_CMD_STO);
    CHECK_EQ_U(ME_EXEC_STOPPED, ctx.state);
}

static void test_full_sequence_reaches_stopped_and_emits_final_idle_frame(void)
{
    TEST_CASE("step_engine: SET->CCChg->STOP reaches STOPPED with a final Idle 0xCC");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* CAN_SET into CCChg */
    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA, .feedback_voltage = 12.0f,
                             .feedback_current = 10.0f };
    me_exec_on_response(&ctx, &fb, 0);

    me_exec_tick(&ctx, 10001, &out); /* cutoff fires -> STOP's CAN_SET */
    CHECK_EQ_U(ME_EXEC_OUT_CAN_SET, out.kind);

    me_exec_tick(&ctx, 10001, &out); /* the final Idle 0xCC, queued alongside it */
    CHECK_EQ_U(ME_EXEC_OUT_REALTIME, out.kind);
    CHECK_EQ_U(ME_RT_PROGRAM_IDLE, out.program_running);
    CHECK_EQ_U(ME_RT_CIRCUIT_IDLE, out.circuit_status);

    CHECK_EQ_U(ME_EXEC_STOPPED, ctx.state);
}

static void test_poll_and_realtime_coincide_every_tenth_poll(void)
{
    TEST_CASE("step_engine: a poll and a realtime emit can both be due in one tick");
    uint8_t program[128];
    const uint32_t len = build_reference_program(program);

    me_exec_ctx_t ctx;
    me_exec_start(&ctx, 0x11, program, len, 0);

    me_exec_output_t out;
    me_exec_tick(&ctx, 0, &out); /* entry CAN_SET */
    me_can_feedback_t fb = { .state = ME_CAN_STATE_CHA, .feedback_voltage = 12.0f,
                             .feedback_current = 10.0f };
    me_exec_on_response(&ctx, &fb, 0);

    /* next_poll_ms = 100, next_realtime_ms = 1000 (both armed at step entry).
     * Neither is due before 100ms. */
    me_exec_tick(&ctx, 50, &out);
    CHECK_EQ_U(ME_EXEC_OUT_NONE, out.kind);

    /* At 1000ms exactly, poll #10 AND the first realtime emit both fire. */
    me_exec_tick(&ctx, 1000, &out);
    CHECK_EQ_U(ME_EXEC_OUT_CAN_READ, out.kind);
    me_exec_on_response(&ctx, &fb, 1000);

    me_exec_tick(&ctx, 1000, &out);
    CHECK_EQ_U(ME_EXEC_OUT_REALTIME, out.kind);
}

void run_step_engine_tests(void)
{
    test_starting_on_a_set_step_immediately_enters_ccchg();
    test_missed_responses_retry_then_go_offline();
    test_a_response_clears_the_missed_counter();
    test_time_does_not_accrue_while_a_response_is_outstanding();
    test_cutoff_fires_at_exactly_the_configured_ms();
    test_full_sequence_reaches_stopped_and_emits_final_idle_frame();
    test_poll_and_realtime_coincide_every_tenth_poll();
}
