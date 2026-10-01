/* step_decode.c - see step_decode.h for the format sources. */
#include "step_decode.h"

#include <string.h>

#include "program_chain.h"
#include "proto_defs.h"

#define WIRE_OP_CCCHG 0x01u
#define WIRE_OP_SET   0x0Au
#define WIRE_OP_STOP  0x0Bu

#define WIRE_SET_SKIP_LEN    1u /* "No. of Global Limit Parameters", unread */
#define WIRE_SET_REGTYPE_LEN 2u

const char *me_step_decode_result_name(me_step_decode_result_t r)
{
    switch (r) {
    case ME_STEP_DECODE_OK:                           return "OK";
    case ME_STEP_DECODE_UNSUPPORTED_OPERATOR:         return "UNSUPPORTED_OPERATOR";
    case ME_STEP_DECODE_TRUNCATED:                    return "TRUNCATED";
    case ME_STEP_DECODE_TOO_MANY_CUTOFFS:             return "TOO_MANY_CUTOFFS";
    case ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED: return "MULTIPLE_CUTOFFS_UNSUPPORTED";
    case ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE:      return "UNSUPPORTED_CUTOFF_TYPE";
    case ME_STEP_DECODE_UNSUPPORTED_COMPARATOR:       return "UNSUPPORTED_COMPARATOR";
    case ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE:      return "UNSUPPORTED_ACTION_TYPE";
    case ME_STEP_DECODE_TOO_MANY_REG_PARAMS:          return "TOO_MANY_REG_PARAMS";
    default:                                          return "UNKNOWN";
    }
}

#define WIRE_COND_TIME 0x39u
#define WIRE_LOGIC_GT  0x51u
#define WIRE_LOGIC_GTE 0x53u
#define WIRE_LOGIC_EQ  0x56u /* treated as inclusive, same as GTE - see below */

#define WIRE_ACTION_BLANK 0x00u
#define WIRE_ACTION_STO   0x0Bu
#define WIRE_ACTION_INT   0x0Eu
#define WIRE_ACTION_ERR   0x10u
#define WIRE_ACTION_MSG   0x11u
#define WIRE_ACTION_GOTO  0x09u

static uint16_t get_u16_be(const uint8_t *p)
{
    return (uint16_t)(((uint16_t)p[0] << 8) | (uint16_t)p[1]);
}

static uint32_t get_u32_be(const uint8_t *p)
{
    return ((uint32_t)p[0] << 24) | ((uint32_t)p[1] << 16)
         | ((uint32_t)p[2] << 8)  |  (uint32_t)p[3];
}

static float get_f32_be(const uint8_t *p)
{
    const uint32_t bits = get_u32_be(p);
    float v;
    memcpy(&v, &bits, sizeof(v));
    return v;
}

/* Width of a cutoff action's trailing value, in bytes. -1 = unrecognised -
 * see ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE's doc comment. */
static int action_value_width(uint8_t action)
{
    switch (action) {
    case WIRE_ACTION_BLANK:
    case WIRE_ACTION_STO:
    case WIRE_ACTION_INT:
        return 0;
    case WIRE_ACTION_ERR:
    case WIRE_ACTION_MSG:
        return 1;
    case WIRE_ACTION_GOTO:
        return 2;
    default:
        return -1;
    }
}

/* Decodes the cutoff-condition block starting at *offset. This iteration's
 * me_step_t has exactly one cutoff slot, so 2+ conditions on the wire are
 * rejected rather than silently truncated to the first - see
 * ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED. */
static me_step_decode_result_t decode_cutoffs(const uint8_t *step, uint32_t bound,
                                              uint32_t *offset, me_step_t *out)
{
    if (*offset + 1u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t num_cutoffs = step[*offset];
    (*offset)++;

    if (num_cutoffs > 15u) { return ME_STEP_DECODE_TOO_MANY_CUTOFFS; }
    if (num_cutoffs > 1u)  { return ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED; }
    if (num_cutoffs == 0u) { return ME_STEP_DECODE_OK; } /* has_cutoff already false */

    if (*offset + 2u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t condition = step[*offset];
    const uint8_t logic     = step[*offset + 1u];
    *offset += 2u;

    if (condition != WIRE_COND_TIME) { return ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE; }
    if (logic != WIRE_LOGIC_GT && logic != WIRE_LOGIC_GTE && logic != WIRE_LOGIC_EQ) {
        return ME_STEP_DECODE_UNSUPPORTED_COMPARATOR;
    }

    if (*offset + 4u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    out->cutoff_time_ms   = get_u32_be(&step[*offset]); /* TIME limit: plain ms, not float */
    /* EQ is decoded as inclusive (fires on step_run_ms >= limit), same as GTE,
     * per the developer's decision (2026-08-17): the tick loop's delta is the
     * real wall-clock gap between service_engines() calls, not a fixed 10 ms
     * step, so step_run_ms can skip past a literal target value entirely.
     * Firing "on or after" is what a real cutoff needs to mean here - a
     * charge/discharge step must still stop even if the exact millisecond was
     * never observed. */
    out->cutoff_inclusive = (logic == WIRE_LOGIC_GTE || logic == WIRE_LOGIC_EQ);
    out->has_cutoff       = true;
    *offset += 4u;

    if (*offset + 1u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t action = step[*offset];
    (*offset)++;

    const int width = action_value_width(action);
    if (width < 0) { return ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE; }
    if (*offset + (uint32_t)width > bound) { return ME_STEP_DECODE_TRUNCATED; }
    *offset += (uint32_t)width; /* value itself unused - only BLANK is acted on */

    return ME_STEP_DECODE_OK;
}

/* Decodes the trailing "No. of Registration parameters" block. ALWAYS
 * present on CCChg-family operators, even when the count is 0 - confirmed
 * both against stepData.c's default: case and Program Packet V0.12.xlsx
 * step 2's own byte 21 (see Docs/specs/2026-08-14-step-execution-design.md
 * §2.2's corrected write-up). */
static me_step_decode_result_t decode_reg_params(const uint8_t *step, uint32_t bound,
                                                  uint32_t *offset, me_step_t *out)
{
    if (*offset + 1u > bound) { return ME_STEP_DECODE_TRUNCATED; }
    const uint8_t count = step[*offset];
    (*offset)++;

    if (count > ME_STEP_MAX_REG_PARAMS) { return ME_STEP_DECODE_TOO_MANY_REG_PARAMS; }
    out->num_reg_params = count;

    for (uint8_t i = 0; i < count; i++) {
        if (*offset + 5u > bound) { return ME_STEP_DECODE_TRUNCATED; }
        out->reg_params[i].registration_type = step[*offset];
        out->reg_params[i].value             = get_f32_be(&step[*offset + 1u]);
        *offset += 5u;
    }
    return ME_STEP_DECODE_OK;
}

me_step_decode_result_t me_step_decode(const uint8_t *step, uint32_t step_len,
                                       me_step_t *out)
{
    memset(out, 0, sizeof(*out));

    /* me_chain_fetch_step() already guarantees step_len >= ME_STEP_MIN_LEN
     * and valid AA55/55AA sentinels - trusted, not re-checked here. */
    const uint32_t bound = step_len - 2u; /* exclude the trailing 55 AA */

    out->step_number = me_chain_step_number(step);
    const uint8_t raw_op = me_chain_step_operator(step);
    uint32_t offset = ME_STEP_HEADER_LEN; /* 9 - first byte after the operator */

    switch (raw_op) {
    case WIRE_OP_SET:
        out->operator = ME_OP_SET;
        if (offset + WIRE_SET_SKIP_LEN + WIRE_SET_REGTYPE_LEN > bound) {
            return ME_STEP_DECODE_TRUNCATED;
        }
        offset += WIRE_SET_SKIP_LEN;
        out->registration_type = get_u16_be(&step[offset]) & 0x1FFFu;
        return ME_STEP_DECODE_OK;

    case WIRE_OP_STOP:
        out->operator = ME_OP_STOP;
        return ME_STEP_DECODE_OK; /* no body at all */

    case WIRE_OP_CCCHG: {
        out->operator = ME_OP_CCCHG;
        if (offset + 4u > bound) { return ME_STEP_DECODE_TRUNCATED; }
        out->nominal_current_a = get_f32_be(&step[offset]);
        offset += 4u;

        const me_step_decode_result_t r = decode_cutoffs(step, bound, &offset, out);
        if (r != ME_STEP_DECODE_OK) { return r; }

        return decode_reg_params(step, bound, &offset, out);
    }

    default:
        return ME_STEP_DECODE_UNSUPPORTED_OPERATOR;
    }
}
