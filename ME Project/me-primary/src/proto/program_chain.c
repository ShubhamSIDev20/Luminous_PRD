/*
 * program_chain.c - the step-chain walker.
 */
#include "program_chain.h"

const char *me_chain_result_name(me_chain_result_t r)
{
    switch (r) {
    case ME_CHAIN_OK:              return "OK";
    case ME_CHAIN_NOT_FOUND:       return "NOT_FOUND";
    case ME_CHAIN_BAD_NEXT_INDEX:  return "BAD_NEXT_INDEX";
    case ME_CHAIN_BAD_END_SEQ:     return "BAD_END_SEQ";
    case ME_CHAIN_TRUNCATED:       return "TRUNCATED";
    default:                       return "UNKNOWN";
    }
}

static uint32_t read_u32_be(const uint8_t *p)
{
    return ((uint32_t)p[0] << 24) | ((uint32_t)p[1] << 16)
         | ((uint32_t)p[2] << 8)  | (uint32_t)p[3];
}

uint16_t me_chain_step_number(const uint8_t *step)
{
    return (uint16_t)(((uint16_t)step[ME_STEP_OFF_STEP_NO] << 8)
                      | step[ME_STEP_OFF_STEP_NO + 1u]);
}

uint8_t me_chain_step_operator(const uint8_t *step)
{
    return step[ME_STEP_OFF_OPERATOR];
}

/*
 * One pass of the walk.
 *
 * `want` is the step number to stop at, or 0 to walk the chain to its end
 * (which is how completeness and step counting are computed - one walker, not
 * three subtly different ones).
 *
 * *found_out receives the number of well-formed steps traversed.
 * *terminated_out is set when the walk ended on ME_STEP_TERMINATOR.
 */
static me_chain_result_t walk(const uint8_t *buf, uint32_t buf_len,
                              uint32_t want,
                              const uint8_t **step_out, uint32_t *step_len_out,
                              uint32_t *found_out, bool *terminated_out)
{
    uint32_t index    = 0;
    uint32_t step_no  = 0;
    bool     terminated = false;

    if (found_out != NULL)      { *found_out = 0; }
    if (terminated_out != NULL) { *terminated_out = false; }

    if (buf == NULL || buf_len < ME_STEP_MIN_LEN) {
        return ME_CHAIN_TRUNCATED;
    }

    while (index + ME_STEP_HEADER_LEN <= buf_len) {
        if (buf[index] != ME_STEP_START_1
            || buf[index + 1u] != ME_STEP_START_2) {
            /* Not a packet boundary. Skip a byte and keep looking - the old
             * firmware tolerated a stray prefix and so does this. */
            index++;
            continue;
        }

        const uint32_t start_of_this = index;
        const uint32_t next_index    = read_u32_be(&buf[index + ME_STEP_OFF_NEXT]);
        const bool     is_last       = (next_index == ME_STEP_TERMINATOR);
        const uint32_t start_of_next = is_last ? buf_len : next_index;

        /* A next offset outside the buffer means the chain disagrees with how
         * much data actually arrived. */
        if (start_of_next > buf_len) {
            return ME_CHAIN_BAD_NEXT_INDEX;
        }
        /* Backwards or self-referencing: without this the walk never ends. */
        if (!is_last && start_of_next <= start_of_this) {
            return ME_CHAIN_BAD_NEXT_INDEX;
        }

        const uint32_t packet_len = start_of_next - start_of_this;
        if (packet_len < ME_STEP_MIN_LEN) {
            return ME_CHAIN_BAD_END_SEQ;
        }
        if (buf[start_of_next - 2u] != ME_STEP_END_1
            || buf[start_of_next - 1u] != ME_STEP_END_2) {
            return ME_CHAIN_BAD_END_SEQ;
        }

        step_no++;

        if (want != 0u && step_no == want) {
            if (step_out != NULL)     { *step_out = &buf[start_of_this]; }
            if (step_len_out != NULL) { *step_len_out = packet_len; }
            if (found_out != NULL)    { *found_out = step_no; }
            return ME_CHAIN_OK;
        }

        if (is_last) {
            terminated = true;
            break;
        }
        index = start_of_next;
    }

    if (found_out != NULL)      { *found_out = step_no; }
    if (terminated_out != NULL) { *terminated_out = terminated; }
    return ME_CHAIN_NOT_FOUND;
}

me_chain_result_t me_chain_fetch_step(const uint8_t *buf, uint32_t buf_len,
                                      uint32_t step_no,
                                      const uint8_t **step_out,
                                      uint32_t *step_len_out)
{
    if (buf == NULL || buf_len < ME_STEP_MIN_LEN) {
        return ME_CHAIN_TRUNCATED;
    }
    if (step_no == 0u) {
        /* Steps are 1-based. Asking for 0 is a caller bug, not a short chain,
         * and must not be answered with step 1. */
        return ME_CHAIN_NOT_FOUND;
    }
    return walk(buf, buf_len, step_no, step_out, step_len_out, NULL, NULL);
}

bool me_chain_is_complete(const uint8_t *buf, uint32_t buf_len)
{
    bool terminated = false;
    (void)walk(buf, buf_len, 0u, NULL, NULL, NULL, &terminated);
    return terminated;
}

uint32_t me_chain_count_steps(const uint8_t *buf, uint32_t buf_len)
{
    uint32_t found = 0;
    (void)walk(buf, buf_len, 0u, NULL, NULL, &found, NULL);
    return found;
}
