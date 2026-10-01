/*
 * test_channel_list.c - CLI channel-list parsing ("1,2,3,4" -> array).
 */
#include "test_util.h"
#include "../src/util/channel_list.h"

static void test_parses_simple_list(void)
{
    TEST_CASE("channel_list: parses a plain comma-separated list in order");
    uint8_t out[8];
    uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_OK, me_channel_list_parse("1,2,3,4", out, &count));
    CHECK_EQ_U(4, count);
    CHECK_EQ_U(1, out[0]); CHECK_EQ_U(2, out[1]);
    CHECK_EQ_U(3, out[2]); CHECK_EQ_U(4, out[3]);
}

static void test_parses_single_channel(void)
{
    TEST_CASE("channel_list: parses a single channel (default CLI shape)");
    uint8_t out[8];
    uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_OK, me_channel_list_parse("1", out, &count));
    CHECK_EQ_U(1, count);
    CHECK_EQ_U(1, out[0]);
}

static void test_rejects_empty_string(void)
{
    TEST_CASE("channel_list: rejects an empty string");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_EMPTY, me_channel_list_parse("", out, &count));
    CHECK_EQ_U(0, count);
}

static void test_rejects_out_of_range_channel(void)
{
    TEST_CASE("channel_list: rejects a channel outside 1..8, does not truncate");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_OUT_OF_RANGE, me_channel_list_parse("1,9", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_OUT_OF_RANGE, me_channel_list_parse("99", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_OUT_OF_RANGE, me_channel_list_parse("0", out, &count));
}

static void test_rejects_too_many_entries(void)
{
    TEST_CASE("channel_list: rejects more than ME_MAX_CHANNELS entries");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_TOO_MANY,
               me_channel_list_parse("1,2,3,4,5,6,7,8,1", out, &count));
}

static void test_rejects_duplicate_channel(void)
{
    TEST_CASE("channel_list: rejects a duplicated channel");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_DUPLICATE, me_channel_list_parse("1,2,1", out, &count));
}

static void test_rejects_malformed_input(void)
{
    TEST_CASE("channel_list: rejects non-numeric input and stray commas");
    uint8_t out[8]; uint8_t count;
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse("1,x", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse("1,", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse(",1", out, &count));
    CHECK_EQ_U(ME_CHANNEL_LIST_MALFORMED, me_channel_list_parse("1,,2", out, &count));
}

void run_channel_list_tests(void)
{
    test_parses_simple_list();
    test_parses_single_channel();
    test_rejects_empty_string();
    test_rejects_out_of_range_channel();
    test_rejects_too_many_entries();
    test_rejects_duplicate_channel();
    test_rejects_malformed_input();
}
