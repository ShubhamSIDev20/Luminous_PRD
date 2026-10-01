/*
 * step_decode.h - decodes the SET/CCChg/STOP operator bodies out of a
 * chain-wrapped program step.
 *
 * PURE LOGIC: no sockets, no I/O, no platform headers. Built by both
 * build-native.ps1 and build.ps1, same as program_chain.h/.c.
 *
 * Byte layout source: Docs/specs/2026-08-14-step-execution-design.md §2.2,
 * cross-checked against stepData.c (old Secondary firmware, authoritative
 * for behaviour) and Ref Docs/program_packet_v0.12.md (byte-exact samples).
 * All multi-byte fields inside a step body are BIG-ENDIAN.
 */
#ifndef ME_STEP_DECODE_H
#define ME_STEP_DECODE_H

#include <stdbool.h>
#include <stdint.h>

/* Mirrors the wire's own cap (a count byte >15 is rejected, never folded to
 * 0 - the old firmware silently produced a step that never ends this way). */
#define ME_STEP_MAX_REG_PARAMS 15u

typedef enum {
    ME_OP_SET   = 0x0A,
    ME_OP_CCCHG = 0x01,
    ME_OP_STOP  = 0x0B,
} me_step_operator_t;

/* One entry of a CCChg step's trailing "registration parameters" list -
 * the per-step "when to log" trigger list, distinct from SET's "what to
 * log" bitmask above. Decoded fully; not yet acted on by step_engine. */
typedef struct {
    uint8_t registration_type; /* 0x21..0x2D */
    float   value;             /* engineering units; for TIME (0x21) this is
                                 * the decoded millisecond count cast to
                                 * float, matching the firmware's own cast */
} me_step_reg_param_t;

typedef struct {
    me_step_operator_t operator;
    uint16_t            step_number;

    /* SET only */
    uint16_t registration_type; /* 13-bit mask, already & 0x1FFF */

    /* CCChg only */
    float    nominal_current_a;
    bool     has_cutoff;
    bool     cutoff_inclusive;  /* true = '>=' (0x53), false = '>' (0x51) */
    uint32_t cutoff_time_ms;
    uint8_t  num_reg_params;
    me_step_reg_param_t reg_params[ME_STEP_MAX_REG_PARAMS];
} me_step_t;

typedef enum {
    ME_STEP_DECODE_OK = 0,
    ME_STEP_DECODE_UNSUPPORTED_OPERATOR,
    ME_STEP_DECODE_TRUNCATED,
    ME_STEP_DECODE_TOO_MANY_CUTOFFS,             /* wire count > 15 */
    ME_STEP_DECODE_MULTIPLE_CUTOFFS_UNSUPPORTED, /* wire count is 2..15 */
    ME_STEP_DECODE_UNSUPPORTED_CUTOFF_TYPE,      /* anything but TIME (0x39) */
    ME_STEP_DECODE_UNSUPPORTED_COMPARATOR,       /* anything but > or >= */
    ME_STEP_DECODE_UNSUPPORTED_ACTION_TYPE,      /* not one of the 6 documented values */
    ME_STEP_DECODE_TOO_MANY_REG_PARAMS,          /* wire count > 15 */
} me_step_decode_result_t;

const char *me_step_decode_result_name(me_step_decode_result_t r);

/*
 * `step` must be a pointer returned by me_chain_fetch_step(), and `step_len`
 * its matching length (including the AA55/55AA sentinels) - both already
 * validated by the chain walker, which this function trusts without
 * re-checking. On any result other than ME_STEP_DECODE_OK, *out is zeroed
 * but its contents should not be used.
 */
me_step_decode_result_t me_step_decode(const uint8_t *step, uint32_t step_len,
                                       me_step_t *out);

#endif /* ME_STEP_DECODE_H */
