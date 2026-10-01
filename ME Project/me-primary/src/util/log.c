/*
 * log.c - timestamped logging and hex dumps.
 *
 * Output goes to stderr so it survives stdout redirection and appears
 * unbuffered in the Docker container's console.
 */
#include "log.h"

#include <stdarg.h>
#include <stdio.h>
#include <string.h>
#include <time.h>

#define ME_HEX_BYTES_PER_LINE 16u
#define ME_HEX_GROUP_SIZE     8u

static me_log_level_t s_level = ME_LOG_INFO;

void me_log_set_level(me_log_level_t level)
{
    s_level = level;
}

static const char *level_tag(me_log_level_t level)
{
    switch (level) {
    case ME_LOG_DEBUG: return "DEBUG";
    case ME_LOG_INFO:  return "INFO ";
    case ME_LOG_WARN:  return "WARN ";
    case ME_LOG_ERROR: return "ERROR";
    default:           return "?????";
    }
}

void me_log(me_log_level_t level, const char *fmt, ...)
{
    if (level < s_level) {
        return;
    }

    char stamp[32];
    const time_t now = time(NULL);
    struct tm tm_buf;
    struct tm *tm_now = NULL;

#if defined(_WIN32)
    if (localtime_s(&tm_buf, &now) == 0) {
        tm_now = &tm_buf;
    }
#else
    tm_now = localtime_r(&now, &tm_buf);
#endif

    if (tm_now == NULL || strftime(stamp, sizeof(stamp), "%H:%M:%S", tm_now) == 0) {
        stamp[0] = '\0';
    }

    fprintf(stderr, "[%s] %s  ", stamp, level_tag(level));

    va_list ap;
    va_start(ap, fmt);
    vfprintf(stderr, fmt, ap);
    va_end(ap);

    fputc('\n', stderr);
    fflush(stderr);
}

void me_hex_line(const uint8_t *data, size_t len, size_t offset,
                 char *out, size_t out_sz)
{
    if (out == NULL || out_sz < ME_HEX_LINE_SIZE) {
        if (out != NULL && out_sz > 0) {
            out[0] = '\0';
        }
        return;
    }

    size_t pos = 0;

    pos += (size_t)snprintf(&out[pos], out_sz - pos, "%04X  ",
                            (unsigned)(offset & 0xFFFFu));

    /* Hex column. Absent bytes get three spaces so a short final line keeps
     * the ASCII column in the same place as every full line above it. */
    for (size_t i = 0; i < ME_HEX_BYTES_PER_LINE; i++) {
        if (i == ME_HEX_GROUP_SIZE) {
            out[pos++] = ' ';
        }
        if (i < len) {
            pos += (size_t)snprintf(&out[pos], out_sz - pos, "%02X ", data[i]);
        } else {
            out[pos++] = ' ';
            out[pos++] = ' ';
            out[pos++] = ' ';
        }
    }

    out[pos++] = ' ';
    out[pos++] = '|';

    for (size_t i = 0; i < len && i < ME_HEX_BYTES_PER_LINE; i++) {
        const uint8_t c = data[i];
        out[pos++] = (c >= 0x20u && c < 0x7Fu) ? (char)c : '.';
    }

    out[pos++] = '|';
    out[pos]   = '\0';
}

void me_hex_dump(me_log_level_t level, const char *caption,
                 const uint8_t *data, size_t len)
{
    if (level < s_level) {
        return;
    }

    me_log(level, "%s (%u bytes)", caption, (unsigned)len);

    for (size_t off = 0; off < len; off += ME_HEX_BYTES_PER_LINE) {
        size_t chunk = len - off;
        if (chunk > ME_HEX_BYTES_PER_LINE) {
            chunk = ME_HEX_BYTES_PER_LINE;
        }
        char line[ME_HEX_LINE_SIZE];
        me_hex_line(&data[off], chunk, off, line, sizeof(line));
        fprintf(stderr, "        %s\n", line);
    }
    fflush(stderr);
}
