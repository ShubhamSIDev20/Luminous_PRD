/*
 * msgq.c - bounded in-process message queue.
 *
 * Classic mutex + two condition variables. The only unusual part is the
 * optional eventfd, which exists so the communication thread can wait on this
 * queue and its three sockets in a single poll().
 */
#include "msgq.h"

#include <errno.h>
#include <string.h>
#include <time.h>

#ifdef __linux__
#include <sys/eventfd.h>
#include <unistd.h>
#endif

#include "log.h"

/*
 * Absolute deadline for pthread_cond_timedwait.
 *
 * CLOCK_REALTIME, not CLOCK_MONOTONIC: pthread_condattr_setclock() does not
 * exist in winpthreads, and the queue must build on the host so its tests can
 * run there. A system clock step could therefore make one wait expire early or
 * late. That is acceptable for a 50-500 ms drop timeout - it changes when a
 * message is dropped, never whether the protocol is correct - and it must not
 * be reused anywhere timing affects the wire.
 */
static void deadline_from_now(struct timespec *ts, int timeout_ms)
{
    clock_gettime(CLOCK_REALTIME, ts);
    ts->tv_sec  += timeout_ms / 1000;
    ts->tv_nsec += (long)(timeout_ms % 1000) * 1000000L;
    if (ts->tv_nsec >= 1000000000L) {
        ts->tv_nsec -= 1000000000L;
        ts->tv_sec  += 1;
    }
}

/* Copy header fields plus exactly len payload bytes - never the whole 64 KB. */
static void msg_copy(me_msg_t *dst, const me_msg_t *src)
{
    dst->type       = src->type;
    dst->circuit_id = src->circuit_id;
    dst->flags      = src->flags;
    dst->status     = src->status;
    dst->offset     = src->offset;
    dst->len        = src->len;
    if (src->len > 0u) {
        memcpy(dst->payload, src->payload, src->len);
    }
}

bool me_msgq_init(me_msgq_t *q, const char *name, bool pollable)
{
    memset(q, 0, sizeof(*q));
    q->name   = name;
    q->evt_fd = -1;

    if (pthread_mutex_init(&q->lock, NULL) != 0) {
        ME_LOGE("msgq %s: mutex init failed", name);
        return false;
    }
    if (pthread_cond_init(&q->not_empty, NULL) != 0) {
        ME_LOGE("msgq %s: not_empty condvar init failed", name);
        (void)pthread_mutex_destroy(&q->lock);
        return false;
    }
    if (pthread_cond_init(&q->not_full, NULL) != 0) {
        ME_LOGE("msgq %s: not_full condvar init failed", name);
        (void)pthread_cond_destroy(&q->not_empty);
        (void)pthread_mutex_destroy(&q->lock);
        return false;
    }

#ifdef __linux__
    if (pollable) {
        /* Counting semantics, not semaphore: one read() clears the whole
         * accumulated count, and the consumer then drains every slot. */
        q->evt_fd = eventfd(0, EFD_NONBLOCK | EFD_CLOEXEC);
        if (q->evt_fd < 0) {
            ME_LOGE("msgq %s: eventfd failed: %s", name, strerror(errno));
            (void)pthread_cond_destroy(&q->not_full);
            (void)pthread_cond_destroy(&q->not_empty);
            (void)pthread_mutex_destroy(&q->lock);
            return false;
        }
    }
#else
    /* The host build has no eventfd. Nothing on the host polls a queue, so the
     * request is accepted and ignored rather than failing the build. */
    (void)pollable;
#endif

    q->initialised = true;
    return true;
}

void me_msgq_destroy(me_msgq_t *q)
{
    if (!q->initialised) {
        return;
    }

    (void)pthread_mutex_lock(&q->lock);
    q->shutting_down = true;
    (void)pthread_cond_broadcast(&q->not_empty);
    (void)pthread_cond_broadcast(&q->not_full);
    (void)pthread_mutex_unlock(&q->lock);

#ifdef __linux__
    if (q->evt_fd >= 0) {
        (void)close(q->evt_fd);
        q->evt_fd = -1;
    }
#endif

    (void)pthread_cond_destroy(&q->not_full);
    (void)pthread_cond_destroy(&q->not_empty);
    (void)pthread_mutex_destroy(&q->lock);
    q->initialised = false;
}

bool me_msgq_send(me_msgq_t *q, const me_msg_t *m, int timeout_ms)
{
    if (!q->initialised) {
        return false;
    }
    if (m->len > ME_MSG_PAYLOAD_MAX) {
        /* Not a drop: the caller handed over a message that cannot exist.
         * Counting it would hide a bug among ordinary congestion. */
        ME_LOGE("msgq %s: refusing %s with len %u (max %u)",
                q->name, me_msg_type_name(m->type),
                (unsigned)m->len, (unsigned)ME_MSG_PAYLOAD_MAX);
        return false;
    }

    (void)pthread_mutex_lock(&q->lock);

    if (timeout_ms < 0) {
        while (q->count == ME_MSGQ_DEPTH && !q->shutting_down) {
            (void)pthread_cond_wait(&q->not_full, &q->lock);
        }
    } else if (timeout_ms > 0) {
        struct timespec deadline;
        deadline_from_now(&deadline, timeout_ms);
        while (q->count == ME_MSGQ_DEPTH && !q->shutting_down) {
            if (pthread_cond_timedwait(&q->not_full, &q->lock, &deadline)
                == ETIMEDOUT) {
                break;
            }
        }
    }

    if (q->count == ME_MSGQ_DEPTH || q->shutting_down) {
        q->dropped++;
        (void)pthread_mutex_unlock(&q->lock);
        ME_LOGW("msgq %s: full, dropped %s (%lu dropped in total)",
                q->name, me_msg_type_name(m->type), q->dropped);
        return false;
    }

    msg_copy(&q->slot[q->tail], m);
    q->tail = (q->tail + 1u) % ME_MSGQ_DEPTH;
    q->count++;
    q->sent++;

    (void)pthread_cond_signal(&q->not_empty);
    (void)pthread_mutex_unlock(&q->lock);

#ifdef __linux__
    if (q->evt_fd >= 0) {
        /* Outside the lock: a blocked reader must never wait on our write.
         * A full 64-bit counter is the only failure and cannot occur here. */
        const uint64_t one = 1;
        if (write(q->evt_fd, &one, sizeof(one)) != (ssize_t)sizeof(one)) {
            ME_LOGW("msgq %s: eventfd post failed: %s", q->name,
                    strerror(errno));
        }
    }
#endif

    return true;
}

bool me_msgq_recv(me_msgq_t *q, me_msg_t *out, int timeout_ms)
{
    if (!q->initialised) {
        return false;
    }

    (void)pthread_mutex_lock(&q->lock);

    if (timeout_ms < 0) {
        while (q->count == 0u && !q->shutting_down) {
            (void)pthread_cond_wait(&q->not_empty, &q->lock);
        }
    } else if (timeout_ms > 0) {
        struct timespec deadline;
        deadline_from_now(&deadline, timeout_ms);
        while (q->count == 0u && !q->shutting_down) {
            if (pthread_cond_timedwait(&q->not_empty, &q->lock, &deadline)
                == ETIMEDOUT) {
                break;
            }
        }
    }

    if (q->count == 0u) {
        (void)pthread_mutex_unlock(&q->lock);
        return false;
    }

    msg_copy(out, &q->slot[q->head]);
    q->head = (q->head + 1u) % ME_MSGQ_DEPTH;
    q->count--;
    q->received++;

    (void)pthread_cond_signal(&q->not_full);
    (void)pthread_mutex_unlock(&q->lock);
    return true;
}

int me_msgq_fd(const me_msgq_t *q)
{
    return q->evt_fd;
}

void me_msgq_ack_event(me_msgq_t *q)
{
#ifdef __linux__
    if (q->evt_fd >= 0) {
        uint64_t counter = 0;
        /* EFD_NONBLOCK: EAGAIN just means another reader got there first. */
        (void)read(q->evt_fd, &counter, sizeof(counter));
    }
#else
    (void)q;
#endif
}

unsigned long me_msgq_dropped(const me_msgq_t *q)  { return q->dropped; }
unsigned long me_msgq_sent(const me_msgq_t *q)     { return q->sent; }
unsigned long me_msgq_received(const me_msgq_t *q) { return q->received; }
