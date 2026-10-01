/*
 * test_program_frame.c - the 0xBB-specific parts of the program handshake.
 *
 * The reply LAYOUT is shared with 0xAA and 0xEE and lives in test_ack_frame.c.
 * What is specific to 0xBB, and tested here: which queries the communication
 * thread may answer by itself, and the one field it reads without storing.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/program_frame.h"

static void test_only_q1_and_q3_are_handshake_queries(void)
{
    TEST_CASE("program: Q1 and Q3 are answerable alone; Q4 is not");
    CHECK(me_prg_query_is_handshake(ME_QID_PRG_IS_READY));
    CHECK(me_prg_query_is_handshake(ME_QID_PRG_PACKET_COUNT));

    /* Q4 carries data that must reach the Data Manager, so its ack means
     * "queued" and cannot be sent from the handshake path. */
    CHECK(!me_prg_query_is_handshake(ME_QID_PROGRAM_DATA));

    /* Q2/Q5/Q6 are recognised by the router but unimplemented: answering them
     * OK would claim work this milestone does not do. */
    CHECK(!me_prg_query_is_handshake(ME_QID_PRG_METADATA));
    CHECK(!me_prg_query_is_handshake(ME_QID_PRG_READ_METADATA));
    CHECK(!me_prg_query_is_handshake(ME_QID_PRG_READ_PROGRAM));

    CHECK(!me_prg_query_is_handshake(0x00));
    CHECK(!me_prg_query_is_handshake(0xFF));
}

static void test_packet_count_parses_the_documented_example(void)
{
    TEST_CASE("program: Q3 'BB 01 01 03 00 03' reads as three packets");
    uint8_t f[ME_PRG_Q3_LEN];
    memset(f, 0, sizeof(f));
    f[ME_HDR_OFF_START]    = ME_START_PROGRAM;
    f[ME_HDR_OFF_DEVICE]   = 0x01;
    f[ME_HDR_OFF_CIRCUIT]  = 0x01;
    f[ME_HDR_OFF_QUERY_ID] = ME_QID_PRG_PACKET_COUNT;
    f[ME_PRG_OFF_PKT_COUNT]      = 0x00;
    f[ME_PRG_OFF_PKT_COUNT + 1u] = 0x03;
    me_crc16_append(f, ME_PRG_Q3_LEN - ME_REG_CRC_LEN, ME_CRC_ORDER_BE);

    uint16_t count = 0xFFFFu;
    CHECK(me_prg_parse_packet_count(f, ME_PRG_Q3_LEN, &count));
    CHECK_EQ_U(3, count);
    CHECK_EQ_U(8, ME_PRG_Q3_LEN);
}

static void test_packet_count_is_big_endian(void)
{
    TEST_CASE("program: the Q3 count is big-endian, high byte first");
    uint8_t f[ME_PRG_Q3_LEN];
    memset(f, 0, sizeof(f));
    f[ME_HDR_OFF_START]    = ME_START_PROGRAM;
    f[ME_HDR_OFF_QUERY_ID] = ME_QID_PRG_PACKET_COUNT;
    f[ME_PRG_OFF_PKT_COUNT]      = 0x01;
    f[ME_PRG_OFF_PKT_COUNT + 1u] = 0x00;

    uint16_t count = 0;
    CHECK(me_prg_parse_packet_count(f, ME_PRG_Q3_LEN, &count));
    CHECK_EQ_U(256, count); /* 0x0100, not 0x0001 */
}

static void test_packet_count_rejects_what_is_not_a_q3(void)
{
    TEST_CASE("program: the Q3 reader refuses anything that is not a Q3");
    uint8_t f[ME_PRG_Q3_LEN];
    memset(f, 0, sizeof(f));
    f[ME_HDR_OFF_START]    = ME_START_PROGRAM;
    f[ME_HDR_OFF_QUERY_ID] = ME_QID_PRG_PACKET_COUNT;

    uint16_t count = 0;

    /* One byte short of a Q3 - reading the count would run past the frame. */
    CHECK(!me_prg_parse_packet_count(f, ME_PRG_Q3_LEN - 1u, &count));

    /* Right length, wrong query. */
    f[ME_HDR_OFF_QUERY_ID] = ME_QID_PROGRAM_DATA;
    CHECK(!me_prg_parse_packet_count(f, ME_PRG_Q3_LEN, &count));

    /* Right length and query, wrong start byte. */
    f[ME_HDR_OFF_QUERY_ID] = ME_QID_PRG_PACKET_COUNT;
    f[ME_HDR_OFF_START]    = ME_START_CONFIG;
    CHECK(!me_prg_parse_packet_count(f, ME_PRG_Q3_LEN, &count));

    CHECK(!me_prg_parse_packet_count(NULL, ME_PRG_Q3_LEN, &count));
}

void run_program_frame_tests(void)
{
    printf("\n-- program_frame --\n");
    test_only_q1_and_q3_are_handshake_queries();
    test_packet_count_parses_the_documented_example();
    test_packet_count_is_big_endian();
    test_packet_count_rejects_what_is_not_a_q3();
}
