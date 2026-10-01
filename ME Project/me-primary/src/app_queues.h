/*
 * app_queues.h - the four inter-thread queues, and the process-wide stop flag.
 *
 * One inbox per consuming thread, so a thread has exactly one place to wait:
 *
 *     g_q_comm   Communication thread   (POLLABLE - carries an eventfd)
 *     g_q_core   Core Logic thread
 *     g_q_data   Data Manager thread
 *     g_q_can    CAN Data Manager thread (POLLABLE - carries an eventfd)
 *
 * The two pollable queues belong to the two threads that must wait on a
 * descriptor at the same time: the communication thread on three sockets, and
 * the CAN manager on the RPMsg device. Neither can afford to block on a queue
 * instead.
 *
 * The stop flag lives here rather than in any one thread so that no thread has
 * to include another thread's header just to learn it should exit. Everything
 * depends on app_queues; app_queues depends on nothing but the queue itself.
 */
#ifndef ME_APP_QUEUES_H
#define ME_APP_QUEUES_H

#include <stdbool.h>

#include "util/msgq.h"

extern me_msgq_t g_q_comm;
extern me_msgq_t g_q_core;
extern me_msgq_t g_q_data;
extern me_msgq_t g_q_can;

/* Send timeouts. Finite by policy: a full queue must degrade into a counted,
 * logged drop, never a hang. See ME_Primary_BTS_Block_Diagram.md section 2.4. */
#define ME_SEND_TIMEOUT_CTRL_MS 50
#define ME_SEND_TIMEOUT_BULK_MS 500
/* Receive timeout, which doubles as how promptly a thread notices a stop. */
#define ME_RECV_TIMEOUT_MS      200

bool me_queues_init(void);
void me_queues_destroy(void);

/* Log every queue's sent/received/dropped counters. Called at shutdown: a
 * non-zero drop count is the first thing to look at when a frame went missing. */
void me_queues_report(void);

/*
 * Async-signal-safe: sets a flag only, so it is safe to call from a
 * SIGINT/SIGTERM handler.
 */
void me_app_request_stop(void);
bool me_app_stop_requested(void);

#endif /* ME_APP_QUEUES_H */
