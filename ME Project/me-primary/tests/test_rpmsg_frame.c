/*
 * test_rpmsg_frame.c - host tests for the RPMsg wire codec.
 *
 * The golden vectors come from Ref Docs/RPMSG_PROTOCOL.md sections 8.2-8.4.
 * Those byte strings are the contract with the M7 firmware: if one of these
 * fails, the two cores disagree about the wire and nothing will work on
 * hardware.
 */
#include "../src/proto/rpmsg_frame.h"

#include "test_util.h"

static void test_wrap_header_golden(void)
{
    /* RPMSG_PROTOCOL.md section 8 step 2: the 11-byte header for an
     * 80-byte CAN_SET_FRAME request. */
    uint8_t payload[ME_RPMSG_PAYLOAD_MAX];
    uint8_t out[ME_RPMSG_FRAME_MAX];
    memset(payload, 0, sizeof(payload));

    TEST_CASE("rpmsg wrap: header golden vector");
    const size_t n = me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                   ME_RPMSG_CMD_CAN_SET_FRAME,
                                   ME_RPMSG_RESULT_REQUEST,
                                   payload, ME_RPMSG_CAN_MSG_LEN,
                                   out, sizeof(out));
    CHECK_EQ_U(91u, n);
    CHECK_BYTE(out, 0, 0xAA); /* sync                       */
    CHECK_BYTE(out, 1, 0x01); /* action = WRITE_SET         */
    CHECK_BYTE(out, 2, 0x00); /* command, big-endian        */
    CHECK_BYTE(out, 3, 0x10);
    CHECK_BYTE(out, 4, 0x00);
    CHECK_BYTE(out, 5, 0x01);
    CHECK_BYTE(out, 6, 0x00); /* result = REQUEST           */
    CHECK_BYTE(out, 7, 0x00); /* length = 80, big-endian    */
    CHECK_BYTE(out, 8, 0x00);
    CHECK_BYTE(out, 9, 0x00);
    CHECK_BYTE(out, 10, 0x50);
}

static void test_wrap_rejects_oversize(void)
{
    uint8_t payload[ME_RPMSG_PAYLOAD_MAX];
    uint8_t out[ME_RPMSG_FRAME_MAX];
    memset(payload, 0, sizeof(payload));

    TEST_CASE("rpmsg wrap: payload over 80 bytes is refused");
    CHECK_EQ_U(0u, me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                 ME_RPMSG_CMD_CAN_SET_FRAME,
                                 ME_RPMSG_RESULT_REQUEST,
                                 payload, ME_RPMSG_PAYLOAD_MAX + 1u,
                                 out, sizeof(out)));

    TEST_CASE("rpmsg wrap: undersized output buffer is refused");
    CHECK_EQ_U(0u, me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                 ME_RPMSG_CMD_CAN_SET_FRAME,
                                 ME_RPMSG_RESULT_REQUEST,
                                 payload, ME_RPMSG_CAN_MSG_LEN,
                                 out, 90u));
}

static void test_parse_header_ack(void)
{
    /* RPMSG_PROTOCOL.md section 8 step 4: the SET ACK. */
    const uint8_t ack[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x01, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00
    };
    me_rpmsg_hdr_t h;

    TEST_CASE("rpmsg parse: SET ACK header");
    CHECK(me_rpmsg_parse_header(ack, sizeof(ack), &h));
    CHECK_EQ_U(ME_RPMSG_ACTION_WRITE_SET, h.action);
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_SET_FRAME, h.command);
    CHECK_EQ_U(ME_RPMSG_RESULT_SUCCESS, h.result);
    CHECK_EQ_U(0u, h.length);
}

static void test_parse_header_rejects(void)
{
    uint8_t buf[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x01, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00
    };
    me_rpmsg_hdr_t h;

    TEST_CASE("rpmsg parse: wrong sync byte is refused");
    buf[0] = 0xAB;
    CHECK(!me_rpmsg_parse_header(buf, sizeof(buf), &h));

    TEST_CASE("rpmsg parse: short buffer is refused");
    buf[0] = 0xAA;
    CHECK(!me_rpmsg_parse_header(buf, ME_RPMSG_HDR_LEN - 1u, &h));

    TEST_CASE("rpmsg parse: implausible length is refused");
    buf[10] = 0x51; /* 81 > 80 */
    CHECK(!me_rpmsg_parse_header(buf, sizeof(buf), &h));
}

static void test_pack_can_golden(void)
{
    /* RPMSG_PROTOCOL.md section 8: Classic CAN, standard ID 0x18F, 8 bytes
     * 11 22 33 44 55 66 77 88. This is the full 91-byte vector the M7
     * firmware was verified against. */
    me_rpmsg_can_t in;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];
    uint8_t out[ME_RPMSG_FRAME_MAX];
    unsigned i;

    memset(&in, 0, sizeof(in));
    in.timestamp_ms = 0u;
    in.can_id       = 0x18Fu;
    in.is_extended  = 0u;
    in.is_fd        = 0u;
    in.brs          = 0u;
    in.esi          = 0u;
    in.dlc          = 8u;
    in.data_len     = 8u;
    for (i = 0u; i < 8u; i++) {
        in.data[i] = (uint8_t)(0x11u * (i + 1u));
    }

    TEST_CASE("rpmsg can: pack the section-8 payload");
    CHECK(me_rpmsg_pack_can(&in, payload));
    CHECK_BYTE(payload, 0, 0x00); /* timestamp_ms = 0, little-endian */
    CHECK_BYTE(payload, 1, 0x00);
    CHECK_BYTE(payload, 2, 0x00);
    CHECK_BYTE(payload, 3, 0x00);
    CHECK_BYTE(payload, 4, 0x8F); /* can_id = 0x18F, LITTLE-endian   */
    CHECK_BYTE(payload, 5, 0x01);
    CHECK_BYTE(payload, 6, 0x00);
    CHECK_BYTE(payload, 7, 0x00);
    CHECK_BYTE(payload, 8, 0x00);  /* is_extended */
    CHECK_BYTE(payload, 9, 0x00);  /* is_fd       */
    CHECK_BYTE(payload, 10, 0x00); /* brs         */
    CHECK_BYTE(payload, 11, 0x00); /* esi         */
    CHECK_BYTE(payload, 12, 0x08); /* dlc         */
    CHECK_BYTE(payload, 13, 0x08); /* data_len    */
    CHECK_BYTE(payload, 14, 0x00); /* reserved    */
    CHECK_BYTE(payload, 15, 0x00);
    CHECK_BYTE(payload, 16, 0x11); /* data[0] at offset 16 */
    CHECK_BYTE(payload, 23, 0x88); /* data[7]              */

    TEST_CASE("rpmsg can: bytes past data_len are zero-filled");
    for (i = 24u; i < ME_RPMSG_CAN_MSG_LEN; i++) {
        CHECK_BYTE(payload, i, 0x00);
    }

    TEST_CASE("rpmsg can: the full 91-byte section-8 frame");
    CHECK_EQ_U(91u, me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET,
                                  ME_RPMSG_CMD_CAN_SET_FRAME,
                                  ME_RPMSG_RESULT_REQUEST,
                                  payload, ME_RPMSG_CAN_MSG_LEN,
                                  out, sizeof(out)));
    CHECK_BYTE(out, 11, 0x00); /* payload starts right after the header */
    CHECK_BYTE(out, 15, 0x8F); /* can_id low byte, at frame offset 11+4 */
    CHECK_BYTE(out, 27, 0x11); /* data[0], at frame offset 11+16        */
}

static void test_can_round_trip(void)
{
    me_rpmsg_can_t in;
    me_rpmsg_can_t back;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];
    unsigned i;

    memset(&in, 0, sizeof(in));
    in.timestamp_ms = 0x12345678u;
    in.can_id       = 0x022u; /* Secondary 1, function 0x02 */
    in.is_extended  = 0u;
    in.is_fd        = 1u;
    in.brs          = 1u;
    in.esi          = 0u;
    in.dlc          = 15u;
    in.data_len     = 64u;
    for (i = 0u; i < 64u; i++) {
        in.data[i] = (uint8_t)(0xA0u + i);
    }

    TEST_CASE("rpmsg can: pack then parse recovers every field");
    CHECK(me_rpmsg_pack_can(&in, payload));
    memset(&back, 0, sizeof(back));
    CHECK(me_rpmsg_parse_can(payload, &back));
    CHECK_EQ_U(in.timestamp_ms, back.timestamp_ms);
    CHECK_EQ_U(in.can_id, back.can_id);
    CHECK_EQ_U(in.is_extended, back.is_extended);
    CHECK_EQ_U(in.is_fd, back.is_fd);
    CHECK_EQ_U(in.brs, back.brs);
    CHECK_EQ_U(in.esi, back.esi);
    CHECK_EQ_U(in.dlc, back.dlc);
    CHECK_EQ_U(in.data_len, back.data_len);
    CHECK_MEM(back.data, in.data, 64u);
}

static void test_can_rejects_bad_len(void)
{
    me_rpmsg_can_t in;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];

    memset(&in, 0, sizeof(in));
    in.data_len = 65u; /* over the 64-byte ceiling */

    TEST_CASE("rpmsg can: data_len over 64 is refused");
    CHECK(!me_rpmsg_pack_can(&in, payload));
}

static void test_dlc_table(void)
{
    TEST_CASE("rpmsg dlc: 1:1 region, 0-8 bytes");
    CHECK_EQ_U(0u, me_rpmsg_dlc_for_len(0u));
    CHECK_EQ_U(8u, me_rpmsg_dlc_for_len(8u));

    TEST_CASE("rpmsg dlc: intermediate lengths pad up");
    CHECK_EQ_U(9u,  me_rpmsg_dlc_for_len(9u));  /* -> 12 */
    CHECK_EQ_U(9u,  me_rpmsg_dlc_for_len(12u));
    CHECK_EQ_U(10u, me_rpmsg_dlc_for_len(13u)); /* -> 16 */
    CHECK_EQ_U(10u, me_rpmsg_dlc_for_len(16u));
    CHECK_EQ_U(11u, me_rpmsg_dlc_for_len(20u));
    CHECK_EQ_U(12u, me_rpmsg_dlc_for_len(24u));
    CHECK_EQ_U(13u, me_rpmsg_dlc_for_len(32u));
    CHECK_EQ_U(14u, me_rpmsg_dlc_for_len(48u));
    CHECK_EQ_U(15u, me_rpmsg_dlc_for_len(64u));

    TEST_CASE("rpmsg dlc: the 64-byte frame this project sends");
    CHECK_EQ_U(15u, me_rpmsg_dlc_for_len(ME_CAN_PAYLOAD_BYTES));

    TEST_CASE("rpmsg dlc: reverse lookup");
    CHECK_EQ_U(8u,  me_rpmsg_len_for_dlc(8u));
    CHECK_EQ_U(12u, me_rpmsg_len_for_dlc(9u));
    CHECK_EQ_U(16u, me_rpmsg_len_for_dlc(10u));
    CHECK_EQ_U(20u, me_rpmsg_len_for_dlc(11u));
    CHECK_EQ_U(24u, me_rpmsg_len_for_dlc(12u));
    CHECK_EQ_U(32u, me_rpmsg_len_for_dlc(13u));
    CHECK_EQ_U(48u, me_rpmsg_len_for_dlc(14u));
    CHECK_EQ_U(64u, me_rpmsg_len_for_dlc(15u));

    TEST_CASE("rpmsg dlc: out-of-range dlc reads back as 0");
    CHECK_EQ_U(0u, me_rpmsg_len_for_dlc(16u));
}

/* Build one complete GET_FRAME stream message (11 + 80 bytes) carrying the
 * given CAN ID, for the stream tests below. */
static size_t make_get_frame(uint32_t can_id, uint8_t *out,
                             size_t out_sz)
{
    me_rpmsg_can_t can;
    uint8_t payload[ME_RPMSG_CAN_MSG_LEN];

    memset(&can, 0, sizeof(can));
    can.can_id   = can_id;
    can.is_fd    = 1u;
    can.brs      = 1u;
    can.dlc      = 15u;
    can.data_len = 64u;
    can.data[0]  = 0x5Au;
    (void)me_rpmsg_pack_can(&can, payload);

    return me_rpmsg_wrap(ME_RPMSG_ACTION_READ, ME_RPMSG_CMD_CAN_GET_FRAME,
                         ME_RPMSG_RESULT_SUCCESS, payload,
                         ME_RPMSG_CAN_MSG_LEN, out, out_sz);
}

static void test_stream_single_frame(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t frame[ME_RPMSG_FRAME_MAX];
    size_t n;

    me_rpmsg_stream_init(&s);
    n = make_get_frame(0x022u, frame, sizeof(frame));

    TEST_CASE("rpmsg stream: one whole frame in one push");
    CHECK_EQ_U(91u, n);
    CHECK_EQ_U(n, me_rpmsg_stream_push(&s, frame, n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_EQ_U(80u, h.length);
    CHECK(payload != NULL);
    CHECK_BYTE(payload, 4, 0x22); /* can_id 0x022, little-endian */
    CHECK_BYTE(payload, 5, 0x00);

    TEST_CASE("rpmsg stream: nothing left after the frame is taken");
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));
}

static void test_stream_split_across_reads(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t frame[ME_RPMSG_FRAME_MAX];
    size_t n;

    me_rpmsg_stream_init(&s);
    n = make_get_frame(0x042u, frame, sizeof(frame));

    TEST_CASE("rpmsg stream: a frame split mid-header yields nothing yet");
    CHECK_EQ_U(5u, me_rpmsg_stream_push(&s, frame, 5u));
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));

    TEST_CASE("rpmsg stream: split mid-payload still yields nothing");
    CHECK_EQ_U(40u, me_rpmsg_stream_push(&s, &frame[5], 40u));
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));

    TEST_CASE("rpmsg stream: the frame appears once the last byte lands");
    CHECK_EQ_U(n - 45u, me_rpmsg_stream_push(&s, &frame[45], n - 45u));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(80u, h.length);
    CHECK_BYTE(payload, 4, 0x42);
}

static void test_stream_two_frames_one_push(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX * 2u];
    size_t n1;
    size_t n2;

    me_rpmsg_stream_init(&s);
    n1 = make_get_frame(0x022u, buf, sizeof(buf));
    n2 = make_get_frame(0x042u, &buf[n1], sizeof(buf) - n1);

    TEST_CASE("rpmsg stream: two concatenated frames both come out");
    CHECK_EQ_U(n1 + n2, me_rpmsg_stream_push(&s, buf, n1 + n2));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_BYTE(payload, 4, 0x22);
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_BYTE(payload, 4, 0x42);
    CHECK(!me_rpmsg_stream_next(&s, &h, &payload));
}

static void test_stream_ack_then_frame(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX * 2u];
    const uint8_t ack[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x01, 0x00, 0x10, 0x00, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00
    };
    size_t n;

    me_rpmsg_stream_init(&s);
    memcpy(buf, ack, sizeof(ack));
    n = make_get_frame(0x022u, &buf[sizeof(ack)], sizeof(buf) - sizeof(ack));

    TEST_CASE("rpmsg stream: a zero-length ACK is a complete frame");
    CHECK_EQ_U(sizeof(ack) + n,
               me_rpmsg_stream_push(&s, buf, sizeof(ack) + n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_SET_FRAME, h.command);
    CHECK_EQ_U(ME_RPMSG_RESULT_SUCCESS, h.result);
    CHECK_EQ_U(0u, h.length);

    TEST_CASE("rpmsg stream: the frame behind the ACK is still found");
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_EQ_U(80u, h.length);
}

static void test_stream_resyncs_on_garbage(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX + 4u];
    size_t n;

    me_rpmsg_stream_init(&s);
    buf[0] = 0x01;
    buf[1] = 0x02;
    buf[2] = 0x03;
    n = make_get_frame(0x022u, &buf[3], sizeof(buf) - 3u);

    TEST_CASE("rpmsg stream: leading garbage is discarded, frame recovered");
    CHECK_EQ_U(3u + n, me_rpmsg_stream_push(&s, buf, 3u + n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_BYTE(payload, 4, 0x22);
}

static void test_stream_skips_implausible_length(void)
{
    me_rpmsg_stream_t s;
    me_rpmsg_hdr_t h;
    const uint8_t *payload = NULL;
    uint8_t buf[ME_RPMSG_FRAME_MAX + ME_RPMSG_HDR_LEN];
    /* A sync byte followed by length 0x51 (81) - over the 80-byte max, so
     * this is not a real header. The parser must skip ONE byte and rescan,
     * not discard the whole buffer, or it would eat the good frame behind
     * it. */
    const uint8_t bad[ME_RPMSG_HDR_LEN] = {
        0xAA, 0x02, 0x00, 0x10, 0x00, 0x02, 0x01, 0x00, 0x00, 0x00, 0x51
    };
    size_t n;

    me_rpmsg_stream_init(&s);
    memcpy(buf, bad, sizeof(bad));
    n = make_get_frame(0x022u, &buf[sizeof(bad)], sizeof(buf) - sizeof(bad));

    TEST_CASE("rpmsg stream: length over 80 resyncs onto the next frame");
    CHECK_EQ_U(sizeof(bad) + n,
               me_rpmsg_stream_push(&s, buf, sizeof(bad) + n));
    CHECK(me_rpmsg_stream_next(&s, &h, &payload));
    CHECK_EQ_U(ME_RPMSG_CMD_CAN_GET_FRAME, h.command);
    CHECK_EQ_U(80u, h.length);
    CHECK_BYTE(payload, 4, 0x22);
}

static void test_stream_overflow_is_bounded(void)
{
    me_rpmsg_stream_t s;
    uint8_t junk[256];
    size_t accepted = 0u;
    unsigned i;

    me_rpmsg_stream_init(&s);
    memset(junk, 0x00, sizeof(junk)); /* no sync byte anywhere */

    TEST_CASE("rpmsg stream: sync-less junk never overruns the buffer");
    for (i = 0u; i < 64u; i++) {
        accepted += me_rpmsg_stream_push(&s, junk, sizeof(junk));
    }
    /* 16 KB pushed into a buffer that cannot hold it: the module must have
     * dropped bytes rather than written past the end. Reaching this line
     * without a crash or a sanitizer trip is the assertion. */
    CHECK(accepted <= 64u * sizeof(junk));
    CHECK(s.used <= ME_RPMSG_STREAM_BUF_SZ);
}

void run_rpmsg_frame_tests(void)
{
    printf("rpmsg_frame...\n");
    test_wrap_header_golden();
    test_wrap_rejects_oversize();
    test_parse_header_ack();
    test_parse_header_rejects();
    test_pack_can_golden();
    test_can_round_trip();
    test_can_rejects_bad_len();
    test_dlc_table();
    test_stream_single_frame();
    test_stream_split_across_reads();
    test_stream_two_frames_one_push();
    test_stream_ack_then_frame();
    test_stream_resyncs_on_garbage();
    test_stream_skips_implausible_length();
    test_stream_overflow_is_bounded();
}
