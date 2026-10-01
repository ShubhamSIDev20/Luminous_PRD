/* can_frame.c - see can_frame.h for the format source. */
#include "can_frame.h"

#include <string.h>

me_can_block_t me_can_block_for_channel(uint8_t channel_num, uint8_t *slot_out)
{
    const uint8_t zero_based = (uint8_t)(channel_num - 1u);
    *slot_out = (uint8_t)(zero_based % ME_CAN_CHANNELS_PER_BLOCK);
    return (channel_num <= ME_CAN_CHANNELS_PER_BLOCK) ? ME_CAN_BLOCK_1 : ME_CAN_BLOCK_2;
}

uint16_t me_can_id(uint8_t circuit_num6, uint8_t function5)
{
    return (uint16_t)(((uint16_t)(circuit_num6 & 0x3Fu) << 5) | (function5 & 0x1Fu));
}

static void put_f32_le(uint8_t *dst, float v)
{
    uint32_t bits;
    memcpy(&bits, &v, sizeof(bits));
    dst[0] = (uint8_t)(bits);
    dst[1] = (uint8_t)(bits >> 8);
    dst[2] = (uint8_t)(bits >> 16);
    dst[3] = (uint8_t)(bits >> 24);
}

static float get_f32_le(const uint8_t *src)
{
    const uint32_t bits = (uint32_t)src[0] | ((uint32_t)src[1] << 8)
                        | ((uint32_t)src[2] << 16) | ((uint32_t)src[3] << 24);
    float v;
    memcpy(&v, &bits, sizeof(v));
    return v;
}

void me_can_pack_set(const me_can_setpoint_t *sp, uint8_t out64[ME_CAN_FRAME_LEN])
{
    memset(out64, 0, ME_CAN_FRAME_LEN);

    uint8_t slot;
    (void)me_can_block_for_channel(sp->channel_num, &slot);
    uint8_t *p = &out64[slot * ME_CAN_SLOT_LEN];

    put_f32_le(&p[0], sp->set_voltage);
    put_f32_le(&p[4], sp->set_current);
    p[8] = sp->command;
    p[9] = sp->channel_num;
    /* +10 EEP Para# (Normal Mode = 0), +11 Reserved, +12..15 Data - all
     * zero this iteration; nothing reads or writes an internal parameter. */
}

void me_can_merge_slot(uint8_t block64[ME_CAN_FRAME_LEN],
                       const uint8_t incoming64[ME_CAN_FRAME_LEN],
                       uint8_t channel_num)
{
    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    const size_t off = (size_t)slot * ME_CAN_SLOT_LEN;
    memcpy(&block64[off], &incoming64[off], ME_CAN_SLOT_LEN);
}

void me_can_pack_read(uint8_t channel_num, uint8_t out64[ME_CAN_FRAME_LEN])
{
    memset(out64, 0, ME_CAN_FRAME_LEN);

    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    uint8_t *p = &out64[slot * ME_CAN_SLOT_LEN];

    p[9] = channel_num;
}

void me_can_pack_feedback(uint8_t channel_num, const me_can_feedback_t *fb,
                          uint8_t out64[ME_CAN_FRAME_LEN])
{
    memset(out64, 0, ME_CAN_FRAME_LEN);

    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    uint8_t *p = &out64[slot * ME_CAN_SLOT_LEN];

    put_f32_le(&p[0], fb->feedback_voltage);
    put_f32_le(&p[4], fb->feedback_current);
    p[8] = fb->state;
    p[9] = channel_num;
}

bool me_can_parse_feedback(const uint8_t frame64[ME_CAN_FRAME_LEN],
                           uint8_t channel_num, me_can_feedback_t *out)
{
    if (channel_num < 1u || channel_num > 8u) { return false; }

    uint8_t slot;
    (void)me_can_block_for_channel(channel_num, &slot);
    const uint8_t *p = &frame64[slot * ME_CAN_SLOT_LEN];

    out->feedback_voltage = get_f32_le(&p[0]);
    out->feedback_current = get_f32_le(&p[4]);
    out->state             = p[8];
    return true;
}
