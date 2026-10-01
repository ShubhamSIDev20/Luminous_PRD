/*
 * test_frame_router.c - classifying an inbound TCP frame by its start byte.
 *
 * The router decides which thread a frame belongs to. Getting it wrong sends a
 * program packet to Core Logic or a Start command to the Data Manager, where
 * each is silently ignored - so these tests are the guard against a whole
 * class of "nothing happened and nothing was logged" faults.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/frame_router.h"

static void test_program_data_routes_to_the_data_manager(void)
{
    TEST_CASE("router: 0xBB Q4 becomes STORE_PROGRAM, past the 2-byte length");
    /* BB | dev | ckt | 04 | LEN_HI | LEN_LO | step bytes... | CRC[2] */
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_PROGRAM;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_QID_PROGRAM_DATA;
    f[4] = 0x00;
    f[5] = 0x08; /* 8 step bytes follow */
    me_crc16_append(f, 14, ME_CRC_ORDER_BE);

    me_frame_info_t fi;
    CHECK(me_frame_classify(f, 16, &fi));
    CHECK(fi.valid);
    CHECK_EQ_U(ME_MSG_STORE_PROGRAM, fi.msg_type);
    CHECK_EQ_U(0x11, fi.circuit_id);
    CHECK_EQ_U(ME_QID_PROGRAM_DATA, fi.query_id);
    /* Body starts after start+dev+ckt+qid+len_hi+len_lo, and excludes the CRC. */
    CHECK_EQ_U(6, fi.body_off);
    CHECK_EQ_U(8, fi.body_len);
    CHECK(fi.reject == NULL);
}

static void test_battery_write_routes_to_the_data_manager(void)
{
    TEST_CASE("router: 0xAA Q5 becomes STORE_BATTERY");
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONFIG;
    f[1] = 0x01;
    f[2] = 0x21;
    f[3] = ME_QID_CFG_WRITE_BATTERY;
    me_crc16_append(f, 26, ME_CRC_ORDER_BE);

    me_frame_info_t fi;
    CHECK(me_frame_classify(f, 28, &fi));
    CHECK_EQ_U(ME_MSG_STORE_BATTERY, fi.msg_type);
    CHECK_EQ_U(0x21, fi.circuit_id);
    CHECK_EQ_U(4, fi.body_off);
    CHECK_EQ_U(22, fi.body_len);
}

static void test_other_config_queries_route_as_config(void)
{
    TEST_CASE("router: any other 0xAA query becomes STORE_CONFIG");
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONFIG;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = 0x02; /* read factory data - not the battery write */
    me_crc16_append(f, 10, ME_CRC_ORDER_BE);

    me_frame_info_t fi;
    CHECK(me_frame_classify(f, 12, &fi));
    CHECK_EQ_U(ME_MSG_STORE_CONFIG, fi.msg_type);
}

static void test_control_routes_to_core_logic(void)
{
    TEST_CASE("router: 0xEE becomes CONTROL and keeps the whole frame");
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONTROL;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_CTRL_START;
    me_crc16_append(f, 4, ME_CRC_ORDER_BE);

    me_frame_info_t fi;
    CHECK(me_frame_classify(f, 6, &fi));
    CHECK_EQ_U(ME_MSG_CONTROL, fi.msg_type);
    /* Core Logic re-parses and re-verifies the frame, so it needs all of it. */
    CHECK_EQ_U(0, fi.body_off);
    CHECK_EQ_U(6, fi.body_len);
}

static void test_registration_is_recognised_but_not_routed(void)
{
    TEST_CASE("router: 0xDD is valid but belongs to the comm thread itself");
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_REGISTRATION;
    f[1] = ME_QID_REGISTER;

    me_frame_info_t fi;
    CHECK(me_frame_classify(f, 7, &fi));
    CHECK(fi.valid);
    CHECK_EQ_U(ME_MSG_NONE, fi.msg_type);
}

static void test_unknown_start_byte_is_rejected_with_a_reason(void)
{
    TEST_CASE("router: an unknown start byte is rejected and says why");
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = 0x42;

    me_frame_info_t fi;
    CHECK(!me_frame_classify(f, 8, &fi));
    CHECK(!fi.valid);
    CHECK(fi.reject != NULL);
    CHECK_EQ_U(ME_MSG_NONE, fi.msg_type);
}

static void test_short_frame_is_rejected(void)
{
    TEST_CASE("router: a frame too short to hold a header is rejected");
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONTROL;

    me_frame_info_t fi;
    CHECK(!me_frame_classify(f, 3, &fi));
    CHECK(fi.reject != NULL);

    /* 0xBB Q4 needs two more bytes than the others for its length field. */
    f[0] = ME_START_PROGRAM;
    f[3] = ME_QID_PROGRAM_DATA;
    CHECK(!me_frame_classify(f, 7, &fi));
}

static void test_declared_length_beyond_the_frame_is_rejected(void)
{
    TEST_CASE("router: a 0xBB length longer than the frame is rejected");
    /* Trusting this field is how the old firmware's memcpy could run past its
     * buffer. The length is checked against what actually arrived. */
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_PROGRAM;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_QID_PROGRAM_DATA;
    f[4] = 0xFF;
    f[5] = 0xFF; /* claims 65535 bytes */

    me_frame_info_t fi;
    CHECK(!me_frame_classify(f, 16, &fi));
    CHECK(fi.reject != NULL);
}

static void test_expected_length(void)
{
    TEST_CASE("router: frame length is derivable where the protocol allows it");
    uint8_t f[32];
    memset(f, 0, sizeof(f));

    /* 0xEE with no payload is 6 bytes. NOTE: Start is NOT one of those - it
     * carries a Session ID, found on hardware 2026-08-12. This assertion used
     * ME_CTRL_START and expected 6, which was the bug. */
    f[0] = ME_START_CONTROL;
    f[3] = ME_CTRL_STOP;
    CHECK_EQ_U(6, me_frame_expected_len(f, 16));

    /* 0xEE Sync Time carries a 4-byte epoch: 10 bytes. */
    f[3] = ME_CTRL_SYNC_TIME;
    CHECK_EQ_U(10, me_frame_expected_len(f, 16));

    /* 0xDD registration response is fixed at 7. */
    f[0] = ME_START_REGISTRATION;
    CHECK_EQ_U(ME_REG_RESPONSE_LEN, me_frame_expected_len(f, 16));

    /* 0xBB Q4 derives from its declared length: 6 + declared + 2. */
    f[0] = ME_START_PROGRAM;
    f[3] = ME_QID_PROGRAM_DATA;
    f[4] = 0x00;
    f[5] = 0x08;
    CHECK_EQ_U(16, me_frame_expected_len(f, 32));
}

static void test_expected_length_of_the_answered_program_queries(void)
{
    TEST_CASE("router: Q1 is 6 bytes and Q3 is 8, so neither can mis-split");
    /*
     * These two are answered by the communication thread, which means they now
     * arrive in the middle of a conversation rather than alone. Without a
     * derivable length, a Q1 that shared a read() with the following frame
     * would be handed to route_frame() as one oversized blob, fail its CRC and
     * take the other frame down with it.
     *
     * 6 and 8 are not guesses: Q1 carries no payload, the same shape as a
     * payload-less 0xEE control frame, and the document's own Q3 example is
     * "BB 01 01 03 00 03 -- --".
     */
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_PROGRAM;
    f[1] = 0x01;
    f[2] = 0x11;

    f[3] = ME_QID_PRG_IS_READY;
    CHECK_EQ_U(6, me_frame_expected_len(f, 32));
    CHECK_EQ_U(ME_PRG_Q1_LEN, me_frame_expected_len(f, 32));

    f[3] = ME_QID_PRG_PACKET_COUNT;
    CHECK_EQ_U(8, me_frame_expected_len(f, 32));
    CHECK_EQ_U(ME_PRG_Q3_LEN, me_frame_expected_len(f, 32));

    /* The unimplemented queries stay undeterminable rather than being given a
     * plausible-looking length nothing has verified. */
    f[3] = ME_QID_PRG_METADATA;
    CHECK_EQ_U(0, me_frame_expected_len(f, 32));
    f[3] = ME_QID_PRG_READ_METADATA;
    CHECK_EQ_U(0, me_frame_expected_len(f, 32));
    f[3] = ME_QID_PRG_READ_PROGRAM;
    CHECK_EQ_U(0, me_frame_expected_len(f, 32));
}

static void test_program_queries_classify_as_unroutable(void)
{
    TEST_CASE("router: Q1/Q3 stay valid-but-unroutable - comm answers them");
    /* The router must not invent a message type for these: no other thread has
     * anything to do with an is-ready query. It reports the query ID, and the
     * communication thread decides. */
    uint8_t f[ME_PRG_Q1_LEN];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_PROGRAM;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_QID_PRG_IS_READY;
    me_crc16_append(f, ME_PRG_Q1_LEN - ME_REG_CRC_LEN, ME_CRC_ORDER_BE);

    me_frame_info_t fi;
    CHECK(me_frame_classify(f, ME_PRG_Q1_LEN, &fi));
    CHECK(fi.valid);
    CHECK_EQ_U(ME_MSG_NONE, fi.msg_type);
    CHECK_EQ_U(ME_START_PROGRAM, fi.start);
    CHECK_EQ_U(0x01, fi.device_id);
    CHECK_EQ_U(0x11, fi.circuit_id);
    CHECK_EQ_U(ME_QID_PRG_IS_READY, fi.query_id);
    CHECK_EQ_U(0, fi.body_len); /* no payload */
}

static void test_expected_length_of_the_config_queries(void)
{
    TEST_CASE("router: every 0xAA query has a derivable length");
    /*
     * 0xAA returned 0 from the length table until 2026-08-12 - "no length is
     * encoded anywhere in the frame", which is true, but the LAYOUT fixes each
     * query's size and bm_config_v6.0.md now documents all six. Without an
     * entry, a Q5 sharing a read() with the frame behind it fell through to
     * ADR-19's CRC scan; with one it splits by layout, which is cheaper and
     * cannot land on a coincidental checksum inside the payload.
     *
     * Literal numbers on purpose: a named constant that encoded the wrong size
     * would make this test agree with the bug. Sources, per query:
     *   Q1..Q4  header + CRC only, no payload          = 6
     *   Q5      header + 40-byte battery payload + CRC = 46  (section 5.3/5.4)
     *   Q6      header + 4-byte epoch + CRC            = 10  (section 3.6)
     */
    uint8_t f[64];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONFIG;
    f[1] = 0x01;
    f[2] = 0x11;

    f[3] = ME_QID_CFG_IS_READY;
    CHECK_EQ_U(6, me_frame_expected_len(f, 64));
    f[3] = ME_QID_CFG_READ_FACTORY;
    CHECK_EQ_U(6, me_frame_expected_len(f, 64));
    f[3] = ME_QID_CFG_READ_MANUFACTURING;
    CHECK_EQ_U(6, me_frame_expected_len(f, 64));
    f[3] = ME_QID_CFG_READ_BATTERY;
    CHECK_EQ_U(6, me_frame_expected_len(f, 64));

    /* The one the board actually receives today. */
    f[3] = ME_QID_CFG_WRITE_BATTERY;
    CHECK_EQ_U(46, me_frame_expected_len(f, 64));

    f[3] = ME_QID_CFG_SYNC_TIME;
    CHECK_EQ_U(10, me_frame_expected_len(f, 64));

    /*
     * Q7-Q10 are BROADCAST and use a DIFFERENT 2-byte header - no DeviceNumber,
     * no CircuitNumber (bm_config_v6.0.md section 9.1). Their length is NOT
     * derivable with the 4-byte assumption this function makes, so they must
     * stay 0 rather than be given a confidently wrong size.
     */
    f[3] = 0x07;
    CHECK_EQ_U(0, me_frame_expected_len(f, 64));
    f[3] = 0x0A;
    CHECK_EQ_U(0, me_frame_expected_len(f, 64));

    /* 0xA0 calibration still encodes nothing and stays undeterminable. */
    f[0] = ME_START_CALIBRATION;
    f[3] = 0x01;
    CHECK_EQ_U(0, me_frame_expected_len(f, 64));
}

static void test_config_write_battery_splits_a_coalesced_read(void)
{
    TEST_CASE("router: a 0xAA Q5 followed by another frame splits at 46");
    /*
     * The reason the entry above matters. Two frames in one read(): a 46-byte
     * battery write and a 6-byte control Stop behind it. Before the Q5 entry
     * existed this resolved via the CRC scan; it must now resolve by layout and
     * report no table error.
     */
    uint8_t buf[64];
    memset(buf, 0, sizeof(buf));

    buf[0] = ME_START_CONFIG;
    buf[1] = 0x01;
    buf[2] = 0x11;
    buf[3] = ME_QID_CFG_WRITE_BATTERY;
    /* 40 bytes of payload, non-zero so a scan has something to trip over */
    for (unsigned i = 0; i < 40u; i++) {
        buf[4u + i] = (uint8_t)(0x10u + i);
    }
    me_crc16_append(buf, 44u, ME_CRC_ORDER_BE);

    uint8_t *second = &buf[46];
    second[0] = ME_START_CONTROL;
    second[1] = 0x01;
    second[2] = 0x11;
    second[3] = ME_CTRL_STOP;
    me_crc16_append(second, 4u, ME_CRC_ORDER_BE);

    bool scanned = true;
    CHECK_EQ_U(46, me_frame_resolve_len(buf, 52u, ME_CRC_ORDER_BE, &scanned));
    CHECK(!scanned); /* resolved by layout, so no "fix the table" WARN */

    CHECK_EQ_U(6, me_frame_resolve_len(second, 6u, ME_CRC_ORDER_BE, &scanned));
    CHECK(!scanned);
}

static void test_control_start_is_ten_bytes(void)
{
    TEST_CASE("router: 0xEE Start is 10 bytes - it carries a Session ID");
    /* The bug this exists to prevent, from hardware 2026-08-12: Start returned 6
     * here, so consume_rx_buffer() handed route_frame() the first 6 bytes of a
     * 10-byte frame and its CRC was computed over the wrong span. */
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONTROL;
    f[1] = 0x01;
    f[2] = 0x11;

    f[3] = ME_CTRL_START;
    CHECK_EQ_U(10, me_frame_expected_len(f, 16));
    CHECK_EQ_U(ME_CTRL_START_LEN, me_frame_expected_len(f, 16));

    /* Sync Time is also 10, for a different field. */
    f[3] = ME_CTRL_SYNC_TIME;
    CHECK_EQ_U(ME_CTRL_SYNC_LEN, me_frame_expected_len(f, 16));

    /* The other four carry nothing and stay at 6. */
    f[3] = ME_CTRL_STOP;
    CHECK_EQ_U(ME_CTRL_BARE_LEN, me_frame_expected_len(f, 16));
    f[3] = ME_CTRL_PAUSE;
    CHECK_EQ_U(6, me_frame_expected_len(f, 16));
    f[3] = ME_CTRL_CONTINUE;
    CHECK_EQ_U(6, me_frame_expected_len(f, 16));
    f[3] = ME_CTRL_RESET;
    CHECK_EQ_U(6, me_frame_expected_len(f, 16));
}

static void test_resolve_len_uses_the_layout_when_it_checksums(void)
{
    TEST_CASE("router: resolve_len takes the layout length when its CRC is good");
    static const uint8_t wire[10] = {
        0xEE, 0x01, 0x11, 0x01, 0x01, 0x11, 0x35, 0xF3, 0x62, 0xE7
    };

    bool scanned = true; /* must be cleared */
    CHECK_EQ_U(10, me_frame_resolve_len(wire, sizeof(wire), ME_CRC_ORDER_BE,
                                        &scanned));
    CHECK(!scanned);
}

static void test_resolve_len_recovers_a_wrong_layout_length(void)
{
    TEST_CASE("router: resolve_len finds the boundary the table got wrong");
    /*
     * THE POINT OF THIS FUNCTION. This protocol carries no length field, so
     * me_frame_expected_len() is a table of layout knowledge - and on
     * 2026-08-12 one entry was wrong, which did not merely reject its own frame:
     * the leftover bytes desynced every following frame on the connection.
     *
     * The CRC is the only reliable delimiter available. Here a 0xEE Stop frame
     * really does carry a 4-byte payload the table says it does not, and the
     * scan recovers it anyway.
     */
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONTROL;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_CTRL_STOP; /* the table says 6 bytes */
    f[4] = 0xDE;
    f[5] = 0xAD;
    f[6] = 0xBE;
    f[7] = 0xEF;
    me_crc16_append(f, 8, ME_CRC_ORDER_BE); /* but it is really 10 */

    CHECK_EQ_U(6, me_frame_expected_len(f, 16)); /* the table is wrong... */

    bool scanned = false;
    CHECK_EQ_U(10, me_frame_resolve_len(f, 10, ME_CRC_ORDER_BE, &scanned));
    CHECK(scanned); /* ...and says so, so the table can be fixed */
}

static void test_resolve_len_takes_a_lengthless_frame_whole(void)
{
    TEST_CASE("router: a lone 0xA0 frame is taken whole, with NO scan warning");
    /*
     * THE REGRESSION THIS EXISTS TO PREVENT. 0xA0 calibration encodes no length
     * anywhere, so the layout table returns 0 - correctly, and deliberately. The
     * first version of resolve_len() treated that as "the table is wrong", which
     * meant:
     *
     *   - every such frame went down the shortest-first scan path, so a
     *     coincidental CRC match inside its own payload could truncate it, and
     *   - every such frame logged "the length table needs fixing", burying real
     *     anomalies in the one log used to debug hardware.
     *
     * "No table entry" and "a table entry that might be wrong" are different
     * states. The whole read is tried for BOTH now (see
     * test_resolve_len_does_not_truncate_on_a_planted_short_crc), but only a
     * non-zero entry that disagrees is reported as a table error.
     *
     * VEHICLE CHANGED 2026-08-12: this test used 0xAA Q5, which was lengthless
     * until bm_config_v6.0.md gave it a real 46-byte entry. 0xA0 is now the
     * genuinely lengthless start byte, so the test asserts what it always meant.
     */
    uint8_t f[28];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CALIBRATION;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = 0x01;
    for (unsigned i = 4; i < 24; i++) {
        f[i] = (uint8_t)(0x40u + i); /* plausible payload */
    }
    me_crc16_append(f, 24, ME_CRC_ORDER_BE); /* 26-byte frame */

    CHECK_EQ_U(0, me_frame_expected_len(f, 26)); /* no table entry, by design */

    bool scanned = true; /* must be cleared */
    CHECK_EQ_U(26, me_frame_resolve_len(f, 26, ME_CRC_ORDER_BE, &scanned));
    CHECK(!scanned); /* the table is not wrong - it has no entry to be wrong */
}

static void test_resolve_len_does_not_truncate_on_a_planted_short_crc(void)
{
    TEST_CASE("router: a coincidental short CRC match must not truncate a frame");
    /*
     * The nastiest realistic case for a shortest-first scan: a frame whose own
     * payload happens to contain, at some earlier offset, two bytes that are a
     * valid CRC of everything before them. Here that is planted deliberately at
     * offset 4, so a 6-byte prefix checksums perfectly - but the real frame is
     * 12 bytes.
     *
     * Shortest-first alone returns 6, truncating the frame and leaving 6 orphan
     * bytes to desync everything after it. Trying the whole read first returns
     * 12. This is not a hypothetical: it is the same failure shape as the 0xEE
     * Start bug of 2026-08-12, arrived at from the opposite direction.
     */
    uint8_t f[12];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CALIBRATION; /* no table entry at all */
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = 0x01;
    me_crc16_append(f, 4, ME_CRC_ORDER_BE); /* plant a valid 6-byte prefix */

    /* The frame actually continues, and ends properly at 12. */
    f[6] = 0xDE;
    f[7] = 0xAD;
    f[8] = 0xBE;
    f[9] = 0xEF;
    me_crc16_append(f, 10, ME_CRC_ORDER_BE);

    /* Both boundaries genuinely checksum - the trap is real, not contrived. */
    CHECK(me_crc16_verify(f, 6, ME_CRC_ORDER_BE));
    CHECK(me_crc16_verify(f, 12, ME_CRC_ORDER_BE));

    bool scanned = true;
    CHECK_EQ_U(12, me_frame_resolve_len(f, 12, ME_CRC_ORDER_BE, &scanned));
    CHECK(!scanned);
}

static void test_resolve_len_does_not_truncate_when_the_layout_overshoots(void)
{
    TEST_CASE("router: an over-long table entry must not enable truncation");
    /*
     * THE REGRESSION FOUND ON 2026-08-12 while adding the 0xAA length entries.
     *
     * The whole-read protection above used to be gated on `layout == 0`. The
     * moment 0xAA Q5 gained its real 46-byte entry, a SHORT 0xAA frame had a
     * layout of 46 - larger than the 12 bytes that had arrived - so step 1 could
     * not use it, the gate excluded it from the whole-read step, and it fell
     * straight into the scan, which truncated it at the planted 6-byte CRC.
     *
     * Adding a correct table entry made a different frame parse WORSE. A layout
     * that overshoots what arrived says nothing about where the frame ends, so it
     * must not forfeit the whole-read check.
     *
     * Here the disagreement IS reported (the table says 46, the frame is 12),
     * unlike the 0xA0 case above where there is no entry to be wrong.
     */
    uint8_t f[12];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONFIG;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_QID_CFG_WRITE_BATTERY; /* the table says 46 bytes */
    me_crc16_append(f, 4, ME_CRC_ORDER_BE); /* plant a valid 6-byte prefix */

    f[6] = 0xDE;
    f[7] = 0xAD;
    f[8] = 0xBE;
    f[9] = 0xEF;
    me_crc16_append(f, 10, ME_CRC_ORDER_BE); /* but it really ends at 12 */

    CHECK_EQ_U(46, me_frame_expected_len(f, 12)); /* overshoots what arrived */
    CHECK(me_crc16_verify(f, 6, ME_CRC_ORDER_BE));
    CHECK(me_crc16_verify(f, 12, ME_CRC_ORDER_BE));

    bool scanned = false;
    CHECK_EQ_U(12, me_frame_resolve_len(f, 12, ME_CRC_ORDER_BE, &scanned));
    CHECK(scanned); /* a real entry disagreed, so the log should say so */
}

static void test_resolve_len_splits_a_coalesced_read(void)
{
    TEST_CASE("router: resolve_len splits two 0xAA frames sharing one read");
    /*
     * When the whole read does NOT checksum, two frames arrived together. Only
     * then does the scan run, and shortest-first finds the seam. Reaching the
     * scan is now conditional on the safe interpretation having failed first.
     */
    uint8_t buf[24];
    memset(buf, 0, sizeof(buf));

    /* First frame: 0xAA, 6 bytes total. */
    buf[0] = ME_START_CONFIG;
    buf[1] = 0x01;
    buf[2] = 0x11;
    buf[3] = ME_QID_CFG_READ_FACTORY;
    me_crc16_append(buf, 4, ME_CRC_ORDER_BE);

    /* Second frame right behind it, 8 bytes total. */
    buf[6] = ME_START_CONFIG;
    buf[7] = 0x01;
    buf[8] = 0x11;
    buf[9] = ME_QID_CFG_READ_BATTERY;
    buf[10] = 0xAB;
    buf[11] = 0xCD;
    me_crc16_append(&buf[6], 6, ME_CRC_ORDER_BE);

    /* Q2 is 6 bytes since bm_config_v6.0.md; before that this was 0 and the seam
     * could only be found by scanning. Now the layout finds it directly. */
    CHECK_EQ_U(6, me_frame_expected_len(buf, 14));
    /* The 14-byte whole read does not checksum, which is what makes this a
     * genuine two-frame read rather than one long frame. */
    CHECK(!me_crc16_verify(buf, 14, ME_CRC_ORDER_BE));

    bool scanned = true;
    CHECK_EQ_U(6, me_frame_resolve_len(buf, 14, ME_CRC_ORDER_BE, &scanned));
    /* Not a table error: the entry was right, so step 1 resolved it. */
    CHECK(!scanned);

    CHECK_EQ_U(8, me_frame_resolve_len(&buf[6], 8, ME_CRC_ORDER_BE, &scanned));
}

static void test_resolve_len_only_reports_a_real_table_error(void)
{
    TEST_CASE("router: out_scanned is true ONLY when a real entry was wrong");
    /* The WARN this drives tells a future maintainer to fix a table entry. It
     * must therefore fire when an entry EXISTS and is wrong (0xEE Stop below),
     * and stay quiet when there is no entry at all (0xAA, tested above). */
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONTROL;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_CTRL_STOP; /* the table claims 6 */
    f[4] = 0xDE;
    f[5] = 0xAD;
    f[6] = 0xBE;
    f[7] = 0xEF;
    me_crc16_append(f, 8, ME_CRC_ORDER_BE); /* really 10 */

    bool scanned = false;
    CHECK_EQ_U(10, me_frame_resolve_len(f, 10, ME_CRC_ORDER_BE, &scanned));
    CHECK(scanned);
}

static void test_resolve_len_trusts_a_declared_length(void)
{
    TEST_CASE("router: resolve_len never second-guesses 0xBB Q4's own length");
    /*
     * Q4 states its payload length in the frame. Scanning step data for a
     * coincidental CRC match could pick a boundary inside a program step, so a
     * declared length is trusted even when it fails to verify - the failure is
     * then reported honestly instead of being papered over.
     */
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_PROGRAM;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_QID_PROGRAM_DATA;
    f[4] = 0x00;
    f[5] = 0x08;                            /* 8 step bytes -> 16-byte frame */
    me_crc16_append(f, 20, ME_CRC_ORDER_BE); /* checksum deliberately misplaced */

    bool scanned = false;
    CHECK_EQ_U(16, me_frame_resolve_len(f, 22, ME_CRC_ORDER_BE, &scanned));
    CHECK(!scanned);
}

static void test_resolve_len_falls_back_when_nothing_checksums(void)
{
    TEST_CASE("router: resolve_len keeps the old behaviour on a bad frame");
    /* A genuinely corrupt or still-incomplete frame must not have a boundary
     * invented for it: the layout length is returned unchanged so the caller
     * behaves exactly as it did before this function existed. */
    uint8_t f[16];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CONTROL;
    f[1] = 0x01;
    f[2] = 0x11;
    f[3] = ME_CTRL_STOP;
    f[4] = 0x00; /* wrong CRC */
    f[5] = 0x00;

    bool scanned = true;
    CHECK_EQ_U(6, me_frame_resolve_len(f, 6, ME_CRC_ORDER_BE, &scanned));
    CHECK(!scanned);

    /* And 0 stays 0 when even that is unknowable. */
    uint8_t g[16];
    memset(g, 0, sizeof(g));
    g[0] = ME_START_CONFIG;
    CHECK_EQ_U(0, me_frame_resolve_len(g, 12, ME_CRC_ORDER_BE, &scanned));
}

static void test_undeterminable_length_reports_zero(void)
{
    TEST_CASE("router: 0xA0 has no length field, so its length is unknown");
    /*
     * A real protocol gap, not an oversight in the parser: a 0xA0 calibration
     * frame carries no length anywhere and this codebase has no layout knowledge
     * for it. Returning 0 tells the caller "consume the rest of what arrived"
     * rather than inventing a boundary.
     *
     * VEHICLE CHANGED 2026-08-12: 0xAA was the example until bm_config_v6.0.md
     * supplied real lengths for all six of its queries.
     */
    uint8_t f[32];
    memset(f, 0, sizeof(f));
    f[0] = ME_START_CALIBRATION;
    f[3] = 0x01;
    CHECK_EQ_U(0, me_frame_expected_len(f, 32));

    /* An unrecognised 0xAA query is still unknown - and Q7-Q10 deliberately so,
     * because their header is 2 bytes rather than 4 (bm_config_v6.0.md 9.1). */
    f[0] = ME_START_CONFIG;
    f[3] = 0x07;
    CHECK_EQ_U(0, me_frame_expected_len(f, 32));

    /* Too few bytes to even read the header. */
    f[0] = ME_START_CONTROL;
    CHECK_EQ_U(0, me_frame_expected_len(f, 2));
}

void run_frame_router_tests(void)
{
    printf("\n-- frame_router --\n");
    test_expected_length();
    test_expected_length_of_the_answered_program_queries();
    test_expected_length_of_the_config_queries();
    test_config_write_battery_splits_a_coalesced_read();
    test_control_start_is_ten_bytes();
    test_program_queries_classify_as_unroutable();
    test_resolve_len_uses_the_layout_when_it_checksums();
    test_resolve_len_recovers_a_wrong_layout_length();
    test_resolve_len_takes_a_lengthless_frame_whole();
    test_resolve_len_does_not_truncate_on_a_planted_short_crc();
    test_resolve_len_does_not_truncate_when_the_layout_overshoots();
    test_resolve_len_splits_a_coalesced_read();
    test_resolve_len_only_reports_a_real_table_error();
    test_resolve_len_trusts_a_declared_length();
    test_resolve_len_falls_back_when_nothing_checksums();
    test_undeterminable_length_reports_zero();
    test_program_data_routes_to_the_data_manager();
    test_battery_write_routes_to_the_data_manager();
    test_other_config_queries_route_as_config();
    test_control_routes_to_core_logic();
    test_registration_is_recognised_but_not_routed();
    test_unknown_start_byte_is_rejected_with_a_reason();
    test_short_frame_is_rejected();
    test_declared_length_beyond_the_frame_is_rejected();
}
