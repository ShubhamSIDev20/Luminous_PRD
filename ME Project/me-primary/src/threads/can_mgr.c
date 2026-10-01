/*
 * can_mgr.c - the CAN Data Manager thread.
 *
 * One poll() loop over two sources:
 *
 *   g_q_can eventfd   ME_MSG_CAN_TX from Core Logic -> wrap -> write()
 *   rpmsg fd          read() -> reassemble -> ACK counters, or
 *                     ME_MSG_CAN_DATA -> g_q_core
 *
 * Single-threaded by design: one owner of the fd means no locking, and ACKs
 * and streamed frames are handled by the same parser.
 *
 * See can_mgr.h and Docs/specs/2026-08-18-rpmsg-can-transport-design.md.
 */
#include "can_mgr.h"

#include <errno.h>
#include <poll.h>
#include <pthread.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

#include "../app_queues.h"
#include "../platform/rpmsg_link.h"
#include "../proto/can_frame.h"
#include "../proto/proto_defs.h"
#include "../proto/rpmsg_frame.h"
#include "../util/log.h"

/* Set to 1 to hex-dump every frame crossing the RPMsg link. Off by default:
 * at a 10 Hz poll across 8 Secondaries this is a lot of output. Mirrors
 * ME_CAN_DEBUG_LOG in core_logic.c. */
#define ME_RPMSG_DEBUG_LOG 0

/* How long poll() waits before looping, which is also how promptly the thread
 * notices a stop request. */
#define ME_CAN_POLL_TIMEOUT_MS 200

/*
 * Minimum gap between two physical CAN-FD transmissions for the same
 * Secondary/block, applied to READ/poll frames ONLY. A READ never carries
 * new data - it just re-announces the shadow buffer - so up to 4 channels
 * independently polling at any moment (step_engine.c, one me_exec_ctx_t per
 * channel) does not need 4 separate frames on the wire; the Secondary's one
 * reply already answers every active channel (handle_stream_frame()). Well
 * under step_engine.c's own ME_EXEC_POLL_PERIOD_MS (100 ms) and
 * ME_EXEC_RESPONSE_TIMEOUT_MS (200 ms), so a genuine per-channel retry is
 * never mistaken for a redundant send. See ADR-35.
 *
 * A SET is NEVER coalesced (see handle_can_tx): it carries a freshly decoded
 * program-step setpoint, and silently folding it into a later, unrelated
 * transmission is exactly what left ch3/ch4's setpoints with no proof of
 * ever reaching the M7 during hardware verification - see ADR-39.
 */
#define ME_CAN_TX_COALESCE_US 50000u

static me_msg_t  s_rx;   /* from g_q_can  - file scope: ~64 KB, not a local */
static me_msg_t  s_tx;   /* to   g_q_core - same reason                     */
static pthread_t s_thread;
static bool      s_started = false;

static int               s_fd = -1;
static me_rpmsg_stream_t s_stream;

/*
 * Which channels of each Secondary we have addressed, learned from TX.
 *
 * An inbound frame carries a CAN ID, and the ID's upper 6 bits give the
 * Secondary - but a 64-byte block frame holds four channel slots and the ID
 * says nothing about which of them are live. Core Logic addresses circuits by
 * the full 8-bit CircuitID, so the reply has to be attributed back to one.
 * Recording what we sent is the only information available to do that.
 *
 * Index: Secondary number 1..ME_MAX_SECONDARIES. Bit (channel - 1) set means
 * that channel has been addressed. Index 0 is unused - Secondary numbers are
 * 1-based, and folding an invalid 0 into a real slot is exactly the bug the
 * CircuitID rules warn about.
 */
static uint8_t s_active_ch[ME_MAX_SECONDARIES + 1u];

/*
 * Per-Secondary, per-block "last known good" SET_VALUES state. Channels 1-4
 * share Block 1; channels 5-8 share Block 2 (me_can_block_for_channel).
 * Four independently-timed channels sharing one 64-byte block frame cannot
 * each transmit their own frame with the other three channels' slots
 * zero-filled - a zero-filled slot means CMD_STO to whichever channel owns
 * it, which would stop an unrelated, currently-active channel. handle_can_tx
 * merges each SET frame's own slot into this buffer and transmits the whole
 * buffer instead, so every other channel's last commanded state survives.
 * A READ/poll frame reads this buffer back unmodified for the same reason -
 * see ADR-35 and the coalescing comment in handle_can_tx.
 *
 * Index 0 in the outer dimension is unused (Secondary numbers are 1-based,
 * same convention as s_active_ch). Index 0/1 in the middle dimension is
 * Block 1 / Block 2 (me_can_block_t is 1-based; store at block - 1).
 *
 * Zero-initialized at process start, which is the correct default: an
 * all-zero slot means CMD_STO / 0 A for a channel that has never sent a
 * setpoint, which is safe. me_exec_force_stop() (step_engine.c) always
 * sends an explicit CMD_STO before a channel goes idle, so a channel that
 * stops never leaves a stale non-zero setpoint behind in its own slot.
 */
static uint8_t s_set_shadow[ME_MAX_SECONDARIES + 1u][2][ME_CAN_FRAME_LEN];

/*
 * Per-Secondary, per-block "last known GENUINE feedback" buffer - separate
 * from s_set_shadow above on purpose (ADR-38). The M7 hands back every frame
 * WE transmit on the same CAN1 RX stream it uses for genuine Secondary
 * replies (RPMSG_PROTOCOL.md documents GET_FRAME length-80 only as
 * "Streamed CAN1 RX frame" - no field distinguishes origin), and since
 * handle_can_tx() transmits s_set_shadow's own bytes for BOTH SET and READ,
 * an echo of either is byte-identical to s_set_shadow at the moment it comes
 * back. handle_stream_frame() below only ever writes into THIS buffer when
 * an inbound frame's bytes differ from s_set_shadow - genuine feedback
 * (real measured voltage/current) essentially never coincides with our own
 * commanded setpoint, so that comparison is the echo filter. Core Logic is
 * handed this buffer, never the raw inbound frame - see forward_to_core().
 *
 * Same indexing as s_set_shadow: index 0 unused, block - 1 in the inner
 * dimension. Zero-initialized at start, same "safe default" reasoning as
 * s_set_shadow: nothing has arrived yet, and me_can_parse_feedback() on an
 * all-zero slot reads as 0 V / 0 A / state CMD_STO, which is not acted on
 * anywhere until a real SET_VALUES starts the channel running.
 */
static uint8_t s_read_feedback[ME_MAX_SECONDARIES + 1u][2][ME_CAN_FRAME_LEN];

/*
 * CLOCK_MONOTONIC timestamp (microseconds, same scale as s_tx_us) of the
 * last physical transmission for each Secondary/block - the coalescing
 * clock ME_CAN_TX_COALESCE_US is measured against. Same indexing as
 * s_set_shadow: index 0 unused, block - 1 in the inner dimension.
 */
static uint32_t s_last_tx_us[ME_MAX_SECONDARIES + 1u][2];

static uint32_t now_us(void)
{
    struct timespec ts;
    (void)clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint32_t)(((uint64_t)ts.tv_sec * 1000000u) + (uint64_t)(ts.tv_nsec / 1000L));
}

/* Counters, reported at shutdown next to the queue counters. */
static unsigned long s_tx_frames;
static unsigned long s_tx_dropped;
static unsigned long s_tx_coalesced;
static unsigned long s_ack_ok;
static unsigned long s_ack_fail;
static unsigned long s_rx_frames;
static unsigned long s_rx_unmatched;
static unsigned long s_rx_echo;

/* ---------------------------------------------------------------- TX ---- */

/*
 * Print one outbound SET_VALUES CAN-FD block frame exactly as it is about to
 * be written over RPMsg to the M7. SET is never coalesced now (ADR-39), so
 * every genuine setpoint update produces its own frame and its own log line
 * here - no need to also print READ/poll frames (CAN ID ...22), which never
 * carry new data and are just periodic shadow re-announcements (2026-08-21,
 * developer request: too much noise to be useful right now).
 * me_can_parse_feedback() decodes the Master's Set Voltage/Current/Command
 * fields too - see its doc comment: the slot layout is identical for both
 * directions.
 */
#define ME_CAN_TX_FRAME_LOG 1

static void log_tx_frame(const me_rpmsg_can_t *can, uint8_t secondary,
                          uint8_t block_ix, uint8_t function5)
{
#if ME_CAN_TX_FRAME_LOG
    if (function5 != ME_CAN_FUNC_SET) {
        return;
    }

    ME_LOGI("can: TX SET to M7 - CAN ID 0x%03X (S%u block %u), %u data byte(s)",
            (unsigned)can->can_id, (unsigned)secondary,
            (unsigned)(block_ix + 1u), (unsigned)can->data_len);

    me_hex_dump(ME_LOG_INFO, "CAN TX SET frame", can->data, ME_CAN_FRAME_LEN);

    for (uint8_t c = 1u; c <= ME_CAN_CHANNELS_PER_BLOCK; c++) {
        me_can_feedback_t sp;
        const uint8_t      channel_num = (uint8_t)((block_ix * ME_CAN_CHANNELS_PER_BLOCK) + c);
        if (!me_can_parse_feedback(can->data, c, &sp)) {
            continue;
        }
        ME_LOGI("can:   slot%u ch%u  set_V %.3f  set_A %.3f  cmd 0x%02X",
                (unsigned)(c - 1u), (unsigned)channel_num,
                (double)sp.feedback_voltage, (double)sp.feedback_current,
                (unsigned)sp.state);
    }
#else
    (void)can;
    (void)secondary;
    (void)block_ix;
    (void)function5;
#endif
}

static void handle_can_tx(const me_msg_t *m)
{
    me_rpmsg_can_t can;
    uint8_t        payload[ME_RPMSG_CAN_MSG_LEN];
    uint8_t        frame[ME_RPMSG_FRAME_MAX];
    size_t         n;

    const uint8_t secondary = ME_CIRCUIT_SECONDARY(m->circuit_id);
    const uint8_t channel   = ME_CIRCUIT_CHANNEL(m->circuit_id);

    if (m->len != ME_CAN_FRAME_LEN) {
        ME_LOGW("can: circuit 0x%02X - CAN_TX with %u bytes, expected %u",
                m->circuit_id, (unsigned)m->len, (unsigned)ME_CAN_FRAME_LEN);
        s_tx_dropped++;
        return;
    }
    /* 1-based nibbles: 0 is malformed, never slot 0. */
    if (secondary == 0u || secondary > ME_MAX_SECONDARIES
        || channel == 0u || channel > ME_MAX_CHANNELS) {
        ME_LOGW("can: circuit 0x%02X - malformed CircuitID, dropped",
                m->circuit_id);
        s_tx_dropped++;
        return;
    }

    /*
     * function5 is the low 5 bits of the CAN ID Core Logic already built
     * into m->offset (me_can_id() = circuit_num6 << 5 | function5) - no new
     * message field needed to tell a SET frame from a READ/poll frame.
     */
    const uint8_t function5 = (uint8_t)(m->offset & 0x1Fu);

    /*
     * Both SET and READ/poll frames go out as a real 64-byte block, and both
     * are routed through the shadow buffer for the same reason: a READ frame
     * is not exempt from the zero-fill danger ADR-32 fixed for SET (byte +8 =
     * 0x00 is an active CMD_STO to whichever channel owns that slot). Only
     * SET merges new data into shadow; a READ changes nothing, so it only
     * needs to read shadow back. By the time any channel's first poll fires,
     * its own SET has already merged into shadow (step_engine.c always sends
     * SET before scheduling a poll), so nothing is lost by not using the
     * poll frame's own near-empty content. See ADR-35.
     */
    uint8_t slot; /* unused here: me_can_merge_slot() recomputes it from channel_num */
    const me_can_block_t block    = me_can_block_for_channel(channel, &slot);
    const uint8_t         block_ix = (uint8_t)(block - 1u);
    (void)slot;
    uint8_t *shadow = s_set_shadow[secondary][block_ix];

    if (function5 == ME_CAN_FUNC_SET) {
        me_can_merge_slot(shadow, m->payload, channel);
    }

    /*
     * READ/poll frames coalesce to one physical CAN-FD frame per block (see
     * ME_CAN_TX_COALESCE_US above): shadow already holds every channel's
     * latest data, so a transmission moments later triggered by another
     * channel's poll carries this channel's data too. This channel has still
     * been meaningfully addressed even though nothing goes on the wire for
     * this particular call, so it must still be marked active: otherwise
     * handle_stream_frame()'s broadcast below would skip it and it would
     * never see the reply that a sibling channel's transmission produces.
     *
     * SET frames are exempt (ADR-39): a setpoint update from a newly decoded
     * program step must reach the M7 immediately, never silently absorbed
     * into a later, unrelated transmission.
     */
    const uint32_t now = now_us();
    if (function5 != ME_CAN_FUNC_SET
        && (now - s_last_tx_us[secondary][block_ix]) < ME_CAN_TX_COALESCE_US) {
        s_active_ch[secondary] |= (uint8_t)(1u << (channel - 1u));
        s_tx_coalesced++;
        return;
    }

    memset(&can, 0, sizeof(can));
    can.timestamp_ms = 0u;            /* M7 sequence counter; unused for SET */
    can.can_id       = m->offset;     /* 11-bit ID, set by Core Logic        */
    can.is_extended  = 0u;            /* standard identifier                 */
    can.is_fd        = 1u;
    can.brs          = 1u;
    can.esi          = 0u;
    can.data_len     = ME_CAN_PAYLOAD_BYTES;
    can.dlc          = me_rpmsg_dlc_for_len(can.data_len);
    memcpy(can.data, shadow, ME_CAN_PAYLOAD_BYTES);

    log_tx_frame(&can, secondary, block_ix, function5);

    if (!me_rpmsg_pack_can(&can, payload)) {
        ME_LOGW("can: circuit 0x%02X - payload pack failed", m->circuit_id);
        s_tx_dropped++;
        return;
    }

    n = me_rpmsg_wrap(ME_RPMSG_ACTION_WRITE_SET, ME_RPMSG_CMD_CAN_SET_FRAME,
                      ME_RPMSG_RESULT_REQUEST, payload, ME_RPMSG_CAN_MSG_LEN,
                      frame, sizeof(frame));
    if (n == 0u) {
        ME_LOGW("can: circuit 0x%02X - frame wrap failed", m->circuit_id);
        s_tx_dropped++;
        return;
    }

    if (s_fd < 0) {
        s_tx_dropped++;
        return; /* the open failure was logged once at startup */
    }

#if ME_RPMSG_DEBUG_LOG
    ME_LOGD("can: circuit 0x%02X - RPMsg TX, CAN ID 0x%03X, %u bytes",
            m->circuit_id, (unsigned)can.can_id, (unsigned)n);
    me_hex_dump(ME_LOG_DEBUG, "RPMsg TX", frame, n);
#endif

    if (!me_rpmsg_write_all(s_fd, frame, n)) {
        s_tx_dropped++;
        return;
    }

    /* Record only after the write succeeded: a channel we never actually
     * addressed must not attract replies. */
    s_active_ch[secondary]           |= (uint8_t)(1u << (channel - 1u));
    s_last_tx_us[secondary][block_ix] = now;
    s_tx_frames++;
}

static void drain_queue(void)
{
    me_msgq_ack_event(&g_q_can);

    while (me_msgq_recv(&g_q_can, &s_rx, 0)) {
        switch (s_rx.type) {
        case ME_MSG_CAN_TX:
            handle_can_tx(&s_rx);
            break;
        default:
            ME_LOGW("can: unexpected message %s",
                    me_msg_type_name(s_rx.type));
            break;
        }
    }
}

/* ---------------------------------------------------------------- RX ---- */

/* Forward one inbound 64-byte frame to Core Logic as `circuit_id`. */
static void forward_to_core(uint8_t circuit_id, const uint8_t *data64)
{
    memset(&s_tx, 0, sizeof(s_tx));
    s_tx.type       = ME_MSG_CAN_DATA;
    s_tx.circuit_id = circuit_id;
    s_tx.len        = ME_CAN_FRAME_LEN;
    memcpy(s_tx.payload, data64, ME_CAN_FRAME_LEN);

    if (!me_msgq_send(&g_q_core, &s_tx, ME_SEND_TIMEOUT_CTRL_MS)) {
        ME_LOGW("can: circuit 0x%02X - inbound frame dropped, core queue full",
                circuit_id);
    }
}

/*
 * Print one inbound CAN-FD frame exactly as the M7 handed it to us, before
 * any of the validation below can reject it.
 *
 * DIAGNOSTIC, on by default (ME_CAN_RX_FRAME_LOG) while the four-channel
 * behaviour is being characterised on hardware - set it to 0 to compile the
 * whole thing out. It logs deliberately early and unconditionally because
 * the frames that get REJECTED are the interesting ones: rx has been running
 * at ~2x tx, and a rejected "function 0x01" frame is the proof that the M7
 * loops our own transmissions back to us. A frame we merely drop with a
 * one-line warning tells us nothing about what was in it.
 *
 * The per-slot decode assumes the 4 slots are channels 1-4 (Block 1), which
 * is what Secondary 1 uses; a Block 2 frame would decode as channels 1-4
 * here. Fine for a diagnostic, not a basis for logic - see the Block 1/2
 * CAN-ID ambiguity noted in handle_stream_frame below.
 *
 * Off (2026-08-21): the bench M7 test rig sends the same static per-channel
 * V/A values on every reply regardless of the setpoint we sent (confirmed
 * with whoever owns that rig), so this frame's content carries no diagnostic
 * value right now. Flip back to 1 if a real Secondary reply needs checking.
 */
#define ME_CAN_RX_FRAME_LOG 0

static void log_rx_frame(const me_rpmsg_can_t *can)
{
#if ME_CAN_RX_FRAME_LOG
    ME_LOGI("can: RX from M7 - CAN ID 0x%03X (S%u func 0x%02X), %u data byte(s), "
            "dlc %u, fd %u, ts %lu",
            (unsigned)can->can_id,
            (unsigned)((can->can_id >> 5) & 0x3Fu),
            (unsigned)(can->can_id & 0x1Fu),
            (unsigned)can->data_len, (unsigned)can->dlc,
            (unsigned)can->is_fd, (unsigned long)can->timestamp_ms);

    if (can->data_len != ME_CAN_PAYLOAD_BYTES) {
        return; /* nothing trustworthy to dump or decode */
    }

    me_hex_dump(ME_LOG_INFO, "CAN RX frame", can->data, ME_CAN_FRAME_LEN);

    for (uint8_t c = 1u; c <= ME_CAN_CHANNELS_PER_BLOCK; c++) {
        me_can_feedback_t fb;
        if (!me_can_parse_feedback(can->data, c, &fb)) {
            continue;
        }
        ME_LOGI("can:   slot%u ch%u  V %.3f  A %.3f  state 0x%02X",
                (unsigned)(c - 1u), (unsigned)c,
                (double)fb.feedback_voltage, (double)fb.feedback_current,
                (unsigned)fb.state);
    }
#else
    (void)can;
#endif
}

static void handle_stream_frame(const uint8_t *payload)
{
    me_rpmsg_can_t can;
    uint8_t        secondary;
    uint8_t        function;
    uint8_t        mask;
    uint8_t        ch;

    if (!me_rpmsg_parse_can(payload, &can)) {
        ME_LOGW("can: inbound payload failed to parse");
        return;
    }

    secondary = (uint8_t)((can.can_id >> 5) & 0x3Fu);
    function  = (uint8_t)(can.can_id & 0x1Fu);

    log_rx_frame(&can);

    if (can.data_len != ME_CAN_PAYLOAD_BYTES) {
        ME_LOGW("can: inbound CAN ID 0x%03X carried %u bytes, expected %u",
                (unsigned)can.can_id, (unsigned)can.data_len,
                (unsigned)ME_CAN_PAYLOAD_BYTES);
        return;
    }
    /* The Secondary always answers on function 0x02, whichever function was
     * requested (Ref Docs/master_slave_can_v1.0.md, "Functions"). Anything
     * else on this link is either our own frame looped back or a foreign
     * node. */
    if (function != ME_CAN_FUNC_READ) {
        ME_LOGW("can: inbound CAN ID 0x%03X has function 0x%02X, not a "
                "Secondary reply - ignored",
                (unsigned)can.can_id, (unsigned)function);
        return;
    }
    if (secondary == 0u || secondary > ME_MAX_SECONDARIES) {
        ME_LOGW("can: inbound CAN ID 0x%03X maps to Secondary %u, out of range",
                (unsigned)can.can_id, (unsigned)secondary);
        return;
    }

    mask = s_active_ch[secondary];
    if (mask == 0u) {
        /* A reply from a Secondary we have not addressed. Expected briefly at
         * startup if the M7 was already streaming; persistent counts mean the
         * bus has a node we do not know about. */
        s_rx_unmatched++;
        return;
    }

    /* Channels 1-4 and 5-8 are two different block frames that share one CAN
     * ID (master_slave_can_v1.0.md). With channels live in both blocks a reply
     * cannot be attributed by ID alone. Unreachable while only channel 1 runs;
     * say so loudly rather than misparse silently if that ever changes. */
    if ((mask & 0x0Fu) != 0u && (mask & 0xF0u) != 0u) {
        ME_LOGW("can: Secondary %u has channels in both blocks - inbound "
                "frames are ambiguous by CAN ID, see design doc open "
                "question 2", (unsigned)secondary);
    }

    /* Same ambiguity as above, applied to pick which block's shadow/feedback
     * buffer this frame belongs to: block 1 if any of its channels are
     * active, else block 2. Correct today because only one block is ever
     * live per Secondary in practice (ADR-28) - not a new limitation. */
    const uint8_t block_ix = ((mask & 0x0Fu) != 0u) ? 0u : 1u;

    /*
     * Echo filter (ADR-38): handle_can_tx() transmits s_set_shadow's own
     * bytes for both SET and READ, so the M7 handing our own transmission
     * back to us (see the s_read_feedback comment above) is byte-identical
     * to s_set_shadow at this instant. Genuine feedback carries real
     * measured voltage/current and essentially never matches our own
     * commanded setpoint exactly. A match means this frame is our own echo -
     * discard it here, before it ever reaches s_read_feedback or Core Logic.
     */
    if (memcmp(can.data, s_set_shadow[secondary][block_ix], ME_CAN_FRAME_LEN) == 0) {
        s_rx_echo++;
        ME_LOGD("can: RX from M7 discarded as our own echo (matches "
                "s_set_shadow[%u][%u])", (unsigned)secondary,
                (unsigned)block_ix);
        return;
    }

#if ME_RPMSG_DEBUG_LOG
    /* The frame itself is already dumped by log_rx_frame() above; this adds
     * only the routing decision that dump cannot know. */
    ME_LOGD("can: RPMsg RX accepted, CAN ID 0x%03X, Secondary %u, channel "
            "mask 0x%02X", (unsigned)can.can_id, (unsigned)secondary,
            (unsigned)mask);
#endif

    s_rx_frames++;
    memcpy(s_read_feedback[secondary][block_ix], can.data, ME_CAN_FRAME_LEN);

    for (ch = 1u; ch <= ME_MAX_CHANNELS; ch++) {
        if ((mask & (uint8_t)(1u << (ch - 1u))) != 0u) {
            forward_to_core((uint8_t)((secondary << 4) | ch),
                            s_read_feedback[secondary][block_ix]);
        }
    }
}

static void handle_ack(const me_rpmsg_hdr_t *h)
{
    if (h->result == ME_RPMSG_RESULT_SUCCESS) {
        s_ack_ok++;
        return;
    }
    s_ack_fail++;
    ME_LOGW("can: M7 rejected command 0x%08lX (result 0x%02X)",
            (unsigned long)h->command, (unsigned)h->result);
}

static void drain_device(void)
{
    uint8_t raw[ME_RPMSG_STREAM_BUF_SZ];
    ssize_t n;

    for (;;) {
        n = read(s_fd, raw, sizeof(raw));
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            if (errno != EAGAIN && errno != EWOULDBLOCK) {
                ME_LOGE("can: read failed: %s", strerror(errno));
            }
            break;
        }
        if (n == 0) {
            break;
        }
        if (me_rpmsg_stream_push(&s_stream, raw, (size_t)n) < (size_t)n) {
            ME_LOGW("can: reassembly buffer full, bytes discarded");
        }
        /* A read() smaller than the buffer means the device had nothing more
         * to give, so stop rather than spin on a second blocking read(). */
        if ((size_t)n < sizeof(raw)) {
            break;
        }
    }

    for (;;) {
        me_rpmsg_hdr_t h;
        const uint8_t *payload = NULL;

        if (!me_rpmsg_stream_next(&s_stream, &h, &payload)) {
            break;
        }
        if (h.length == 0u) {
            handle_ack(&h);
        } else if (h.command == ME_RPMSG_CMD_CAN_GET_FRAME
                   && h.length == ME_RPMSG_CAN_MSG_LEN) {
            handle_stream_frame(payload);
        } else {
            ME_LOGW("can: unexpected frame, command 0x%08lX length %lu",
                    (unsigned long)h.command, (unsigned long)h.length);
        }
    }
}

/* ------------------------------------------------------------- thread ---- */

static void *can_mgr_main(void *arg)
{
    const char *path = me_rpmsg_device_path();

    (void)arg;

    memset(s_active_ch, 0, sizeof(s_active_ch));
    me_rpmsg_stream_init(&s_stream);

    s_fd = me_rpmsg_open(path);
    if (s_fd < 0) {
        /* Degraded, not fatal: registration and Web Application traffic keep
         * working, so the board is still reachable to diagnose the M7. */
        ME_LOGE("can manager thread: %s unavailable - M7 link is down, CAN "
                "frames will be counted and dropped", path);
    } else {
        ME_LOGI("can manager thread: started, M7 link on %s", path);
    }

    while (!me_app_stop_requested()) {
        struct pollfd pfds[2];
        nfds_t        nfds = 0u;
        int           qi   = -1;
        int           di   = -1;
        int           pr;

        const int qfd = me_msgq_fd(&g_q_can);
        if (qfd >= 0) {
            qi = (int)nfds;
            pfds[nfds].fd = qfd;
            pfds[nfds].events = POLLIN;
            pfds[nfds].revents = 0;
            nfds++;
        }
        if (s_fd >= 0) {
            di = (int)nfds;
            pfds[nfds].fd = s_fd;
            pfds[nfds].events = POLLIN;
            pfds[nfds].revents = 0;
            nfds++;
        }

        if (nfds == 0u) {
            /* No eventfd and no device: nothing to poll on. Fall back to a
             * blocking queue receive so the thread still drains and still
             * notices a stop. */
            if (me_msgq_recv(&g_q_can, &s_rx, ME_RECV_TIMEOUT_MS)) {
                if (s_rx.type == ME_MSG_CAN_TX) {
                    handle_can_tx(&s_rx);
                }
            }
            continue;
        }

        pr = poll(pfds, nfds, ME_CAN_POLL_TIMEOUT_MS);
        if (pr < 0) {
            if (errno == EINTR) {
                continue;
            }
            ME_LOGE("can: poll failed: %s", strerror(errno));
            break;
        }

        /* Always drain the queue: a send that raced the poll() must not wait
         * for the next wake-up. Same rule as the communication thread. */
        drain_queue();

        if (di >= 0 && (pfds[di].revents & (POLLIN | POLLHUP | POLLERR)) != 0) {
            drain_device();
        }
        (void)qi;
    }

    me_rpmsg_close(s_fd);
    s_fd = -1;

    ME_LOGI("can manager thread: stopped - tx %lu (dropped %lu, coalesced "
            "%lu), ack ok %lu fail %lu, rx %lu (unmatched %lu, echo %lu)",
            s_tx_frames, s_tx_dropped, s_tx_coalesced, s_ack_ok, s_ack_fail,
            s_rx_frames, s_rx_unmatched, s_rx_echo);
    return NULL;
}

bool me_can_mgr_start(void)
{
    const int rc = pthread_create(&s_thread, NULL, can_mgr_main, NULL);
    if (rc != 0) {
        ME_LOGE("can manager thread: pthread_create failed (%d)", rc);
        return false;
    }
    s_started = true;
    return true;
}

void me_can_mgr_join(void)
{
    if (s_started) {
        (void)pthread_join(s_thread, NULL);
        s_started = false;
    }
}
