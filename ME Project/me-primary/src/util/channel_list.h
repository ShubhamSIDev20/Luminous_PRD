/*
 * channel_list.h - parses a CLI channel list (e.g. "1,2,3,4") into a
 * validated, order-preserving array.
 *
 * PURE LOGIC: no sockets, no platform headers - host-testable via
 * build-native.ps1, same rule as proto/ and store/.
 */
#ifndef ME_CHANNEL_LIST_H
#define ME_CHANNEL_LIST_H

#include <stdint.h>

#include "../proto/proto_defs.h"

typedef enum {
    ME_CHANNEL_LIST_OK = 0,
    ME_CHANNEL_LIST_EMPTY,        /* the string had no entries at all    */
    ME_CHANNEL_LIST_TOO_MANY,     /* more than ME_MAX_CHANNELS entries   */
    ME_CHANNEL_LIST_OUT_OF_RANGE, /* an entry was not 1..ME_MAX_CHANNELS */
    ME_CHANNEL_LIST_DUPLICATE,    /* the same channel appeared twice     */
    ME_CHANNEL_LIST_MALFORMED     /* not a plain decimal integer list    */
} me_channel_list_result_t;

/*
 * Parses str (e.g. "1,2,3,4") into out[0..*count_out), preserving input
 * order. out must have room for ME_MAX_CHANNELS entries. Rejects instead of
 * truncating or silently deduplicating - out-of-range input must never be
 * folded onto a valid channel, same discipline ME_CIRCUIT_ID's callers
 * already follow for --secondary/--channel (see main.c parse_args).
 *
 * *count_out is always set (0 on any failure) so a caller that ignores the
 * return value still sees an empty list rather than garbage.
 */
me_channel_list_result_t me_channel_list_parse(const char *str,
                                               uint8_t *out,
                                               uint8_t *count_out);

const char *me_channel_list_result_name(me_channel_list_result_t r);

#endif /* ME_CHANNEL_LIST_H */
