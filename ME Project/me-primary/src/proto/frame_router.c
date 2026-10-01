/*
 * frame_router.c - classify an inbound frame by its start byte.
 */
#include "frame_router.h"

#include <string.h>

/* For ME_BATTERY_FRAME_LEN in the 0xAA length table below. Pure module including
 * a pure module - the 40-byte payload size stays defined in exactly one place
 * rather than being restated here as a literal. */
#include "battery_frame.h"

static bool reject(me_frame_info_t *out, const char *why)
{
    out->valid    = false;
    out->msg_type = ME_MSG_NONE;
    out->reject   = why;
    return false;
}

uint32_t me_frame_expected_len(const uint8_t *buf, uint32_t len)
{
    if (buf == NULL || len < ME_HDR_LEN) {
        return 0u;
    }

    switch (buf[ME_HDR_OFF_START]) {
    case ME_START_REGISTRATION:
        return ME_REG_RESPONSE_LEN;

    case ME_START_CONTROL:
        /*
         * Two of the six carry a 4-byte payload: Q5 an epoch, and Q1 a Session
         * ID. Q1 was believed payload-less until hardware proved otherwise on
         * 2026-08-12 - see me_frame_resolve_len(), which exists because of it.
         */
        switch (buf[ME_HDR_OFF_QUERY_ID]) {
        case ME_CTRL_SYNC_TIME: return ME_CTRL_SYNC_LEN;
        case ME_CTRL_START:     return ME_CTRL_START_LEN;
        default:                return ME_CTRL_BARE_LEN;
        }

    case ME_START_PROGRAM:
        if (buf[ME_HDR_OFF_QUERY_ID] == ME_QID_PROGRAM_DATA
            && len >= ME_PRG_OFF_STEPS) {
            const uint32_t declared =
                ((uint32_t)buf[ME_PRG_OFF_LENGTH] << 8)
                | (uint32_t)buf[ME_PRG_OFF_LENGTH + 1u];
            return ME_PRG_OFF_STEPS + declared + ME_REG_CRC_LEN;
        }
        /*
         * Q1 and Q3 are the two the board answers, so they are also the two
         * that now arrive mid-conversation and can share a read() with the
         * frame behind them. Their sizes are fixed by the layout - Q1 carries
         * no payload, Q3 carries a 16-bit count - so give the caller a real
         * boundary instead of "consume everything that arrived".
         */
        if (buf[ME_HDR_OFF_QUERY_ID] == ME_QID_PRG_IS_READY) {
            return ME_PRG_Q1_LEN;
        }
        if (buf[ME_HDR_OFF_QUERY_ID] == ME_QID_PRG_PACKET_COUNT) {
            return ME_PRG_Q3_LEN;
        }
        return 0u; /* Q2/Q5/Q6 encode no length and are not implemented */

    case ME_START_CONFIG:
        /*
         * 0xAA encodes no length either, but every query's size is fixed by its
         * layout and bm_config_v6.0.md now documents all six (section 2). This
         * returned 0 until 2026-08-12, which pushed every config frame onto
         * me_frame_resolve_len()'s CRC scan - correct, but it can only work
         * once the whole frame has arrived and could in principle stop at a
         * coincidental checksum inside a 40-byte payload.
         *
         * Q7-Q10 fall through to 0 on purpose: they are broadcast frames whose
         * header is 2 bytes, not 4, so their length is NOT derivable by the
         * arithmetic used here (section 9.1). Returning a confidently wrong
         * number would be worse than admitting ignorance and letting the CRC
         * decide.
         */
        switch (buf[ME_HDR_OFF_QUERY_ID]) {
        case ME_QID_CFG_IS_READY:
        case ME_QID_CFG_READ_FACTORY:
        case ME_QID_CFG_READ_MANUFACTURING:
        case ME_QID_CFG_READ_BATTERY:      return ME_CFG_BARE_LEN;
        case ME_QID_CFG_WRITE_BATTERY:     return ME_BATTERY_FRAME_LEN;
        case ME_QID_CFG_SYNC_TIME:         return ME_CFG_SYNC_LEN;
        default:                           return 0u;
        }

    default:
        /* 0xA0 calibration encodes no length anywhere in the frame. */
        return 0u;
    }
}

/*
 * True when the frame states its own length, so scanning for a CRC boundary
 * would be second-guessing the protocol rather than recovering from a gap in our
 * knowledge of it.
 *
 * 0xDD is fixed-size. 0xBB Q4 declares its payload length explicitly - and its
 * payload is program step data, in which a coincidental CRC match could place a
 * boundary mid-step. For both, a checksum failure is reported honestly.
 */
static bool length_is_declared(const uint8_t *buf)
{
    return buf[ME_HDR_OFF_START] == ME_START_REGISTRATION
        || (buf[ME_HDR_OFF_START] == ME_START_PROGRAM
            && buf[ME_HDR_OFF_QUERY_ID] == ME_QID_PROGRAM_DATA);
}

uint32_t me_frame_resolve_len(const uint8_t *buf, uint32_t avail,
                              me_crc_order_t order, bool *out_scanned)
{
    if (out_scanned != NULL) {
        *out_scanned = false;
    }
    if (buf == NULL || avail < ME_FRAME_MIN_LEN) {
        return 0u;
    }

    const uint32_t layout = me_frame_expected_len(buf, avail);

    /* The normal path: the table has an entry, and the frame checksums there. */
    if (layout >= ME_FRAME_MIN_LEN && layout <= avail
        && me_crc16_verify(buf, layout, order)) {
        return layout;
    }

    if (length_is_declared(buf)) {
        return layout;
    }

    /*
     * WHOLE READ. If the entire available buffer checksums, that IS a complete,
     * self-consistent frame - the single most likely reading of these bytes. Try
     * it before scanning, because a scan inside a payload can find a coincidental
     * CRC match at a shorter length and truncate a perfectly good frame. See
     * test_resolve_len_does_not_truncate_on_a_planted_short_crc.
     *
     * THIS IS DELIBERATELY NOT GATED ON layout == 0, and that gate was a bug.
     * It was written when 0xAA had no table entry, so "no entry" and "an entry
     * that failed to verify" could be treated as different states. Once 0xAA Q5
     * gained its real 46-byte entry (2026-08-12), a 12-byte 0xAA frame had a
     * layout of 46 - larger than what had arrived - so it skipped this step and
     * fell into the scan, which truncated it at a planted 6-byte CRC match. A
     * layout entry that OVERSHOOTS what arrived tells us nothing about where the
     * frame ends; it must not cost us this protection.
     *
     * Note the ordering still favours the layout: step 1 above already returned
     * for any frame whose table entry both fits and checksums, which is what
     * splits genuinely coalesced frames. Reaching here means that failed.
     */
    if (me_crc16_verify(buf, avail, order)) {
        /* Same rule as the scan below: only a NON-ZERO entry that disagrees is a
         * table error worth reporting. */
        if (out_scanned != NULL) {
            *out_scanned = (layout != 0u && layout != avail);
        }
        return avail;
    }

    /*
     * Recovery. Either the table is wrong about this frame type, or several
     * frames arrived in one read with no length field to split them. Find the
     * shortest prefix whose trailing two bytes are its own CRC.
     *
     * Shortest-first matters: a longer coincidental match would swallow the
     * following frame whole, while a shorter one can only ever truncate this one.
     *
     * This is now reached only when the safe interpretations above have already
     * failed, so a truncation here costs no more than the pre-existing behaviour
     * of failing outright.
     */
    for (uint32_t total = ME_FRAME_MIN_LEN; total <= avail; total++) {
        if (me_crc16_verify(buf, total, order)) {
            /*
             * Only a NON-ZERO layout that disagrees is a table error worth
             * reporting. Reporting layout == 0 would fire on every ordinary 0xAA
             * frame and bury real anomalies in the log used to debug hardware.
             */
            if (out_scanned != NULL) {
                *out_scanned = (layout != 0u && total != layout);
            }
            return total;
        }
    }

    /* Nothing checksums: corrupt, or still incomplete. Behave exactly as this
     * code did before the scan existed and let the caller report it. */
    return layout;
}

bool me_frame_classify(const uint8_t *buf, uint32_t len, me_frame_info_t *out)
{
    memset(out, 0, sizeof(*out));

    if (buf == NULL || len < ME_FRAME_MIN_LEN) {
        return reject(out, "frame shorter than a header plus CRC");
    }

    out->start = buf[ME_HDR_OFF_START];

    /*
     * 0xDD is the odd one out: its QueryID is byte 1 and it has no
     * CircuitNumber at byte 2, so the generic header fields do not apply.
     * The communication thread handles registration itself, synchronously,
     * so there is nothing to route - valid, but not a message.
     */
    if (out->start == ME_START_REGISTRATION) {
        out->valid    = true;
        out->msg_type = ME_MSG_NONE;
        return true;
    }

    out->device_id  = buf[ME_HDR_OFF_DEVICE];
    out->circuit_id = buf[ME_HDR_OFF_CIRCUIT];
    out->query_id   = buf[ME_HDR_OFF_QUERY_ID];

    switch (out->start) {
    case ME_START_PROGRAM:
        if (out->query_id == ME_QID_PROGRAM_DATA) {
            /* BB | dev | ckt | 04 | LEN_HI | LEN_LO | steps... | CRC[2] */
            if (len < (ME_PRG_OFF_STEPS + ME_REG_CRC_LEN)) {
                return reject(out, "0xBB Q4 shorter than its own header");
            }
            const uint32_t declared =
                ((uint32_t)buf[ME_PRG_OFF_LENGTH] << 8)
                | (uint32_t)buf[ME_PRG_OFF_LENGTH + 1u];

            /* Trusting this field without checking it is exactly how the old
             * firmware's memcpy could run past its buffer. */
            if ((ME_PRG_OFF_STEPS + declared + ME_REG_CRC_LEN) > len) {
                return reject(out, "0xBB Q4 declared length exceeds the frame");
            }
            out->msg_type = ME_MSG_STORE_PROGRAM;
            out->body_off = ME_PRG_OFF_STEPS;
            out->body_len = declared;
        } else {
            /* Q1 is-ready, Q2 metadata, Q5/Q6 read-back. Not handled in this
             * milestone, but recognised so they are logged as "not handled"
             * rather than as a protocol error. */
            out->msg_type = ME_MSG_NONE;
            out->body_off = ME_HDR_LEN;
            out->body_len = len - ME_HDR_LEN - ME_REG_CRC_LEN;
        }
        break;

    case ME_START_CONFIG:
        out->msg_type = (out->query_id == ME_QID_CFG_WRITE_BATTERY)
                        ? ME_MSG_STORE_BATTERY
                        : ME_MSG_STORE_CONFIG;
        out->body_off = ME_HDR_LEN;
        out->body_len = len - ME_HDR_LEN - ME_REG_CRC_LEN;
        break;

    case ME_START_CONTROL:
        /* The whole frame travels, CRC included: Core Logic re-verifies rather
         * than trusting that the router already did. */
        out->msg_type = ME_MSG_CONTROL;
        out->body_off = 0;
        out->body_len = len;
        break;

    case ME_START_CALIBRATION:
        /* Recognised so it is reported by name, not implemented. */
        out->msg_type = ME_MSG_NONE;
        out->body_off = ME_HDR_LEN;
        out->body_len = len - ME_HDR_LEN - ME_REG_CRC_LEN;
        break;

    default:
        return reject(out, "unrecognised start byte");
    }

    if (out->body_len > ME_MSG_PAYLOAD_MAX) {
        return reject(out, "payload larger than a queue message can carry");
    }

    out->valid = true;
    return true;
}
