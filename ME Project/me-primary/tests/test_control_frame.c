/*
 * test_control_frame.c - 0xEE control frame parsing.
 *
 * Layout source: Docs/Ref Docs/bm_control_v3.0.md.
 * Frame: EE | DeviceNumber | CircuitNumber | QueryID | [payload] | CRC[2]
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/control_frame.h"

/* Build a control frame with an optional payload and a valid big-endian CRC. */
static uint32_t build(uint8_t *f, uint8_t qid, const uint8_t *payload,
                      uint32_t payload_len)
{
    uint32_t i = 0;
    f[i++] = ME_START_CONTROL;
    f[i++] = 0x01; /* device  */
    f[i++] = 0x11; /* circuit */
    f[i++] = qid;
    if (payload_len > 0u) {
        memcpy(&f[i], payload, payload_len);
        i += payload_len;
    }
    me_crc16_append(f, i, ME_CRC_ORDER_BE);
    return i + 2u;
}

static void test_start_command(void)
{
    TEST_CASE("control: a Start frame parses to device, circuit and query");
    uint8_t f[16];
    const uint32_t n = build(f, ME_CTRL_START, NULL, 0);
    CHECK_EQ_U(6, n);

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_OK,
               me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
    CHECK_EQ_U(0x01, c.device_id);
    CHECK_EQ_U(0x11, c.circuit_id);
    CHECK_EQ_U(ME_CTRL_START, c.query_id);
    CHECK(!c.has_epoch);
}

static void test_all_six_commands_parse(void)
{
    TEST_CASE("control: all six documented commands are accepted");
    const uint8_t qids[] = { ME_CTRL_START, ME_CTRL_STOP, ME_CTRL_PAUSE,
                             ME_CTRL_CONTINUE, ME_CTRL_RESET };
    for (unsigned i = 0; i < sizeof(qids) / sizeof(qids[0]); i++) {
        uint8_t f[16];
        const uint32_t n = build(f, qids[i], NULL, 0);
        me_control_t c;
        CHECK_EQ_U(ME_CTRL_PARSE_OK,
                   me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
        CHECK_EQ_U(qids[i], c.query_id);
    }

    /* Sync Time carries a 4-byte epoch, so it is built separately. */
    const uint8_t epoch[4] = { 0x68, 0x21, 0xD3, 0x94 };
    uint8_t f[16];
    const uint32_t n = build(f, ME_CTRL_SYNC_TIME, epoch, 4);
    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_OK, me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
    CHECK_EQ_U(ME_CTRL_SYNC_TIME, c.query_id);
}

static void test_sync_time_epoch_is_big_endian(void)
{
    TEST_CASE("control: Sync Time yields the epoch as a big-endian uint32");
    /* bm_control_v3.0 example: EE 01 01 05 68 21 D3 94 -- -- */
    const uint8_t epoch[4] = { 0x68, 0x21, 0xD3, 0x94 };
    uint8_t f[16];
    const uint32_t n = build(f, ME_CTRL_SYNC_TIME, epoch, 4);

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_OK, me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
    CHECK(c.has_epoch);
    CHECK_EQ_U(0x6821D394u, c.epoch);
}

static void test_bad_start_byte(void)
{
    TEST_CASE("control: a frame that is not 0xEE is rejected as BAD_START");
    uint8_t f[16];
    const uint32_t n = build(f, ME_CTRL_START, NULL, 0);
    f[0] = ME_START_PROGRAM;

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_BAD_START,
               me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
}

static void test_short_frame(void)
{
    TEST_CASE("control: a frame shorter than the minimum is SHORT");
    uint8_t f[16];
    const uint32_t n = build(f, ME_CTRL_START, NULL, 0);

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_SHORT,
               me_control_parse(f, n - 1u, ME_CRC_ORDER_BE, &c));
    CHECK_EQ_U(ME_CTRL_PARSE_SHORT, me_control_parse(f, 0, ME_CRC_ORDER_BE, &c));
}

static void test_bad_crc(void)
{
    TEST_CASE("control: a corrupted body fails the CRC rather than parsing");
    uint8_t f[16];
    const uint32_t n = build(f, ME_CTRL_START, NULL, 0);
    f[2] ^= 0xFFu; /* flip the circuit byte, leave the CRC stale */

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_BAD_CRC,
               me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
}

static void test_unknown_query_id(void)
{
    TEST_CASE("control: an unrecognised query ID is reported, not guessed at");
    uint8_t f[16];
    const uint32_t n = build(f, 0x7F, NULL, 0);

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_BAD_QUERY,
               me_control_parse(f, n, ME_CRC_ORDER_BE, &c));
}

static void test_command_names(void)
{
    TEST_CASE("control: commands have printable names");
    CHECK_STR("Start",     me_control_query_name(ME_CTRL_START));
    CHECK_STR("Stop",      me_control_query_name(ME_CTRL_STOP));
    CHECK_STR("Pause",     me_control_query_name(ME_CTRL_PAUSE));
    CHECK_STR("Continue",  me_control_query_name(ME_CTRL_CONTINUE));
    CHECK_STR("SyncTime",  me_control_query_name(ME_CTRL_SYNC_TIME));
    CHECK_STR("Reset",     me_control_query_name(ME_CTRL_RESET));
    CHECK_STR("Unknown",   me_control_query_name(0x7F));
}

static void test_start_carries_a_session_id(void)
{
    TEST_CASE("control: the REAL 10-byte Start frame from hardware parses");
    /*
     * Captured on hardware 2026-08-12 08:59:28 and rejected as BAD_CRC:
     *
     *   EE 01 11 01 01 11 35 F3 62 E7
     *   |  |  |  |  \_________/ \___/
     *   |  |  |  |   SessionID   CRC over the preceding 8 bytes
     *   |  |  |  QueryID 01 = Start
     *   |  |  CircuitID 0x11
     *   |  DeviceNumber
     *   Start 0xEE
     *
     * The CRC was correct all along. The board sliced the frame at 6 bytes -
     * bm_control_v3.0.md said Start had no payload - and checksummed
     * "EE 01 11 01" against "01 11", the first half of the Session ID.
     *
     * This is the regression test for that: the literal bytes off the wire.
     */
    static const uint8_t wire[10] = {
        0xEE, 0x01, 0x11, 0x01, 0x01, 0x11, 0x35, 0xF3, 0x62, 0xE7
    };

    me_control_t c;
    const me_ctrl_parse_result_t r =
        me_control_parse(wire, sizeof(wire), ME_CRC_ORDER_BE, &c);

    CHECK_EQ_U(ME_CTRL_PARSE_OK, r);
    CHECK_EQ_U(0x01, c.device_id);
    CHECK_EQ_U(0x11, c.circuit_id);
    CHECK_EQ_U(ME_CTRL_START, c.query_id);
    CHECK(c.has_session_id);
    CHECK_EQ_U(0x011135F3u, c.session_id);

    /* A Start frame carries no epoch, whatever else it carries. */
    CHECK(!c.has_epoch);

    CHECK_EQ_U(10, ME_CTRL_START_LEN);
}

static void test_session_id_is_big_endian(void)
{
    TEST_CASE("control: the Session ID is big-endian, like every other field");
    uint8_t f[ME_CTRL_START_LEN];
    memset(f, 0, sizeof(f));
    f[ME_HDR_OFF_START]    = ME_START_CONTROL;
    f[ME_HDR_OFF_DEVICE]   = 0x01;
    f[ME_HDR_OFF_CIRCUIT]  = 0x11;
    f[ME_HDR_OFF_QUERY_ID] = ME_CTRL_START;
    f[ME_CTRL_OFF_SESSION_ID + 0u] = 0x12;
    f[ME_CTRL_OFF_SESSION_ID + 1u] = 0x34;
    f[ME_CTRL_OFF_SESSION_ID + 2u] = 0x56;
    f[ME_CTRL_OFF_SESSION_ID + 3u] = 0x78;
    me_crc16_append(f, ME_CTRL_START_LEN - ME_REG_CRC_LEN, ME_CRC_ORDER_BE);

    me_control_t c;
    CHECK_EQ_U(ME_CTRL_PARSE_OK,
               me_control_parse(f, sizeof(f), ME_CRC_ORDER_BE, &c));
    CHECK_EQ_U(0x12345678u, c.session_id); /* not 0x78563412 */
}

static void test_only_start_carries_a_session_id(void)
{
    TEST_CASE("control: Stop/Pause/Continue/Reset carry no Session ID");
    /* Confirmed by the developer 2026-08-12: only Start carries it. If that ever
     * changes, me_frame_resolve_len() recovers the boundary from the CRC rather
     * than mis-splitting - but has_session_id must still not lie. */
    static const uint8_t bare[4] = {
        ME_CTRL_STOP, ME_CTRL_PAUSE, ME_CTRL_CONTINUE, ME_CTRL_RESET
    };

    for (unsigned i = 0; i < sizeof(bare); i++) {
        uint8_t f[ME_CTRL_BARE_LEN];
        memset(f, 0, sizeof(f));
        f[ME_HDR_OFF_START]    = ME_START_CONTROL;
        f[ME_HDR_OFF_DEVICE]   = 0x01;
        f[ME_HDR_OFF_CIRCUIT]  = 0x11;
        f[ME_HDR_OFF_QUERY_ID] = bare[i];
        me_crc16_append(f, ME_CTRL_BARE_LEN - ME_REG_CRC_LEN, ME_CRC_ORDER_BE);

        me_control_t c;
        CHECK_EQ_U(ME_CTRL_PARSE_OK,
                   me_control_parse(f, sizeof(f), ME_CRC_ORDER_BE, &c));
        CHECK(!c.has_session_id);
        CHECK(!c.has_epoch);
        CHECK_EQ_U(0, c.session_id);
    }

    /* Sync Time carries an epoch at the same offset - it must NOT be reported
     * as a session ID, or a clock value would be logged as a session. */
    uint8_t s[ME_CTRL_SYNC_LEN];
    memset(s, 0, sizeof(s));
    s[ME_HDR_OFF_START]    = ME_START_CONTROL;
    s[ME_HDR_OFF_QUERY_ID] = ME_CTRL_SYNC_TIME;
    s[ME_CTRL_OFF_EPOCH + 0u] = 0x68;
    s[ME_CTRL_OFF_EPOCH + 1u] = 0x21;
    s[ME_CTRL_OFF_EPOCH + 2u] = 0xD3;
    s[ME_CTRL_OFF_EPOCH + 3u] = 0x94;
    me_crc16_append(s, ME_CTRL_SYNC_LEN - ME_REG_CRC_LEN, ME_CRC_ORDER_BE);

    me_control_t sc;
    CHECK_EQ_U(ME_CTRL_PARSE_OK,
               me_control_parse(s, sizeof(s), ME_CRC_ORDER_BE, &sc));
    CHECK(sc.has_epoch);
    CHECK_EQ_U(0x6821D394u, sc.epoch);
    CHECK(!sc.has_session_id);
}

void run_control_frame_tests(void)
{
    test_start_carries_a_session_id();
    test_session_id_is_big_endian();
    test_only_start_carries_a_session_id();
    printf("\n-- control_frame --\n");
    test_start_command();
    test_all_six_commands_parse();
    test_sync_time_epoch_is_big_endian();
    test_bad_start_byte();
    test_short_frame();
    test_bad_crc();
    test_unknown_query_id();
    test_command_names();
}
