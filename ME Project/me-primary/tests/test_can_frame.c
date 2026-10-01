/*
 * test_can_frame.c - CAN-FD SET_VALUES/READ_VALUES frame construction and
 * feedback parsing.
 */
#include "test_util.h"
#include "../src/proto/can_frame.h"

static void test_channels_one_to_four_are_block_one(void)
{
    TEST_CASE("can_frame: channels 1-4 map to Block 1, slots 0-3 in order");
    uint8_t slot;
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(1, &slot)); CHECK_EQ_U(0, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(2, &slot)); CHECK_EQ_U(1, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(3, &slot)); CHECK_EQ_U(2, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_1, me_can_block_for_channel(4, &slot)); CHECK_EQ_U(3, slot);
}

static void test_channels_five_to_eight_are_block_two(void)
{
    TEST_CASE("can_frame: channels 5-8 map to Block 2, slots 0-3 in order");
    uint8_t slot;
    CHECK_EQ_U(ME_CAN_BLOCK_2, me_can_block_for_channel(5, &slot)); CHECK_EQ_U(0, slot);
    CHECK_EQ_U(ME_CAN_BLOCK_2, me_can_block_for_channel(8, &slot)); CHECK_EQ_U(3, slot);
}

static void test_can_id_composition(void)
{
    TEST_CASE("can_frame: CAN ID is (circuit6 << 5) | function5");
    /* Secondary 1, SET_VALUES: (1 << 5) | 1 = 0x21. */
    CHECK_EQ_U(0x21, me_can_id(1, ME_CAN_FUNC_SET));
    /* Secondary 1, READ_VALUES: (1 << 5) | 2 = 0x22. */
    CHECK_EQ_U(0x22, me_can_id(1, ME_CAN_FUNC_READ));
}

static void test_pack_set_exact_bytes(void)
{
    TEST_CASE("can_frame: pack_set produces the exact 64 bytes for a known setpoint");
    me_can_setpoint_t sp = { .channel_num = 1, .command = ME_CAN_CMD_CHA,
                             .set_voltage = 0.0f, .set_current = 10.0f };
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, frame);

    /* 10.0f little-endian = 00 00 20 41. Slot 0 starts at byte 0. */
    CHECK_BYTE(frame, 0, 0x00); CHECK_BYTE(frame, 1, 0x00);
    CHECK_BYTE(frame, 2, 0x00); CHECK_BYTE(frame, 3, 0x00); /* voltage = 0.0f */
    CHECK_BYTE(frame, 4, 0x00); CHECK_BYTE(frame, 5, 0x00);
    CHECK_BYTE(frame, 6, 0x20); CHECK_BYTE(frame, 7, 0x41); /* current = 10.0f LE */
    CHECK_BYTE(frame, 8, ME_CAN_CMD_CHA);
    CHECK_BYTE(frame, 9, 1);
    /* Slot 1 (channel 2, unaddressed) is entirely zero. */
    for (int i = 16; i < 32; i++) { CHECK_BYTE(frame, i, 0x00); }
}

static void test_pack_read_only_sets_channel_number(void)
{
    TEST_CASE("can_frame: pack_read leaves every byte but Channel # at zero");
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_read(1, frame);

    for (int i = 0; i < 9; i++)  { CHECK_BYTE(frame, i, 0x00); }
    CHECK_BYTE(frame, 9, 1);
    for (int i = 10; i < 64; i++) { CHECK_BYTE(frame, i, 0x00); }
}

static void test_parse_feedback_round_trips_pack_feedback(void)
{
    TEST_CASE("can_frame: parse_feedback correctly reads back pack_feedback's output");
    me_can_feedback_t fb_in = { .state = ME_CAN_STATE_CHA,
                               .feedback_voltage = 14.1f,
                               .feedback_current = 9.5f };
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_feedback(3, &fb_in, frame);

    me_can_feedback_t fb_out;
    CHECK(me_can_parse_feedback(frame, 3, &fb_out));
    CHECK_EQ_U(ME_CAN_STATE_CHA, fb_out.state);
    CHECK(fb_out.feedback_voltage > 14.099f && fb_out.feedback_voltage < 14.101f);
    CHECK(fb_out.feedback_current > 9.499f && fb_out.feedback_current < 9.501f);
}

static void test_parse_feedback_rejects_bad_channel(void)
{
    TEST_CASE("can_frame: parse_feedback rejects a channel number outside 1..8");
    uint8_t frame[ME_CAN_FRAME_LEN] = { 0 };
    me_can_feedback_t fb;
    CHECK(!me_can_parse_feedback(frame, 0, &fb));
    CHECK(!me_can_parse_feedback(frame, 9, &fb));
}

static void test_set_and_read_use_different_functions_same_shape(void)
{
    TEST_CASE("can_frame: a SET request's Set Current reads back via parse_feedback");
    /* Confirms the request/response layout really is identical - the
     * fabricator (Task 9) relies on decoding a REQUEST with
     * me_can_parse_feedback(). */
    me_can_setpoint_t sp = { .channel_num = 2, .command = ME_CAN_CMD_CHA,
                             .set_voltage = 0.0f, .set_current = 5.0f };
    uint8_t frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, frame);

    me_can_feedback_t decoded_request;
    CHECK(me_can_parse_feedback(frame, 2, &decoded_request));
    CHECK_EQ_U(ME_CAN_CMD_CHA, decoded_request.state); /* Command, read as "state" */
    CHECK(decoded_request.feedback_current > 4.999f &&
          decoded_request.feedback_current < 5.001f);  /* Set Current, read as "feedback" */
}

static void test_merge_slot_preserves_other_channels(void)
{
    TEST_CASE("can_frame: merge_slot updates only the target channel's slot");
    uint8_t block[ME_CAN_FRAME_LEN];
    memset(block, 0xAA, sizeof(block)); /* sentinel: must survive outside slot 1 */

    me_can_setpoint_t sp = { .channel_num = 2, .command = ME_CAN_CMD_CHA,
                             .set_voltage = 0.0f, .set_current = 7.5f };
    uint8_t incoming[ME_CAN_FRAME_LEN];
    me_can_pack_set(&sp, incoming);

    me_can_merge_slot(block, incoming, 2);

    for (int i = 0; i < 16; i++)  { CHECK_BYTE(block, i, 0xAA); }        /* ch1 slot untouched */
    for (int i = 16; i < 32; i++) { CHECK_BYTE(block, i, incoming[i]); } /* ch2 slot updated   */
    for (int i = 32; i < 64; i++) { CHECK_BYTE(block, i, 0xAA); }        /* ch3/ch4 untouched  */
}

static void test_merge_slot_stop_overwrites_stale_charge(void)
{
    TEST_CASE("can_frame: merge_slot lets an explicit STO overwrite a stale CHA slot");
    uint8_t block[ME_CAN_FRAME_LEN];
    memset(block, 0, sizeof(block));

    me_can_setpoint_t charge = { .channel_num = 1, .command = ME_CAN_CMD_CHA,
                                 .set_voltage = 0.0f, .set_current = 12.0f };
    uint8_t charge_frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&charge, charge_frame);
    me_can_merge_slot(block, charge_frame, 1);
    CHECK_BYTE(block, 8, ME_CAN_CMD_CHA);

    me_can_setpoint_t stop = { .channel_num = 1, .command = ME_CAN_CMD_STO,
                              .set_voltage = 0.0f, .set_current = 0.0f };
    uint8_t stop_frame[ME_CAN_FRAME_LEN];
    me_can_pack_set(&stop, stop_frame);
    me_can_merge_slot(block, stop_frame, 1);

    CHECK_BYTE(block, 8, ME_CAN_CMD_STO);
    CHECK_BYTE(block, 6, 0x00); CHECK_BYTE(block, 7, 0x00); /* current back to 0.0f */
}

void run_can_frame_tests(void)
{
    test_channels_one_to_four_are_block_one();
    test_channels_five_to_eight_are_block_two();
    test_can_id_composition();
    test_pack_set_exact_bytes();
    test_pack_read_only_sets_channel_number();
    test_parse_feedback_round_trips_pack_feedback();
    test_parse_feedback_rejects_bad_channel();
    test_set_and_read_use_different_functions_same_shape();
    test_merge_slot_preserves_other_channels();
    test_merge_slot_stop_overwrites_stale_charge();
}
