/*
 * test_util.h - minimal assertion harness for host-side unit tests.
 *
 * No external framework: these tests must build with nothing but the host
 * compiler so they can run on the Windows dev laptop via build-native.ps1.
 */
#ifndef ME_TEST_UTIL_H
#define ME_TEST_UTIL_H

#include <stdio.h>
#include <string.h>

extern int g_tests_run;
extern int g_tests_failed;
extern const char *g_current_test;

#define TEST_CASE(name)                                                       \
    do {                                                                      \
        g_current_test = (name);                                              \
        g_tests_run++;                                                        \
    } while (0)

#define CHECK(cond)                                                           \
    do {                                                                      \
        if (!(cond)) {                                                        \
            g_tests_failed++;                                                 \
            printf("  FAIL [%s] %s:%d: %s\n",                                 \
                   g_current_test, __FILE__, __LINE__, #cond);                \
        }                                                                     \
    } while (0)

/* Integer equality with both values reported - a bare CHECK(a == b) tells you
 * nothing about what the value actually was, which is the first thing you want
 * when a byte offset is wrong. */
#define CHECK_EQ_U(expected, actual)                                          \
    do {                                                                      \
        unsigned long _e = (unsigned long)(expected);                         \
        unsigned long _a = (unsigned long)(actual);                           \
        if (_e != _a) {                                                       \
            g_tests_failed++;                                                 \
            printf("  FAIL [%s] %s:%d: %s\n"                                  \
                   "       expected 0x%lX (%lu), got 0x%lX (%lu)\n",          \
                   g_current_test, __FILE__, __LINE__, #actual,               \
                   _e, _e, _a, _a);                                           \
        }                                                                     \
    } while (0)

/* Byte-at-offset check. Names the offset in the failure so a shifted field
 * identifies itself instead of showing up as an opaque buffer diff. */
#define CHECK_BYTE(buf, off, expected)                                        \
    do {                                                                      \
        unsigned _e = (unsigned)(expected);                                   \
        unsigned _a = (unsigned)((buf)[(off)]);                               \
        if (_e != _a) {                                                       \
            g_tests_failed++;                                                 \
            printf("  FAIL [%s] %s:%d: %s[%d]\n"                              \
                   "       expected 0x%02X, got 0x%02X\n",                    \
                   g_current_test, __FILE__, __LINE__, #buf, (int)(off),      \
                   _e, _a);                                                   \
        }                                                                     \
    } while (0)

#define CHECK_STR(expected, actual)                                           \
    do {                                                                      \
        if (strcmp((expected), (actual)) != 0) {                              \
            g_tests_failed++;                                                 \
            printf("  FAIL [%s] %s:%d: %s\n"                                  \
                   "       expected [%s]\n"                                   \
                   "            got [%s]\n",                                  \
                   g_current_test, __FILE__, __LINE__, #actual,               \
                   (expected), (actual));                                     \
        }                                                                     \
    } while (0)

#define CHECK_MEM(buf, expected, len)                                         \
    do {                                                                      \
        if (memcmp((buf), (expected), (len)) != 0) {                          \
            g_tests_failed++;                                                 \
            printf("  FAIL [%s] %s:%d: %s mismatch over %d bytes\n",          \
                   g_current_test, __FILE__, __LINE__, #buf, (int)(len));     \
        }                                                                     \
    } while (0)

void run_crc16_tests(void);
void run_reg_frame_tests(void);
void run_log_tests(void);
void run_msgq_tests(void);
void run_circuit_store_tests(void);
void run_circuit_registry_tests(void);
void run_program_chain_tests(void);
void run_frame_router_tests(void);
void run_ack_frame_tests(void);
void run_program_frame_tests(void);
void run_control_frame_tests(void);
void run_battery_frame_tests(void);
void run_realtime_frame_tests(void);
void run_step_decode_tests(void);
void run_can_frame_tests(void);
void run_step_engine_tests(void);
void run_rpmsg_frame_tests(void);
void run_channel_list_tests(void);

#endif /* ME_TEST_UTIL_H */
