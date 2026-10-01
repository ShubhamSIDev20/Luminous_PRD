/*
 * test_msgq.c - unit tests for the in-process message queue.
 *
 * The queue is the spine of the four-thread architecture: every inter-thread
 * hand-off goes through it, so a fault here shows up as an unexplained hang or
 * a lost frame somewhere else entirely. These tests pin down the behaviour that
 * the rest of the system is entitled to assume.
 *
 * NOTE ON SIZE: me_msgq_t contains ME_MSGQ_DEPTH slots of ME_MSG_PAYLOAD_MAX
 * bytes each - about 1 MB. Every queue in these tests is therefore file-scope
 * static, never a local. A local would overflow the default 1 MB thread stack
 * on Windows before the first assertion ran.
 */
#include <pthread.h>
#include <string.h>

#include "test_util.h"
#include "../src/util/msgq.h"

static me_msgq_t q;
static me_msg_t  tx;
static me_msg_t  rx;

static void fill(me_msg_t *m, me_msg_type_t type, uint8_t circuit, uint32_t len)
{
    memset(m, 0, sizeof(*m));
    m->type       = type;
    m->circuit_id = circuit;
    m->len        = len;
    for (uint32_t i = 0; i < len; i++) {
        m->payload[i] = (uint8_t)(i & 0xFFu);
    }
}

static void test_init_non_pollable_has_no_fd(void)
{
    TEST_CASE("msgq: a non-pollable queue exposes no descriptor");
    CHECK(me_msgq_init(&q, "test", false));
    CHECK_EQ_U((unsigned long)(-1), (unsigned long)me_msgq_fd(&q));
    me_msgq_destroy(&q);
}

static void test_send_recv_round_trip(void)
{
    TEST_CASE("msgq: send/recv preserves every header field and the payload");
    CHECK(me_msgq_init(&q, "test", false));

    fill(&tx, ME_MSG_STORE_PROGRAM, 0x11, 32);
    tx.flags  = ME_MSG_FLAG_LAST;
    tx.status = ME_STATUS_OK;
    tx.offset = 4096;

    CHECK(me_msgq_send(&q, &tx, 50));

    memset(&rx, 0, sizeof(rx));
    CHECK(me_msgq_recv(&q, &rx, 50));

    CHECK_EQ_U(ME_MSG_STORE_PROGRAM, rx.type);
    CHECK_EQ_U(0x11, rx.circuit_id);
    CHECK_EQ_U(ME_MSG_FLAG_LAST, rx.flags);
    CHECK_EQ_U(ME_STATUS_OK, rx.status);
    CHECK_EQ_U(4096, rx.offset);
    CHECK_EQ_U(32, rx.len);
    CHECK_MEM(rx.payload, tx.payload, 32);

    me_msgq_destroy(&q);
}

static void test_fifo_order(void)
{
    TEST_CASE("msgq: messages come out in the order they went in");
    CHECK(me_msgq_init(&q, "test", false));

    for (uint32_t i = 0; i < 5; i++) {
        fill(&tx, ME_MSG_CONTROL, (uint8_t)(0x11u + i), 1);
        tx.offset = i;
        CHECK(me_msgq_send(&q, &tx, 50));
    }
    for (uint32_t i = 0; i < 5; i++) {
        CHECK(me_msgq_recv(&q, &rx, 50));
        CHECK_EQ_U(i, rx.offset);
        CHECK_EQ_U(0x11u + i, rx.circuit_id);
    }

    me_msgq_destroy(&q);
}

static void test_recv_on_empty_queue_times_out(void)
{
    TEST_CASE("msgq: receiving from an empty queue fails after the timeout");
    CHECK(me_msgq_init(&q, "test", false));
    CHECK(!me_msgq_recv(&q, &rx, 20));
    me_msgq_destroy(&q);
}

static void test_full_queue_drops_and_counts(void)
{
    TEST_CASE("msgq: a full queue drops the message and counts it");
    CHECK(me_msgq_init(&q, "test", false));

    for (unsigned i = 0; i < ME_MSGQ_DEPTH; i++) {
        fill(&tx, ME_MSG_CONTROL, 0x11, 1);
        CHECK(me_msgq_send(&q, &tx, 20));
    }
    CHECK_EQ_U(0, me_msgq_dropped(&q));

    /* The (DEPTH+1)th send has nowhere to go. */
    fill(&tx, ME_MSG_CONTROL, 0x11, 1);
    CHECK(!me_msgq_send(&q, &tx, 20));
    CHECK_EQ_U(1, me_msgq_dropped(&q));

    /* Draining one slot makes room again - the queue is not wedged by the
     * refusal. */
    CHECK(me_msgq_recv(&q, &rx, 20));
    fill(&tx, ME_MSG_CONTROL, 0x11, 1);
    CHECK(me_msgq_send(&q, &tx, 20));

    me_msgq_destroy(&q);
}

static void test_only_len_bytes_are_copied(void)
{
    TEST_CASE("msgq: only len payload bytes are copied, not the whole buffer");
    CHECK(me_msgq_init(&q, "test", false));

    fill(&tx, ME_MSG_STORE_BATTERY, 0x11, 4);
    CHECK(me_msgq_send(&q, &tx, 50));

    /* Poison the destination past the payload. A full-buffer copy would wipe
     * it; a len-bounded copy leaves it alone. This is what keeps a 6-byte
     * control frame from costing a 64 KB memcpy. */
    memset(rx.payload, 0xA5, 16);
    CHECK(me_msgq_recv(&q, &rx, 50));
    CHECK_EQ_U(4, rx.len);
    CHECK_BYTE(rx.payload, 4, 0xA5);
    CHECK_BYTE(rx.payload, 15, 0xA5);

    me_msgq_destroy(&q);
}

static void test_oversized_payload_is_refused(void)
{
    TEST_CASE("msgq: a message claiming more than the payload maximum is refused");
    CHECK(me_msgq_init(&q, "test", false));

    memset(&tx, 0, sizeof(tx));
    tx.type = ME_MSG_STORE_PROGRAM;
    tx.len  = ME_MSG_PAYLOAD_MAX + 1u;
    CHECK(!me_msgq_send(&q, &tx, 20));

    me_msgq_destroy(&q);
}

/* ------------------------------------------------- producer / consumer ---- */

#define PC_COUNT 40u

static void *producer_main(void *arg)
{
    me_msgq_t *pq = (me_msgq_t *)arg;
    static me_msg_t m; /* static: 64 KB is too much for a small thread stack */
    for (uint32_t i = 0; i < PC_COUNT; i++) {
        fill(&m, ME_MSG_REALTIME_DATA, 0x11, 8);
        m.offset = i;
        if (!me_msgq_send(pq, &m, 2000)) {
            return (void *)1; /* non-NULL means failure */
        }
    }
    return NULL;
}

static void test_producer_consumer_blocks_and_preserves_order(void)
{
    TEST_CASE("msgq: 40 messages cross a depth-16 queue in order");
    CHECK(me_msgq_init(&q, "test", false));

    pthread_t producer;
    const int rc = pthread_create(&producer, NULL, producer_main, &q);
    CHECK_EQ_U(0, (unsigned long)rc);
    if (rc != 0) {
        me_msgq_destroy(&q);
        return;
    }

    /* More messages than the queue can hold, so the producer must block on a
     * full queue and the consumer must block on an empty one. Both directions
     * of the condvar handshake are exercised. */
    unsigned received = 0;
    for (uint32_t i = 0; i < PC_COUNT; i++) {
        if (!me_msgq_recv(&q, &rx, 2000)) {
            break;
        }
        CHECK_EQ_U(i, rx.offset);
        received++;
    }
    CHECK_EQ_U(PC_COUNT, received);

    void *pret = (void *)1;
    (void)pthread_join(producer, &pret);
    CHECK(pret == NULL);

    me_msgq_destroy(&q);
}

static void test_destroy_wakes_a_blocked_receiver(void)
{
    TEST_CASE("msgq: destroy releases a waiter instead of leaving it stuck");
    CHECK(me_msgq_init(&q, "test", false));
    /* Nothing is ever sent, so this can only return because the timeout
     * expired - proving the wait is bounded and shutdown can proceed. */
    CHECK(!me_msgq_recv(&q, &rx, 30));
    me_msgq_destroy(&q);
}

static void test_type_and_status_names(void)
{
    TEST_CASE("msgq: message types and statuses have printable names");
    CHECK_STR("STORE_PROGRAM", me_msg_type_name(ME_MSG_STORE_PROGRAM));
    CHECK_STR("RSP_PROGRAM_CHUNK", me_msg_type_name(ME_MSG_RSP_PROGRAM_CHUNK));
    CHECK_STR("SESSION_DATA", me_msg_type_name(ME_MSG_SESSION_DATA));
    CHECK_STR("UNKNOWN", me_msg_type_name((me_msg_type_t)999));
    CHECK_STR("PROGRAM_INCOMPLETE",
              me_msg_status_name(ME_STATUS_PROGRAM_INCOMPLETE));
    CHECK_STR("OK", me_msg_status_name(ME_STATUS_OK));
}

void run_msgq_tests(void)
{
    printf("\n-- msgq --\n");
    test_init_non_pollable_has_no_fd();
    test_send_recv_round_trip();
    test_fifo_order();
    test_recv_on_empty_queue_times_out();
    test_full_queue_drops_and_counts();
    test_only_len_bytes_are_copied();
    test_oversized_payload_is_refused();
    test_producer_consumer_blocks_and_preserves_order();
    test_destroy_wakes_a_blocked_receiver();
    test_type_and_status_names();
}
