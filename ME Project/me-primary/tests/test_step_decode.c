/*
 * test_step_decode.c - SET/CCChg/STOP program-step decoding.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/step_decode.h"
#include "../src/proto/program_chain.h"

/* Writes a chain-wrapped step: AA55 | nextIndex(4) | stepNo(2) | body |
 * 55AA. Returns the packet's total length. body_len may be 0. */
static uint32_t put_wrapped(uint8_t *buf, uint32_t next, uint16_t step_no,
                            const uint8_t *body, uint32_t body_len)
{
    uint32_t i = 0;
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
    return i;
}

static void test_set_decodes_the_registration_mask(void)
{
    TEST_CASE("step_decode: SET extracts the 13-bit registration mask");
    /* op(0x0A) | skip(1) | regType(2, 0x01FF) - the exact V0.12 step 5 body. */
    uint8_t body[] = { 0x0A, 0x00, 0x01, 0xFF };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 5, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(ME_OP_SET, step.operator);
    CHECK_EQ_U(5, step.step_number);
    CHECK_EQ_U(0x01FF, step.registration_type);
}

static void test_set_masks_to_thirteen_bits(void)
{
    TEST_CASE("step_decode: SET masks the registration type to 13 bits");
    /* 0xFFFF & 0x1FFF = 0x1FFF - the top 3 bits are firmware-internal only. */
    uint8_t body[] = { 0x0A, 0x00, 0xFF, 0xFF };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 1, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(0x1FFF, step.registration_type);
}

static void test_stop_has_no_body(void)
{
    TEST_CASE("step_decode: STO carries no body at all - 11-byte packet");
    uint8_t body[] = { 0x0B }; /* operator byte only - no further data */
    uint8_t buf[16];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 6, body, sizeof(body));

    CHECK_EQ_U(11, len); /* matches Program Packet V0.12.xlsx step 6 exactly */

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(ME_OP_STOP, step.operator);
    CHECK_EQ_U(6, step.step_number);
}

static void test_unknown_operator_is_rejected(void)
{
    TEST_CASE("step_decode: an operator outside {SET,CCChg,STO} is rejected loudly");
    uint8_t body[] = { 0x08 }; /* PAU - a real BTS-600 operator, but out of scope here */
    uint8_t buf[16];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 1, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_OPERATOR, me_step_decode(buf, len, &step));
}

static void test_ccchg_decodes_current_and_time_cutoff(void)
{
    TEST_CASE("step_decode: CCChg decodes nominal current and a '>' TIME cutoff");
    uint8_t body[] = {
        0x01,                   /* operator: CCChg                        */
        0x41, 0x20, 0x00, 0x00, /* nominal current = 10.0f (BE)            */
        0x01,                   /* numCutoffConditions = 1                */
        0x39,                   /* condition: TIME                        */
        0x51,                   /* logic: '>'                             */
        0x00, 0x00, 0x27, 0x10, /* limit = 10000 ms (BE u32)               */
        0x00,                   /* actionType: BLANK                      */
        0x00,                   /* numRegParams = 0 - ALWAYS present, see
                                  * Task 3's design-decision note          */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(ME_OP_CCCHG, step.operator);
    CHECK(step.nominal_current_a > 9.999f && step.nominal_current_a < 10.001f);
    CHECK(step.has_cutoff);
    CHECK(!step.cutoff_inclusive);
    CHECK_EQ_U(10000, step.cutoff_time_ms);
    CHECK_EQ_U(0, step.num_reg_params);
}

static void test_ccchg_accepts_gte_comparator(void)
{
    TEST_CASE("step_decode: CCChg accepts '>=' as well as '>'");
    uint8_t body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x53,             /* logic: '>=' */
        0x00, 0x00, 0x03, 0xE8,       /* limit = 1000 ms */
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK(step.cutoff_inclusive);
}

static void test_ccchg_rejects_lt_on_time(void)
{
    TEST_CASE("step_decode: '<' on a TIME cutoff is rejected (true from t=0)");
    uint8_t body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x52,             /* logic: '<' - rejected */
        0x00, 0x00, 0x03, 0xE8,
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_COMPARATOR, me_step_decode(buf, len, &step));
}

static void test_ccchg_rejects_lte_on_time(void)
{
    TEST_CASE("step_decode: '<=' on a TIME cutoff is rejected (true from t=0)");
    uint8_t body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x54,             /* logic: '<=' - rejected */
        0x00, 0x00, 0x03, 0xE8,
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_COMPARATOR, me_step_decode(buf, len, &step));
}

static void test_ccchg_rejects_neq_on_time(void)
{
    TEST_CASE("step_decode: '!=' on a TIME cutoff is rejected (true almost always)");
    uint8_t body[] = {
        0x01, 0x41, 0x20, 0x00, 0x00,
        0x01, 0x39, 0x55,             /* logic: '!=' - rejected */
        0x00, 0x00, 0x03, 0xE8,
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_COMPARATOR, me_step_decode(buf, len, &step));
}

static void test_ccchg_accepts_eq_comparator_as_inclusive(void)
{
    /* Real bytes captured from the actual battery-testing program's step 2,
     * 2026-08-17: circuit 0x11 sent CCChg/1.0A with a TIME cutoff at 10000ms
     * using '=' (0x56), not '>' or '>='. Decoded as inclusive (same as GTE) -
     * see the developer's decision note in step_decode.c's decode_cutoffs(). */
    TEST_CASE("step_decode: CCChg accepts '=' on TIME, decoded as inclusive");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00, /* current = 1.0f */
        0x01, 0x39, 0x56,             /* logic: '=' */
        0x00, 0x00, 0x27, 0x10,       /* limit = 10000 ms */
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK(step.has_cutoff);
    CHECK(step.cutoff_inclusive);
    CHECK_EQ_U(10000, step.cutoff_time_ms);
}

static void test_ccchg_rejects_non_time_cutoff(void)
{
    TEST_CASE("step_decode: a VOLTAGE cutoff is rejected - only TIME is decoded");
    /* This is Program Packet V0.12.xlsx step 2's OWN raw bytes, verbatim -
     * a real WebApp-shaped CC_Chg step with a VOLTAGE cutoff. Proves this
     * decoder correctly recognises and rejects it rather than mis-decoding
     * it as something plausible. */
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00, /* current = 1.0f */
        0x01, 0x32, 0x51,             /* VOLTAGE, '>' */
        0x41, 0x61, 0x99, 0x9A,       /* limit = 14.1f */
        0x00, 0x00,
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE, me_step_decode(buf, len, &step));
}

static void test_ccchg_zero_cutoffs_is_valid(void)
{
    TEST_CASE("step_decode: zero cutoff conditions is a valid (if degenerate) decode");
    uint8_t body[] = { 0x01, 0x3F, 0x80, 0x00, 0x00, 0x00, 0x00 };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK(!step.has_cutoff);
}

static void test_ccchg_rejects_more_than_one_cutoff(void)
{
    TEST_CASE("step_decode: 2+ cutoffs are rejected - me_step_t has one slot");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x02, /* numCutoffConditions = 2 */
        0x39, 0x51, 0x00, 0x00, 0x03, 0xE8, 0x00,
        0x39, 0x51, 0x00, 0x00, 0x07, 0xD0, 0x00,
        0x00,
    };
    uint8_t buf[40];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED,
              me_step_decode(buf, len, &step));
}

static void test_ccchg_rejects_more_than_fifteen_cutoffs(void)
{
    TEST_CASE("step_decode: a wire count > 15 is rejected, not folded to 0");
    uint8_t body[] = { 0x01, 0x3F, 0x80, 0x00, 0x00, 16 };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TOO_MANY_CUTOFFS, me_step_decode(buf, len, &step));
}

static void test_ccchg_truncated_buffer_is_rejected(void)
{
    TEST_CASE("step_decode: a step cut off mid-cutoff is rejected, not read past");
    uint8_t body[] = { 0x01, 0x3F, 0x80, 0x00, 0x00, 0x01, 0x39 }; /* logic byte missing */
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TRUNCATED, me_step_decode(buf, len, &step));
}

static void test_ccchg_decodes_nonzero_reg_params(void)
{
    TEST_CASE("step_decode: CCChg decodes a nonzero registration-param list");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00, /* current = 1.0f */
        0x01, 0x39, 0x51, 0x00, 0x00, 0x03, 0xE8, 0x00, /* 1 TIME cutoff, 1000ms, '>' */
        0x02, /* numRegParams = 2 */
        0x22, 0x3D, 0xCC, 0xCC, 0xCD, /* Current, 0.1f */
        0x24, 0x42, 0x22, 0x00, 0x00, /* Temperature, 40.5f */
    };
    uint8_t buf[64];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 3, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(2, step.num_reg_params);
    CHECK_EQ_U(0x22, step.reg_params[0].registration_type);
    CHECK(step.reg_params[0].value > 0.0999f && step.reg_params[0].value < 0.1001f);
    CHECK_EQ_U(0x24, step.reg_params[1].registration_type);
    CHECK(step.reg_params[1].value > 40.499f && step.reg_params[1].value < 40.501f);
}

static void test_ccchg_v012_step2_layout_decodes_zero_reg_params(void)
{
    TEST_CASE("step_decode: V0.12 step 2's own byte-21 0x00 decodes as zero params");
    /* Same shape as Program Packet V0.12.xlsx step 2, with the VOLTAGE
     * cutoff swapped for a TIME one so this exercises the SUPPORTED path -
     * see test_ccchg_rejects_non_time_cutoff (Task 2) for the real bytes. */
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x01, 0x39, 0x51, 0x00, 0x00, 0x03, 0xE8, 0x00,
        0x00, /* numRegParams = 0 - present and zero, per V0.12 step 2 offset 21 */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_OK, me_step_decode(buf, len, &step));
    CHECK_EQ_U(0, step.num_reg_params);
}

static void test_ccchg_rejects_more_than_fifteen_reg_params(void)
{
    TEST_CASE("step_decode: a reg-param wire count > 15 is rejected");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x00, /* zero cutoffs, to keep this test focused */
        16,   /* numRegParams = 16 */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TOO_MANY_REG_PARAMS, me_step_decode(buf, len, &step));
}

static void test_ccchg_missing_reg_param_count_is_truncated(void)
{
    TEST_CASE("step_decode: a step missing its (always-present) reg-param count is truncated");
    uint8_t body[] = {
        0x01, 0x3F, 0x80, 0x00, 0x00,
        0x00, /* zero cutoffs */
        /* no trailing numRegParams byte at all */
    };
    uint8_t buf[32];
    const uint32_t len = put_wrapped(buf, ME_STEP_TERMINATOR, 2, body, sizeof(body));

    me_step_t step;
    CHECK_EQ_U(ME_STEP_DECODE_TRUNCATED, me_step_decode(buf, len, &step));
}

void run_step_decode_tests(void)
{
    test_set_decodes_the_registration_mask();
    test_set_masks_to_thirteen_bits();
    test_stop_has_no_body();
    test_unknown_operator_is_rejected();
    test_ccchg_decodes_current_and_time_cutoff();
    test_ccchg_accepts_gte_comparator();
    test_ccchg_rejects_lt_on_time();
    test_ccchg_rejects_lte_on_time();
    test_ccchg_rejects_neq_on_time();
    test_ccchg_accepts_eq_comparator_as_inclusive();
    test_ccchg_rejects_non_time_cutoff();
    test_ccchg_zero_cutoffs_is_valid();
    test_ccchg_rejects_more_than_one_cutoff();
    test_ccchg_rejects_more_than_fifteen_cutoffs();
    test_ccchg_truncated_buffer_is_rejected();
    test_ccchg_decodes_nonzero_reg_params();
    test_ccchg_v012_step2_layout_decodes_zero_reg_params();
    test_ccchg_rejects_more_than_fifteen_reg_params();
    test_ccchg_missing_reg_param_count_is_truncated();
}
