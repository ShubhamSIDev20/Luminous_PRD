/*
 * test_reg_frame.c - device registration frame pack/parse unit tests.
 *
 * Every field offset is asserted individually rather than comparing one opaque
 * 33-byte blob, so a shifted field names itself in the failure output.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/reg_frame.h"

/* The demo device from the design spec: DeviceID 1, Secondary 1, Channel 1,
 * "BTS-600" at 192.168.0.14. */
static me_reg_request_t demo_request(void)
{
    me_reg_request_t r;
    memset(&r, 0, sizeof(r));
    r.device_id  = 0x01;
    r.circuit_id = ME_CIRCUIT_ID(1, 1);
    strcpy(r.device_name, "BTS-600");
    r.ip[0] = 192; r.ip[1] = 168; r.ip[2] = 0; r.ip[3] = 14;
    r.mac[0] = 0x00; r.mac[1] = 0x14; r.mac[2] = 0x2D;
    r.mac[3] = 0xAB; r.mac[4] = 0xCD; r.mac[5] = 0xEF;
    return r;
}

/* ------------------------------------------------------- value semantics -- */

static void test_value_success_predicate(void)
{
    /* 0x02 is a SUCCESS response: it means the Web Application already holds a
     * registration for this device. Reading it as a rejection made the board
     * re-register every 30 seconds (observed on hardware 2026-08-10). */
    TEST_CASE("value: 0x01 and 0x02 are success; 0x00 and unknown are not");

    CHECK(!ME_REG_VALUE_IS_SUCCESS(ME_REG_VALUE_FAILED));
    CHECK(ME_REG_VALUE_IS_SUCCESS(ME_REG_VALUE_REGISTERED));
    CHECK(ME_REG_VALUE_IS_SUCCESS(ME_REG_VALUE_ALREADY_REGISTERED));
    CHECK(!ME_REG_VALUE_IS_SUCCESS(0x03));
    CHECK(!ME_REG_VALUE_IS_SUCCESS(0xFF));
}

/* ------------------------------------------------------ circuit encoding -- */

static void test_circuit_id_packs_two_nibbles(void)
{
    TEST_CASE("circuit: ME_CIRCUIT_ID(1,1) == 0x11");
    CHECK_EQ_U(0x11, ME_CIRCUIT_ID(1, 1));

    TEST_CASE("circuit: secondary occupies the upper nibble");
    CHECK_EQ_U(0x30, ME_CIRCUIT_ID(3, 0));

    TEST_CASE("circuit: channel occupies the lower nibble");
    CHECK_EQ_U(0x07, ME_CIRCUIT_ID(0, 7));

    TEST_CASE("circuit: maximum secondary and channel");
    CHECK_EQ_U(0xFF, ME_CIRCUIT_ID(15, 15));
}

static void test_circuit_id_round_trips(void)
{
    TEST_CASE("circuit: decode recovers secondary and channel");
    const uint8_t id = ME_CIRCUIT_ID(9, 4);
    CHECK_EQ_U(9, ME_CIRCUIT_SECONDARY(id));
    CHECK_EQ_U(4, ME_CIRCUIT_CHANNEL(id));
}

/* --------------------------------------------------------- frame sizing -- */

static void test_frame_constants_are_self_consistent(void)
{
    TEST_CASE("layout: payload length is 28 bytes");
    CHECK_EQ_U(28, ME_REG_PAYLOAD_LEN);

    TEST_CASE("layout: total request is 33 bytes");
    CHECK_EQ_U(33, ME_REG_REQUEST_LEN);

    TEST_CASE("layout: response is 7 bytes");
    CHECK_EQ_U(7, ME_REG_RESPONSE_LEN);

    /* The CRC must start exactly where header + payload ends, or the length
     * byte and the frame describe different things. */
    TEST_CASE("layout: CRC offset equals header + payload");
    CHECK_EQ_U(ME_REG_OFF_CRC, ME_REG_HEADER_LEN + ME_REG_PAYLOAD_LEN);
}

static void test_pack_returns_full_frame_length(void)
{
    TEST_CASE("pack: returns 33");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    CHECK_EQ_U(ME_REG_REQUEST_LEN, me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE));
}

/* -------------------------------------------------------- field offsets -- */

static void test_pack_writes_header_fields(void)
{
    TEST_CASE("pack: header is 0xDD, QueryID 0x01, Length 0x1C");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_BYTE(buf, ME_REG_OFF_START, 0xDD);
    CHECK_BYTE(buf, ME_REG_OFF_QUERY_ID, 0x01);
    CHECK_BYTE(buf, ME_REG_OFF_LENGTH, 28); /* DeviceID..MAC inclusive */
}

static void test_pack_writes_device_and_circuit_at_offsets_3_and_4(void)
{
    /* This is the offset the whole design turned on: the length byte only
     * makes sense if DeviceID starts at 3, not at 1. */
    TEST_CASE("pack: DeviceID at offset 3, CircuitID at offset 4");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_BYTE(buf, 3, 0x01);
    CHECK_BYTE(buf, 4, 0x11);
}

static void test_pack_writes_ip_in_dotted_quad_order(void)
{
    /* 192.168.0.14 -> C0 A8 00 0E, NOT the little-endian uint32 0E 00 A8 C0. */
    TEST_CASE("pack: IP is dotted-quad order, not little-endian uint32");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_BYTE(buf, 21, 0xC0);
    CHECK_BYTE(buf, 22, 0xA8);
    CHECK_BYTE(buf, 23, 0x00);
    CHECK_BYTE(buf, 24, 0x0E);
}

static void test_pack_writes_mac_in_wire_order(void)
{
    TEST_CASE("pack: MAC occupies offsets 25..30 in wire order");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_BYTE(buf, 25, 0x00);
    CHECK_BYTE(buf, 26, 0x14);
    CHECK_BYTE(buf, 27, 0x2D);
    CHECK_BYTE(buf, 28, 0xAB);
    CHECK_BYTE(buf, 29, 0xCD);
    CHECK_BYTE(buf, 30, 0xEF);
}

/* ------------------------------------------------------- name handling  -- */

static void test_pack_null_pads_short_name(void)
{
    TEST_CASE("pack: \"BTS-600\" at 5..11, zero padding through 20");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_MEM(&buf[ME_REG_OFF_NAME], "BTS-600", 7);
    for (unsigned i = 12; i <= 20; i++) {
        CHECK_BYTE(buf, i, 0x00);
    }
}

static void test_pack_fills_exactly_sixteen_char_name(void)
{
    /* A 16-char name must fill the field with no NUL terminator spilling into
     * the IP field at offset 21. */
    TEST_CASE("pack: 16-char name fills the field without touching the IP");
    me_reg_request_t r = demo_request();
    strcpy(r.device_name, "ABCDEFGHIJKLMNOP");
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_MEM(&buf[ME_REG_OFF_NAME], "ABCDEFGHIJKLMNOP", 16);
    CHECK_BYTE(buf, 21, 0xC0); /* IP field intact */
}

static void test_pack_truncates_overlong_name(void)
{
    TEST_CASE("pack: name longer than 16 is truncated, IP field intact");
    me_reg_request_t r = demo_request();
    /* device_name is 17 bytes; write 16 chars + NUL, then verify the packer
     * never reads past the field even if the caller filled it completely. */
    memcpy(r.device_name, "0123456789ABCDEF", 16);
    r.device_name[16] = '\0';
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    CHECK_MEM(&buf[ME_REG_OFF_NAME], "0123456789ABCDEF", 16);
    CHECK_BYTE(buf, 21, 0xC0);
}

static void test_pack_handles_empty_name(void)
{
    TEST_CASE("pack: empty name zero-fills all 16 bytes");
    me_reg_request_t r = demo_request();
    r.device_name[0] = '\0';
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    for (unsigned i = 5; i <= 20; i++) {
        CHECK_BYTE(buf, i, 0x00);
    }
}

/* -------------------------------------------------------------- CRC     -- */

static void test_pack_crc_covers_bytes_0_to_30(void)
{
    TEST_CASE("pack: CRC is computed over bytes 0..30 and stored LE");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_LE);

    const uint16_t expected = me_crc16_modbus(buf, ME_REG_OFF_CRC);
    CHECK_BYTE(buf, 31, (uint8_t)(expected & 0xFF));
    CHECK_BYTE(buf, 32, (uint8_t)(expected >> 8));
    CHECK(me_crc16_verify(buf, ME_REG_REQUEST_LEN, ME_CRC_ORDER_LE));
}

static void test_pack_honours_big_endian_crc_order(void)
{
    TEST_CASE("pack: BE order stores the CRC high byte first");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_BE);

    const uint16_t expected = me_crc16_modbus(buf, ME_REG_OFF_CRC);
    CHECK_BYTE(buf, 31, (uint8_t)(expected >> 8));
    CHECK_BYTE(buf, 32, (uint8_t)(expected & 0xFF));
}

static void test_pack_default_order_stores_crc_high_byte_first(void)
{
    TEST_CASE("pack: the default order stores the CRC high byte first");
    const me_reg_request_t r = demo_request();
    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_DEFAULT);

    const uint16_t expected = me_crc16_modbus(buf, ME_REG_OFF_CRC);
    CHECK_BYTE(buf, 31, (uint8_t)(expected >> 8));
    CHECK_BYTE(buf, 32, (uint8_t)(expected & 0xFF));
    CHECK(me_crc16_verify(buf, ME_REG_REQUEST_LEN, ME_CRC_ORDER_DEFAULT));
}

/*
 * Golden vector: the exact 33-byte frame the board emits on the corporate LAN,
 * captured from hardware. Byte-for-byte, so any regression in ANY field - not
 * just the CRC - names itself here rather than on the wire.
 *
 * BTS-600 as device 1 / secondary 1 / channel 1 at 172.16.18.238,
 * MAC 00:14:2d:ef:86:e2. CRC-16/Modbus over bytes 0..30 is 0xADBE, which the
 * default big-endian order puts on the wire as "AD BE".
 */
static void test_pack_matches_captured_hardware_frame(void)
{
    TEST_CASE("pack: golden hardware frame ends AD BE, not BE AD");

    me_reg_request_t r;
    memset(&r, 0, sizeof(r));
    r.device_id  = 0x01;
    r.circuit_id = ME_CIRCUIT_ID(1, 1);
    strcpy(r.device_name, "BTS-600");
    r.ip[0] = 172; r.ip[1] = 16;   r.ip[2] = 18;   r.ip[3] = 238;
    r.mac[0] = 0x00; r.mac[1] = 0x14; r.mac[2] = 0x2D;
    r.mac[3] = 0xEF; r.mac[4] = 0x86; r.mac[5] = 0xE2;

    static const uint8_t golden[ME_REG_REQUEST_LEN] = {
        0xDD, 0x01, 0x1C, 0x01, 0x11,
        0x42, 0x54, 0x53, 0x2D, 0x36, 0x30, 0x30, /* "BTS-600" */
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, /* padding */
        0xAC, 0x10, 0x12, 0xEE,                   /* 172.16.18.238 */
        0x00, 0x14, 0x2D, 0xEF, 0x86, 0xE2,       /* MAC */
        0xAD, 0xBE                                /* CRC 0xADBE, high first */
    };

    uint8_t buf[ME_REG_REQUEST_LEN];
    me_reg_pack_request(&r, buf, ME_CRC_ORDER_DEFAULT);

    for (unsigned i = 0; i < ME_REG_REQUEST_LEN; i++) {
        CHECK_BYTE(buf, i, golden[i]);
    }
}

/* ------------------------------------------------ response construction -- */

static void build_response(uint8_t *buf, uint8_t dev, uint8_t ckt,
                           uint8_t value, me_crc_order_t order)
{
    buf[ME_RSP_OFF_START]      = ME_START_REGISTRATION;
    buf[ME_RSP_OFF_QUERY_ID]   = ME_QID_REGISTER;
    buf[ME_RSP_OFF_DEVICE_ID]  = dev;
    buf[ME_RSP_OFF_CIRCUIT_ID] = ckt;
    buf[ME_RSP_OFF_VALUE]      = value;
    me_crc16_append(buf, ME_REG_RESPONSE_BODY_LEN, order);
}

static void test_parse_accepts_success_response(void)
{
    TEST_CASE("parse: Value 0x01 means registered");
    const me_reg_request_t sent = demo_request();
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    const me_reg_parse_result_t r = me_reg_parse_response(
        buf, sizeof(buf), &sent, ME_CRC_ORDER_LE, &rsp);

    CHECK_EQ_U(ME_REG_PARSE_OK, r);
    CHECK(rsp.registered);
    CHECK(!rsp.echo_mismatch);
    CHECK_EQ_U(0x01, rsp.value);
}

static void test_parse_accepts_already_registered(void)
{
    /* The exact defect: server said 0x02, board called it a rejection. */
    TEST_CASE("parse: Value 0x02 (Already Registered) means registered");
    const me_reg_request_t sent = demo_request();
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_ALREADY_REGISTERED,
                   ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    const me_reg_parse_result_t r = me_reg_parse_response(
        buf, sizeof(buf), &sent, ME_CRC_ORDER_LE, &rsp);

    CHECK_EQ_U(ME_REG_PARSE_OK, r);
    CHECK(rsp.registered);
    CHECK(!rsp.echo_mismatch);
    CHECK_EQ_U(ME_REG_VALUE_ALREADY_REGISTERED, rsp.value);
}

/*
 * Golden vector: the exact 7-byte response captured from the Web Application
 * on 2026-08-10, replayed byte for byte.
 *
 *   DD 01 01 11 02 15 BE
 *   |  |  |  |  |  +--+-- CRC-16/Modbus over bytes 0..4 = 0xBE15
 *   |  |  |  |  +-------- Value 0x02, Already Registered
 *   |  |  |  +----------- CircuitID echo 0x11
 *   |  |  +-------------- DeviceID echo 0x01
 *   |  +----------------- QueryID 0x01
 *   +-------------------- Start 0xDD
 *
 * Parsed with an EXPLICIT little-endian order because that is what the server
 * actually sent ("15 BE" is the low byte first). Deliberately NOT
 * ME_CRC_ORDER_DEFAULT: this test replays hardware and must not change meaning
 * if the default order is revisited.
 */
static void test_parse_matches_captured_hardware_response(void)
{
    TEST_CASE("parse: the captured 0x02 response registers the device");

    static const uint8_t golden[ME_REG_RESPONSE_LEN] = {
        0xDD, 0x01, 0x01, 0x11, 0x02, 0x15, 0xBE
    };

    me_reg_request_t sent;
    memset(&sent, 0, sizeof(sent));
    sent.device_id  = 0x01;
    sent.circuit_id = ME_CIRCUIT_ID(1, 1);

    me_reg_response_t rsp;
    const me_reg_parse_result_t r = me_reg_parse_response(
        golden, sizeof(golden), &sent, ME_CRC_ORDER_LE, &rsp);

    CHECK_EQ_U(ME_REG_PARSE_OK, r);
    CHECK(rsp.registered);
    CHECK(!rsp.echo_mismatch);
    CHECK_EQ_U(ME_REG_VALUE_ALREADY_REGISTERED, rsp.value);
    CHECK_EQ_U(0x01, rsp.device_id);
    CHECK_EQ_U(0x11, rsp.circuit_id);
}

static void test_parse_rejects_non_success_values(void)
{
    /* 0x02 deliberately absent: it moved to the success set on 2026-08-10.
     * 0x03 stands in for "unrecognised but well-formed". */
    const uint8_t values[] = { ME_REG_VALUE_FAILED, 0x03, 0xFF };
    const me_reg_request_t sent = demo_request();

    TEST_CASE("parse: 0x00 and unrecognised values are not registered");
    for (unsigned i = 0; i < sizeof(values); i++) {
        uint8_t buf[ME_REG_RESPONSE_LEN];
        build_response(buf, 0x01, 0x11, values[i], ME_CRC_ORDER_LE);

        me_reg_response_t rsp;
        const me_reg_parse_result_t r = me_reg_parse_response(
            buf, sizeof(buf), &sent, ME_CRC_ORDER_LE, &rsp);

        /* Well-formed frame, so parsing succeeds - but not registered. */
        CHECK_EQ_U(ME_REG_PARSE_OK, r);
        CHECK(!rsp.registered);
        CHECK_EQ_U(values[i], rsp.value);
    }
}

static void test_parse_rejects_wrong_start_byte(void)
{
    TEST_CASE("parse: start byte other than 0xDD is rejected");
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);
    buf[ME_RSP_OFF_START] = ME_START_CONFIG;
    me_crc16_append(buf, ME_REG_RESPONSE_BODY_LEN, ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    CHECK_EQ_U(ME_REG_PARSE_BAD_START,
               me_reg_parse_response(buf, sizeof(buf), NULL,
                                     ME_CRC_ORDER_LE, &rsp));
}

static void test_parse_rejects_wrong_query_id(void)
{
    TEST_CASE("parse: QueryID other than 0x01 is rejected");
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);
    buf[ME_RSP_OFF_QUERY_ID] = ME_QID_DELETE;
    me_crc16_append(buf, ME_REG_RESPONSE_BODY_LEN, ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    CHECK_EQ_U(ME_REG_PARSE_BAD_QUERY,
               me_reg_parse_response(buf, sizeof(buf), NULL,
                                     ME_CRC_ORDER_LE, &rsp));
}

static void test_parse_rejects_wrong_length(void)
{
    TEST_CASE("parse: responses that are not exactly 7 bytes are rejected");
    uint8_t buf[ME_REG_RESPONSE_LEN + 1];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    CHECK_EQ_U(ME_REG_PARSE_BAD_LENGTH,
               me_reg_parse_response(buf, 6, NULL, ME_CRC_ORDER_LE, &rsp));
    CHECK_EQ_U(ME_REG_PARSE_BAD_LENGTH,
               me_reg_parse_response(buf, 8, NULL, ME_CRC_ORDER_LE, &rsp));
    CHECK_EQ_U(ME_REG_PARSE_BAD_LENGTH,
               me_reg_parse_response(buf, 0, NULL, ME_CRC_ORDER_LE, &rsp));
}

static void test_parse_rejects_bad_crc(void)
{
    /* Per the design decision: reject and retry, do not honour Value. */
    TEST_CASE("parse: a corrupted CRC is rejected, not honoured");
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);
    buf[ME_REG_RESPONSE_LEN - 1] ^= 0xFF;

    me_reg_response_t rsp;
    CHECK_EQ_U(ME_REG_PARSE_BAD_CRC,
               me_reg_parse_response(buf, sizeof(buf), NULL,
                                     ME_CRC_ORDER_LE, &rsp));
}

static void test_parse_rejects_response_in_wrong_crc_order(void)
{
    TEST_CASE("parse: a BE-built response is rejected when read as LE");
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x11, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_BE);

    me_reg_response_t rsp;
    CHECK_EQ_U(ME_REG_PARSE_BAD_CRC,
               me_reg_parse_response(buf, sizeof(buf), NULL,
                                     ME_CRC_ORDER_LE, &rsp));

    TEST_CASE("parse: the same response is accepted when read as BE");
    CHECK_EQ_U(ME_REG_PARSE_OK,
               me_reg_parse_response(buf, sizeof(buf), NULL,
                                     ME_CRC_ORDER_BE, &rsp));
}

static void test_parse_flags_echo_mismatch_without_failing(void)
{
    /* The reference document shows CktNum 0x01 echoed while this device sends
     * 0x11. Flag it, but never let it block registration. */
    TEST_CASE("parse: echo mismatch is flagged but still registers");
    const me_reg_request_t sent = demo_request(); /* circuit 0x11 */
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x01, 0x01, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    const me_reg_parse_result_t r = me_reg_parse_response(
        buf, sizeof(buf), &sent, ME_CRC_ORDER_LE, &rsp);

    CHECK_EQ_U(ME_REG_PARSE_OK, r);
    CHECK(rsp.registered);
    CHECK(rsp.echo_mismatch);
    CHECK_EQ_U(0x01, rsp.circuit_id);
}

static void test_parse_without_sent_request_reports_no_mismatch(void)
{
    TEST_CASE("parse: NULL sent request means no echo comparison");
    uint8_t buf[ME_REG_RESPONSE_LEN];
    build_response(buf, 0x07, 0x22, ME_REG_VALUE_REGISTERED, ME_CRC_ORDER_LE);

    me_reg_response_t rsp;
    CHECK_EQ_U(ME_REG_PARSE_OK,
               me_reg_parse_response(buf, sizeof(buf), NULL,
                                     ME_CRC_ORDER_LE, &rsp));
    CHECK(!rsp.echo_mismatch);
    CHECK_EQ_U(0x07, rsp.device_id);
    CHECK_EQ_U(0x22, rsp.circuit_id);
}

void run_reg_frame_tests(void)
{
    printf("-- reg_frame --\n");
    test_value_success_predicate();
    test_circuit_id_packs_two_nibbles();
    test_circuit_id_round_trips();
    test_frame_constants_are_self_consistent();
    test_pack_returns_full_frame_length();
    test_pack_writes_header_fields();
    test_pack_writes_device_and_circuit_at_offsets_3_and_4();
    test_pack_writes_ip_in_dotted_quad_order();
    test_pack_writes_mac_in_wire_order();
    test_pack_null_pads_short_name();
    test_pack_fills_exactly_sixteen_char_name();
    test_pack_truncates_overlong_name();
    test_pack_handles_empty_name();
    test_pack_crc_covers_bytes_0_to_30();
    test_pack_honours_big_endian_crc_order();
    test_pack_default_order_stores_crc_high_byte_first();
    test_pack_matches_captured_hardware_frame();
    test_parse_accepts_success_response();
    test_parse_accepts_already_registered();
    test_parse_matches_captured_hardware_response();
    test_parse_rejects_non_success_values();
    test_parse_rejects_wrong_start_byte();
    test_parse_rejects_wrong_query_id();
    test_parse_rejects_wrong_length();
    test_parse_rejects_bad_crc();
    test_parse_rejects_response_in_wrong_crc_order();
    test_parse_flags_echo_mismatch_without_failing();
    test_parse_without_sent_request_reports_no_mismatch();
}
