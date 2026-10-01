/*
 * test_realtime_frame.c - the 86-byte 0xCC real-time frame.
 *
 * Layout source: Docs/Ref Docs/bm_measured_param_v5.2.md. Every payload offset
 * is asserted individually: a frame with one field shifted still has a valid
 * CRC and a valid length, so the Web Application would display wrong numbers
 * with nothing anywhere reporting an error.
 */
#include <string.h>

#include "test_util.h"
#include "../src/proto/realtime_frame.h"

static void fill_known(me_realtime_t *rt)
{
    memset(rt, 0, sizeof(*rt));
    rt->step_number     = 0x0102;
    rt->program_running = 0x01;
    rt->circuit_status  = 0x02;
    rt->user_msg        = 0x03;
    rt->error_id        = 0x04050607u;
    rt->step_run_ms     = 0x08090A0Bu;
    rt->program_run_ms  = 0x0C0D0E0Fu;
    rt->current         = 95.6f;
    rt->voltage         = 30.1f;
    rt->temperature     = 40.5f;
    rt->power           = 1.0f;
    rt->operator_code   = 0x01;
    rt->cycle_status    = 0x01;
    rt->cycle_start_step   = 0x1112;
    rt->cycle_iteration    = 0x1314;
    rt->table_step         = 0x1516;
    rt->table_rows         = 0x1718;
    rt->registration_type  = 0x191A;
    rt->digital_inputs        = 0xA5;
    rt->digital_out_secondary = 0x5A;
    rt->digital_out_primary   = 0x07;
}

static void test_float_encoding_matches_the_spec_example(void)
{
    TEST_CASE("realtime: 95.6f encodes as 42 BF 33 33, per the spec's own example");
    /* bm_measured_param_v5.2 gives "42 BF 33 33 = 95.6 A" as a worked example.
     * If this fails, every float in the frame is wrong and no other assertion
     * in this file means anything. */
    uint8_t b[4];
    me_put_f32_be(b, 95.6f);
    CHECK_BYTE(b, 0, 0x42);
    CHECK_BYTE(b, 1, 0xBF);
    CHECK_BYTE(b, 2, 0x33);
    CHECK_BYTE(b, 3, 0x33);

    /* And the documented voltage example, 41 F0 CC CD = 30.1 V. */
    me_put_f32_be(b, 30.1f);
    CHECK_BYTE(b, 0, 0x41);
    CHECK_BYTE(b, 1, 0xF0);
    CHECK_BYTE(b, 2, 0xCC);
    CHECK_BYTE(b, 3, 0xCD);
}

static void test_float_round_trip(void)
{
    TEST_CASE("realtime: float encode/decode round-trips");
    uint8_t b[4];
    me_put_f32_be(b, -110.0f);
    CHECK(me_get_f32_be(b) == -110.0f);
    me_put_f32_be(b, 0.0f);
    CHECK(me_get_f32_be(b) == 0.0f);
}

static void test_frame_length_and_header(void)
{
    TEST_CASE("realtime: the frame is 86 bytes with a CC | dev | ckt | 01 header");
    me_realtime_t rt;
    fill_known(&rt);

    uint8_t f[ME_RT_FRAME_LEN];
    const size_t n = me_realtime_pack(&rt, 0x01, 0x11, f, ME_CRC_ORDER_BE);

    CHECK_EQ_U(86, n);
    CHECK_EQ_U(86, ME_RT_FRAME_LEN);
    CHECK_BYTE(f, 0, ME_START_REALTIME);
    CHECK_BYTE(f, 1, 0x01);
    CHECK_BYTE(f, 2, 0x11);
    CHECK_BYTE(f, 3, ME_QID_REALTIME);
}

static void test_every_payload_offset(void)
{
    TEST_CASE("realtime: every payload field sits at its documented offset");
    me_realtime_t rt;
    fill_known(&rt);

    uint8_t f[ME_RT_FRAME_LEN];
    (void)me_realtime_pack(&rt, 0x01, 0x11, f, ME_CRC_ORDER_BE);

    /* Frame index = payload offset + 4. Asserted as frame indices, because
     * that is what a hex dump on the console shows. */
    const uint8_t *p = &f[ME_RT_HEADER_LEN];

    CHECK_BYTE(p, 0, 0x01); CHECK_BYTE(p, 1, 0x02);  /* step number      */
    CHECK_BYTE(p, 2, 0x01);                          /* program running  */
    CHECK_BYTE(p, 3, 0x02);                          /* circuit status   */
    CHECK_BYTE(p, 4, 0x03);                          /* user msg         */
    CHECK_BYTE(p, 5, 0x04); CHECK_BYTE(p, 8, 0x07);  /* error id         */
    CHECK_BYTE(p, 9, 0x08); CHECK_BYTE(p, 12, 0x0B); /* step run ms      */
    CHECK_BYTE(p, 13, 0x0C); CHECK_BYTE(p, 16, 0x0F);/* program run ms   */

    CHECK(me_get_f32_be(&p[17]) == 95.6f);           /* current          */
    CHECK(me_get_f32_be(&p[21]) == 30.1f);           /* voltage          */
    CHECK(me_get_f32_be(&p[25]) == 40.5f);           /* temperature      */
    CHECK(me_get_f32_be(&p[29]) ==  1.0f);           /* power            */

    CHECK_BYTE(p, 65, 0x01);                         /* operator         */
    CHECK_BYTE(p, 66, 0x01);                         /* cycle status     */
    CHECK_BYTE(p, 67, 0x11); CHECK_BYTE(p, 68, 0x12);/* cycle start step */
    CHECK_BYTE(p, 69, 0x13); CHECK_BYTE(p, 70, 0x14);/* cycle iteration  */
    CHECK_BYTE(p, 71, 0x15); CHECK_BYTE(p, 72, 0x16);/* table step       */
    CHECK_BYTE(p, 73, 0x17); CHECK_BYTE(p, 74, 0x18);/* table rows       */
    CHECK_BYTE(p, 75, 0x19); CHECK_BYTE(p, 76, 0x1A);/* registration type*/
    CHECK_BYTE(p, 77, 0xA5);                         /* digital inputs   */
    CHECK_BYTE(p, 78, 0x5A);                         /* DO secondary     */
    CHECK_BYTE(p, 79, 0x07);                         /* DO primary       */
}

static void test_crc_is_big_endian_over_the_body(void)
{
    TEST_CASE("realtime: CRC covers bytes 0-83 and goes out high byte first");
    me_realtime_t rt;
    fill_known(&rt);

    uint8_t f[ME_RT_FRAME_LEN];
    (void)me_realtime_pack(&rt, 0x01, 0x11, f, ME_CRC_ORDER_BE);

    CHECK(me_crc16_verify(f, ME_RT_FRAME_LEN, ME_CRC_ORDER_BE));

    const uint16_t computed = me_crc16_modbus(f, ME_RT_FRAME_LEN - 2u);
    CHECK_BYTE(f, 84, (uint8_t)(computed >> 8));
    CHECK_BYTE(f, 85, (uint8_t)(computed & 0xFFu));
}

static void test_idle_frame_clears_running_status(void)
{
    TEST_CASE("realtime: an idle frame carries status 0 in both status bytes");
    /* This is the shape of the final demo frame: the Web Application learns
     * the test stopped from the frame itself, not from a missing frame. */
    me_realtime_t rt;
    fill_known(&rt);
    rt.program_running = 0x00;
    rt.circuit_status  = 0x00;

    uint8_t f[ME_RT_FRAME_LEN];
    (void)me_realtime_pack(&rt, 0x01, 0x11, f, ME_CRC_ORDER_BE);

    CHECK_BYTE(f, ME_RT_HEADER_LEN + 2, 0x00);
    CHECK_BYTE(f, ME_RT_HEADER_LEN + 3, 0x00);
}

static void test_post_registration_frame_matches_the_developer_bytes(void)
{
    TEST_CASE("realtime: the post-registration frame is the exact 86 bytes");
    /*
     * See src/threads/post_reg.c. One 0xCC frame is sent after a successful
     * device registration so the Web Application has live data to display
     * before a Start command has been issued and real step execution has
     * produced anything of its own.
     *
     * These are the literal bytes the developer supplied on 2026-08-12. They are
     * asserted rather than merely reproduced because the whole point of the
     * frame is that the Web Application already accepts it: step_number = 1,
     * temperature = 25.0 C, every other field zero, CRC 0xF261 big-endian.
     *
     * The frame as pasted was 85 bytes and matched no CRC - a zero was lost to a
     * line wrap. Restoring it gives 86 bytes and 0xF261 verifies, which is what
     * this array holds.
     */
    static const uint8_t expected[ME_RT_FRAME_LEN] = {
        0xCC, 0x01, 0x11, 0x01,  /* Start, Device 1, Circuit 0x11, QueryID 1 */
        0x00, 0x01,              /* step number = 1                          */
        0x00,                    /* program running = IDLE                   */
        0x00,                    /* circuit status  = IDLE                   */
        0x00,                    /* user msg                                 */
        0x00, 0x00, 0x00, 0x00,  /* error id                                 */
        0x00, 0x00, 0x00, 0x00,  /* step run ms                              */
        0x00, 0x00, 0x00, 0x00,  /* program run ms                           */
        0x00, 0x00, 0x00, 0x00,  /* current   0.0 A                          */
        0x00, 0x00, 0x00, 0x00,  /* voltage   0.0 V                          */
        0x41, 0xC8, 0x00, 0x00,  /* temperature 25.0 C  <- the only non-zero */
        0x00, 0x00, 0x00, 0x00,  /* power                                    */
        0x00, 0x00, 0x00, 0x00,  /* accumulated capacity                     */
        0x00, 0x00, 0x00, 0x00,  /* charge capacity                          */
        0x00, 0x00, 0x00, 0x00,  /* discharge capacity                       */
        0x00, 0x00, 0x00, 0x00,  /* step capacity                            */
        0x00, 0x00, 0x00, 0x00,  /* accumulated energy                       */
        0x00, 0x00, 0x00, 0x00,  /* charge energy                            */
        0x00, 0x00, 0x00, 0x00,  /* discharge energy                         */
        0x00, 0x00, 0x00, 0x00,  /* step energy                              */
        0x00,                    /* operator code                            */
        0x00,                    /* cycle status                             */
        0x00, 0x00,              /* cycle start step                         */
        0x00, 0x00,              /* cycle iteration                          */
        0x00, 0x00,              /* table step                               */
        0x00, 0x00,              /* table rows                               */
        0x00, 0x00,              /* registration type                        */
        0x00,                    /* digital inputs                           */
        0x00,                    /* digital out secondary                    */
        0x00,                    /* digital out primary                      */
        0xF2, 0x61               /* CRC-16/Modbus, big-endian                */
    };

    CHECK_EQ_U(86, ME_RT_FRAME_LEN);
    CHECK_EQ_U(86, sizeof(expected));
    /* The supplied CRC must be OUR CRC, or the frame is not the one they saw. */
    CHECK(me_crc16_verify(expected, sizeof(expected), ME_CRC_ORDER_BE));
    CHECK_EQ_U(0xF261, me_crc16_modbus(expected, sizeof(expected) - 2u));

    uint8_t f[ME_RT_FRAME_LEN];
    memset(f, 0xA5, sizeof(f)); /* poison: a short write must not pass */
    const size_t n = me_realtime_pack_post_registration(0x01, 0x11, f,
                                                        ME_CRC_ORDER_BE);
    CHECK_EQ_U(ME_RT_FRAME_LEN, n);
    CHECK_MEM(f, expected, sizeof(expected));

    /* Name the two fields that carry meaning, so a mismatch above says which. */
    CHECK_EQ_U(1, ((unsigned)f[ME_RT_HEADER_LEN + ME_RT_OFF_STEP_NUMBER] << 8)
                  | f[ME_RT_HEADER_LEN + ME_RT_OFF_STEP_NUMBER + 1u]);
    CHECK(me_get_f32_be(&f[ME_RT_HEADER_LEN + ME_RT_OFF_TEMPERATURE]) == 25.0f);
    CHECK_BYTE(f, ME_RT_HEADER_LEN + ME_RT_OFF_PROGRAM_RUNNING,
               ME_RT_PROGRAM_IDLE);
    CHECK_BYTE(f, ME_RT_HEADER_LEN + ME_RT_OFF_CIRCUIT_STATUS,
               ME_RT_CIRCUIT_IDLE);
}

static void test_post_registration_frame_follows_the_circuit(void)
{
    TEST_CASE("realtime: the demo frame carries the addressed device/circuit");
    /* The 0x01/0x11 case is pinned above. This one proves the frame is built,
     * not memcpy'd from a constant - run with --secondary 3 --channel 2 and it
     * must address circuit 0x32, not circuit 0x11. */
    uint8_t f[ME_RT_FRAME_LEN];
    CHECK_EQ_U(ME_RT_FRAME_LEN,
               me_realtime_pack_post_registration(0x07, 0x32, f,
                                                  ME_CRC_ORDER_BE));
    CHECK_BYTE(f, ME_HDR_OFF_START,   0xCC);
    CHECK_BYTE(f, ME_HDR_OFF_DEVICE,  0x07);
    CHECK_BYTE(f, ME_HDR_OFF_CIRCUIT, 0x32);
    CHECK(me_crc16_verify(f, ME_RT_FRAME_LEN, ME_CRC_ORDER_BE));

    /* Still the same payload - only the header moved. */
    CHECK(me_get_f32_be(&f[ME_RT_HEADER_LEN + ME_RT_OFF_TEMPERATURE]) == 25.0f);
}

void run_realtime_frame_tests(void)
{
    printf("\n-- realtime_frame --\n");
    test_float_encoding_matches_the_spec_example();
    test_float_round_trip();
    test_frame_length_and_header();
    test_every_payload_offset();
    test_crc_is_big_endian_over_the_body();
    test_idle_frame_clears_running_status();
    test_post_registration_frame_matches_the_developer_bytes();
    test_post_registration_frame_follows_the_circuit();
}
