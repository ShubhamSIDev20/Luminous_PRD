/*
 * core_logic.c - the Core Logic thread.
 */
#define _GNU_SOURCE
#include "core_logic.h"

#include <pthread.h>
#include <sched.h>
#include <stdio.h>
#include <string.h>

#include "../app_queues.h"
#include "../exec/step_engine.h"
#include "../proto/can_frame.h"
#include "../proto/control_frame.h"
#include "../proto/program_chain.h"
#include "../proto/realtime_frame.h"
#include "../store/circuit_store.h"
#include "../util/log.h"

#include <time.h>

/* Set to 1 to compile in hex dumps of every CAN-FD frame Core Logic sends to
 * and receives from the CAN Manager (SET_VALUES/READ_VALUES TX, feedback
 * RX); 0 compiles the dump code out entirely. Independent of --verbose's
 * ME_LOG_DEBUG gate below it - this macro controls whether the dump code
 * exists in the binary at all. */
#define ME_CAN_DEBUG_LOG 0

/* Single-threaded within this file. */
static me_msg_t s_rx;
static me_msg_t s_tx;

static pthread_t           s_thread;
static bool                s_started = false;
static const me_system_t  *s_sys     = NULL;

/*
 * Core Logic's own resident copy of each started program.
 *
 * This mirrors what the Data Manager holds, by design: the step walker then
 * runs against a buffer this thread owns, unchanged, with no cross-thread
 * access to storage. 64 x 2 MB = 128 MB of .bss, which Linux reserves without
 * committing - only pages actually written by a loaded program cost memory.
 */
static uint8_t  g_cl_program[ME_MAX_CIRCUITS][ME_PROGRAM_BUF_SIZE];

typedef struct {
    bool         loading;     /* a chunked transfer is in progress          */
    uint32_t     expect_off;  /* offset the next chunk must carry           */
    uint32_t     len;         /* bytes assembled so far                     */
    bool         have_battery;
    me_battery_t battery;
} cl_circuit_t;

static cl_circuit_t s_cl[ME_MAX_CIRCUITS];

static me_exec_ctx_t s_exec[ME_MAX_CIRCUITS];

static uint32_t now_ms(void)
{
    struct timespec ts;
    (void)clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint32_t)(((uint64_t)ts.tv_sec * 1000u) + (uint64_t)(ts.tv_nsec / 1000000L));
}

/* ------------------------------------------------------------- helpers -- */

static void request(me_msg_type_t type, uint8_t circuit_id)
{
    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = type;
    s_tx.circuit_id = circuit_id;

    if (!me_msgq_send(&g_q_data, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
        ME_LOGE("core: circuit 0x%02X - could not send %s to the data manager",
                circuit_id, me_msg_type_name(type));
    }
}

static void me_execute_program(uint8_t circuit_id)
{
    const int slot = me_circuit_slot(circuit_id);
    if (slot == ME_SLOT_INVALID) { return; } /* callers already validated this */
    me_exec_start(&s_exec[slot], circuit_id, g_cl_program[slot], s_cl[slot].len,
                 now_ms());
    ME_LOGI("core: circuit 0x%02X - execution started, engine state %s",
            circuit_id, me_exec_state_name(s_exec[slot].state));
}

static void dispatch_output(uint8_t circuit_id, const me_exec_output_t *out)
{
    switch (out->kind) {
    case ME_EXEC_OUT_CAN_SET:
    case ME_EXEC_OUT_CAN_READ:
#if ME_CAN_DEBUG_LOG
        ME_LOGD("core: circuit 0x%02X - CAN TX: %s, CAN ID 0x%03X",
                circuit_id,
                out->kind == ME_EXEC_OUT_CAN_SET ? "SET_VALUES" : "READ_VALUES",
                out->can_id);
        me_hex_dump(ME_LOG_DEBUG, "CAN TX frame", out->can_frame, ME_CAN_FRAME_LEN);
#endif

        memset(&s_tx, 0, sizeof(s_tx));
        s_tx.type       = ME_MSG_CAN_TX;
        s_tx.circuit_id = circuit_id;
        s_tx.offset     = out->can_id; /* repurposed to carry the 11-bit CAN
                                        * ID, per the design doc §3 */
        s_tx.len        = ME_CAN_FRAME_LEN;
        memcpy(s_tx.payload, out->can_frame, ME_CAN_FRAME_LEN);
        if (!me_msgq_send(&g_q_can, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
            ME_LOGW("core: circuit 0x%02X - CAN frame dropped, can queue full",
                    circuit_id);
        }
        break;

    case ME_EXEC_OUT_REALTIME: {
        me_realtime_t rt;
        memset(&rt, 0, sizeof(rt));
        rt.step_number     = out->step_number;
        rt.program_running = out->program_running;
        rt.circuit_status  = out->circuit_status;
        rt.step_run_ms     = out->step_run_ms;
        rt.program_run_ms  = out->program_run_ms;
        rt.current         = out->current;
        rt.voltage         = out->voltage;
        rt.operator_code   = out->operator_code;

        memset(&s_tx, 0, sizeof(s_tx));
        s_tx.type       = ME_MSG_REALTIME_DATA;
        s_tx.circuit_id = circuit_id;
        s_tx.len        = (uint32_t)me_realtime_pack(&rt, s_sys->cfg.device_id,
                                                      circuit_id, s_tx.payload,
                                                      s_sys->cfg.crc_order);
        if (!me_msgq_send(&g_q_comm, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
            ME_LOGW("core: circuit 0x%02X - realtime frame dropped, comm "
                    "queue full", circuit_id);
        }
        break;
    }

    default:
        break;
    }
}

static void service_engines(void)
{
    const uint32_t t = now_ms();
    for (int slot = 0; slot < (int)ME_MAX_CIRCUITS; slot++) {
        me_exec_output_t out;
        for (;;) {
            me_exec_tick(&s_exec[slot], t, &out);
            if (out.kind == ME_EXEC_OUT_NONE) { break; }
            dispatch_output(me_circuit_from_slot(slot), &out);
        }
    }
}

/* The whole program has arrived; hand over to execution. step_engine.c's own
 * enter_step() fetches/decodes each step as it runs and logs its own error
 * if a step can't be extracted - no need to duplicate that here. */
static void on_program_loaded(uint8_t circuit_id, int slot)
{
    const uint32_t len   = s_cl[slot].len;
    const uint32_t steps = me_chain_count_steps(g_cl_program[slot], len);

    ME_LOGI("core: circuit 0x%02X - program loaded, %u byte(s), %u step(s)",
            circuit_id, (unsigned)len, (unsigned)steps);

    me_execute_program(circuit_id);
}

/* ------------------------------------------------------------- control -- */

static void handle_control(const me_msg_t *m)
{
    me_control_t c;
    const me_ctrl_parse_result_t pr =
        me_control_parse(m->payload, m->len, s_sys->cfg.crc_order, &c);

    if (pr != ME_CTRL_PARSE_OK) {
        ME_LOGE("core: control frame rejected - %s",
                me_ctrl_parse_result_name(pr));
        me_hex_dump(ME_LOG_INFO, "rejected control frame", m->payload, m->len);
        return;
    }

    const int slot = me_circuit_slot(c.circuit_id);
    if (slot == ME_SLOT_INVALID) {
        ME_LOGW("core: %s for CircuitID 0x%02X ignored - not a valid "
                "secondary/channel pair",
                me_control_query_name(c.query_id), c.circuit_id);
        return;
    }

    ME_LOGI("core: control %s for circuit 0x%02X (secondary %u, channel %u)",
            me_control_query_name(c.query_id), c.circuit_id,
            ME_CIRCUIT_SECONDARY(c.circuit_id),
            ME_CIRCUIT_CHANNEL(c.circuit_id));

    if (c.has_session_id) {
        /*
         * Logged, not stored. The Session ID identifies the test session on the
         * Web Application's side; nothing on the board consumes it yet, and the
         * 0xCC real-time frame has no field for it (the UDP 10001 session
         * variant does - see realtime_frame.h - which is where it will belong
         * once session records exist, T-29).
         *
         * Worth a line anyway: this field is the reason a valid Start frame was
         * rejected as BAD_CRC on 2026-08-12, so seeing it parsed is the fastest
         * confirmation that the fix works on hardware.
         */
        ME_LOGI("core: circuit 0x%02X - session ID 0x%08lX (logged, not stored)",
                c.circuit_id, (unsigned long)c.session_id);
    }

    switch (c.query_id) {
    case ME_CTRL_START:
        /* A fresh Start restarts a stopped circuit deliberately, so the flow
         * can be re-demonstrated without restarting the application. */
        memset(&s_cl[slot], 0, sizeof(s_cl[slot]));
        s_cl[slot].loading    = true;
        s_cl[slot].expect_off = 0;
        request(ME_MSG_REQ_PROGRAM, c.circuit_id);
        request(ME_MSG_REQ_BATTERY, c.circuit_id);
        break;

    case ME_CTRL_STOP:
        s_cl[slot].loading = false;
        me_exec_force_stop(&s_exec[slot], now_ms());
        break;

    case ME_CTRL_PAUSE:
    case ME_CTRL_CONTINUE:
        if (s_exec[slot].state == ME_EXEC_STOPPED) {
            ME_LOGW("core: circuit 0x%02X is STOPPED - %s ignored",
                    c.circuit_id, me_control_query_name(c.query_id));
        } else {
            ME_LOGW("core: circuit 0x%02X - %s is not implemented yet",
                    c.circuit_id, me_control_query_name(c.query_id));
        }
        break;

    case ME_CTRL_SYNC_TIME:
        ME_LOGI("core: time sync received, epoch %u (not applied - the board "
                "clock is owned by Torizon OS)", (unsigned)c.epoch);
        break;

    case ME_CTRL_RESET:
        ME_LOGW("core: circuit 0x%02X - Reset is not implemented yet",
                c.circuit_id);
        break;

    default:
        break;
    }
}

/* ----------------------------------------------------- program chunks --- */

static void handle_program_chunk(const me_msg_t *m)
{
    const int slot = me_circuit_slot(m->circuit_id);
    if (slot == ME_SLOT_INVALID) {
        return;
    }
    if (!s_cl[slot].loading) {
        ME_LOGW("core: circuit 0x%02X - unexpected program chunk at offset %u "
                "(no transfer in progress)", m->circuit_id, (unsigned)m->offset);
        return;
    }

    /*
     * Every chunk carries its absolute offset, and it must be exactly what we
     * expect. A gap means the data manager's send timed out mid-transfer; the
     * alternative to this check is assembling a program with a hole in it and
     * running it.
     */
    if (m->offset != s_cl[slot].expect_off) {
        ME_LOGE("core: circuit 0x%02X - program chunk out of order: expected "
                "offset %u, got %u. Transfer abandoned.",
                m->circuit_id, (unsigned)s_cl[slot].expect_off,
                (unsigned)m->offset);
        s_cl[slot].loading = false;
        s_cl[slot].len     = 0;
        return;
    }

    if (m->offset > (ME_PROGRAM_BUF_SIZE - m->len)) {
        ME_LOGE("core: circuit 0x%02X - program exceeds the %u-byte resident "
                "buffer. Transfer abandoned.",
                m->circuit_id, (unsigned)ME_PROGRAM_BUF_SIZE);
        s_cl[slot].loading = false;
        s_cl[slot].len     = 0;
        return;
    }

    memcpy(&g_cl_program[slot][m->offset], m->payload, m->len);
    s_cl[slot].expect_off += m->len;
    s_cl[slot].len         = s_cl[slot].expect_off;

    if ((m->flags & ME_MSG_FLAG_LAST) != 0u) {
        s_cl[slot].loading = false;
        on_program_loaded(m->circuit_id, slot);
    }
}

static void handle_not_found(const me_msg_t *m)
{
    const int           slot = me_circuit_slot(m->circuit_id);
    const me_msg_type_t what = (me_msg_type_t)m->offset;

    if (what == ME_MSG_REQ_BATTERY) {
        /* Battery data is not required to start: the demo emitter does not use
         * it, and a real run would fail later with a clearer error than
         * refusing the Start here. */
        ME_LOGW("core: circuit 0x%02X - no battery data stored (%s). "
                "Continuing without it.",
                m->circuit_id, me_msg_status_name((me_msg_status_t)m->status));
        return;
    }

    ME_LOGE("core: circuit 0x%02X - START abandoned: %s",
            m->circuit_id, me_msg_status_name((me_msg_status_t)m->status));
    if (slot != ME_SLOT_INVALID) {
        s_cl[slot].loading = false;
    }
}

static void handle(const me_msg_t *m)
{
    switch (m->type) {
    case ME_MSG_CONTROL:
        handle_control(m);
        break;

    case ME_MSG_RSP_PROGRAM_CHUNK:
        handle_program_chunk(m);
        break;

    case ME_MSG_RSP_BATTERY: {
        const int slot = me_circuit_slot(m->circuit_id);
        if (slot != ME_SLOT_INVALID && m->len == sizeof(me_battery_t)) {
            memcpy(&s_cl[slot].battery, m->payload, sizeof(me_battery_t));
            s_cl[slot].have_battery = true;
            /* The parameters a step will actually be executed against, so all
             * of them are logged: this is the record a wrong test result would
             * have to be explained by. */
            const me_battery_t *b = &s_cl[slot].battery;
            ME_LOGI("core: circuit 0x%02X - battery data received: %.3f Ah, "
                    "%u cells, nominal %.2f V, max %.2f V, break %.2f V, "
                    "nominal %.2f A",
                    m->circuit_id, (double)b->nom_capacity,
                    (unsigned)b->no_of_cells, (double)b->nom_voltage,
                    (double)b->max_voltage, (double)b->break_voltage,
                    (double)b->nom_current);
            ME_LOGI("core: circuit 0x%02X -   battery ID %u, impedance %.2f Ohm, "
                    "energy density %.2f Wh/Kg, charge factor %u %%",
                    m->circuit_id, (unsigned)b->battery_id,
                    (double)b->impedance, (double)b->energy_density,
                    (unsigned)b->charge_factor);
        }
        break;
    }

    case ME_MSG_RSP_NOT_FOUND:
        handle_not_found(m);
        break;

    case ME_MSG_CAN_DATA: {
        const int slot = me_circuit_slot(m->circuit_id);
        if (slot == ME_SLOT_INVALID || m->len != ME_CAN_FRAME_LEN) {
            ME_LOGW("core: circuit 0x%02X - malformed CAN_DATA (%u bytes)",
                    m->circuit_id, (unsigned)m->len);
            break;
        }
#if ME_CAN_DEBUG_LOG
        ME_LOGD("core: circuit 0x%02X - CAN RX", m->circuit_id);
        me_hex_dump(ME_LOG_DEBUG, "CAN RX frame", m->payload, m->len);
#endif

        me_can_feedback_t fb;
        if (me_can_parse_feedback(m->payload, ME_CIRCUIT_CHANNEL(m->circuit_id), &fb)) {
            me_exec_on_response(&s_exec[slot], &fb, now_ms());
        }
        break;
    }

    default:
        ME_LOGW("core: unexpected message %s", me_msg_type_name(m->type));
        break;
    }
}

/*
 * Best-effort operator warning, not a safety gate: if the board was never
 * provisioned per Docs/runbooks/2026-08-19-imx8mp-core-isolation-provisioning.md,
 * pinning still succeeds (the thread really does run only on `core`) but the
 * kernel can still schedule other work, IRQs, and RCU callbacks onto it too -
 * the determinism guarantee is silently gone. This turns that into a loud
 * log line instead.
 */
static void warn_if_core_not_isolated(int core)
{
    FILE *f = fopen("/sys/devices/system/cpu/isolated", "r");
    if (!f) {
        return; /* file absent on some kernels - nothing to compare against */
    }
    char buf[64] = {0};
    (void)fgets(buf, sizeof(buf), f);
    fclose(f);

    /* buf is a cpu list like "3" or "2-3" - a full range parser is
     * unnecessary for a warning that is advisory, not load-bearing. */
    char needle[8];
    snprintf(needle, sizeof(needle), "%d", core);
    if (strstr(buf, needle) == NULL) {
        ME_LOGW("core logic thread: CPU%d is pinned but NOT isolated by the "
                "kernel (isolcpus missing?) - determinism is not guaranteed",
                core);
    }
}

static void *core_logic_main(void *arg)
{
    (void)arg;
    ME_LOGI("core logic thread: started");

    if (s_sys->cfg.core_affinity >= 0) {
        ME_LOGI("core logic thread: running on CPU%d", sched_getcpu());
        warn_if_core_not_isolated(s_sys->cfg.core_affinity);
    }

    while (!me_app_stop_requested()) {
        /*
         * Fixed 10 ms tick, matching the engine's own timing budget
         * (Docs/specs/2026-08-14-step-execution-design.md §4). Simpler
         * than computing a dynamic "ms until next due" across circuits,
         * and cheap at this circuit count - revisit if this thread ever
         * becomes measurably hot at full 64-circuit scale.
         */
        if (me_msgq_recv(&g_q_core, &s_rx, 10)) {
            handle(&s_rx);
        }
        service_engines();
    }

    ME_LOGI("core logic thread: stopped");
    return NULL;
}

bool me_core_logic_start(const me_system_t *sys)
{
    s_sys = sys;
    memset(s_cl, 0, sizeof(s_cl));
    memset(s_exec, 0, sizeof(s_exec));

    pthread_attr_t attr;
    pthread_attr_init(&attr);

    const int core = sys->cfg.core_affinity;
    if (core >= 0) {
        cpu_set_t cpuset;
        CPU_ZERO(&cpuset);
        CPU_SET(core, &cpuset);
        const int arc = pthread_attr_setaffinity_np(&attr, sizeof(cpuset), &cpuset);
        if (arc != 0) {
            ME_LOGE("core logic thread: setaffinity to CPU%d failed (%d) - "
                    "thread will run unpinned", core, arc);
        }
    }

    const int rc = pthread_create(&s_thread, &attr, core_logic_main, NULL);
    pthread_attr_destroy(&attr);
    if (rc != 0) {
        ME_LOGE("core logic thread: pthread_create failed (%d)", rc);
        return false;
    }
    s_started = true;
    return true;
}

void me_core_logic_join(void)
{
    if (s_started) {
        (void)pthread_join(s_thread, NULL);
        s_started = false;
    }
}
