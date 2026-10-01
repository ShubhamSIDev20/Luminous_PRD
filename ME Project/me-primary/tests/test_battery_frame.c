/*
 * test_battery_frame.c - 0xAA Q5 battery-information payload parsing.
 *
 * Layout source: Ref Docs/bm_config_v6.0.md section 5.3, transcribed from
 * "Config Data Frame Format V6.0.xlsx" sheet "Battery Data" on 2026-08-12.
 *
 * Section 5.4 of that document confirms the layout against a frame actually
 * captured from the ME Web Application, and test_captured_web_app_frame() below
 * asserts those literal bytes. That is the strongest evidence in this suite:
 * the specification and the wire agree, field boundary for field boundary.
 *
 * Every offset is asserted individually. A payload with one field shifted still
 * has a valid length and a valid CRC, so the board would store wrong battery
 * parameters and run a test against them with nothing reporting an error.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/battery_frame.h"
#include "../src/proto/crc16.h"
#include "../src/proto/realtime_frame.h" /* me_put_f32_be */

/* Distinct value per field, so a shifted offset reads a recognisably wrong
 * number rather than a plausible one. */
static void build_payload(uint8_t *p)
{
    memset(p, 0, ME_BATTERY_PAYLOAD_LEN);
    me_put_f32_be(&p[ME_BAT_OFF_NOM_CAPACITY],   100.0f); /* Ah    */
    p[ME_BAT_OFF_NO_OF_CELLS]     = 12;                   /*       */
    me_put_f32_be(&p[ME_BAT_OFF_GASSING_VOLTAGE], 13.8f); /* V     */
    me_put_f32_be(&p[ME_BAT_OFF_MAX_VOLTAGE],     14.4f); /* V     */
    me_put_f32_be(&p[ME_BAT_OFF_NOM_CURRENT],     20.0f); /* A     */
    me_put_f32_be(&p[ME_BAT_OFF_COLD_CRANKING],  550.0f); /* A     */
    p[ME_BAT_OFF_CHARGE_FACTOR]   = 110;                  /* %     */
    me_put_f32_be(&p[ME_BAT_OFF_IMPEDANCE],       42.5f); /* Ohm   */
    me_put_f32_be(&p[ME_BAT_OFF_BREAK_VOLTAGE],   10.5f); /* V     */
    me_put_f32_be(&p[ME_BAT_OFF_NOM_VOLTAGE],     12.0f); /* V     */
    me_put_f32_be(&p[ME_BAT_OFF_ENERGY_DENSITY], 175.0f); /* Wh/Kg */
    p[ME_BAT_OFF_BATTERY_ID]      = 0x12;                 /* u16   */
    p[ME_BAT_OFF_BATTERY_ID + 1u] = 0x34;
}

static void test_payload_length_is_forty(void)
{
    TEST_CASE("battery: the Q5 payload is 40 bytes and the frame is 46");
    /*
     * 4 + 1 + 4 + 4 + 4 + 4 + 1 + 4 + 4 + 4 + 4 + 2 = 40.
     *
     * This was 22 until 2026-08-12, because the legacy layout in
     * BTS_Primary_SOM stopped after Charge Factor. The parser tolerated the
     * longer payload and silently discarded the last 18 bytes.
     */
    CHECK_EQ_U(40, ME_BATTERY_PAYLOAD_LEN);
    CHECK_EQ_U(46, ME_BATTERY_FRAME_LEN);
    CHECK_EQ_U(ME_HDR_LEN + ME_BATTERY_PAYLOAD_LEN + ME_REG_CRC_LEN,
               ME_BATTERY_FRAME_LEN);

    /* Offsets must tile the payload exactly - no gap, no overlap. */
    CHECK_EQ_U(0,  ME_BAT_OFF_NOM_CAPACITY);
    CHECK_EQ_U(4,  ME_BAT_OFF_NO_OF_CELLS);
    CHECK_EQ_U(5,  ME_BAT_OFF_GASSING_VOLTAGE);
    CHECK_EQ_U(9,  ME_BAT_OFF_MAX_VOLTAGE);
    CHECK_EQ_U(13, ME_BAT_OFF_NOM_CURRENT);
    CHECK_EQ_U(17, ME_BAT_OFF_COLD_CRANKING);
    CHECK_EQ_U(21, ME_BAT_OFF_CHARGE_FACTOR);
    CHECK_EQ_U(22, ME_BAT_OFF_IMPEDANCE);
    CHECK_EQ_U(26, ME_BAT_OFF_BREAK_VOLTAGE);
    CHECK_EQ_U(30, ME_BAT_OFF_NOM_VOLTAGE);
    CHECK_EQ_U(34, ME_BAT_OFF_ENERGY_DENSITY);
    CHECK_EQ_U(38, ME_BAT_OFF_BATTERY_ID);
}

static void test_every_field_offset(void)
{
    TEST_CASE("battery: every field is read from its own documented offset");
    uint8_t p[ME_BATTERY_PAYLOAD_LEN];
    build_payload(p);

    me_battery_t b;
    CHECK(me_battery_parse(p, sizeof(p), &b));
    CHECK(b.valid);
    CHECK(b.nom_capacity          == 100.0f);
    CHECK_EQ_U(12, b.no_of_cells);
    CHECK(b.gassing_voltage       ==  13.8f);
    CHECK(b.max_voltage           ==  14.4f);
    CHECK(b.nom_current           ==  20.0f);
    CHECK(b.cold_cranking_current == 550.0f);
    CHECK_EQ_U(110, b.charge_factor);
    /* The five fields the legacy 22-byte layout stopped short of. */
    CHECK(b.impedance             ==  42.5f);
    CHECK(b.break_voltage         ==  10.5f);
    CHECK(b.nom_voltage           ==  12.0f);
    CHECK(b.energy_density        == 175.0f);
    CHECK_EQ_U(0x1234, b.battery_id);
}

static void test_fields_are_independent(void)
{
    TEST_CASE("battery: changing one field moves only that field");
    /* A shifted offset usually still parses - it just reads its neighbour.
     * Perturbing one field at a time is what catches that. */
    uint8_t p[ME_BATTERY_PAYLOAD_LEN];
    build_payload(p);
    me_put_f32_be(&p[ME_BAT_OFF_NOM_CURRENT], 999.0f);

    me_battery_t b;
    CHECK(me_battery_parse(p, sizeof(p), &b));
    CHECK(b.nom_current           == 999.0f);
    CHECK(b.max_voltage           ==  14.4f); /* neighbour before */
    CHECK(b.cold_cranking_current == 550.0f); /* neighbour after  */

    /* Same again in the newly-added tail, where an off-by-four is most likely
     * because Impedance follows the 1-byte Charge Factor. */
    build_payload(p);
    me_put_f32_be(&p[ME_BAT_OFF_NOM_VOLTAGE], 777.0f);
    CHECK(me_battery_parse(p, sizeof(p), &b));
    CHECK(b.nom_voltage    == 777.0f);
    CHECK(b.break_voltage  ==  10.5f); /* neighbour before */
    CHECK(b.energy_density == 175.0f); /* neighbour after  */
    CHECK_EQ_U(110, b.charge_factor);  /* the 1-byte field before the tail */
}

static void test_short_payload_is_refused(void)
{
    TEST_CASE("battery: a payload one byte short is refused, not read past");
    uint8_t p[ME_BATTERY_PAYLOAD_LEN];
    build_payload(p);

    me_battery_t b;
    memset(&b, 0xFF, sizeof(b));
    CHECK(!me_battery_parse(p, ME_BATTERY_PAYLOAD_LEN - 1u, &b));
    CHECK(!b.valid);
    CHECK(!me_battery_parse(p, 0, &b));
}

static void test_legacy_twentytwo_byte_payload_is_refused(void)
{
    TEST_CASE("battery: the legacy 22-byte payload is refused, not half-parsed");
    /*
     * Developer decision, 2026-08-12: require the full 40. A 22-byte payload
     * would leave Impedance, Break Voltage, Nominal Voltage, Energy Density and
     * Battery ID at zero while b.valid said the record was good - and a battery
     * test would then run against a zeroed nominal voltage.
     *
     * Failing loudly is the point. If the Web Application ever sends the short
     * form, this refusal plus the WARN in data_mgr.c names it immediately
     * instead of producing a plausible-looking record.
     */
    uint8_t p[ME_BATTERY_PAYLOAD_LEN];
    build_payload(p);

    me_battery_t b;
    memset(&b, 0xFF, sizeof(b));
    CHECK(!me_battery_parse(p, 22u, &b));
    CHECK(!b.valid);
}

static void test_longer_payload_is_accepted(void)
{
    TEST_CASE("battery: a payload longer than 40 parses; extra bytes ignored");
    /* A future protocol revision may append fields. Refusing extra bytes would
     * break on that revision for no benefit - the 40 we know are all present. */
    uint8_t p[ME_BATTERY_PAYLOAD_LEN + 8u];
    memset(p, 0xAB, sizeof(p));
    build_payload(p);

    me_battery_t b;
    CHECK(me_battery_parse(p, sizeof(p), &b));
    CHECK(b.valid);
    CHECK_EQ_U(12, b.no_of_cells);
    CHECK_EQ_U(0x1234, b.battery_id); /* the last known field still lands */
}

static void test_captured_web_app_frame(void)
{
    TEST_CASE("battery: the frame captured from the Web Application decodes");
    /*
     * Literal bytes received by the board on 2026-08-12 for Secondary 1 /
     * Channel 1, recorded in bm_config_v6.0.md section 5.4. The operator had
     * entered 1 in every field and 2 for energy density, so the values are test
     * data - the BOUNDARIES are what this locks in.
     *
     * This is the assertion that proves the 40-byte layout is not merely a
     * faithful transcription of a spreadsheet: it is what the Web Application
     * actually sends.
     */
    static const uint8_t frame[] = {
        0xAA, 0x01, 0x11, 0x05,             /* Start, Device 1, Circuit 0x11, Q5 */
        0x3F, 0x80, 0x00, 0x00,             /* nominal capacity      1.0 Ah   */
        0x01,                               /* number of cells       1        */
        0x3F, 0x80, 0x00, 0x00,             /* gassing voltage       1.0 V    */
        0x3F, 0x80, 0x00, 0x00,             /* maximum voltage       1.0 V    */
        0x3F, 0x80, 0x00, 0x00,             /* nominal current       1.0 A    */
        0x3F, 0x80, 0x00, 0x00,             /* cold cranking current 1.0 A    */
        0x01,                               /* charge factor         1 %      */
        0x3F, 0x80, 0x00, 0x00,             /* impedance             1.0 Ohm  */
        0x3F, 0x80, 0x00, 0x00,             /* break voltage         1.0 V    */
        0x3F, 0x80, 0x00, 0x00,             /* nominal voltage       1.0 V    */
        0x40, 0x00, 0x00, 0x00,             /* energy density        2.0 Wh/Kg*/
        0x00, 0x01,                         /* battery ID            1        */
        0x98, 0xE8                          /* CRC-16/Modbus, big-endian      */
    };

    CHECK_EQ_U(ME_BATTERY_FRAME_LEN, sizeof(frame));
    CHECK_EQ_U(46, sizeof(frame));

    /* The CRC the board must agree with, or none of the above matters. */
    CHECK(me_crc16_verify(frame, sizeof(frame), ME_CRC_ORDER_BE));
    CHECK_EQ_U(0x98E8, me_crc16_modbus(frame, sizeof(frame) - ME_REG_CRC_LEN));

    CHECK_BYTE(frame, ME_HDR_OFF_START,    ME_START_CONFIG);
    CHECK_BYTE(frame, ME_HDR_OFF_CIRCUIT,  0x11);
    CHECK_BYTE(frame, ME_HDR_OFF_QUERY_ID, ME_QID_CFG_WRITE_BATTERY);

    me_battery_t b;
    CHECK(me_battery_parse(&frame[ME_HDR_LEN],
                           (uint32_t)(sizeof(frame) - ME_HDR_LEN
                                      - ME_REG_CRC_LEN), &b));
    CHECK(b.valid);
    CHECK(b.nom_capacity          == 1.0f);
    CHECK_EQ_U(1, b.no_of_cells);
    CHECK(b.gassing_voltage       == 1.0f);
    CHECK(b.max_voltage           == 1.0f);
    CHECK(b.nom_current           == 1.0f);
    CHECK(b.cold_cranking_current == 1.0f);
    CHECK_EQ_U(1, b.charge_factor);
    CHECK(b.impedance             == 1.0f);
    CHECK(b.break_voltage         == 1.0f);
    CHECK(b.nom_voltage           == 1.0f);
    CHECK(b.energy_density        == 2.0f); /* the one field that differs */
    CHECK_EQ_U(1, b.battery_id);
}

static void test_impedance_and_energy_density_are_floats(void)
{
    TEST_CASE("battery: impedance and energy density decode as float, not u32");
    /*
     * bm_config_v6.0.md section 9.3: these are the only two 4-byte battery
     * fields the source spreadsheet does NOT annotate "It will be in float",
     * and its samples (00 00 00 64 labelled "100 Ohm") decode sensibly only as
     * uint32. Observed traffic settled it - the Web Application sent
     * 3F 80 00 00 for a field the operator set to 1 - and the developer
     * CONFIRMED float on 2026-08-12. The spreadsheet samples are wrong.
     *
     * Kept as a regression pin, not an open question: the width is 4 bytes
     * either way, so reverting to uint32 would produce a nonsense value and
     * never a parse error. This test is what makes that loud.
     */
    uint8_t p[ME_BATTERY_PAYLOAD_LEN];
    build_payload(p);

    /* 0x00000064 - uint32 100, but 1.4e-43 as a float. */
    p[ME_BAT_OFF_IMPEDANCE]      = 0x00; p[ME_BAT_OFF_IMPEDANCE + 1u]      = 0x00;
    p[ME_BAT_OFF_IMPEDANCE + 2u] = 0x00; p[ME_BAT_OFF_IMPEDANCE + 3u]      = 0x64;

    me_battery_t b;
    CHECK(me_battery_parse(p, sizeof(p), &b));
    CHECK(b.impedance != 100.0f);  /* would hold if it were read as uint32 */
    CHECK(b.impedance  <   1e-40f);
    CHECK(b.impedance  >   0.0f);
}

void run_battery_frame_tests(void)
{
    printf("\n-- battery_frame --\n");
    test_payload_length_is_forty();
    test_every_field_offset();
    test_fields_are_independent();
    test_short_payload_is_refused();
    test_legacy_twentytwo_byte_payload_is_refused();
    test_longer_payload_is_accepted();
    test_captured_web_app_frame();
    test_impedance_and_energy_density_are_floats();
}
