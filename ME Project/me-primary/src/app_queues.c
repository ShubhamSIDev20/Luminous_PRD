/*
 * app_queues.c - the four queues and the stop flag.
 */
#include "app_queues.h"

#include <signal.h>

#include "util/log.h"

me_msgq_t g_q_comm;
me_msgq_t g_q_core;
me_msgq_t g_q_data;
me_msgq_t g_q_can;

static volatile sig_atomic_t s_stop_requested = 0;

bool me_queues_init(void)
{
    /* g_q_comm is pollable: its consumer waits in poll() on three sockets and
     * must be woken by a send without abandoning them. */
    if (!me_msgq_init(&g_q_comm, "comm", true)) {
        return false;
    }
    if (!me_msgq_init(&g_q_core, "core", false)) {
        me_msgq_destroy(&g_q_comm);
        return false;
    }
    if (!me_msgq_init(&g_q_data, "data", false)) {
        me_msgq_destroy(&g_q_core);
        me_msgq_destroy(&g_q_comm);
        return false;
    }
    /* Pollable: the CAN manager waits on this queue and the RPMsg device fd
     * in one poll(), the same way the communication thread waits on g_q_comm
     * alongside its sockets. */
    if (!me_msgq_init(&g_q_can, "can", true)) {
        me_msgq_destroy(&g_q_data);
        me_msgq_destroy(&g_q_core);
        me_msgq_destroy(&g_q_comm);
        return false;
    }

    ME_LOGI("queues: 4 initialised, depth %u, payload max %u bytes",
            (unsigned)ME_MSGQ_DEPTH, (unsigned)ME_MSG_PAYLOAD_MAX);
    return true;
}

static void report_one(const me_msgq_t *q)
{
    const unsigned long dropped = me_msgq_dropped(q);
    /* A drop is the explanation for a frame that "never arrived". Raise its log
     * level so it is not lost among the ordinary counters. */
    me_log(dropped > 0 ? ME_LOG_WARN : ME_LOG_INFO,
           "queues: %-5s sent %lu, received %lu, dropped %lu",
           q->name, me_msgq_sent(q), me_msgq_received(q), dropped);
}

void me_queues_report(void)
{
    report_one(&g_q_comm);
    report_one(&g_q_core);
    report_one(&g_q_data);
    report_one(&g_q_can);
}

void me_queues_destroy(void)
{
    me_msgq_destroy(&g_q_can);
    me_msgq_destroy(&g_q_data);
    me_msgq_destroy(&g_q_core);
    me_msgq_destroy(&g_q_comm);
    ME_LOGI("queues: destroyed");
}

void me_app_request_stop(void)
{
    s_stop_requested = 1;
}

bool me_app_stop_requested(void)
{
    return s_stop_requested != 0;
}
