/*
 * test_ack_frame.c - the 7-byte acknowledgement shared by 0xAA, 0xBB and 0xEE.
 *
 * These are the only frames the board sends on TCP other than its registration
 * request, so there is no hardware-verified precedent to fall back on if a field
 * lands in the wrong place. Every field is asserted at its own offset, and both
 * documented example frames are asserted literally.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/ack_frame.h"

/* Poison: a byte the packer failed to write, or wrote past the end of the
 * frame, shows up as 0x7E rather than as a plausible zero. */
#define POISON 0x7Eu

static void test_ack_layout_field_by_field(void)
{
    TEST_CASE("ack: 7 bytes, checked one field at a time");
    uint8_t f[16];
    memset(f, POISON, sizeof(f));

    const size_t n = me_ack_pack(ME_START_PROGRAM, 0x01, 0x11,
                                 ME_QID_PRG_IS_READY, ME_ACK_VALUE_OK,
                                 ME_CRC_ORDER_BE, f);

    CHECK_EQ_U(ME_ACK_LEN, n);
    CHECK_EQ_U(7, n); /* the literal, so a redefined macro cannot hide a change */

    CHECK_BYTE(f, ME_HDR_OFF_START,    ME_START_PROGRAM);
    CHECK_BYTE(f, ME_HDR_OFF_DEVICE,   0x01);
    CHECK_BYTE(f, ME_HDR_OFF_CIRCUIT,  0x11);
    CHECK_BYTE(f, ME_HDR_OFF_QUERY_ID, ME_QID_PRG_IS_READY);
    CHECK_BYTE(f, ME_ACK_OFF_VALUE,    ME_ACK_VALUE_OK);

    CHECK(me_crc16_verify(f, n, ME_CRC_ORDER_BE));

    /* Nothing written past the frame. */
    CHECK_BYTE(f, 7, POISON);
    CHECK_BYTE(f, 8, POISON);
}

static void test_ack_matches_both_documented_examples(void)
{
    TEST_CASE("ack: matches 'BB 01 01 04 01' and 'EE 01 01 01 01' exactly");
    uint8_t f[16];

    /* bm_program_v3.0.md: "Response ACK per step: BB 01 01 04 01 -- --" */
    me_ack_pack(ME_START_PROGRAM, 0x01, 0x01, ME_QID_PROGRAM_DATA,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    static const uint8_t want_prg[5] = { 0xBB, 0x01, 0x01, 0x04, 0x01 };
    CHECK_MEM(f, want_prg, sizeof(want_prg));

    /* bm_control_v3.0.md: "Response (OK): EE 01 01 01 01 -- --" */
    me_ack_pack(ME_START_CONTROL, 0x01, 0x01, ME_CTRL_START,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    static const uint8_t want_ctrl[5] = { 0xEE, 0x01, 0x01, 0x01, 0x01 };
    CHECK_MEM(f, want_ctrl, sizeof(want_ctrl));

    /* bm_control_v3.0.md: "Response (FAIL): EE 01 01 01 00 -- --" */
    me_ack_pack(ME_START_CONTROL, 0x01, 0x01, ME_CTRL_START,
                ME_ACK_VALUE_FAIL, ME_CRC_ORDER_BE, f);
    static const uint8_t want_nack[5] = { 0xEE, 0x01, 0x01, 0x01, 0x00 };
    CHECK_MEM(f, want_nack, sizeof(want_nack));
}

static void test_ack_echoes_the_start_byte_of_the_group(void)
{
    TEST_CASE("ack: the start byte is echoed, not hardcoded to one group");
    /* One packer serves three groups. Hardcoding 0xBB here - which the first
     * version of this module did - silently answers a battery write with a
     * program frame. */
    uint8_t f[16];

    me_ack_pack(ME_START_CONFIG, 0x01, 0x11, ME_QID_CFG_WRITE_BATTERY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_START, 0xAA);

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PROGRAM_DATA,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_START, 0xBB);

    me_ack_pack(ME_START_CONTROL, 0x01, 0x11, ME_CTRL_STOP,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_START, 0xEE);
}

static void test_ack_echoes_the_query_it_answers(void)
{
    TEST_CASE("ack: the QueryID is echoed from the query being answered");
    /* The QueryID is the only thing telling the Web Application which question
     * was answered. Wrong here looks like a reply to a different query, not
     * like a malformed frame. */
    uint8_t f[16];

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_QUERY_ID, 0x01);

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PRG_PACKET_COUNT,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_QUERY_ID, 0x03);

    me_ack_pack(ME_START_CONTROL, 0x01, 0x11, ME_CTRL_RESET,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_QUERY_ID, 0x06);
}

static void test_ack_echoes_the_inbound_device_and_circuit(void)
{
    TEST_CASE("ack: device and circuit are echoed verbatim");
    uint8_t f[16];

    /* Not this board's own 0x11 - whatever the query carried. */
    me_ack_pack(ME_START_CONFIG, 0x07, 0x83, ME_QID_CFG_WRITE_BATTERY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, f);
    CHECK_BYTE(f, ME_HDR_OFF_DEVICE,  0x07);
    CHECK_BYTE(f, ME_HDR_OFF_CIRCUIT, 0x83);
}

static void test_ack_can_report_failure(void)
{
    TEST_CASE("ack: value 0x00 says Fail and re-checksums");
    uint8_t ok[16];
    uint8_t no[16];

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PROGRAM_DATA,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, ok);
    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PROGRAM_DATA,
                ME_ACK_VALUE_FAIL, ME_CRC_ORDER_BE, no);

    CHECK_BYTE(no, ME_ACK_OFF_VALUE, 0x00);
    CHECK(me_crc16_verify(no, ME_ACK_LEN, ME_CRC_ORDER_BE));

    /* A CRC computed before the value byte was written would be identical. */
    CHECK(memcmp(&ok[ME_ACK_OFF_VALUE + 1u], &no[ME_ACK_OFF_VALUE + 1u],
                 ME_REG_CRC_LEN) != 0);
}

static void test_ack_honours_the_crc_byte_order(void)
{
    TEST_CASE("ack: CRC order puts the same value in the other order");
    uint8_t be[16];
    uint8_t le[16];

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, be);
    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_LE, le);

    const uint16_t crc = me_crc16_modbus(be, ME_ACK_LEN - ME_REG_CRC_LEN);

    /* Big-endian is the project default: high byte first (ADR-9). */
    CHECK_BYTE(be, 5, (crc >> 8) & 0xFFu);
    CHECK_BYTE(be, 6, crc & 0xFFu);
    CHECK_BYTE(le, 5, crc & 0xFFu);
    CHECK_BYTE(le, 6, (crc >> 8) & 0xFFu);

    /* Both are well-formed under their own order - which is exactly why a wrong
     * order fails only in the peer's checksum test, never in parsing. */
    CHECK(me_crc16_verify(be, ME_ACK_LEN, ME_CRC_ORDER_BE));
    CHECK(me_crc16_verify(le, ME_ACK_LEN, ME_CRC_ORDER_LE));
}

static void test_ack_crc_covers_every_body_byte(void)
{
    TEST_CASE("ack: the CRC covers all five body bytes");
    /* A CRC computed over too few bytes still verifies against itself. Changing
     * one field at a time is what catches a short CRC span. */
    uint8_t base[16];
    uint8_t other[16];

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, base);

    me_ack_pack(ME_START_CONTROL, 0x01, 0x11, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, other);
    CHECK(memcmp(&base[5], &other[5], ME_REG_CRC_LEN) != 0);

    me_ack_pack(ME_START_PROGRAM, 0x02, 0x11, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, other);
    CHECK(memcmp(&base[5], &other[5], ME_REG_CRC_LEN) != 0);

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x12, ME_QID_PRG_IS_READY,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, other);
    CHECK(memcmp(&base[5], &other[5], ME_REG_CRC_LEN) != 0);

    me_ack_pack(ME_START_PROGRAM, 0x01, 0x11, ME_QID_PRG_PACKET_COUNT,
                ME_ACK_VALUE_OK, ME_CRC_ORDER_BE, other);
    CHECK(memcmp(&base[5], &other[5], ME_REG_CRC_LEN) != 0);
}

void run_ack_frame_tests(void)
{
    printf("\n-- ack_frame --\n");
    test_ack_layout_field_by_field();
    test_ack_matches_both_documented_examples();
    test_ack_echoes_the_start_byte_of_the_group();
    test_ack_echoes_the_query_it_answers();
    test_ack_echoes_the_inbound_device_and_circuit();
    test_ack_can_report_failure();
    test_ack_honours_the_crc_byte_order();
    test_ack_crc_covers_every_body_byte();
}
