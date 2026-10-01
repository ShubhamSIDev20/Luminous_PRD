/*
 * main.c - ME Primary Board application entry point.
 *
 * Milestone: demonstrate TCP/IP communication with the Web Application by
 * completing a device registration handshake on TCP 9999.
 *
 * The board is the TCP client. The Web Application is the server.
 */
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "app_queues.h"
#include "store/circuit_registry.h"
#include "store/circuit_store.h"
#include "sys_init.h"
#include "threads/can_mgr.h"
#include "threads/comm_thread.h"
#include "threads/core_logic.h"
#include "threads/data_mgr.h"
#include "threads/post_reg.h"
#include "util/channel_list.h"

static void on_signal(int signo)
{
    (void)signo;
    /* Async-signal-safe: sets a flag only. Needed because docker stop sends
     * SIGTERM, and an unhandled SIGTERM in PID 1 means an ungraceful kill.
     * All four threads poll this flag, so one write stops the process. */
    me_app_request_stop();
}

static void install_signal_handlers(void)
{
    struct sigaction sa;
    memset(&sa, 0, sizeof(sa));
    sa.sa_handler = on_signal;
    sigemptyset(&sa.sa_mask);
    sa.sa_flags = 0; /* no SA_RESTART: poll() should return EINTR promptly */

    (void)sigaction(SIGINT, &sa, NULL);
    (void)sigaction(SIGTERM, &sa, NULL);

    /* A peer that vanishes mid-send must not kill the process. Sends use
     * MSG_NOSIGNAL as well; this is belt and braces. */
    signal(SIGPIPE, SIG_IGN);
}

static void usage(const char *argv0)
{
    fprintf(stderr,
        "ME Primary Board - device registration demo\n"
        "\n"
        "Usage: %s --server <ip> [options]\n"
        "\n"
        "Required:\n"
        "  --server <ip>       Web Application IPv4 address (dotted quad)\n"
        "\n"
        "Options:\n"
        "  --port <n>          TCP command port           (default %u)\n"
        "  --iface <name>      Interface for IP and MAC   (default eth0)\n"
        "  --device <n>        Device number              (default 1)\n"
        "  --secondary <n>     CircuitID upper nibble     (default 1)\n"
        "  --channels <list>   Comma-separated channels, e.g. 1,2,3,4 (default 1)\n"
        "  --core <n>          Dedicated CPU for Core Logic (default 3, -1 disables)\n"
        "  --name <str>        Device name, max 16 chars  (default BTS-600)\n"
        "  --crc-order <le|be> Request/response CRC order\n"
        "                      (default %s)\n"
        "  --timeout <ms>      Response timeout           (default 5000)\n"
        "  --verbose           Enable debug logging\n"
        "  --help              Show this message\n"
        "\n"
        "Note: the container must run with --network host, or the IP and MAC\n"
        "placed in the registration payload will be the container's, not the\n"
        "board's, and the Web Application will not be able to reach back.\n",
        argv0, (unsigned)ME_PORT_TCP_CMD,
        me_crc_order_name(ME_CRC_ORDER_DEFAULT));
}

static bool parse_args(int argc, char **argv, me_config_t *cfg)
{
    me_config_defaults(cfg);

    for (int i = 1; i < argc; i++) {
        const char *a = argv[i];
        const bool has_value = (i + 1 < argc);

#define NEED_VALUE()                                                          \
    do {                                                                      \
        if (!has_value) {                                                     \
            fprintf(stderr, "error: %s requires a value\n", a);               \
            return false;                                                     \
        }                                                                     \
    } while (0)

        if (strcmp(a, "--help") == 0 || strcmp(a, "-h") == 0) {
            usage(argv[0]);
            exit(0);
        } else if (strcmp(a, "--server") == 0) {
            NEED_VALUE();
            snprintf(cfg->server_ip, sizeof(cfg->server_ip), "%s", argv[++i]);
        } else if (strcmp(a, "--port") == 0) {
            NEED_VALUE();
            cfg->server_port = (uint16_t)atoi(argv[++i]);
        } else if (strcmp(a, "--iface") == 0) {
            NEED_VALUE();
            snprintf(cfg->iface, sizeof(cfg->iface), "%s", argv[++i]);
        } else if (strcmp(a, "--device") == 0) {
            NEED_VALUE();
            cfg->device_id = (uint8_t)atoi(argv[++i]);
        } else if (strcmp(a, "--secondary") == 0) {
            NEED_VALUE();
            cfg->secondary = (uint8_t)atoi(argv[++i]);
        } else if (strcmp(a, "--channels") == 0) {
            NEED_VALUE();
            uint8_t parsed[ME_MAX_CHANNELS];
            uint8_t parsed_count = 0;
            const me_channel_list_result_t r =
                me_channel_list_parse(argv[++i], parsed, &parsed_count);
            if (r != ME_CHANNEL_LIST_OK) {
                fprintf(stderr, "error: --channels invalid (%s): \"%s\"\n\n",
                        me_channel_list_result_name(r), argv[i]);
                return false;
            }
            memcpy(cfg->channels, parsed, parsed_count);
            cfg->channel_count = parsed_count;
        } else if (strcmp(a, "--core") == 0) {
            NEED_VALUE();
            cfg->core_affinity = atoi(argv[++i]);
        } else if (strcmp(a, "--name") == 0) {
            NEED_VALUE();
            snprintf(cfg->device_name, sizeof(cfg->device_name), "%s", argv[++i]);
        } else if (strcmp(a, "--crc-order") == 0) {
            NEED_VALUE();
            if (!me_crc_order_parse(argv[++i], &cfg->crc_order)) {
                fprintf(stderr, "error: --crc-order must be 'le' or 'be'\n");
                return false;
            }
        } else if (strcmp(a, "--timeout") == 0) {
            NEED_VALUE();
            cfg->response_timeout_ms = atoi(argv[++i]);
        } else if (strcmp(a, "--verbose") == 0) {
            cfg->verbose = true;
        } else {
            fprintf(stderr, "error: unknown argument '%s'\n", a);
            return false;
        }
#undef NEED_VALUE
    }

    if (cfg->server_ip[0] == '\0') {
        fprintf(stderr, "error: --server is required\n\n");
        return false;
    }

    /*
     * ME_CIRCUIT_ID masks each nibble with 0x0F, so an out-of-range value does
     * not fail - it TRUNCATES. --secondary 99 would silently become Secondary
     * 3 (99 & 0x0F). That was cosmetic when the CircuitID only labelled the
     * registration frame; now it also decides which circuit's data the board
     * will accept at all, so a typo would register and gate on the wrong
     * circuit with nothing in the log to say so. Reject instead.
     */
    if (cfg->secondary < 1u || cfg->secondary > ME_MAX_SECONDARIES) {
        fprintf(stderr, "error: --secondary must be 1-%u (got %u)\n\n",
                (unsigned)ME_MAX_SECONDARIES, (unsigned)cfg->secondary);
        return false;
    }
    return true;
}

int main(int argc, char **argv)
{
    me_config_t cfg;
    if (!parse_args(argc, argv, &cfg)) {
        usage(argv[0]);
        return 2;
    }

    me_log_set_level(cfg.verbose ? ME_LOG_DEBUG : ME_LOG_INFO);

    ME_LOGI("ME Primary Board - device registration demo");

    install_signal_handlers();

    /*
     * Static, not a local: me_system_t is small, but making it static removes
     * any doubt that it outlives the four threads that hold a pointer to it.
     */
    static me_system_t sys;
    if (!me_sys_init(&sys, &cfg)) {
        ME_LOGE("system init failed - exiting");
        return 1;
    }

    if (!me_queues_init()) {
        ME_LOGE("queue init failed - exiting");
        me_sys_shutdown(&sys);
        return 1;
    }
    me_store_init();
    me_registry_init();
    me_post_reg_init(cfg.device_id, cfg.crc_order);

    /*
     * State the admission policy once, at the top of every run's log. Until
     * the CAN-side handshake lands, only the operator-configured
     * Secondary/Channels can ever register, and frames for any other
     * circuit are dropped. Without this line that policy is invisible until
     * something goes wrong and someone reads a "not registered" warning with
     * no idea it was expected.
     */
    {
        char adm_chlist[64] = { 0 };
        size_t adm_len = 0;
        for (uint8_t i = 0; i < cfg.channel_count; i++) {
            adm_len += (size_t)snprintf(adm_chlist + adm_len, sizeof(adm_chlist) - adm_len,
                                        "%s%u", (i == 0) ? "" : ",",
                                        (unsigned)cfg.channels[i]);
        }
        ME_LOGI("admission control: only secondary %u, channels [%s] will be "
                "handled - a frame for any other circuit is dropped until "
                "CAN-side registration is implemented",
                (unsigned)cfg.secondary, adm_chlist);
    }

    /*
     * START ORDER MATTERS. Consumers first, the communication thread LAST, so
     * that no frame can arrive before the thread that owns it exists. A frame
     * routed to a queue nobody is draining would sit there until the queue
     * filled and then be dropped - with a log line, but for no good reason.
     */
    bool ok = me_data_mgr_start();
    if (ok) { ok = me_can_mgr_start(); }
    if (ok) { ok = me_core_logic_start(&sys); }
    if (ok) { ok = me_comm_thread_start(&sys); }

    if (!ok) {
        ME_LOGE("thread startup failed - shutting down");
        me_app_request_stop();
    }

    /* Every thread polls the stop flag, so joining in any order terminates. */
    me_comm_thread_join();
    me_core_logic_join();
    me_can_mgr_join();
    me_data_mgr_join();

    me_queues_report();
    me_queues_destroy();
    me_sys_shutdown(&sys);

    if (!ok) {
        return 1;
    }

    /* NOTE: this reports the DEVICE's registration with the server, which is
     * not the same question as "was the circuit admitted into the registry".
     * If the registry mark failed, the device really was registered and this
     * still reports success, while every data packet was dropped. That
     * divergence is deliberate - the two facts are genuinely different - but
     * the ERROR logged at that point in comm_thread.c is what explains a run
     * that exits 0 having done nothing. */
    if (me_comm_is_registered()) {
        ME_LOGI("exiting - device was registered during this run");
        return 0;
    }

    ME_LOGW("exiting - device was never registered");
    return 1;
}
