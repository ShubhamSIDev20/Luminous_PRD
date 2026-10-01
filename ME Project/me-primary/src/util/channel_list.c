/* channel_list.c - see channel_list.h. */
#include "channel_list.h"

#include <stddef.h> /* NULL */

const char *me_channel_list_result_name(me_channel_list_result_t r)
{
    switch (r) {
    case ME_CHANNEL_LIST_OK:           return "OK";
    case ME_CHANNEL_LIST_EMPTY:        return "EMPTY";
    case ME_CHANNEL_LIST_TOO_MANY:     return "TOO_MANY";
    case ME_CHANNEL_LIST_OUT_OF_RANGE: return "OUT_OF_RANGE";
    case ME_CHANNEL_LIST_DUPLICATE:    return "DUPLICATE";
    case ME_CHANNEL_LIST_MALFORMED:    return "MALFORMED";
    default:                           return "UNKNOWN";
    }
}

me_channel_list_result_t me_channel_list_parse(const char *str,
                                               uint8_t *out,
                                               uint8_t *count_out)
{
    *count_out = 0u;
    if (str == NULL || str[0] == '\0') {
        return ME_CHANNEL_LIST_EMPTY;
    }

    const char *p = str;
    while (*p != '\0') {
        if (*count_out >= ME_MAX_CHANNELS) {
            return ME_CHANNEL_LIST_TOO_MANY;
        }
        if (*p < '0' || *p > '9') {
            return ME_CHANNEL_LIST_MALFORMED;
        }

        long v = 0;
        while (*p >= '0' && *p <= '9') {
            v = (v * 10) + (*p - '0');
            p++;
            /* Clamp rather than let a long run of digits overflow `long`:
             * any clamped value is still > ME_MAX_CHANNELS, so it still
             * resolves to OUT_OF_RANGE below instead of undefined behavior. */
            if (v > 255) { v = 256; }
        }

        if (v < 1 || v > (long)ME_MAX_CHANNELS) {
            return ME_CHANNEL_LIST_OUT_OF_RANGE;
        }

        const uint8_t ch = (uint8_t)v;
        for (uint8_t i = 0; i < *count_out; i++) {
            if (out[i] == ch) {
                return ME_CHANNEL_LIST_DUPLICATE;
            }
        }
        out[*count_out] = ch;
        (*count_out)++;

        if (*p == ',') {
            p++;
            if (*p == '\0') {
                return ME_CHANNEL_LIST_MALFORMED; /* trailing comma */
            }
        } else if (*p != '\0') {
            return ME_CHANNEL_LIST_MALFORMED;
        }
    }

    if (*count_out == 0u) {
        return ME_CHANNEL_LIST_EMPTY;
    }
    return ME_CHANNEL_LIST_OK;
}
