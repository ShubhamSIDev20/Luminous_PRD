/* post_reg.c - see post_reg.h. */
#include "post_reg.h"

#include <string.h>

#include "../app_queues.h"
#include "../proto/realtime_frame.h"
#include "../store/circuit_store.h"
#include "../util/log.h"

static uint8_t        s_device_id = 0x01;
static me_crc_order_t s_crc_order = ME_CRC_ORDER_DEFAULT;
static me_msg_t       s_tx; /* owned by the Communication thread only */

void me_post_reg_init(uint8_t device_id, me_crc_order_t crc_order)
{
    s_device_id = device_id;
    s_crc_order = crc_order;
}

void me_post_reg_send(uint8_t circuit_id)
{
    if (me_circuit_slot(circuit_id) == ME_SLOT_INVALID) {
        ME_LOGW("post_reg: frame skipped - CircuitID 0x%02X is not a valid "
                "secondary(1-8)/channel(1-8) pair", circuit_id);
        return;
    }

    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_REALTIME_DATA;
    s_tx.circuit_id = circuit_id;
    s_tx.len        = (uint32_t)me_realtime_pack_post_registration(
        s_device_id, circuit_id, s_tx.payload, s_crc_order);

    if (!me_msgq_send(&g_q_comm, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
        ME_LOGW("post_reg: circuit 0x%02X post-registration frame dropped - "
                "comm queue full", circuit_id);
        return;
    }

    ME_LOGI("post_reg: circuit 0x%02X - sent one %u-byte 0xCC frame to UDP "
            "%u after registration (step %u, %.1f C, all else zero)",
            circuit_id, (unsigned)s_tx.len, (unsigned)ME_PORT_UDP_LIVE,
            (unsigned)ME_RT_POST_REG_STEP_NUMBER,
            (double)ME_RT_POST_REG_TEMPERATURE);
}
