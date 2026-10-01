/*
 * test_crc16.c - CRC-16/Modbus unit tests.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/crc16.h"

static void test_reference_vector(void)
{
    /* The universally published CRC-16/Modbus check value. If this is wrong,
     * nothing else in the protocol can be trusted. */
    TEST_CASE("crc16: \"123456789\" -> 0x4B37");
    const uint8_t data[] = "123456789";
    CHECK_EQ_U(0x4B37, me_crc16_modbus(data, 9));
}

static void test_empty_input_is_init_value(void)
{
    TEST_CASE("crc16: empty input returns the 0xFFFF init value");
    CHECK_EQ_U(0xFFFF, me_crc16_modbus((const uint8_t *)"", 0));
}

static void test_single_zero_byte(void)
{
    /* Known Modbus value for a lone 0x00 byte. Guards against an init or
     * shift-direction error that the "123456789" vector alone might mask. */
    TEST_CASE("crc16: single 0x00 byte -> 0x40BF");
    const uint8_t data[1] = { 0x00 };
    CHECK_EQ_U(0x40BF, me_crc16_modbus(data, 1));
}

static void test_append_little_endian_writes_low_byte_first(void)
{
    TEST_CASE("crc16: LE append writes low byte first");
    uint8_t frame[11];
    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_LE);
    CHECK_BYTE(frame, 9, 0x37);  /* low  byte of 0x4B37 */
    CHECK_BYTE(frame, 10, 0x4B); /* high byte of 0x4B37 */
}

static void test_append_big_endian_writes_high_byte_first(void)
{
    TEST_CASE("crc16: BE append writes high byte first");
    uint8_t frame[11];
    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_BE);
    CHECK_BYTE(frame, 9, 0x4B);
    CHECK_BYTE(frame, 10, 0x37);
}

static void test_verify_accepts_what_append_produced(void)
{
    TEST_CASE("crc16: verify accepts a frame append() built, both orders");
    uint8_t frame[11];

    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_LE);
    CHECK(me_crc16_verify(frame, 11, ME_CRC_ORDER_LE));

    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_BE);
    CHECK(me_crc16_verify(frame, 11, ME_CRC_ORDER_BE));
}

static void test_verify_rejects_wrong_byte_order(void)
{
    /* This is the whole reason order is a parameter: a frame built one way
     * must be rejected when read the other way, or the flag is useless. */
    TEST_CASE("crc16: verify rejects a frame read in the wrong byte order");
    uint8_t frame[11];
    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_LE);
    CHECK(!me_crc16_verify(frame, 11, ME_CRC_ORDER_BE));
}

static void test_verify_rejects_corrupted_payload(void)
{
    TEST_CASE("crc16: verify rejects a frame whose payload was altered");
    uint8_t frame[11];
    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_LE);
    frame[4] ^= 0x01;
    CHECK(!me_crc16_verify(frame, 11, ME_CRC_ORDER_LE));
}

static void test_verify_rejects_undersized_frame(void)
{
    TEST_CASE("crc16: verify rejects a frame too short to hold a CRC");
    const uint8_t frame[2] = { 0x37, 0x4B };
    CHECK(!me_crc16_verify(frame, 2, ME_CRC_ORDER_LE));
    CHECK(!me_crc16_verify(frame, 0, ME_CRC_ORDER_LE));
}

static void test_read_honours_byte_order(void)
{
    TEST_CASE("crc16: read returns the stored CRC in both orders");
    const uint8_t le[3] = { 0x00, 0x37, 0x4B };
    const uint8_t be[3] = { 0x00, 0x4B, 0x37 };
    CHECK_EQ_U(0x4B37, me_crc16_read(le, 3, ME_CRC_ORDER_LE));
    CHECK_EQ_U(0x4B37, me_crc16_read(be, 3, ME_CRC_ORDER_BE));
}

static void test_default_order_is_big_endian(void)
{
    /* The wire default. bm_device_registration_v5.0 writes the trailer as
     * "CRC_HI CRC_LO", so the high byte goes out first. Everything that has a
     * CRC-order default must resolve through this constant, which is why one
     * assertion here is enough to pin the behaviour of the whole program. */
    TEST_CASE("crc16: the default byte order is big-endian (high byte first)");
    CHECK_EQ_U(ME_CRC_ORDER_BE, ME_CRC_ORDER_DEFAULT);
}

static void test_append_in_default_order_writes_high_byte_first(void)
{
    TEST_CASE("crc16: append in the default order writes the high byte first");
    uint8_t frame[11];
    memcpy(frame, "123456789", 9);
    me_crc16_append(frame, 9, ME_CRC_ORDER_DEFAULT);
    CHECK_BYTE(frame, 9, 0x4B);  /* high byte of 0x4B37 */
    CHECK_BYTE(frame, 10, 0x37); /* low  byte of 0x4B37 */
}

static void test_order_parse_accepts_both_spellings(void)
{
    TEST_CASE("crc16: order string parses le/be case-insensitively");
    me_crc_order_t o;

    CHECK(me_crc_order_parse("le", &o));
    CHECK_EQ_U(ME_CRC_ORDER_LE, o);

    CHECK(me_crc_order_parse("BE", &o));
    CHECK_EQ_U(ME_CRC_ORDER_BE, o);

    CHECK(!me_crc_order_parse("middle", &o));
    CHECK(!me_crc_order_parse("", &o));
}

void run_crc16_tests(void)
{
    printf("-- crc16 --\n");
    test_reference_vector();
    test_empty_input_is_init_value();
    test_single_zero_byte();
    test_append_little_endian_writes_low_byte_first();
    test_append_big_endian_writes_high_byte_first();
    test_verify_accepts_what_append_produced();
    test_verify_rejects_wrong_byte_order();
    test_verify_rejects_corrupted_payload();
    test_verify_rejects_undersized_frame();
    test_read_honours_byte_order();
    test_default_order_is_big_endian();
    test_append_in_default_order_writes_high_byte_first();
    test_order_parse_accepts_both_spellings();
}
