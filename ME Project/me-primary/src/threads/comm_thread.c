/*
 * comm_thread.c - the single communication thread.
 *
 * State machine:
 *
 *   CONNECTING --> REGISTERING --> IDLE
 *        ^              |            |
 *        +--------------+------------+
 *        on failure or disconnect, with capped backoff
 *
 * REGISTERED and MONITORING were collapsed into IDLE in session #3 - nothing
 * ever distinguished them (ADR-10).
 *
 * Reaching IDLE also admits every one of this board's configured
 * Secondary/Channels (--channels) into the circuit registry, which is what
 * allows route_frame() to accept data for them. Frames for any other
 * circuit are dropped until something registers it - today nothing else
 * does; a future CAN-side handshake will.
 *
 * This thread also ANSWERS two frames rather than only routing them: the 0xBB
 * Q1 is-ready query and the Q3 packet count, plus a per-packet acknowledgement
 * for Q4. Those are the only frames the board writes to TCP other than its own
 * registration request, which is why route_frame() carries the socket fd.
 */
#include "comm_thread.h"

#include <errno.h>
#include <poll.h>
#include <pthread.h>
#include <signal.h>
#include <stdio.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/types.h>
#include <time.h>

#include "../app_queues.h"
#include "../net/tcp_client.h"
#include "../net/udp_sock.h"
#include "../proto/ack_frame.h"
#include "../proto/frame_router.h"
#include "../proto/program_frame.h"
#include "../proto/proto_defs.h"
#include "../store/circuit_registry.h"
#include "post_reg.h"

#define ME_BACKOFF_MIN_MS 1000
#define ME_BACKOFF_MAX_MS 30000

/*
 * How often idle_loop() retries registration for a configured channel that
 * has NOT yet been admitted (server keeps answering Value 0x00 - typically
 * "new device, awaiting manual approval in the Web Application"). See
 * ADR-34: this is a per-circuit, registry-gated retry - it never touches a
 * circuit once me_registry_is_registered() is true for it, so it cannot
 * regress into ADR-10's old "re-register a working circuit every 30s" bug.
 */
#define ME_REG_RETRY_MS 10000u

/* Larger than any valid response, so an over-long reply is captured and
 * reported rather than silently truncated to the expected length. */
#define ME_RESPONSE_CAP 64

/*
 * Inbound reassembly buffer. TCP is a stream: two frames can arrive in one
 * read() and one frame can arrive split across two reads. Bytes accumulate
 * here and whole frames are peeled off the front.
 */
#define ME_RX_BUF_SIZE (ME_MSG_PAYLOAD_MAX + 4096u)
static uint8_t s_rxbuf[ME_RX_BUF_SIZE];
static size_t  s_rxlen = 0;

/* Owned by this thread alone. */
static me_msg_t s_route_msg;
static me_msg_t s_out_msg;

static volatile sig_atomic_t s_registered = 0;

/* Owned by the communication thread alone - never touched from a signal
 * handler, so it needs no volatile/sig_atomic_t treatment. */
static me_comm_state_t s_state = ME_COMM_CONNECTING;

/*
 * Per-configured-channel "last registration attempt" timestamp, indexed the
 * same way sys->cfg.channels[] is (0..channel_count-1), not by CircuitID.
 * Stamped after every do_registration() call - from the initial pass in
 * comm_thread_main() and from idle_loop()'s periodic retry alike - so the
 * retry interval is measured from whichever attempt was most recent.
 */
static uint32_t s_last_reg_attempt_ms[ME_MAX_CHANNELS];

static uint32_t now_ms(void)
{
    struct timespec ts;
    (void)clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint32_t)(((uint64_t)ts.tv_sec * 1000u) + (uint64_t)(ts.tv_nsec / 1000000L));
}

static pthread_t s_thread;
static bool      s_thread_started = false;

const char *me_comm_state_name(me_comm_state_t s)
{
    switch (s) {
    case ME_COMM_CONNECTING:  return "CONNECTING";
    case ME_COMM_REGISTERING: return "REGISTERING";
    case ME_COMM_IDLE:        return "IDLE";
    case ME_COMM_STOPPED:     return "STOPPED";
    default:                  return "UNKNOWN";
    }
}

/*
 * Kept as thin wrappers: the flag itself moved to app_queues so that no thread
 * has to include another thread's header just to learn it should exit.
 */
void me_comm_thread_request_stop(void)
{
    me_app_request_stop();
}

bool me_comm_thread_stop_requested(void)
{
    return me_app_stop_requested();
}

bool me_comm_is_registered(void)
{
    return s_registered != 0;
}

/* Sleep that still notices a stop request promptly. */
static void interruptible_sleep_ms(int total_ms)
{
    const int slice_ms = 200;
    int waited = 0;
    while (waited < total_ms && !me_app_stop_requested()) {
        const int chunk = (total_ms - waited < slice_ms) ? (total_ms - waited)
                                                         : slice_ms;
        struct pollfd dummy = { .fd = -1, .events = 0, .revents = 0 };
        (void)poll(&dummy, 1, chunk);
        waited += chunk;
    }
}

static void print_registered_banner(const me_reg_request_t *req,
                                    const me_reg_response_t *rsp)
{
    const uint8_t ckt = req->circuit_id;
    printf("\n");
    printf("======================================================\n");
    printf("  DEVICE REGISTERED\n");
    printf("------------------------------------------------------\n");
    printf("  Device number   : %u\n", req->device_id);
    printf("  Circuit number  : 0x%02X  (secondary %u, channel %u)\n",
           ckt, ME_CIRCUIT_SECONDARY(ckt), ME_CIRCUIT_CHANNEL(ckt));
    printf("  Device name     : %s\n", req->device_name);
    printf("  Server response : 0x%02X (%s)\n",
           rsp->value, me_reg_value_name(rsp->value));
    printf("======================================================\n\n");
    fflush(stdout);
}

/*
 * Send the registration frame and evaluate the reply.
 * Returns true only when the server answered Value == 0x01.
 */
static bool do_registration(me_system_t *sys, int fd, uint8_t circuit_id)
{
    me_reg_request_t req = sys->reg_request;
    req.circuit_id = circuit_id;

    uint8_t frame[ME_REG_REQUEST_LEN];
    const size_t frame_len = me_reg_pack_request(&req, frame,
                                                 sys->cfg.crc_order);

    ME_LOGI("registration: sending %u-byte 0xDD frame (CRC %s)",
            (unsigned)frame_len, me_crc_order_name(sys->cfg.crc_order));
    me_hex_dump(ME_LOG_INFO, "registration request", frame, frame_len);

    if (!me_tcp_send_all(fd, frame, frame_len)) {
        return false;
    }

    uint8_t rx[ME_RESPONSE_CAP];
    const int n = me_tcp_recv_response(fd, rx, sizeof(rx),
                                       ME_REG_RESPONSE_LEN,
                                       sys->cfg.response_timeout_ms);
    if (n < 0) {
        return false;
    }
    if (n == 0) {
        ME_LOGE("registration: no response within %d ms",
                sys->cfg.response_timeout_ms);
        return false;
    }

    me_hex_dump(ME_LOG_INFO, "registration response", rx, (size_t)n);

    me_reg_response_t rsp;
    const me_reg_parse_result_t pr = me_reg_parse_response(
        rx, (size_t)n, &req, sys->cfg.crc_order, &rsp);

    if (pr != ME_REG_PARSE_OK) {
        ME_LOGE("registration: response rejected - %s",
                me_reg_parse_result_name(pr));

        /* A CRC rejection is the one failure most likely to be a convention
         * mismatch rather than corruption, so show the arithmetic. The source
         * documents disagree on byte order: bm_device_registration_v5.0 writes
         * "CRC_HI CRC_LO" (what this build sends), ICD says little-endian.
         *
         * The suggestion below is derived, not hardcoded to one order: if the
         * default ever moves again, the advice moves with it instead of
         * telling the operator to switch to the order already in use. */
        if (pr == ME_REG_PARSE_BAD_CRC && n >= 3) {
            const uint16_t computed = me_crc16_modbus(rx, (size_t)n - 2u);
            ME_LOGE("registration: computed CRC 0x%04X; frame carries "
                    "0x%04X read as LE, 0x%04X read as BE",
                    computed,
                    me_crc16_read(rx, (size_t)n, ME_CRC_ORDER_LE),
                    me_crc16_read(rx, (size_t)n, ME_CRC_ORDER_BE));

            const me_crc_order_t other =
                (sys->cfg.crc_order == ME_CRC_ORDER_BE) ? ME_CRC_ORDER_LE
                                                        : ME_CRC_ORDER_BE;
            if (me_crc16_read(rx, (size_t)n, other) == computed) {
                ME_LOGE("registration: the response CRC matches in %s. "
                        "The server disagrees with this build's order - "
                        "re-run with --crc-order %s",
                        me_crc_order_name(other),
                        (other == ME_CRC_ORDER_BE) ? "be" : "le");
            }
        }
        return false;
    }

    if (rsp.echo_mismatch) {
        ME_LOGW("registration: server echoed device %u circuit 0x%02X, "
                "we sent device %u circuit 0x%02X (informational)",
                rsp.device_id, rsp.circuit_id,
                req.device_id, req.circuit_id);
    }

    if (!rsp.registered) {
        ME_LOGE("registration: rejected by server - value 0x%02X (%s)",
                rsp.value, me_reg_value_name(rsp.value));
        return false;
    }

    /*
     * Admission control for this circuit starts here: only a circuit whose
     * device registration actually succeeded (Value 0x01 or 0x02) is added
     * to the registry, so route_frame() below will accept data for it.
     *
     * parse_args() already rejects any --secondary/--channel outside 1-8, so
     * reaching here with a malformed CircuitID would mean a bug rather than
     * operator error. It is still checked rather than cast to (void): the
     * consequence of a silent failure is that every subsequent data packet on
     * this connection is dropped by route_frame() below while registration
     * itself looks completely normal - the "no error, no log, no symptom"
     * failure mode this project treats as a bug elsewhere (see
     * me_circuit_slot).
     */
    if (!me_registry_mark_registered(req.circuit_id)) {
        ME_LOGE("registration: server accepted CircuitID 0x%02X, but it is "
                "not a valid secondary(1-%u)/channel(1-%u) pair. This should "
                "be unreachable - parse_args() validates both nibbles. Every "
                "data packet on this connection will be ignored.",
                req.circuit_id,
                (unsigned)ME_MAX_SECONDARIES, (unsigned)ME_MAX_CHANNELS);
    }

    print_registered_banner(&req, &rsp);

    /*
     * One 0xCC frame so the Web Application has live data to show for this
     * circuit immediately, before a Start command has been issued and real
     * step execution has produced anything of its own.
     *
     * Deliberately AFTER the banner and after the registry mark: it is not part
     * of registration succeeding, and a queue-full drop here must not make a
     * successful registration look failed. The function logs its own outcome and
     * returns void for exactly that reason.
     *
     * Fires on every successful registration, including reconnects.
     */
    me_post_reg_send(req.circuit_id);

    return true;
}

/*
 * Verify a whole inbound frame against its own trailing CRC.
 *
 * me_frame_classify() deliberately does not do this - each consumer re-verifies
 * with its own parser. Until now every 0xBB consumer was another thread. Now
 * this thread answers some of them itself, and AN ACKNOWLEDGEMENT IS A CLAIM
 * ABOUT THE BYTES: replying "OK" to a frame whose checksum was never tested
 * would tell the Web Application its packet arrived intact when nothing
 * established that.
 *
 * The mismatch diagnostic mirrors do_registration()'s, and for the same reason:
 * on a LAN a CRC failure is far more likely to be a byte-order disagreement
 * between the two implementations than actual corruption, so print the
 * arithmetic instead of just the verdict.
 */
static bool frame_crc_ok(const me_system_t *sys, const uint8_t *frame,
                         uint32_t len, const char *what)
{
    if (me_crc16_verify(frame, len, sys->cfg.crc_order)) {
        return true;
    }

    /*
     * Unreachable today - every caller runs after me_frame_classify(), which
     * rejects anything below ME_FRAME_MIN_LEN. Guarded anyway because the
     * diagnostic below computes (len - 2) as a size_t: on a short frame that
     * underflows to a near-SIZE_MAX read length, turning a logging path into an
     * out-of-bounds read. A one-line guard is cheaper than that being someone's
     * afternoon later.
     */
    if (len < ME_FRAME_MIN_LEN) {
        ME_LOGE("%s: %u bytes is too short to hold a CRC", what, (unsigned)len);
        return false;
    }

    ME_LOGE("%s: CRC mismatch - computed 0x%04X; the frame carries 0x%04X read "
            "as LE, 0x%04X read as BE (this build reads %s)",
            what,
            me_crc16_modbus(frame, (size_t)len - ME_REG_CRC_LEN),
            me_crc16_read(frame, len, ME_CRC_ORDER_LE),
            me_crc16_read(frame, len, ME_CRC_ORDER_BE),
            me_crc_order_name(sys->cfg.crc_order));
    me_hex_dump(ME_LOG_INFO, "frame with a bad CRC", frame, len);
    return false;
}

/*
 * Put an acknowledgement on the wire, for whichever group the frame came from.
 *
 * The start byte is echoed from the inbound frame, so one function answers
 * 0xAA, 0xBB and 0xEE. Answering with the wrong group's start byte would be a
 * reply the Web Application cannot match to any question it asked.
 *
 * Best effort: a failed send means the connection is going away, and
 * idle_loop() finds that out on its next poll(). There is nothing useful to do
 * here beyond saying so.
 */
static void send_ack(const me_system_t *sys, int tcp_fd,
                     const me_frame_info_t *fi, uint8_t value)
{
    uint8_t ack[ME_ACK_LEN];
    const size_t n = me_ack_pack(fi->start, fi->device_id, fi->circuit_id,
                                 fi->query_id, value, sys->cfg.crc_order, ack);

    ME_LOGI("route: circuit 0x%02X - answering 0x%02X Q%u with value 0x%02X (%s)",
            fi->circuit_id, fi->start, (unsigned)fi->query_id, (unsigned)value,
            (value == ME_ACK_VALUE_OK) ? "OK/Success" : "Fail");
    me_hex_dump(ME_LOG_DEBUG, "acknowledgement", ack, n);

    if (!me_tcp_send_all(tcp_fd, ack, n)) {
        ME_LOGE("route: failed to send the 0x%02X Q%u acknowledgement",
                fi->start, (unsigned)fi->query_id);
    }
}

/*
 * Answer a 0xBB query this thread owns outright - one with no side effect
 * anywhere else in the program.
 *
 * Q1 "are you ready to accept program data" is answered OK unconditionally.
 * Readiness genuinely is unconditional today: the Data Manager accepts a
 * program at any time and a new one replaces the previous one (ADR-14). Once
 * step execution lands, a circuit already running a test is the case that will
 * need ME_ACK_VALUE_FAIL here - see the task note in .claude/TASKS.md.
 *
 * Q3 "this many packets follow" is answered and then discarded. The count is
 * logged rather than stored because completion is detected from the chain
 * terminator, not by counting; logging it is what makes "3 announced, 1 stored"
 * visible instead of invisible.
 */
static void handle_program_handshake(const me_system_t *sys, int tcp_fd,
                                     const uint8_t *frame, uint32_t len,
                                     const me_frame_info_t *fi)
{
    if (!frame_crc_ok(sys, frame, len, "program query")) {
        return; /* no reply: an ack would vouch for bytes that failed the check */
    }

    if (fi->query_id == ME_QID_PRG_PACKET_COUNT) {
        uint16_t count = 0;
        if (me_prg_parse_packet_count(frame, len, &count)) {
            ME_LOGI("route: circuit 0x%02X - Web Application announces %u "
                    "program packet(s). Not stored: completion comes from the "
                    "chain terminator, not from this count",
                    fi->circuit_id, (unsigned)count);
        }
    }

    send_ack(sys, tcp_fd, fi, ME_ACK_VALUE_OK);
}

/*
 * Hand one complete frame to the thread that owns it, or answer it here.
 *
 * The router decides; this function copies, sends, and replies. Anything the
 * router recognises but nothing consumes is logged by name rather than dropped
 * silently - "nothing happened and nothing was logged" is the failure mode
 * worth spending a log line to avoid.
 */
static void route_frame(const me_system_t *sys, int tcp_fd,
                        const uint8_t *frame, uint32_t len)
{
    me_frame_info_t fi;
    if (!me_frame_classify(frame, len, &fi)) {
        ME_LOGW("route: frame rejected - %s (start byte 0x%02X, %u bytes)",
                fi.reject, fi.start, (unsigned)len);
        me_hex_dump(ME_LOG_INFO, "rejected frame", frame, len);
        return;
    }

    /*
     * 0xDD FIRST, because it is the one valid frame with no CircuitNumber
     * field - fi.circuit_id is zero for it, and the admission check below would
     * report a nonexistent "circuit 0x00" instead of what actually arrived.
     * Registration is handled synchronously in do_registration(), so one
     * reaching here is unsolicited.
     */
    if (fi.start == ME_START_REGISTRATION) {
        ME_LOGI("route: unsolicited 0xDD frame while %s - registration is "
                "handled at connect time, ignoring (%u bytes)",
                me_comm_state_name(s_state), (unsigned)len);
        return;
    }

    /*
     * ADMISSION CONTROL. A frame is only handled once its Secondary/Channel
     * has registered - Program data, Control commands and configuration alike.
     * Anything for an unregistered circuit is dropped here, before it reaches
     * a queue or earns a reply, so no downstream thread has to repeat the
     * check.
     *
     * THIS SITS AHEAD OF THE HANDSHAKE ANSWER BELOW ON PURPOSE. Replying "yes,
     * ready" to an is-ready query for a circuit this board never registered
     * would be a false claim about hardware it has no record of - so an
     * unregistered circuit gets no answer at all, not a negative one.
     *
     * One rejection covers two cases deliberately: a CircuitID that is
     * malformed, and one that is well-formed but never registered. Both mean
     * "we have no business acting on this", and me_registry_is_registered()
     * is false for both.
     *
     * WHY A LOG AND NOT A SILENT DROP: a Web Application sending to the wrong
     * Secondary/Channel would otherwise see the board accept the connection
     * and do nothing, with no way to tell that from a crash.
     */
    if (!me_registry_is_registered(fi.circuit_id)) {
        ME_LOGW("route: circuit 0x%02X (secondary %u, channel %u) is not "
                "registered - start 0x%02X query 0x%02X dropped with no reply "
                "(%u bytes)",
                fi.circuit_id,
                ME_CIRCUIT_SECONDARY(fi.circuit_id),
                ME_CIRCUIT_CHANNEL(fi.circuit_id),
                fi.start, fi.query_id, (unsigned)len);
        return;
    }

    /* A query answerable here and now, with nothing else involved. */
    if (fi.start == ME_START_PROGRAM
        && me_prg_query_is_handshake(fi.query_id)) {
        handle_program_handshake(sys, tcp_fd, frame, len, &fi);
        return;
    }

    if (fi.msg_type == ME_MSG_NONE) {
        ME_LOGI("route: start 0x%02X query 0x%02X recognised but not handled "
                "in this milestone (%u bytes)",
                fi.start, fi.query_id, (unsigned)len);
        return;
    }

    /*
     * Which routed frames get an acknowledgement, and why only these:
     *
     *   0xBB Q4 STORE_PROGRAM  - bm_program_v3.0.md documents a per-step ACK
     *   0xAA Q5 STORE_BATTERY  - the data really is stored, so success is
     *                            something this board can honestly assert
     *   0xEE    CONTROL        - bm_control_v3.0.md documents a response for
     *                            every one of the six commands
     *
     * STORE_CONFIG is NOT acked. The 0xAA read queries (Q2 factory, Q3
     * manufacturing, Q4 battery) owe the Web Application actual DATA back, not a
     * yes/no - and this milestone only stashes their bytes unparsed. A bare
     * "success" would claim a read that never happened.
     */
    const bool wants_ack = (fi.msg_type == ME_MSG_STORE_PROGRAM)
                        || (fi.msg_type == ME_MSG_STORE_BATTERY)
                        || (fi.msg_type == ME_MSG_CONTROL);

    /*
     * An ack is a claim about the bytes, so the CRC is checked before the frame
     * is acted on. Storing whatever arrived was tolerable while nothing answered;
     * an ack saying OK for a corrupt frame would both store bad data AND tell the
     * Web Application it landed cleanly.
     *
     * 0xEE IS INCLUDED, though Core Logic re-verifies with me_control_parse().
     * ADR-18 originally exempted it to avoid duplicating that check - which was
     * wrong: the ack is sent from HERE, synchronously, while Core Logic parses
     * later on its own thread and has no path back to the socket. A corrupt 0xEE
     * frame was therefore told "OK" and then silently dropped. Checking twice
     * costs a CRC over 10 bytes; the two cannot disagree, because both compute
     * the same function over the same bytes.
     */
    if (wants_ack
        && !frame_crc_ok(sys, frame, len, me_msg_type_name(fi.msg_type))) {
        send_ack(sys, tcp_fd, &fi, ME_ACK_VALUE_FAIL);
        return;
    }

    memset(&s_route_msg, 0, sizeof(s_route_msg));
    s_route_msg.type       = fi.msg_type;
    s_route_msg.circuit_id = fi.circuit_id;
    /* status carries the query ID for config frames, which are stored whole
     * and parsed later. */
    s_route_msg.status     = fi.query_id;
    s_route_msg.len        = fi.body_len;
    if (fi.body_len > 0u) {
        memcpy(s_route_msg.payload, &frame[fi.body_off], fi.body_len);
    }

    me_msgq_t *dest = (fi.msg_type == ME_MSG_CONTROL) ? &g_q_core : &g_q_data;
    const int timeout = (fi.msg_type == ME_MSG_STORE_PROGRAM)
                        ? ME_SEND_TIMEOUT_BULK_MS : ME_SEND_TIMEOUT_CTRL_MS;

    if (!me_msgq_send(dest, &s_route_msg, timeout)) {
        ME_LOGE("route: circuit 0x%02X - %s dropped, destination queue full",
                fi.circuit_id, me_msg_type_name(fi.msg_type));
        if (wants_ack) {
            /* Telling the Web Application the frame was lost is the whole value
             * of the ack: a dropped program step or an ignored Start is
             * otherwise invisible until something downstream fails to happen. */
            send_ack(sys, tcp_fd, &fi, ME_ACK_VALUE_FAIL);
        }
        return;
    }

    /*
     * THE ACK MEANS QUEUED TO THE OWNING THREAD, not "done". That is the
     * strongest claim this thread can honestly make: the store or the command
     * executes on another thread and this one never learns its outcome. A real
     * limit of the ack's meaning, not an oversight - a full program buffer is
     * reported by the Data Manager's own log line, and whether a test actually
     * started is reported by Core Logic's.
     */
    if (wants_ack) {
        send_ack(sys, tcp_fd, &fi, ME_ACK_VALUE_OK);
    }
}

/*
 * Peel complete frames off the front of the reassembly buffer.
 *
 * me_frame_expected_len() returns 0 for frame types that carry no length field
 * anywhere - 0xAA in particular. For those there is no way to find the
 * boundary, so the remainder of what arrived is treated as one frame, which is
 * what the old firmware did for every frame type. It is correct whenever the
 * Web Application sends one frame per write.
 */
static void consume_rx_buffer(const me_system_t *sys, int tcp_fd)
{
    size_t off = 0;

    while ((s_rxlen - off) >= ME_FRAME_MIN_LEN) {
        const uint8_t *p         = &s_rxbuf[off];
        const size_t   available = s_rxlen - off;

        /*
         * me_frame_resolve_len() prefers the layout length and falls back to the
         * CRC when that does not verify. The fallback is not theoretical: on
         * 2026-08-12 a 10-byte 0xEE Start frame was split at 6 because the
         * layout table did not know about its Session ID. The frame was rejected
         * as BAD_CRC, and the 4 orphaned bytes then desynced every following
         * frame on the connection.
         */
        bool           scanned  = false;
        const uint32_t expected = me_frame_resolve_len(p, (uint32_t)available,
                                                       sys->cfg.crc_order,
                                                       &scanned);

        if (scanned) {
            /*
             * Loud on purpose. The frame was recovered, so nothing is broken
             * right now - but the layout table in frame_router.c is WRONG about
             * this frame type and should be corrected. Without this line the
             * recovery would be silent and the table would stay wrong forever.
             */
            ME_LOGW("route: start 0x%02X query 0x%02X - layout expected %u "
                    "byte(s), but the CRC says the frame is %u. RECOVERED, but "
                    "the length table in frame_router.c needs fixing for this "
                    "frame type",
                    p[ME_HDR_OFF_START], p[ME_HDR_OFF_QUERY_ID],
                    (unsigned)me_frame_expected_len(p, (uint32_t)available),
                    (unsigned)expected);
        }

        if (expected == 0u) {
            route_frame(sys, tcp_fd, p, (uint32_t)available);
            off = s_rxlen;
            break;
        }
        if (expected > available) {
            break; /* the rest of this frame has not arrived yet */
        }
        route_frame(sys, tcp_fd, p, expected);
        off += expected;
    }

    if (off > 0u) {
        s_rxlen -= off;
        if (s_rxlen > 0u) {
            memmove(s_rxbuf, &s_rxbuf[off], s_rxlen);
        }
    } else if (s_rxlen >= ME_RX_BUF_SIZE) {
        /* Nothing consumable and no room left: the stream is out of sync.
         * Discarding is better than wedging the connection forever. */
        ME_LOGE("route: reassembly buffer full with no complete frame - "
                "discarding %u bytes", (unsigned)s_rxlen);
        s_rxlen = 0;
    }
}

/* Drain g_q_comm and put each frame on its UDP port. */
static void drain_outbound(const me_system_t *sys)
{
    me_msgq_ack_event(&g_q_comm);

    while (me_msgq_recv(&g_q_comm, &s_out_msg, 0)) {
        switch (s_out_msg.type) {
        case ME_MSG_REALTIME_DATA:
            (void)me_udp_send_to(sys->udp_live_fd, &sys->udp_live_dest,
                                 s_out_msg.payload, s_out_msg.len);
            break;

        case ME_MSG_SESSION_DATA:
            (void)me_udp_send_to(sys->udp_session_fd, &sys->udp_session_dest,
                                 s_out_msg.payload, s_out_msg.len);
            break;

        default:
            ME_LOGW("comm: unexpected outbound message %s",
                    me_msg_type_name(s_out_msg.type));
            break;
        }
    }
}

/*
 * Re-attempt registration for any configured channel the server has not yet
 * admitted (Value 0x00 - typically "new device, awaiting operator approval
 * in the Web Application"). me_registry_is_registered() is the gate: a
 * circuit that already succeeded (0x01 or 0x02) is never touched again, so
 * this cannot regress into ADR-10's old "re-register a working circuit every
 * 30s" bug. See ADR-34.
 */
static void retry_pending_registrations(me_system_t *sys, int tcp_fd)
{
    const uint32_t now = now_ms();

    for (uint8_t i = 0; i < sys->cfg.channel_count; i++) {
        const uint8_t circuit_id =
            ME_CIRCUIT_ID(sys->cfg.secondary, sys->cfg.channels[i]);

        if (me_registry_is_registered(circuit_id)) {
            continue;
        }
        if (now - s_last_reg_attempt_ms[i] < ME_REG_RETRY_MS) {
            continue;
        }

        ME_LOGI("idle: retrying registration for circuit 0x%02X "
                "(secondary %u, channel %u) - not yet admitted",
                circuit_id, (unsigned)sys->cfg.secondary,
                (unsigned)sys->cfg.channels[i]);
        (void)do_registration(sys, tcp_fd, circuit_id);
        s_last_reg_attempt_ms[i] = now_ms();
    }
}

/*
 * Registered and idle: hold the connection open and watch all three sockets
 * plus the outbound queue's eventfd.
 *
 * A circuit's registration frame is re-sent from here ONLY while that circuit
 * is still unadmitted - retry_pending_registrations() above, gated on
 * me_registry_is_registered() at ME_REG_RETRY_MS intervals (ADR-34). Once a
 * circuit is admitted it is never re-registered again on this connection;
 * that "register once per successful circuit" rule remains structural,
 * enforced by the registry rather than a flag.
 * Returns when the peer disconnects or a stop is requested.
 */
static void idle_loop(me_system_t *sys, int tcp_fd)
{
    s_state = ME_COMM_IDLE;
    s_rxlen = 0; /* a new connection starts a new stream */

    ME_LOGI("state: %s - registered, holding connection, watching TCP, "
            "UDP %u/%u and the outbound queue",
            me_comm_state_name(s_state),
            ME_PORT_UDP_LIVE, ME_PORT_UDP_SESSION);

    while (!me_app_stop_requested()) {
        struct pollfd pfds[4];
        pfds[0].fd = tcp_fd;                 pfds[0].events = POLLIN; pfds[0].revents = 0;
        pfds[1].fd = sys->udp_live_fd;       pfds[1].events = POLLIN; pfds[1].revents = 0;
        pfds[2].fd = sys->udp_session_fd;    pfds[2].events = POLLIN; pfds[2].revents = 0;
        pfds[3].fd = me_msgq_fd(&g_q_comm);  pfds[3].events = POLLIN; pfds[3].revents = 0;

        /* nfds is 4 only when the queue really has an eventfd. A -1 fd is
         * ignored by poll(), but being explicit documents the dependency. */
        const nfds_t nfds = (pfds[3].fd >= 0) ? 4u : 3u;

        const int pr = poll(pfds, nfds, 500);
        if (pr < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("idle: poll failed: %s", strerror(errno));
            return;
        }

        /* Always drain: a send that raced the poll() must not wait for the next
         * wake-up, and a 1 Hz emitter would otherwise stutter. */
        drain_outbound(sys);

        /* Always check: retry_pending_registrations() no-ops in one pass over
         * s_last_reg_attempt_ms[] when nothing is due, so running it on every
         * wake-up (idle timeout or real traffic alike) is cheap and keeps the
         * ME_REG_RETRY_MS interval accurate even on a busy connection. */
        retry_pending_registrations(sys, tcp_fd);

        if (pr == 0) {
            continue;
        }

        if ((pfds[0].revents & (POLLERR | POLLHUP | POLLNVAL)) != 0) {
            ME_LOGW("idle: TCP connection lost");
            return;
        }

        if ((pfds[0].revents & POLLIN) != 0) {
            const ssize_t n = recv(tcp_fd, &s_rxbuf[s_rxlen],
                                   ME_RX_BUF_SIZE - s_rxlen, 0);
            if (n <= 0) {
                ME_LOGW("idle: TCP connection closed by peer");
                return;
            }
            ME_LOGI("idle: %d byte(s) on TCP", (int)n);
            me_hex_dump(ME_LOG_DEBUG, "inbound TCP data",
                        &s_rxbuf[s_rxlen], (size_t)n);
            s_rxlen += (size_t)n;
            consume_rx_buffer(sys, tcp_fd);
        }

        /* UDP 10000/10001 are outbound-only in this protocol. Anything
         * arriving here is unexpected, so log and discard rather than parse. */
        for (int i = 1; i <= 2; i++) {
            if ((pfds[i].revents & POLLIN) == 0) {
                continue;
            }
            uint8_t buf[512];
            const ssize_t n = recv(pfds[i].fd, buf, sizeof(buf), 0);
            if (n > 0) {
                ME_LOGW("idle: unexpected %d-byte datagram on UDP %u",
                        (int)n,
                        (i == 1) ? ME_PORT_UDP_LIVE : ME_PORT_UDP_SESSION);
                me_hex_dump(ME_LOG_DEBUG, "unexpected datagram", buf, (size_t)n);
            }
        }
    }
}

static void *comm_thread_main(void *arg)
{
    me_system_t *sys = (me_system_t *)arg;
    int backoff_ms = ME_BACKOFF_MIN_MS;

    ME_LOGI("comm thread: started");

    while (!me_app_stop_requested()) {
        s_state = ME_COMM_CONNECTING;
        ME_LOGI("state: %s to %s:%u", me_comm_state_name(s_state),
                sys->cfg.server_ip, sys->cfg.server_port);

        const int fd = me_tcp_connect(sys->cfg.server_ip,
                                      sys->cfg.server_port,
                                      sys->cfg.connect_timeout_ms);
        if (fd < 0) {
            if (me_app_stop_requested()) {
                break;
            }
            ME_LOGW("state: %s failed, retrying in %d ms",
                    me_comm_state_name(s_state), backoff_ms);
            interruptible_sleep_ms(backoff_ms);
            backoff_ms = (backoff_ms * 2 > ME_BACKOFF_MAX_MS) ? ME_BACKOFF_MAX_MS
                                                              : backoff_ms * 2;
            continue;
        }

        /*
         * One 0xDD registration per configured channel, sent serially over
         * this single TCP connection - the wire is one stream, so attempts
         * are serialized, but no channel's outcome gates another's: a
         * rejection on one channel does not stop the rest from being
         * attempted or from running. On a reconnect every configured
         * channel is re-registered; the server answers 0x02 (Already
         * Registered) for ones already admitted, which is success.
         */
        s_state = ME_COMM_REGISTERING;
        ME_LOGI("state: %s (%u channel(s))", me_comm_state_name(s_state),
                (unsigned)sys->cfg.channel_count);

        unsigned registered_count = 0u;
        for (uint8_t i = 0; i < sys->cfg.channel_count; i++) {
            const uint8_t circuit_id =
                ME_CIRCUIT_ID(sys->cfg.secondary, sys->cfg.channels[i]);
            const bool ok = do_registration(sys, fd, circuit_id);
            s_last_reg_attempt_ms[i] = now_ms();
            if (ok) {
                registered_count++;
            } else {
                ME_LOGW("state: %s failed for circuit 0x%02X "
                        "(secondary %u, channel %u)",
                        me_comm_state_name(ME_COMM_REGISTERING), circuit_id,
                        (unsigned)sys->cfg.secondary,
                        (unsigned)sys->cfg.channels[i]);
            }
        }

        if (registered_count > 0u) {
            s_registered = 1;
            backoff_ms   = ME_BACKOFF_MIN_MS; /* healthy again */
            idle_loop(sys, fd);
        } else {
            ME_LOGW("state: %s failed for every configured channel, "
                    "retrying in %d ms",
                    me_comm_state_name(ME_COMM_REGISTERING), backoff_ms);
        }

        me_tcp_close(fd);

        if (me_app_stop_requested()) {
            break;
        }

        interruptible_sleep_ms(backoff_ms);
        backoff_ms = (backoff_ms * 2 > ME_BACKOFF_MAX_MS) ? ME_BACKOFF_MAX_MS
                                                          : backoff_ms * 2;
    }

    s_state = ME_COMM_STOPPED;
    ME_LOGI("comm thread: stopped (state: %s)", me_comm_state_name(s_state));
    return NULL;
}

bool me_comm_thread_start(me_system_t *sys)
{
    s_state = ME_COMM_CONNECTING;
    s_rxlen = 0;

    const int rc = pthread_create(&s_thread, NULL, comm_thread_main, sys);
    if (rc != 0) {
        ME_LOGE("comm thread: pthread_create failed: %s", strerror(rc));
        return false;
    }
    s_thread_started = true;
    return true;
}

void me_comm_thread_join(void)
{
    if (s_thread_started) {
        (void)pthread_join(s_thread, NULL);
        s_thread_started = false;
    }
}
