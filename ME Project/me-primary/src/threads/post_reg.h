/*
 * post_reg.h - the one-shot 0xCC frame sent right after registration.
 *
 * Relocated out of demo_realtime.c/.h, which is deleted in Task 12: this
 * function serves the registration flow, not step execution, and deleting
 * demo_realtime.* would otherwise silently remove a frame the Web
 * Application currently receives after every successful connect.
 */
#ifndef ME_POST_REG_H
#define ME_POST_REG_H

#include <stdint.h>

#include "../proto/crc16.h"

/* Must be called once at startup before me_post_reg_send(). */
void me_post_reg_init(uint8_t device_id, me_crc_order_t crc_order);

/*
 * Sends ONE 0xCC frame for a circuit that has just registered successfully.
 * Contents are fixed - step 1, temperature 25.0 C, everything else zero,
 * both status bytes Idle - built by me_realtime_pack_post_registration(),
 * where the exact bytes are unit-tested (test_realtime_frame.c).
 *
 * Called from the COMMUNICATION thread on every successful registration.
 */
void me_post_reg_send(uint8_t circuit_id);

#endif /* ME_POST_REG_H */
