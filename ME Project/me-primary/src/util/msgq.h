/*
 * msgq.h - bounded in-process message queue.
 *
 * One queue per consuming thread, so a thread has exactly one place to wait
 * and its main loop is a single recv and a switch.
 *
 * WHY NOT POSIX mq_open: all four threads live in one process, so a kernel
 * round trip buys nothing - and mq_msgsize_max defaults to 8192 bytes and
 * cannot be raised from inside an unprivileged container. A 0xBB program packet
 * can exceed that. This queue has no such ceiling and compiles on the Windows
 * host, so the plumbing gets unit tests without a board.
 *
 * DEADLOCK POLICY: every send takes a finite timeout and drops on expiry. No
 * thread ever blocks forever, so congestion degrades into a counted, logged
 * loss rather than a hang. See ME_Primary_BTS_Block_Diagram.md section 2.4.
 *
 * Portable: pthreads only, with the eventfd support compiled in on Linux and
 * absent on the host. Built by BOTH build.ps1 and build-native.ps1.
 */
#ifndef ME_MSGQ_H
#define ME_MSGQ_H

#include <pthread.h>
#include <stdbool.h>

#include "../msg.h"

/*
 * Slots per queue. 16 x ~64 KB is ~1 MB of .bss per queue; four queues is ~4 MB
 * reserved, and on Linux none of it is committed until actually written.
 */
#ifndef ME_MSGQ_DEPTH
#define ME_MSGQ_DEPTH 16u
#endif

typedef struct {
    const char     *name;   /* for log messages only                        */
    me_msg_t        slot[ME_MSGQ_DEPTH];
    unsigned        head;   /* next slot to read                            */
    unsigned        tail;   /* next slot to write                           */
    unsigned        count;
    pthread_mutex_t lock;
    pthread_cond_t  not_empty;
    pthread_cond_t  not_full;
    int             evt_fd; /* -1 unless pollable                           */
    unsigned long   sent;
    unsigned long   received;
    unsigned long   dropped;
    bool            initialised;
    bool            shutting_down;
} me_msgq_t;

/*
 * Initialise a queue. `pollable` requests an eventfd so a poll()-based consumer
 * can wait on this queue alongside its sockets; it is honoured on Linux and
 * silently ignored on the host, where evt_fd stays -1.
 *
 * `name` must outlive the queue - use a string literal.
 */
bool me_msgq_init(me_msgq_t *q, const char *name, bool pollable);

/* Wake every waiter and release resources. Safe to call on an uninitialised
 * queue. */
void me_msgq_destroy(me_msgq_t *q);

/*
 * Copy a message in. Blocks up to timeout_ms for space (0 = never block,
 * negative = wait indefinitely - which the deadlock policy says not to use).
 *
 * Returns false if the queue stayed full, if m->len exceeds
 * ME_MSG_PAYLOAD_MAX, or if the queue is shutting down. A false return
 * increments the drop counter: the message is gone, not deferred.
 */
bool me_msgq_send(me_msgq_t *q, const me_msg_t *m, int timeout_ms);

/*
 * Copy a message out. Blocks up to timeout_ms for one to arrive.
 * Returns false on timeout or shutdown, leaving *out untouched.
 */
bool me_msgq_recv(me_msgq_t *q, me_msg_t *out, int timeout_ms);

/* The eventfd, or -1. Add this to a poll() set to be woken by a send. */
int me_msgq_fd(const me_msgq_t *q);

/*
 * Clear the eventfd counter after poll() reported it readable, then drain the
 * queue with me_msgq_recv(..., 0) until it returns false.
 *
 * This exists so the consumer never touches evt_fd itself. A caller that read
 * the descriptor directly would be one refactor away from clearing the counter
 * without draining the slots - which loses every queued message silently until
 * the next send re-arms poll().
 */
void me_msgq_ack_event(me_msgq_t *q);

unsigned long me_msgq_dropped(const me_msgq_t *q);
unsigned long me_msgq_sent(const me_msgq_t *q);
unsigned long me_msgq_received(const me_msgq_t *q);

#endif /* ME_MSGQ_H */
