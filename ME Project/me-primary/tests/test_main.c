/*
 * test_main.c - host-side unit test runner.
 *
 * Covers the pure protocol logic only (CRC + frame pack/parse). The socket
 * layer is deliberately not covered here: it requires hardware-in-the-loop
 * testing against the real board and Web Application.
 */
#include <stdio.h>

#include "test_util.h"

int g_tests_run = 0;
int g_tests_failed = 0;
const char *g_current_test = "(none)";

int main(void)
{
    printf("ME Primary - protocol unit tests\n");
    printf("================================\n");

    run_crc16_tests();
    run_reg_frame_tests();
    run_log_tests();
    run_msgq_tests();
    run_program_chain_tests();
    run_frame_router_tests();
    run_ack_frame_tests();
    run_program_frame_tests();
    run_control_frame_tests();
    run_battery_frame_tests();
    run_realtime_frame_tests();
    run_circuit_store_tests();
    run_circuit_registry_tests();
    run_step_decode_tests();
    run_can_frame_tests();
    run_step_engine_tests();
    run_rpmsg_frame_tests();
    run_channel_list_tests();

    printf("--------------------------------\n");
    printf("%d checks run, %d failed\n", g_tests_run, g_tests_failed);

    if (g_tests_failed != 0) {
        printf("RESULT: FAIL\n");
        return 1;
    }
    printf("RESULT: PASS\n");
    return 0;
}
