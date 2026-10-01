/*
 * test_program_chain.c - the battery-testing program chain walker.
 *
 * A program is a singly-linked list embedded in a byte array: each step names
 * the ABSOLUTE offset of the next, and 0xFFFFFFFF ends it. The walker is the
 * single most valuable thing in this milestone to have under test, because a
 * malformed chain arriving from the Web Application must fail loudly rather
 * than walk off the end of a 2 MB buffer.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/program_chain.h"

#define STEP_LEN 15u /* 9 header + 4 payload + 2 end sequence */

/* Write one step packet at buf[off]. Returns its length. */
static uint32_t put_step(uint8_t *buf, uint32_t off, uint32_t next,
                         uint16_t step_no, uint8_t op)
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
    buf[i++] = op;
    buf[i++] = 0x11;
    buf[i++] = 0x22;
    buf[i++] = 0x33;
    buf[i++] = 0x44;
    buf[i++] = ME_STEP_END_1;
    buf[i++] = ME_STEP_END_2;
    return i - off;
}

/* A well-formed three-step chain: offsets 0, 15, 30; total 45 bytes. */
static uint32_t build_chain(uint8_t *buf)
{
    (void)put_step(buf, 0,  STEP_LEN,           1, 0x01);
    (void)put_step(buf, 15, STEP_LEN * 2u,      2, 0x02);
    (void)put_step(buf, 30, ME_STEP_TERMINATOR, 3, 0x0C);
    return STEP_LEN * 3u;
}

static void test_fetch_each_step(void)
{
    TEST_CASE("chain: every step in a three-step chain is reachable");
    uint8_t buf[64];
    const uint32_t len = build_chain(buf);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;

    CHECK_EQ_U(ME_CHAIN_OK, me_chain_fetch_step(buf, len, 1, &step, &slen));
    CHECK(step == &buf[0]);
    CHECK_EQ_U(STEP_LEN, slen);

    CHECK_EQ_U(ME_CHAIN_OK, me_chain_fetch_step(buf, len, 2, &step, &slen));
    CHECK(step == &buf[15]);
    CHECK_EQ_U(STEP_LEN, slen);

    CHECK_EQ_U(ME_CHAIN_OK, me_chain_fetch_step(buf, len, 3, &step, &slen));
    CHECK(step == &buf[30]);
    CHECK_EQ_U(STEP_LEN, slen);
}

static void test_step_fields_are_readable(void)
{
    TEST_CASE("chain: a fetched step exposes its own number and operator");
    uint8_t buf[64];
    const uint32_t len = build_chain(buf);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_OK, me_chain_fetch_step(buf, len, 3, &step, &slen));
    CHECK_EQ_U(3,    me_chain_step_number(step));
    CHECK_EQ_U(0x0C, me_chain_step_operator(step)); /* CYC */
}

static void test_positional_counting_beats_the_step_number_field(void)
{
    TEST_CASE("chain: steps are counted by position, not by their stepNo field");
    /* Deliberately mislabelled: the field says 77, 88, 99. Asking for step 2
     * must still return the second packet in the buffer. That is the old
     * firmware's behaviour and it is retained on purpose - a program with
     * mislabelled numbers still executes in buffer order. */
    uint8_t buf[64];
    (void)put_step(buf, 0,  STEP_LEN,           77, 0x01);
    (void)put_step(buf, 15, STEP_LEN * 2u,      88, 0x02);
    (void)put_step(buf, 30, ME_STEP_TERMINATOR, 99, 0x03);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_OK, me_chain_fetch_step(buf, 45, 2, &step, &slen));
    CHECK(step == &buf[15]);
    CHECK_EQ_U(88, me_chain_step_number(step));
}

static void test_missing_step_is_not_found(void)
{
    TEST_CASE("chain: asking past the end, or for step 0, is NOT_FOUND");
    uint8_t buf[64];
    const uint32_t len = build_chain(buf);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_NOT_FOUND, me_chain_fetch_step(buf, len, 4, &step, &slen));
    CHECK_EQ_U(ME_CHAIN_NOT_FOUND, me_chain_fetch_step(buf, len, 0, &step, &slen));
}

static void test_completeness(void)
{
    TEST_CASE("chain: completeness is decided by the terminator");
    uint8_t buf[64];
    const uint32_t len = build_chain(buf);
    CHECK(me_chain_is_complete(buf, len));
    CHECK_EQ_U(3, me_chain_count_steps(buf, len));

    /* Same chain with the last step pointing onward instead of terminating:
     * the Web Application has more to send. */
    (void)put_step(buf, 30, STEP_LEN * 3u, 3, 0x0C);
    CHECK(!me_chain_is_complete(buf, len));
}

static void test_next_index_past_buffer_is_rejected(void)
{
    TEST_CASE("chain: a next index outside the buffer is BAD_NEXT_INDEX");
    uint8_t buf[64];
    (void)put_step(buf, 0, 9999, 1, 0x01); /* far past the 15 bytes present */

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_BAD_NEXT_INDEX,
               me_chain_fetch_step(buf, STEP_LEN, 2, &step, &slen));
}

static void test_backward_next_index_is_rejected(void)
{
    TEST_CASE("chain: a next index pointing backwards is BAD_NEXT_INDEX");
    /* Without this check a self-referencing or backward link is an infinite
     * loop inside the walker - a hung thread, not a rejected program. */
    uint8_t buf[64];
    (void)put_step(buf, 0,  STEP_LEN,           1, 0x01);
    (void)put_step(buf, 15, 0,                  2, 0x02); /* points at itself's predecessor */
    (void)put_step(buf, 30, ME_STEP_TERMINATOR, 3, 0x03);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_BAD_NEXT_INDEX,
               me_chain_fetch_step(buf, 45, 3, &step, &slen));
}

static void test_missing_end_sequence_is_rejected(void)
{
    TEST_CASE("chain: a step without its end sequence is BAD_END_SEQ");
    uint8_t buf[64];
    (void)put_step(buf, 0,  STEP_LEN,           1, 0x01);
    (void)put_step(buf, 15, ME_STEP_TERMINATOR, 2, 0x02);
    buf[28] = 0x00; /* clobber END_SEQ_1 of the second packet */

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_BAD_END_SEQ,
               me_chain_fetch_step(buf, 30, 2, &step, &slen));
}

static void test_truncated_buffer_is_rejected(void)
{
    TEST_CASE("chain: a buffer too short to hold a header is TRUNCATED");
    uint8_t buf[64];
    (void)put_step(buf, 0, ME_STEP_TERMINATOR, 1, 0x01);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_TRUNCATED, me_chain_fetch_step(buf, 4, 1, &step, &slen));
    CHECK_EQ_U(ME_CHAIN_TRUNCATED, me_chain_fetch_step(buf, 0, 1, &step, &slen));
    CHECK(!me_chain_is_complete(buf, 0));
    CHECK_EQ_U(0, me_chain_count_steps(buf, 0));
}

static void test_leading_garbage_is_skipped(void)
{
    TEST_CASE("chain: bytes before the first start sequence are skipped");
    /* The old firmware advanced one byte at a time until it saw AA 55. Keeping
     * that makes the walker tolerant of a stray prefix rather than declaring an
     * otherwise good program unusable. */
    uint8_t buf[64];
    memset(buf, 0x00, sizeof(buf));
    (void)put_step(buf, 3, ME_STEP_TERMINATOR, 1, 0x01);

    const uint8_t *step = NULL;
    uint32_t       slen = 0;
    CHECK_EQ_U(ME_CHAIN_OK, me_chain_fetch_step(buf, 18, 1, &step, &slen));
    CHECK(step == &buf[3]);
}

static void test_result_names(void)
{
    TEST_CASE("chain: results have printable names");
    CHECK_STR("OK", me_chain_result_name(ME_CHAIN_OK));
    CHECK_STR("BAD_NEXT_INDEX", me_chain_result_name(ME_CHAIN_BAD_NEXT_INDEX));
    CHECK_STR("BAD_END_SEQ", me_chain_result_name(ME_CHAIN_BAD_END_SEQ));
    CHECK_STR("TRUNCATED", me_chain_result_name(ME_CHAIN_TRUNCATED));
    CHECK_STR("NOT_FOUND", me_chain_result_name(ME_CHAIN_NOT_FOUND));
}

void run_program_chain_tests(void)
{
    printf("\n-- program_chain --\n");
    test_fetch_each_step();
    test_step_fields_are_readable();
    test_positional_counting_beats_the_step_number_field();
    test_missing_step_is_not_found();
    test_completeness();
    test_next_index_past_buffer_is_rejected();
    test_backward_next_index_is_rejected();
    test_missing_end_sequence_is_rejected();
    test_truncated_buffer_is_rejected();
    test_leading_garbage_is_skipped();
    test_result_names();
}
