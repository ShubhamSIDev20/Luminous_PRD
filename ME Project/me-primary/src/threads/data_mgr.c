/*
 * data_mgr.c - the Data Manager thread.
 */
#include "data_mgr.h"

#include <pthread.h>
#include <string.h>

#include "../app_queues.h"
#include "../proto/battery_frame.h"
#include "../store/circuit_store.h"
#include "../util/log.h"

/* Single-threaded within this file, so file-scope buffers are safe and keep
 * 64 KB structures off the thread stack. */
static me_msg_t s_rx;
static me_msg_t s_tx;

static pthread_t s_thread;
static bool      s_started = false;

/* ------------------------------------------------------- session ring --- */

typedef struct {
    uint8_t  circuit_id;
    uint32_t len;
    uint8_t  data[ME_SESSION_FRAME_MAX];
} session_rec_t;

static session_rec_t s_ring[ME_SESSION_RING_DEPTH];
static unsigned      s_ring_head;
static unsigned      s_ring_tail;
static unsigned      s_ring_count;
static unsigned long s_ring_dropped;

static void ring_push(uint8_t circuit_id, const uint8_t *data, uint32_t len)
{
    if (len > ME_SESSION_FRAME_MAX) {
        ME_LOGW("data: session frame of %u bytes exceeds %u, discarded",
                (unsigned)len, (unsigned)ME_SESSION_FRAME_MAX);
        return;
    }

    if (s_ring_count == ME_SESSION_RING_DEPTH) {
        /* Drop the OLDEST. Session data is a running log: losing the oldest
         * record is strictly better than refusing the newest. */
        s_ring_head = (s_ring_head + 1u) % ME_SESSION_RING_DEPTH;
        s_ring_count--;
        s_ring_dropped++;
        ME_LOGW("data: session ring full, oldest record dropped (%lu total)",
                s_ring_dropped);
    }

    session_rec_t *r = &s_ring[s_ring_tail];
    r->circuit_id = circuit_id;
    r->len        = len;
    memcpy(r->data, data, len);

    s_ring_tail = (s_ring_tail + 1u) % ME_SESSION_RING_DEPTH;
    s_ring_count++;
}

/* Forward as many ringed records as the communication queue will take. */
static void ring_flush(void)
{
    while (s_ring_count > 0u) {
        const session_rec_t *r = &s_ring[s_ring_head];

        memset(&s_tx, 0, sizeof(s_tx));
        s_tx.type       = ME_MSG_SESSION_DATA;
        s_tx.circuit_id = r->circuit_id;
        s_tx.len        = r->len;
        memcpy(s_tx.payload, r->data, r->len);

        if (!me_msgq_send(&g_q_comm, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
            /* Leave it in the ring and try again next iteration - unlike
             * real-time data, session records are worth retrying. */
            return;
        }
        s_ring_head = (s_ring_head + 1u) % ME_SESSION_RING_DEPTH;
        s_ring_count--;
    }
}

/* ---------------------------------------------------------- responses --- */

static void reply_not_found(uint8_t circuit_id, me_msg_type_t what,
                            me_msg_status_t status)
{
    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_RSP_NOT_FOUND;
    s_tx.circuit_id = circuit_id;
    s_tx.status     = (uint16_t)status;
    /* offset carries which request failed, so Core Logic can tell a missing
     * program from a missing battery record. */
    s_tx.offset     = (uint32_t)what;

    (void)me_msgq_send(&g_q_core, &s_tx, ME_SEND_TIMEOUT_CTRL_MS);
    ME_LOGW("data: circuit 0x%02X - %s -> %s", circuit_id,
            me_msg_type_name(what), me_msg_status_name(status));
}

static void serve_program(uint8_t circuit_id)
{
    if (me_circuit_slot(circuit_id) == ME_SLOT_INVALID) {
        reply_not_found(circuit_id, ME_MSG_REQ_PROGRAM, ME_STATUS_BAD_CIRCUIT);
        return;
    }

    const uint32_t len = me_store_program_len(circuit_id);
    if (len == 0u) {
        reply_not_found(circuit_id, ME_MSG_REQ_PROGRAM, ME_STATUS_NO_PROGRAM);
        return;
    }
    if (!me_store_program_is_complete(circuit_id)) {
        /* No terminator seen. Either the Web Application is still sending, or
         * it never set nextIndex = 0xFFFFFFFF on the final step. The log names
         * both possibilities so the cause is obvious from the console. */
        ME_LOGE("data: circuit 0x%02X - %u bytes stored but no chain "
                "terminator (0xFFFFFFFF) yet: either the transfer is "
                "incomplete, or the final step is missing its terminator",
                circuit_id, (unsigned)len);
        reply_not_found(circuit_id, ME_MSG_REQ_PROGRAM,
                        ME_STATUS_PROGRAM_INCOMPLETE);
        return;
    }

    const uint8_t *p = me_store_program_ptr(circuit_id);
    ME_LOGI("data: circuit 0x%02X - serving %u-byte program in chunks of %u",
            circuit_id, (unsigned)len, (unsigned)ME_MSG_PAYLOAD_MAX);

    uint32_t off = 0;
    while (off < len) {
        const uint32_t chunk = ((len - off) > ME_MSG_PAYLOAD_MAX)
                               ? ME_MSG_PAYLOAD_MAX : (len - off);

        memset(&s_tx, 0, sizeof(s_tx));
        s_tx.type       = ME_MSG_RSP_PROGRAM_CHUNK;
        s_tx.circuit_id = circuit_id;
        s_tx.offset     = off;
        s_tx.len        = chunk;
        s_tx.flags      = ((off + chunk) >= len) ? ME_MSG_FLAG_LAST : 0u;
        memcpy(s_tx.payload, &p[off], chunk);

        if (!me_msgq_send(&g_q_core, &s_tx, ME_SEND_TIMEOUT_BULK_MS)) {
            /* Core Logic checks every chunk's offset, so an abandoned transfer
             * is detected there rather than silently assembling a hole. */
            ME_LOGE("data: circuit 0x%02X - program transfer abandoned at "
                    "offset %u of %u", circuit_id, (unsigned)off, (unsigned)len);
            return;
        }
        off += chunk;
    }
}

static void serve_battery(uint8_t circuit_id)
{
    me_battery_t b;
    if (!me_store_battery_get(circuit_id, &b)) {
        reply_not_found(circuit_id, ME_MSG_REQ_BATTERY,
                        (me_circuit_slot(circuit_id) == ME_SLOT_INVALID)
                            ? ME_STATUS_BAD_CIRCUIT : ME_STATUS_NO_BATTERY);
        return;
    }

    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_RSP_BATTERY;
    s_tx.circuit_id = circuit_id;
    s_tx.len        = (uint32_t)sizeof(b);
    memcpy(s_tx.payload, &b, sizeof(b));

    (void)me_msgq_send(&g_q_core, &s_tx, ME_SEND_TIMEOUT_CTRL_MS);
}

/* ------------------------------------------------------------ dispatch -- */

static void handle(const me_msg_t *m)
{
    switch (m->type) {
    case ME_MSG_STORE_PROGRAM:
        if (me_store_program_append(m->circuit_id, m->payload, m->len)) {
            ME_LOGI("data: circuit 0x%02X - stored %u program byte(s), "
                    "%u total%s",
                    m->circuit_id, (unsigned)m->len,
                    (unsigned)me_store_program_len(m->circuit_id),
                    me_store_program_is_complete(m->circuit_id)
                        ? " [COMPLETE]" : "");
        }
        break;

    case ME_MSG_STORE_BATTERY: {
        me_battery_t b;
        if (!me_battery_parse(m->payload, m->len, &b)) {
            /*
             * Names the document, because the most likely cause is a Web
             * Application still sending the legacy 22-byte payload. Refusing is
             * deliberate (developer decision, 2026-08-12): a half-populated
             * record with valid = true would let a battery test run against a
             * zeroed nominal voltage. route_frame() answers Q5 with Value 0x00.
             */
            ME_LOGW("data: circuit 0x%02X - battery payload of %u bytes is "
                    "shorter than the %u that bm_config_v6.0.md Q5 defines; "
                    "REJECTED, nothing stored",
                    m->circuit_id, (unsigned)m->len,
                    (unsigned)ME_BATTERY_PAYLOAD_LEN);
            break;
        }
        if (m->len > ME_BATTERY_PAYLOAD_LEN) {
            /* Forward-compatible, but say so - it means the protocol moved. */
            ME_LOGW("data: circuit 0x%02X - battery payload is %u bytes, %u more "
                    "than the %u this build knows; the extra bytes are ignored "
                    "and bm_config_v6.0.md needs revisiting",
                    m->circuit_id, (unsigned)m->len,
                    (unsigned)(m->len - ME_BATTERY_PAYLOAD_LEN),
                    (unsigned)ME_BATTERY_PAYLOAD_LEN);
        }
        if (me_store_battery_set(m->circuit_id, &b)) {
            /* All twelve fields, on two lines: this record is what a battery
             * test will run against, so it is worth being able to read it back
             * out of a hardware log without guessing. */
            ME_LOGI("data: circuit 0x%02X - battery stored (%u bytes): "
                    "%.3f Ah, %u cells, gassing %.2f V, max %.2f V, "
                    "nominal %.2f A, cold-crank %.2f A",
                    m->circuit_id, (unsigned)ME_BATTERY_PAYLOAD_LEN,
                    (double)b.nom_capacity, (unsigned)b.no_of_cells,
                    (double)b.gassing_voltage, (double)b.max_voltage,
                    (double)b.nom_current, (double)b.cold_cranking_current);
            ME_LOGI("data: circuit 0x%02X -   charge factor %u %%, "
                    "impedance %.2f Ohm, break %.2f V, nominal %.2f V, "
                    "energy density %.2f Wh/Kg, battery ID %u",
                    m->circuit_id, (unsigned)b.charge_factor,
                    (double)b.impedance, (double)b.break_voltage,
                    (double)b.nom_voltage, (double)b.energy_density,
                    (unsigned)b.battery_id);
        }
        break;
    }

    case ME_MSG_STORE_CONFIG:
        if (me_store_config_set(m->circuit_id, m->status, m->payload, m->len)) {
            ME_LOGI("data: circuit 0x%02X - config query 0x%02X stored "
                    "(%u bytes, not parsed)",
                    m->circuit_id, (unsigned)m->status, (unsigned)m->len);
        }
        break;

    case ME_MSG_REQ_PROGRAM:
        serve_program(m->circuit_id);
        break;

    case ME_MSG_REQ_BATTERY:
        serve_battery(m->circuit_id);
        break;

    case ME_MSG_SESSION_DATA:
        ring_push(m->circuit_id, m->payload, m->len);
        break;

    case ME_MSG_CAN_DATA:
        /* Provision. The CAN payload format is not defined yet, so it is
         * recorded and discarded rather than guessed at. */
        ME_LOGD("data: circuit 0x%02X - %u CAN byte(s) received, no handler yet",
                m->circuit_id, (unsigned)m->len);
        break;

    default:
        ME_LOGW("data: unexpected message %s", me_msg_type_name(m->type));
        break;
    }
}

static void *data_mgr_main(void *arg)
{
    (void)arg;
    ME_LOGI("data manager thread: started");

    while (!me_app_stop_requested()) {
        if (me_msgq_recv(&g_q_data, &s_rx, ME_RECV_TIMEOUT_MS)) {
            handle(&s_rx);
        }
        /* Runs on every iteration, including the timeout path, so a record
         * that could not be forwarded earlier is retried without needing a new
         * message to arrive. */
        ring_flush();
    }

    ME_LOGI("data manager thread: stopped (%u session record(s) unsent)",
            s_ring_count);
    return NULL;
}

bool me_data_mgr_start(void)
{
    s_ring_head  = 0;
    s_ring_tail  = 0;
    s_ring_count = 0;

    const int rc = pthread_create(&s_thread, NULL, data_mgr_main, NULL);
    if (rc != 0) {
        ME_LOGE("data manager thread: pthread_create failed (%d)", rc);
        return false;
    }
    s_started = true;
    return true;
}

void me_data_mgr_join(void)
{
    if (s_started) {
        (void)pthread_join(s_thread, NULL);
        s_started = false;
    }
}
