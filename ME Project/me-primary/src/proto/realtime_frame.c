/*
 * realtime_frame.c - build the 0xCC measured-parameter frame.
 */
#include "realtime_frame.h"

#include <string.h>

static void put_u16_be(uint8_t *dst, uint16_t v)
{
    dst[0] = (uint8_t)(v >> 8);
    dst[1] = (uint8_t)(v & 0xFFu);
}

static void put_u32_be(uint8_t *dst, uint32_t v)
{
    dst[0] = (uint8_t)(v >> 24);
    dst[1] = (uint8_t)(v >> 16);
    dst[2] = (uint8_t)(v >> 8);
    dst[3] = (uint8_t)(v & 0xFFu);
}

/*
 * memcpy through a uint32_t rather than a pointer cast: type-punning a float*
 * to a uint32_t* is undefined behaviour and -O2 is entitled to miscompile it.
 * The memcpy compiles to the same single move.
 */
void me_put_f32_be(uint8_t *dst, float v)
{
    uint32_t bits;
    memcpy(&bits, &v, sizeof(bits));
    put_u32_be(dst, bits);
}

float me_get_f32_be(const uint8_t *src)
{
    const uint32_t bits = ((uint32_t)src[0] << 24) | ((uint32_t)src[1] << 16)
                        | ((uint32_t)src[2] << 8)  | (uint32_t)src[3];
    float v;
    memcpy(&v, &bits, sizeof(v));
    return v;
}

size_t me_realtime_pack(const me_realtime_t *rt, uint8_t device_id,
                        uint8_t circuit_id, uint8_t *out, me_crc_order_t order)
{
    memset(out, 0, ME_RT_FRAME_LEN);

    out[ME_HDR_OFF_START]    = ME_START_REALTIME;
    out[ME_HDR_OFF_DEVICE]   = device_id;
    out[ME_HDR_OFF_CIRCUIT]  = circuit_id;
    out[ME_HDR_OFF_QUERY_ID] = ME_QID_REALTIME;

    uint8_t *p = &out[ME_RT_HEADER_LEN];

    put_u16_be(&p[ME_RT_OFF_STEP_NUMBER], rt->step_number);
    p[ME_RT_OFF_PROGRAM_RUNNING] = rt->program_running;
    p[ME_RT_OFF_CIRCUIT_STATUS]  = rt->circuit_status;
    p[ME_RT_OFF_USER_MSG]        = rt->user_msg;
    put_u32_be(&p[ME_RT_OFF_ERROR_ID],       rt->error_id);
    put_u32_be(&p[ME_RT_OFF_STEP_RUN_MS],    rt->step_run_ms);
    put_u32_be(&p[ME_RT_OFF_PROGRAM_RUN_MS], rt->program_run_ms);

    me_put_f32_be(&p[ME_RT_OFF_CURRENT],        rt->current);
    me_put_f32_be(&p[ME_RT_OFF_VOLTAGE],        rt->voltage);
    me_put_f32_be(&p[ME_RT_OFF_TEMPERATURE],    rt->temperature);
    me_put_f32_be(&p[ME_RT_OFF_POWER],          rt->power);
    me_put_f32_be(&p[ME_RT_OFF_ACCUM_CAPACITY], rt->accum_capacity);
    me_put_f32_be(&p[ME_RT_OFF_CHG_CAPACITY],   rt->charge_capacity);
    me_put_f32_be(&p[ME_RT_OFF_DIS_CAPACITY],   rt->discharge_capacity);
    me_put_f32_be(&p[ME_RT_OFF_STEP_CAPACITY],  rt->step_capacity);
    me_put_f32_be(&p[ME_RT_OFF_ACCUM_ENERGY],   rt->accum_energy);
    me_put_f32_be(&p[ME_RT_OFF_CHG_ENERGY],     rt->charge_energy);
    me_put_f32_be(&p[ME_RT_OFF_DIS_ENERGY],     rt->discharge_energy);
    me_put_f32_be(&p[ME_RT_OFF_STEP_ENERGY],    rt->step_energy);

    p[ME_RT_OFF_OPERATOR]     = rt->operator_code;
    p[ME_RT_OFF_CYCLE_STATUS] = rt->cycle_status;
    put_u16_be(&p[ME_RT_OFF_CYCLE_START], rt->cycle_start_step);
    put_u16_be(&p[ME_RT_OFF_CYCLE_ITER],  rt->cycle_iteration);
    put_u16_be(&p[ME_RT_OFF_TABLE_STEP],  rt->table_step);
    put_u16_be(&p[ME_RT_OFF_TABLE_ROWS],  rt->table_rows);
    put_u16_be(&p[ME_RT_OFF_REG_TYPE],    rt->registration_type);

    p[ME_RT_OFF_DIGITAL_IN]      = rt->digital_inputs;
    p[ME_RT_OFF_DIGITAL_OUT_SEC] = rt->digital_out_secondary;
    p[ME_RT_OFF_DIGITAL_OUT_PRI] = rt->digital_out_primary;

    me_crc16_append(out, ME_RT_FRAME_LEN - ME_REG_CRC_LEN, order);
    return ME_RT_FRAME_LEN;
}

size_t me_realtime_pack_post_registration(uint8_t device_id, uint8_t circuit_id,
                                          uint8_t *out, me_crc_order_t order)
{
    /*
     * TEMPORARY - see the header. Built from me_realtime_t rather than copied
     * from a byte array so the DeviceID and CircuitID follow whatever
     * --secondary/--channel the board was started with. For 0x01/0x11 the output
     * is byte-identical to the frame the Web Application is known to accept.
     *
     * Both status bytes stay Idle: nothing is running yet at registration time,
     * and claiming ME_RT_PROGRAM_RUNNING here would have the Web Application
     * display a test in progress that does not exist.
     */
    me_realtime_t rt;
    memset(&rt, 0, sizeof(rt));

    rt.step_number     = ME_RT_POST_REG_STEP_NUMBER;
    rt.temperature     = ME_RT_POST_REG_TEMPERATURE;
    rt.program_running = ME_RT_PROGRAM_IDLE;
    rt.circuit_status  = ME_RT_CIRCUIT_IDLE;

    return me_realtime_pack(&rt, device_id, circuit_id, out, order);
}
