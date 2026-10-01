/*
 * log.h - timestamped logging and hex dumps.
 *
 * The hex dump is the primary diagnostic for this milestone: verification
 * happens against the real Web Application with no decoder on the board side,
 * so a rejected frame must be diagnosable from the console alone.
 */
#ifndef ME_LOG_H
#define ME_LOG_H

#include <stddef.h>
#include <stdint.h>

typedef enum {
    ME_LOG_DEBUG = 0,
    ME_LOG_INFO,
    ME_LOG_WARN,
    ME_LOG_ERROR
} me_log_level_t;

void me_log_set_level(me_log_level_t level);

void me_log(me_log_level_t level, const char *fmt, ...);

#define ME_LOGD(...) me_log(ME_LOG_DEBUG, __VA_ARGS__)
#define ME_LOGI(...) me_log(ME_LOG_INFO, __VA_ARGS__)
#define ME_LOGW(...) me_log(ME_LOG_WARN, __VA_ARGS__)
#define ME_LOGE(...) me_log(ME_LOG_ERROR, __VA_ARGS__)

/*
 * Format one line of a hex dump: up to 16 bytes at the given offset, as
 *
 *   "0000  DD 01 1C 01 11 42 54 53  2D 36 30 30 00 00 00 00  |.....BTS-600....|"
 *
 * Pure formatting with no I/O, so the dump format itself is unit-testable.
 * Writes a NUL-terminated string; out_sz must be at least ME_HEX_LINE_SIZE.
 */
#define ME_HEX_LINE_SIZE 96u

void me_hex_line(const uint8_t *data, size_t len, size_t offset,
                 char *out, size_t out_sz);

/* Dump an entire buffer, one 16-byte line at a time, with a caption. */
void me_hex_dump(me_log_level_t level, const char *caption,
                 const uint8_t *data, size_t len);

#endif /* ME_LOG_H */
